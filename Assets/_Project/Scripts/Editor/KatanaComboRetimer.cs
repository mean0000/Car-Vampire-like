using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// ★재실행 가능한 콤보 리타이머 (Animation 에이전트 소유. 2026-06-21 신설 → 2026-07-04 3세그 스트라이크 스냅으로 개편).
///
/// 카타나 평타 콤보 3타(Combo1/2/3)를 "윈드업(보통) → 스트라이크(★확 빠르게=스냅) → 회수(보통)" 3구간
/// 비균일 리타이밍으로 재프로파일한다. 유저 요구: ①전체 속도↑ ②베는 그 순간만 확 빠르게(비균일 스냅).
///
/// 메커니즘 = 클립 물리 리샘플(브루트 상태-분절 대신 채택 — 콤보는 분기 구조라 상태 분절이 체이닝/이벤트/busy/
/// Attack진입 배선을 그래프-폭발시킨다. 리샘플은 컨트롤러 구조를 100% 무손으로 두고 스냅을 *모션에 굽는다*).
/// FBX 서브클립(읽기전용)의 모든 커브를 편집 가능한 .anim으로 복제하면서 구간 경계에 앵커 키를 박고 키타임을
/// piecewise time-map T()로 재배치한다(탄젠트는 체인룰 dt/dT=구간배율로 스케일). 이벤트는 아래 노름 상수로
/// *저작*해 같은 T()로 remap한다 → 단일 클립 = 단일 상태 유지(헌법: 한 동작=한 상태, 동작 중 블렌드 금지).
///
/// ★이벤트를 소스에서 안 읽고 상수로 저작하는 이유(2026-07-04): Combo1/Combo3 소스 FBX 서브클립엔 이벤트가
///   0개다(이벤트는 그동안 retimed .anim에만 생존). 소스에서 읽으면 FindEventTime throw + Combo가 이벤트
///   없이 구워지면 OnComboEnd 미발화 → ComboStep 고착 소프트락(과거 Combo2 이벤트갭 사고). 그래서 3개 이벤트를
///   원본-FBX 정규화 시각(hitNorm/windowNorm/endNorm) 상수에서 저작해 항상 정확히 3개를 remap해 심는다.
///   norm 값은 원본 FBX 블레이드-피크 실측 + 기존 retimed .anim 역-remap 교차검증으로 확정.
///
/// ★재튜닝 = 아래 속도 노브만 바꾸고 메뉴 "Retime Katana Combos (3-seg strike snap)" 재실행 → .anim 덮어씀
///   (guid 보존: 기존 에셋 있으면 in-place CopySerialized). FBX 리임포트도, Animator 배선도 안 건드린다.
///   Combo2는 최초 1회 별도 메뉴 "Repoint Combo2 Motion ..."으로 상태 m_Motion을 새 .anim에 물린다(에디터 API).
/// </summary>
public static class KatanaComboRetimer
{
    // ───────────────────────── 속도 노브 (여기만 만지고 메뉴 재실행 — 단일 진실원) ─────────────────────────
    // 1.0 = 원본 속도. >1.0 = 그 구간을 그만큼 빠르게(압축). <1.0 = 느리게.
    // 3세그 프로파일: 윈드업(보통·전체속도 약간 bump) → 스트라이크(★스냅) → 회수(brisk / 피니셔는 무게 보존).
    const float WindupSpeed           = 1.25f;  // [0, hit-lead] 앵티시페이션 — 읽히되 약간 빠르게(전체 속도↑). 너무 크면 예비동작이 사라져 스냅 대비가 죽음.
    const float StrikeSpeed           = 2.2f;   // [hit-lead, window] ★베는 순간 스냅. 윈드업 1.25 대비 1.76× 가속 대비 = "팍". 카타나=경량이라 브루트(0.5→1.25)보다 두 베이스 다 높음.
    const float RecoverySpeed         = 1.4f;   // [window, end] 회수 — Combo1·Combo2(brisk 회수 = 후딜 제거 방향).
    const float FinisherRecoverySpeed = 1.0f;   // [window, end] 회수 — Combo3(피니셔): 무게 상대 보존(1/2보다 느긋). <1.0=더 무겁게.
    const float StrikeLead            = 0.07f;  // 스트라이크 시작 = OnAttackHit − 이 값(초). 컨택 직전 휘두름-진입을 스냅 창에 브래킷(컨택을 fast 구간 안에 둠).

    const string OutDir = "Assets/_Project/Animations";
    const string FbxDir = "Assets/Frank_Slash_Pack/Assets/Animations/Frank_SlashPack_Katana/FBX_Animation/Root_Motion";
    const string CtrlPath = OutDir + "/KatanaMelee.controller";

    /// <summary>콤보 1단의 정의(단일 진실원). norm = 원본 FBX 정규화 이벤트 시각(0..1). abs = norm × clip.length.</summary>
    class ComboDef
    {
        public string fbxPath, outPath, outName;
        public int comboIndex;               // OnAttackHit intParameter(현행 1/2/3 보존 — OnHitFrame은 무시하나 안전)
        public float hitNorm, windowNorm, endNorm;
        public float recoverySpeed;
    }

    static ComboDef[] Defs() => new[]
    {
        new ComboDef {
            fbxPath = FbxDir + "/Frank_RPG_Katana_S1_Combo01_01.FBX",
            outPath = OutDir + "/S1_Combo01_01_Retimed.anim", outName = "S1_Combo01_01_Retimed",
            comboIndex = 1, hitNorm = 0.3670f, windowNorm = 0.4840f, endNorm = 0.9200f,
            recoverySpeed = RecoverySpeed },
        new ComboDef {
            fbxPath = FbxDir + "/Frank_RPG_Katana_S1_Combo01_02.FBX",
            outPath = OutDir + "/S1_Combo01_02_Retimed.anim", outName = "S1_Combo01_02_Retimed",
            comboIndex = 2, hitNorm = 0.2000f, windowNorm = 0.3440f, endNorm = 0.9100f,
            recoverySpeed = RecoverySpeed },
        new ComboDef {
            fbxPath = FbxDir + "/Frank_RPG_Katana_S1_Combo01_03.FBX",
            outPath = OutDir + "/S1_Combo01_03_Retimed.anim", outName = "S1_Combo01_03_Retimed",
            comboIndex = 3, hitNorm = 0.2060f, windowNorm = 0.3180f, endNorm = 0.9200f,
            recoverySpeed = FinisherRecoverySpeed },   // ★피니셔 무게 보존
    };

    [MenuItem("ZombieCrush/Animation/Retime Katana Combos (3-seg strike snap)")]
    public static void RetimeAll()
    {
        foreach (var d in Defs()) RetimeOne(d);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[KatanaComboRetimer] Done. Windup×{WindupSpeed} Strike×{StrikeSpeed} " +
                  $"Recovery×{RecoverySpeed} (finisher×{FinisherRecoverySpeed}) lead {StrikeLead}s. " +
                  "Combo2는 최초 1회 'Repoint Combo2 Motion ...' 메뉴로 상태에 물릴 것.");
    }

    static void RetimeOne(ComboDef d)
    {
        // ★노브 오타 방어(Stab L-2, 에디터 전용): 순서 깨지면 strikeStart clamp가 크래시는 막지만 스트라이크 구간이
        //   0으로 조용히 퇴화 → 스냅 소실. 착공 전 순서 단조 확인.
        if (!(d.hitNorm < d.windowNorm && d.windowNorm < d.endNorm))
            Debug.LogError($"[KatanaComboRetimer] {d.outName}: norm 순서 위반(hit {d.hitNorm} < win {d.windowNorm} < end {d.endNorm} 아님) — 스트라이크 구간 퇴화 위험. 노브 확인.");

        var src = LoadFbxClip(d.fbxPath);
        float len = src.length;
        float hit = d.hitNorm * len;
        float win = d.windowNorm * len;
        float end = d.endNorm * len;
        // 스트라이크 시작 = hit − lead. 방어: 경계 단조(0 < strikeStart < win) 보장 — 비정상 노름/lead에서도 세그 길이 양수.
        float strikeStart = Mathf.Clamp(hit - StrikeLead, 1e-4f, win - 1e-4f);

        var map = BuildPiecewise(new[]
        {
            new Seg(0f,          strikeStart, WindupSpeed),      // 윈드업(보통)
            new Seg(strikeStart, win,         StrikeSpeed),      // ★스트라이크(스냅)
            new Seg(win,         len,         d.recoverySpeed),  // 회수
        });

        // ★이벤트 저작(소스 미의존) — 원본 abs 시각. WriteRetimed가 map으로 remap해 심는다.
        var events = new[]
        {
            MakeEvent(hit, "OnAttackHit",   d.comboIndex),  // 컨택 — 스트라이크 구간 내
            MakeEvent(win, "OnComboWindow", 0),             // 캔슬창 시작 — 스트라이크/회수 경계
            MakeEvent(end, "OnComboEnd",    0),             // 종료 — 회수 구간 내
        };

        WriteRetimed(src, map, events, d.outPath, d.outName);
    }

    static AnimationEvent MakeEvent(float time, string fn, int intParam) => new AnimationEvent
    {
        time = time,
        functionName = fn,
        intParameter = intParam,
        // =1(DontRequireReceiver) — 현행 이벤트와 동일. 파라미터 없는 수신자(OnComboWindow/End)도 안전.
        messageOptions = SendMessageOptions.DontRequireReceiver,
    };

    // ─────────── ★Combo2 상태 m_Motion 재지정(최초 1회, 에디터 API — 하드 YAML ❌) ───────────
    [MenuItem("ZombieCrush/Animation/Repoint Combo2 Motion to Retimed anim (one-time)")]
    public static void RepointCombo2()
    {
        const string animPath = OutDir + "/S1_Combo01_02_Retimed.anim";
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        var anim = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
        if (ctrl == null) { Debug.LogError("[KatanaComboRetimer] 컨트롤러 못 찾음: " + CtrlPath); return; }
        if (anim == null) { Debug.LogError("[KatanaComboRetimer] Combo2 retimed .anim 없음(먼저 Retime 메뉴 실행): " + animPath); return; }

        int changed = 0;
        foreach (var layer in ctrl.layers)
            changed += RepointStateMotion(layer.stateMachine, "Combo2", anim);

        if (changed > 0) { EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); }
        Debug.Log($"[KatanaComboRetimer] Repoint Combo2 → {animPath}: {changed} state 갱신" +
                  (changed == 0 ? " (이미 물려 있거나 상태명 불일치)." : "."));
    }

    /// <summary>SM(및 하위 SM) 재귀 탐색 — 이름이 stateName인 상태의 motion을 clip으로(이미 같으면 스킵=멱등).</summary>
    static int RepointStateMotion(AnimatorStateMachine sm, string stateName, AnimationClip clip)
    {
        int n = 0;
        foreach (var cs in sm.states)
            if (cs.state.name == stateName && cs.state.motion != clip) { cs.state.motion = clip; n++; }
        foreach (var sub in sm.stateMachines)
            n += RepointStateMotion(sub.stateMachine, stateName, clip);
        return n;
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
        /// <summary>원본시간 t에서 *끝나는*(또는 내부를 지나는) 구간 속도 — inTangent(키 이전 구간) 스케일용.
        /// ★경계 키(strikeStart·win)에서 앞 구간을 정확히 집는다: t가 seg.t1이면 그 seg가 걸린다(Codex/Stab 수렴 수정).</summary>
        public float SpeedBefore(float t)
        {
            foreach (var s in segs)
                if (t > s.t0 && t <= s.t1) return s.speed;
            return segs.Count > 0 ? segs[0].speed : 1f;   // t≤첫 t0(=0): 첫 구간
        }
        /// <summary>원본시간 t에서 *시작하는*(또는 내부를 지나는) 구간 속도 — outTangent(키 이후 구간) 스케일용.
        /// ★경계 키에서 뒤 구간을 정확히 집는다: t가 seg.t0이면 그 seg가 걸린다. 이전엔 양쪽 다 앞 구간 속도라
        /// 스트라이크 선두가 windup 속도로 램프업돼 스냅이 뭉갰다(Codex P2 / Stab L-1 수렴).</summary>
        public float SpeedAfter(float t)
        {
            foreach (var s in segs)
                if (t >= s.t0 && t < s.t1) return s.speed;
            return segs.Count > 0 ? segs[segs.Count - 1].speed : 1f;   // t≥마지막 t1: 마지막 구간
        }
    }

    static TimeMap BuildPiecewise(Seg[] segs)
    {
        var m = new TimeMap();
        foreach (var s in segs) m.segs.Add(s);
        // 경계 = 각 세그먼트 끝(마지막 = 클립 끝) + 내부 경계 중복(RetimeCurve가 중복 무시). 시작 0은 보통 이미 키 있음.
        for (int i = 0; i < segs.Length; i++) m.sourceBreaks.Add(segs[i].t1);
        m.sourceBreaks.Add(0f);
        for (int i = 0; i < segs.Length - 1; i++) m.sourceBreaks.Add(segs[i].t1);
        return m;
    }

    // ──────────────────────────────── 리샘플 본체 ────────────────────────────────
    static void WriteRetimed(AnimationClip src, TimeMap map, AnimationEvent[] authoredEvents, string outPath, string newName)
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

        // 2) 오브젝트참조 커브(휴머노이드 클립엔 보통 없음)는 시간 키만 이동해 보존
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

        // 4) ★AnimationEvent 저작 — 상수에서 만든 이벤트를 같은 T()로 remap해 심는다(소스 미의존 = 이벤트갭 소프트락 원천 차단)
        var dstEvents = new AnimationEvent[authoredEvents.Length];
        for (int i = 0; i < authoredEvents.Length; i++)
        {
            var e = authoredEvents[i];
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

        // 5) .anim 쓰기(있으면 in-place 덮어쓰기 = guid 보존 / 없으면 신규 생성)
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
                float v = src.Evaluate(bt);
                work.AddKey(new Keyframe(bt, v));
            }
        }

        // (b) 각 키 시간 → T(), 탄젠트 ×speed(체인룰: 시간 ×(1/speed) 압축 ⇒ dv/dT = dv/dt·dt/dT = slope·speed)
        //   ★경계 키는 in/out을 각 구간 속도로 분리(Codex P2/Stab L-1 수렴): inTangent=앞 구간, outTangent=뒤 구간.
        //   이전엔 SpeedAt 하나로 양쪽 다 앞 구간 속도라, strikeStart의 outTangent가 StrikeSpeed 대신 WindupSpeed로
        //   스케일돼 스트라이크 선두가 램프업(스냅 뭉갬)됐다. 내부 키는 앞=뒤라 결과 동일.
        var keys = work.keys;
        for (int i = 0; i < keys.Length; i++)
        {
            float origT = keys[i].time;
            keys[i].time = map.Map(origT);
            keys[i].inTangent *= map.SpeedBefore(origT);
            keys[i].outTangent *= map.SpeedAfter(origT);
            // weight는 정규화 비율이라 시간 스케일에 불변 — 건드리지 않음.
        }
        var outCurve = new AnimationCurve(keys);
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
}
