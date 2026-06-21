using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ★재실행 가능한 콤보 리타이머 (Animation 에이전트 소유, 2026-06-21).
///
/// 카타나 콤보의 "굼뜬 불쾌함"을 구간별 비균일 리타이밍으로 제거한다.
///   · Combo1 = 윈드업(시작→타격 직전)만 빠르게. 타격~캔슬창~회수는 평속(가독 보존).
///   · Combo3 = 타격~캔슬창은 평속. 회수(캔슬창 이후→끝)만 빠르게.
///
/// 메커니즘 = 클립 물리 리샘플(나·Codex 수렴 #1안). FBX 서브클립(읽기전용)의 모든 커브를
/// 편집 가능한 .anim으로 복제하면서, 구간 경계에 앵커 키를 박고 키타임을 piecewise time-map T()로
/// 재배치한다. 탄젠트는 체인룰(dt/dT = 구간 속도배율)로 스케일. AnimationEvent도 같은 T()로 옮긴다.
/// → 단일 클립 = 단일 상태 유지(헌법: 한 동작=한 상태, 동작 중 블렌드 금지). 루트모션은 한 클립에
///    구워져 OnAnimatorMove가 그대로 적용(상태 분할식 이중적용 위험 없음).
///
/// ★재튜닝 = 아래 상수만 바꾸고 메뉴 다시 실행 → .anim 덮어씀. FBX 리임포트도, Animator 배선도 안 건드림.
///   (컨트롤러 상태 m_Motion은 최초 1회 .anim으로 교체하면 끝 — guid 안 바뀜.)
/// </summary>
public static class KatanaComboRetimer
{
    // ───────────────────────── 튜닝 노브 (여기만 만지고 메뉴 재실행) ─────────────────────────
    // 1.0 = 원본 속도. >1.0 = 그 구간을 그만큼 빠르게(압축). <1.0 = 느리게.
    // 첫 패스: 윈드업/회수 1.5× (체감 ~1.4~1.6 범위 권장 시작점).

    const float Combo1_WindupSpeed   = 1.5f;  // Combo1 시작→타격 직전 구간 가속 (스내피 시작 유지)
    const float Combo3_RecoverySpeed = 0.9f;  // Combo3 캔슬창 이후→끝 = 회수 살짝 느리게(피니셔 무게 ↑, 대시로만 캔슬). 1.0=원본, <1.0=느림

    // ───────────────────────── 클립 식별 (guid = 프롬프트 확정값) ─────────────────────────
    const string Combo1_Fbx = "Assets/Frank_Slash_Pack/Assets/Animations/Frank_SlashPack_Katana/FBX_Animation/Root_Motion/Frank_RPG_Katana_S1_Combo01_01.FBX";
    const string Combo3_Fbx = "Assets/Frank_Slash_Pack/Assets/Animations/Frank_SlashPack_Katana/FBX_Animation/Root_Motion/Frank_RPG_Katana_S1_Combo01_03.FBX";

    const string OutDir = "Assets/_Project/Animation";
    const string Combo1_Out = OutDir + "/S1_Combo01_01_Retimed.anim";
    const string Combo3_Out = OutDir + "/S1_Combo01_03_Retimed.anim";

    [MenuItem("ZombieCrush/Animation/Retime Katana Combo1+Combo3")]
    public static void RetimeBoth()
    {
        // Combo1: 윈드업 가속. 경계 = OnAttackHit(타격) 직전. 타격부터 평속.
        //   세그먼트1 [0, hitNorm] ×WindupSpeed,  세그먼트2 [hitNorm, 1] ×1.0
        var c1 = LoadFbxClip(Combo1_Fbx);
        float c1Len = c1.length;
        float c1Hit = FindEventTime(c1, "OnAttackHit"); // 절대초
        var c1Map = BuildPiecewise(new[]
        {
            new Seg(0f,       c1Hit,  Combo1_WindupSpeed),
            new Seg(c1Hit,    c1Len,  1f),
        });
        WriteRetimed(c1, c1Map, Combo1_Out, "S1_Combo01_01_Retimed");

        // Combo3: 회수 가속. 경계 = OnComboWindow(캔슬창 시작). 그 전까지 평속(타격+창 가독), 이후 가속.
        //   세그먼트1 [0, winNorm] ×1.0,  세그먼트2 [winNorm, 1] ×RecoverySpeed
        var c3 = LoadFbxClip(Combo3_Fbx);
        float c3Len = c3.length;
        float c3Win = FindEventTime(c3, "OnComboWindow");
        var c3Map = BuildPiecewise(new[]
        {
            new Seg(0f,       c3Win,  1f),
            new Seg(c3Win,    c3Len,  Combo3_RecoverySpeed),
        });
        WriteRetimed(c3, c3Map, Combo3_Out, "S1_Combo01_03_Retimed");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[KatanaComboRetimer] Done. Combo1 windup ×" + Combo1_WindupSpeed +
                  ", Combo3 recovery ×" + Combo3_RecoverySpeed + ". Outputs:\n  " + Combo1_Out + "\n  " + Combo3_Out);
    }

    // ──────────────────────────────── piecewise time-map ────────────────────────────────
    struct Seg { public float t0, t1, speed; public Seg(float a, float b, float s){t0=a;t1=b;speed=s;} }

    /// <summary>원본시간 t(절대초) → 신규시간 T(절대초). 구간별 압축. 경계(앵커) 목록도 함께 반환.</summary>
    class TimeMap
    {
        public List<Seg> segs = new List<Seg>();
        public List<float> sourceBreaks = new List<float>(); // 원본시간 경계(앵커 키 삽입용)
        public float Map(float t)
        {
            float T = 0f;
            foreach (var s in segs)
            {
                if (t <= s.t0) break;
                float clamped = Mathf.Min(t, s.t1);
                T += (clamped - s.t0) / s.speed; // 압축: 구간길이/속도
                if (t <= s.t1) return T;
            }
            return T;
        }
        /// <summary>주어진 원본시간 t가 속한 구간의 속도배율(탄젠트 스케일 = dT/dt의 역 = ... 아래 주석).</summary>
        public float SpeedAt(float t)
        {
            foreach (var s in segs)
                if (t >= s.t0 && t <= s.t1) return s.speed;
            return segs.Count > 0 ? segs[segs.Count - 1].speed : 1f;
        }
    }

    static TimeMap BuildPiecewise(Seg[] segs)
    {
        var m = new TimeMap();
        foreach (var s in segs) m.segs.Add(s);
        // 경계 = 각 세그먼트 끝(마지막 = 클립 끝). 시작 0은 모든 커브가 이미 키를 가짐(보통).
        for (int i = 0; i < segs.Length; i++) m.sourceBreaks.Add(segs[i].t1);
        m.sourceBreaks.Add(0f);
        for (int i = 0; i < segs.Length - 1; i++) m.sourceBreaks.Add(segs[i].t1);
        return m;
    }

    // ──────────────────────────────── 리샘플 본체 ────────────────────────────────
    static void WriteRetimed(AnimationClip src, TimeMap map, string outPath, string newName)
    {
        var dst = new AnimationClip { frameRate = src.frameRate };

        // 1) float 커브(휴머노이드 머슬/IK/루트 전부) 복제 + 리타임
        var bindings = AnimationUtility.GetCurveBindings(src);
        foreach (var b in bindings)
        {
            var srcCurve = AnimationUtility.GetEditorCurve(src, b);
            if (srcCurve == null) continue;
            var dstCurve = RetimeCurve(srcCurve, map);
            AnimationUtility.SetEditorCurve(dst, b, dstCurve);
        }

        // 2) 오브젝트참조 커브(있다면 — 휴머노이드 클립엔 보통 없음)는 그대로 보존(시간 키만 이동)
        var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(src);
        foreach (var b in objBindings)
        {
            var keys = AnimationUtility.GetObjectReferenceCurve(src, b);
            if (keys == null) continue;
            for (int i = 0; i < keys.Length; i++) keys[i].time = map.Map(keys[i].time);
            AnimationUtility.SetObjectReferenceCurve(dst, b, keys);
        }

        // 3) 휴머노이드 메타(loop 등) 복사 — humanMotion 클립 보존
        var srcSettings = AnimationUtility.GetAnimationClipSettings(src);
        AnimationUtility.SetAnimationClipSettings(dst, srcSettings);

        // 4) AnimationEvent 동일 T()로 이동(★이벤트가 진실 — 같은 모션 순간 유지)
        var srcEvents = AnimationUtility.GetAnimationEvents(src);
        var dstEvents = new AnimationEvent[srcEvents.Length];
        for (int i = 0; i < srcEvents.Length; i++)
        {
            var e = srcEvents[i];
            dstEvents[i] = new AnimationEvent
            {
                time = map.Map(e.time),
                functionName = e.functionName,
                stringParameter = e.stringParameter,
                floatParameter = e.floatParameter,
                intParameter = e.intParameter,
                objectReferenceParameter = e.objectReferenceParameter,
                messageOptions = e.messageOptions,
            };
        }
        AnimationUtility.SetAnimationEvents(dst, dstEvents);

        dst.name = newName;
        dst.EnsureQuaternionContinuity();

        // 5) .anim 쓰기(있으면 덮어쓰기 — guid 보존 위해 기존 에셋이 있으면 in-place 갱신)
        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(dst, existing);
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(dst, outPath);
        }

        // 검증 로그
        var verify = existing != null ? existing : dst;
        Debug.Log($"[KatanaComboRetimer] {newName}: srcLen {src.length:F4}s → dstLen {verify.length:F4}s, " +
                  $"events {AnimationUtility.GetAnimationEvents(verify).Length}, humanMotion {verify.humanMotion}");
        foreach (var e in AnimationUtility.GetAnimationEvents(verify))
            Debug.Log($"    event {e.functionName}@{e.time:F4}s (norm {e.time / verify.length:F3}) int={e.intParameter}");
    }

    /// <summary>한 커브를 piecewise time-map으로 리타임. 경계에 앵커 키 삽입, 탄젠트 체인룰 스케일.</summary>
    static AnimationCurve RetimeCurve(AnimationCurve src, TimeMap map)
    {
        // (a) 원본 커브에 경계 앵커 키 삽입(이미 키 있으면 무시) → 경계 모션값 정확 보존
        var work = new AnimationCurve(src.keys);
        foreach (float bt in map.sourceBreaks)
        {
            if (bt <= 0f) continue;
            bool exists = false;
            for (int i = 0; i < work.length; i++)
                if (Mathf.Abs(work.keys[i].time - bt) < 1e-5f) { exists = true; break; }
            if (!exists)
            {
                // 보간값으로 키 삽입. AddKey가 자동 탄젠트를 잡으므로, 이후 원본 탄젠트 스케일은
                // 기존 키에만 적용(삽입 키는 Unity 자동 탄젠트 유지 = 경계 매끈).
                float v = src.Evaluate(bt);
                work.AddKey(new Keyframe(bt, v));
            }
        }

        // (b) 각 키 시간 → T(), 탄젠트 ×speed(체인룰: 시간 ×(1/speed) 압축 ⇒ dv/dT = dv/dt · dt/dT = slope·speed)
        var keys = work.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            float origT = keys[i].time;
            float s = map.SpeedAt(origT);
            keys[i].time = map.Map(origT);
            keys[i].inTangent *= s;
            keys[i].outTangent *= s;
            // weight는 정규화 비율이라 시간 스케일에 불변 — 건드리지 않음.
        }
        var outCurve = new AnimationCurve(keys);
        // 원본 wrap 보존
        outCurve.preWrapMode = src.preWrapMode;
        outCurve.postWrapMode = src.postWrapMode;
        return outCurve;
    }

    // ──────────────────────────────── 헬퍼 ────────────────────────────────
    static AnimationClip LoadFbxClip(string fbxPath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        foreach (var o in all)
        {
            var clip = o as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview")) return clip;
        }
        throw new System.Exception("[KatanaComboRetimer] No AnimationClip in " + fbxPath);
    }

    static float FindEventTime(AnimationClip clip, string fn)
    {
        foreach (var e in AnimationUtility.GetAnimationEvents(clip))
            if (e.functionName == fn) return e.time;
        throw new System.Exception("[KatanaComboRetimer] Event " + fn + " not found in " + clip.name);
    }
}
