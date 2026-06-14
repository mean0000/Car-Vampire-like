// Venosaur 데이터 셋업 — 클립 복제(이벤트 주입+분할용 사본)·AnimationEvent(ClawHit) 주입·머티리얼 URP 변환·AnimatorController 빌드.
//   ★Dimaxillosaurus 클로월 셋업의 *직접 재활용* — 클립 경로/프레임 수/무게 노브만 Venosaur 실측값으로 교체.
// ★원본 .meta 보존: 이벤트는 원본이 아니라 VenosaurRM/ 복제 사본에 박는다(Venodonte/Dimax 전례).
//
// ════════ ★함정 (재발 금지) ════════
//   1. AnimatorController 디스크 영속화: CreateAssetController로 직접 디스크 생성 → SaveAssets + ImportAsset(ForceUpdate) + 재로드 검증.
//   2. AnimationEvent time = *정규화(0~1)*(importer가 클립 길이로 곱함). seconds로 넣으면 ×길이 밀려 클립 밖. MakeEvent 참조.
//   3. ★Venosaur 단발 클로 = 30프레임/1.0s (Dimax는 35프레임/1.1667s였음 — 분할 경계 frame 재유도). 컨택 frame 12 동일.
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class VenosaurLabSetup
{
    const string SrcDir = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/FBX Files";
    const string RmDir = "Assets/_Project/Animations/VenosaurRM";
    const string ControllerPath = "Assets/_Project/Animations/VenosaurBrawler.controller";
    const string MaterialPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 12/Venosaur/Materials/M_Venosaur.mat";

    // ════════ ★"묵직 브루저 클로월" — Dimax 클로월 틀 직접 재활용(2026-06-14) ════════
    //   상태머신/시퀀스는 Dimax와 동일(Idle→Roar→L_Windup→…Recovery→반대손 Windup 직행). 신규 = 클립 교체 + 무게 노브 + L/R 비대칭.
    //
    // ★★단발 클로 _RM 실측(SampleAnimation 본추적 + 루트 베이크, 2026-06-14):
    //   ClawsAttackLeftForward_RM:  길이 1.000s/30f, 전진(z) 2.413m(2.413 m/s), 측면0·상승0. 컨택(L Finger fwd-reach peak) frame 12/norm 0.400.
    //   ClawsAttackRightForward_RM: 길이 1.000s/30f, 전진(z) 4.094m(4.094 m/s), 측면0·상승0. 컨택(R Finger fwd-reach peak) frame 12/norm 0.400.
    //   ★★L/R 전진 비대칭(R이 ~70% 큰 런지) — 클립 저작 차이. 드라이버 per-hand AdvanceGain로 보존/균등화(기본 보존, 둘 다 1.0).
    //
    // ════════ ★★무게 이즈 4구간 분할 (30프레임 기준 재유도) ════════
    //   reach 스캔(실측): f0(1.03)→f9(1.55/2.09 cocking)→f12 peak(2.73/3.06 컨택)→f15(1.90/2.78 초기팔로)→f21(1.60/1.78 후기팔로)→f30(1.03 중립).
    //   → 구간 경계: Windup 0~9(cocking) / Strike 9~15(컨택 f12+초기 팔로스루) / FollowOut 15~21(후기 팔로스루) / Recovery 21~30(중립 복귀).
    //   ★연속성: 같은 take 분할이라 경계 frame(9/15/21) 포즈 비트-동일 → CUT(dur0)여도 포즈 점프 0(헌법 준수, crossfade 아님).
    //   ★루트모션 손실 0: 4구간이 0~9/9~15/15~21/21~30을 각자 운반 → 합 = 풀클립(L 2.413 / R 4.094).
    const int   ClipFrames    = 30;     // ★Venosaur 단발 원본 프레임 수(실측 30프레임/1.000s = 30fps). Dimax 35와 다름!
    const int   SrcFps        = 30;
    const int   WindupFrame   = 9;      // ★윈드업/타격 경계(컨택 f12 직전 — cocking 끝). 0~9=Windup, 9~15=Strike.
    const int   StrikeFrame   = 15;     // ★타격/후기팔로스루 경계(초기 팔로스루 끝). 9~15=Strike, 15~21=FollowOut.
    const int   SplitFrame    = 21;     // ★팔로스루/회수 경계(후기 팔로스루 끝·회수 시작). 15~21=FollowOut, 21~30=Recovery.

    // ★★무게 이즈 램프 = 둔중 브루저 + ★강약 대비(v2, 위협감) — 드라이버 const 단일 진실원(state.speed와 desync 차단).
    const float WindupSpeed   = VenosaurBrawler.WindupSpeed;    // 0.70 — ★느린 응축(긴 텔레그래프=위협 빌드업). v1 1.1.
    const float StrikeSpeed   = VenosaurBrawler.StrikeSpeed;    // 2.4  — ★확 박히는 스냅(Windup 3.4배속=강약 핵심, ★ClawHit norm0.5). v1 1.0.
    const float FollowSpeed   = VenosaurBrawler.FollowSpeed;    // 1.3  — 무거운 팔로스루(스냅 직후 무게 carry). v1 1.6.
    const float RecoverySpeed = VenosaurBrawler.RecoverySpeed;  // 1.7  — 묵직 중립 복귀. v1 1.9.

    // ★ClawHit = *Strike 클립*(frame 9~15)에 박는다 → 정규화 = (컨택절대프레임 − WindupFrame) / (StrikeFrame − WindupFrame).
    //   컨택 절대 frame 12(L/R 동일, 실측 norm 0.400×30) → Strike 내 정규화 = (12−9)/(15−9) = 3/6 = 0.500.
    const float SrcContactFrame   = 0.400f * 30f;  // = 12 (원본 절대 프레임, L/R 동일)
    const float StrikeContactNorm = (SrcContactFrame - WindupFrame) / (StrikeFrame - WindupFrame); // = 3/6 = 0.500

    // ★사본 내 네 sub-clip 이름(같은 take, frame 범위만 다름). 컨트롤러가 이름으로 로드.
    const string LWindupName = "LeftClaw_Windup";
    const string LStrikeName = "LeftClaw_Strike";
    const string LFollowName = "LeftClaw_FollowOut";
    const string LRecovName  = "LeftClaw_Recovery";
    const string RWindupName = "RightClaw_Windup";
    const string RStrikeName = "RightClaw_Strike";
    const string RFollowName = "RightClaw_FollowOut";
    const string RRecovName  = "RightClaw_Recovery";

    [MenuItem("ZombieCrush/Venosaur Lab/1. Setup Data (clips+events+material+controller)")]
    public static void SetupData()
    {
        Directory.CreateDirectory(RmDir);

        // ── 1. 머티리얼 URP 변환(Standard → URP/Lit, 마젠타 회피). ──
        //   ★Venosaur는 ×5 Tint 변형 머티리얼이 있으나 *현장조 통째 변이*라 개별 모션 금지(Story 가드). 베이스 M_Venosaur만 변환(랩 1색).
        ConvertMaterialToURP();

        // ── 2. 클립 사본 복제(이벤트 주입 + 4구간 분할용). ──
        string leftSrc  = $"{SrcDir}/Venosaur@ClawsAttackLeftForward_RM.fbx";
        string leftDst  = $"{RmDir}/Venosaur@ClawsAttackLeftForward_RM.fbx";
        string rightSrc = $"{SrcDir}/Venosaur@ClawsAttackRightForward_RM.fbx";
        string rightDst = $"{RmDir}/Venosaur@ClawsAttackRightForward_RM.fbx";
        CopyFbx(leftSrc, leftDst);
        CopyFbx(rightSrc, rightDst);
        AssetDatabase.Refresh();

        // ── 3. 각 사본을 Windup/Strike/FollowOut/Recovery 네 sub-clip 분할 + Strike에 ClawHit 주입(컨택 모먼트). ──
        SplitClawClip(leftDst,  LWindupName, LStrikeName, LFollowName, LRecovName, StrikeContactNorm);
        SplitClawClip(rightDst, RWindupName, RStrikeName, RFollowName, RRecovName, StrikeContactNorm);

        // ── 4. AnimatorController 빌드 ──
        BuildController(leftDst, rightDst);

        Debug.Log("[VenosaurLab] 데이터 셋업 완료 — 다음: '2. Build Combat Test'.");
    }

    static void ConvertMaterialToURP()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null) { Debug.LogWarning($"[VenosaurLab] 머티리얼 미발견: {MaterialPath}"); return; }
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogWarning("[VenosaurLab] URP/Lit 셰이더 미발견"); return; }
        if (mat.shader == urp) { Debug.Log("[VenosaurLab] 머티리얼 이미 URP — 변환 생략"); return; }

        Texture main = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        mat.shader = urp;
        if (main != null) mat.SetTexture("_BaseMap", main);
        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[VenosaurLab] 머티리얼 URP 변환 완료.");
    }

    static void CopyFbx(string src, string dst)
    {
        if (!File.Exists(src)) { Debug.LogError($"[VenosaurLab] 원본 FBX 미발견: {src}"); return; }
        if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
        AssetDatabase.CopyAsset(src, dst);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
    }

    // ★단발 클로를 Windup(0~9)/Strike(9~15)/FollowOut(15~21)/Recovery(21~30) 네 sub-clip 분할. Strike에만 ClawHit 주입.
    static void SplitClawClip(string fbxPath, string windupName, string strikeName, string followName, string recovName, float strikeContactNorm)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) { Debug.LogError($"[VenosaurLab] ModelImporter 없음: {fbxPath}"); return; }

        var defaults = importer.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0) { Debug.LogError($"[VenosaurLab] 기본 클립 없음: {fbxPath}"); return; }
        string takeName = defaults[0].takeName;

        var windup = MakeClipDef(windupName, takeName, 0,           WindupFrame, new AnimationEvent[0]);
        var strike = MakeClipDef(strikeName, takeName, WindupFrame,  StrikeFrame, new[] { MakeEvent("ClawHit", strikeContactNorm, 0) });
        var follow = MakeClipDef(followName, takeName, StrikeFrame,  SplitFrame,  new AnimationEvent[0]);
        var recov  = MakeClipDef(recovName,  takeName, SplitFrame,   ClipFrames,  new AnimationEvent[0]);
        importer.clipAnimations = new[] { windup, strike, follow, recov };

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var sb = new System.Text.StringBuilder();
        sb.Append($"[VenosaurLab] 4분할 ({System.IO.Path.GetFileName(fbxPath)}):");
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__"))
            {
                sb.Append($"  [{c.name} len={c.length:0.000}s ev={c.events.Length}");
                foreach (var e in c.events) sb.Append($" {e.functionName}@{e.time:0.000}s");
                sb.Append("]");
            }
        float windupLen = (float)WindupFrame / SrcFps, strikeLen = (float)(StrikeFrame - WindupFrame) / SrcFps;
        float followLen = (float)(SplitFrame - StrikeFrame) / SrcFps, recovLen = (float)(ClipFrames - SplitFrame) / SrcFps;
        sb.Append($"  (기대: {windupName} {windupLen:0.000}s ev0 / {strikeName} {strikeLen:0.000}s ev1 컨택≈{strikeContactNorm*strikeLen:0.000}s / {followName} {followLen:0.000}s ev0 / {recovName} {recovLen:0.000}s ev0)");
        Debug.Log(sb.ToString());
    }

    static ModelImporterClipAnimation MakeClipDef(string name, string takeName, int firstFrame, int lastFrame, AnimationEvent[] events)
    {
        return new ModelImporterClipAnimation
        {
            name = name,
            takeName = takeName,
            firstFrame = firstFrame,
            lastFrame = lastFrame,
            loopTime = false,
            loop = false,
            wrapMode = WrapMode.Once,
            keepOriginalPositionY = true,    // 상승 0 보존
            keepOriginalPositionXZ = false,  // ★루트모션 전진(XZ)을 살린다
            keepOriginalOrientation = false,
            maskType = ClipAnimationMaskType.CreateFromThisModel,
            events = events,
        };
    }

    static AnimationEvent MakeEvent(string fn, float timeNormalized, int intParam)
    {
        return new AnimationEvent
        {
            functionName = fn,
            time = timeNormalized,     // ★정규화(0~1) — importer가 클립 길이로 곱한다(seconds 아님!)
            intParameter = intParam,
            messageOptions = SendMessageOptions.DontRequireReceiver,
        };
    }

    static void BuildController(string leftFbxPath, string rightFbxPath)
    {
        AnimationClip lWindup = LoadClipByName(leftFbxPath,  LWindupName);
        AnimationClip lStrike = LoadClipByName(leftFbxPath,  LStrikeName);
        AnimationClip lFollow = LoadClipByName(leftFbxPath,  LFollowName);
        AnimationClip lRecov  = LoadClipByName(leftFbxPath,  LRecovName);
        AnimationClip rWindup = LoadClipByName(rightFbxPath, RWindupName);
        AnimationClip rStrike = LoadClipByName(rightFbxPath, RStrikeName);
        AnimationClip rFollow = LoadClipByName(rightFbxPath, RFollowName);
        AnimationClip rRecov  = LoadClipByName(rightFbxPath, RRecovName);
        AnimationClip idle    = LoadClipByName($"{SrcDir}/Venosaur@Idle.fbx", null);
        AnimationClip roar    = LoadClipByName($"{SrcDir}/Venosaur@Roar.fbx", null);

        if (lWindup == null || lStrike == null || lFollow == null || lRecov == null
         || rWindup == null || rStrike == null || rFollow == null || rRecov == null || idle == null || roar == null)
        { Debug.LogError("[VenosaurLab] 클립 로드 실패 — 컨트롤러 빌드 중단"); return; }

        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ★파라미터(3) — 트리거 3개로 손 명시 라우팅(로코모션 파라미터 없음 → BlendTree 없음 → "Float여야" 함정 자체 소멸).
        ctrl.AddParameter("attack", AnimatorControllerParameterType.Trigger);   // 오프너: Idle→Roar (타깃 인지 1회)
        ctrl.AddParameter("chainL", AnimatorControllerParameterType.Trigger);   // ★좌 단발: Idle→LeftClaw_Windup 직행
        ctrl.AddParameter("chainR", AnimatorControllerParameterType.Trigger);   // ★우 단발: Idle→RightClaw_Windup 직행

        var sm = ctrl.layers[0].stateMachine;

        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        sm.defaultState = sIdle;

        // ── Roar (앵티시페이션·위협 텔레그래프, 압축 재생) ──
        var sRoar = sm.AddState("Roar");
        sRoar.motion = roar;
        sRoar.speed = 4.5f;   // ★Roar 4.0s를 ~0.89s로 압축 — 묵직 브루저는 Dimax(~0.57s)보다 살짝 긴 포효(무게). 정적 speed(스크럽 아님).

        // ── 단발 = Windup → Strike → FollowOut → Recovery 네 상태씩 (구간별 정적 speed = 무게 이즈 램프) ──
        var sLWindup = sm.AddState("LeftClaw_Windup");
        sLWindup.motion = lWindup; sLWindup.speed = WindupSpeed;
        var sLStrike = sm.AddState("LeftClaw_Strike");
        sLStrike.motion = lStrike; sLStrike.speed = StrikeSpeed;
        var sLFollow = sm.AddState("LeftClaw_FollowOut");
        sLFollow.motion = lFollow; sLFollow.speed = FollowSpeed;
        var sLRecov = sm.AddState("LeftClaw_Recovery");
        sLRecov.motion = lRecov; sLRecov.speed = RecoverySpeed;

        var sRWindup = sm.AddState("RightClaw_Windup");
        sRWindup.motion = rWindup; sRWindup.speed = WindupSpeed;
        var sRStrike = sm.AddState("RightClaw_Strike");
        sRStrike.motion = rStrike; sRStrike.speed = StrikeSpeed;
        var sRFollow = sm.AddState("RightClaw_FollowOut");
        sRFollow.motion = rFollow; sRFollow.speed = FollowSpeed;
        var sRRecov = sm.AddState("RightClaw_Recovery");
        sRRecov.motion = rRecov; sRRecov.speed = RecoverySpeed;

        // ════════ 전이 (정체성 동작 전이 전부 CUT dur0 — 한 동작씩 완결, 뭉개기 금지) ════════
        var tIdleRoar = sIdle.AddTransition(sRoar);
        tIdleRoar.AddCondition(AnimatorConditionMode.If, 0, "attack");
        tIdleRoar.hasExitTime = false; tIdleRoar.duration = 0f;

        var tIdleLWindup = sIdle.AddTransition(sLWindup);
        tIdleLWindup.AddCondition(AnimatorConditionMode.If, 0, "chainL");
        tIdleLWindup.hasExitTime = false; tIdleLWindup.duration = 0f;

        var tIdleRWindup = sIdle.AddTransition(sRWindup);
        tIdleRWindup.AddCondition(AnimatorConditionMode.If, 0, "chainR");
        tIdleRWindup.hasExitTime = false; tIdleRWindup.duration = 0f;

        // Roar → LeftClaw_Windup: 포효 완결 후 자동(ExitTime) — 오프너는 항상 좌 단발로 시작. ★CUT(dur0).
        var tRoarWindup = sRoar.AddTransition(sLWindup);
        tRoarWindup.hasExitTime = true; tRoarWindup.exitTime = 0.95f; tRoarWindup.duration = 0f;

        // ★★단발 4구간 체인 (Windup→Strike→FollowOut→Recovery): 같은 동작의 분할 → 경계 포즈 비트-동일 → CUT(dur0)여도 포즈 점프 0.
        ChainCut(sLWindup, sLStrike);   ChainCut(sLStrike, sLFollow);   ChainCut(sLFollow, sLRecov);
        ChainCut(sRWindup, sRStrike);   ChainCut(sRStrike, sRFollow);   ChainCut(sRFollow, sRRecov);

        // ════════ ★★Recovery → *반대 손* Windup 직행 (Idle "잠시 쉼" 제거 — 끊임없는 좌우) ════════
        //   ★전이 *순서* 핵심: 트리거-게이트 직행을 **먼저**, Idle 폴백을 **나중**(같은 exitTime 0.98). Animator는 리스트 순서로 평가.
        var tLRecovRWindup = sLRecov.AddTransition(sRWindup);   // L 회수 끝 → R 윈드업(chainR)
        tLRecovRWindup.AddCondition(AnimatorConditionMode.If, 0, "chainR");
        tLRecovRWindup.hasExitTime = true; tLRecovRWindup.exitTime = 0.98f; tLRecovRWindup.duration = 0f;

        var tRRecovLWindup = sRRecov.AddTransition(sLWindup);   // R 회수 끝 → L 윈드업(chainL)
        tRRecovLWindup.AddCondition(AnimatorConditionMode.If, 0, "chainL");
        tRRecovLWindup.hasExitTime = true; tRRecovLWindup.exitTime = 0.98f; tRRecovLWindup.duration = 0f;

        // ★폴백(타깃 소실 등 trigger 미셋 시에만): Recovery → Idle. ★직행 전이보다 *나중*(트리거 셋이면 직행이 이김).
        var tLRecovIdle = sLRecov.AddTransition(sIdle);
        tLRecovIdle.hasExitTime = true; tLRecovIdle.exitTime = 0.98f; tLRecovIdle.duration = 0f;

        var tRRecovIdle = sRRecov.AddTransition(sIdle);
        tRRecovIdle.hasExitTime = true; tRRecovIdle.exitTime = 0.98f; tRRecovIdle.duration = 0f;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);

        var reloaded = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int stateCount = reloaded != null && reloaded.layers.Length > 0 ? reloaded.layers[0].stateMachine.states.Length : -1;
        int paramCount = reloaded != null ? reloaded.parameters.Length : -1;
        Debug.Log($"[VenosaurLab] 컨트롤러 빌드+영속화 — 재로드 상태 수={stateCount} (기대 10: Idle/Roar + L/R × Windup/Strike/FollowOut/Recovery), 파라미터 수={paramCount} (기대 3: attack/chainL/chainR). ★묵직 브루저 무게 램프(Windup={WindupSpeed}/Strike={StrikeSpeed}/Follow={FollowSpeed}/Recovery={RecoverySpeed}, 경계 frame {WindupFrame}/{StrikeFrame}/{SplitFrame}/{ClipFrames}). ★L/R 전진 비대칭 L2.413m/R4.094m(per-hand gain 보존).");
    }

    static void ChainCut(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true; t.exitTime = 0.99f; t.duration = 0f;
    }

    static AnimationClip LoadClipByName(string fbxPath, string clipName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__"))
            {
                if (clipName == null || c.name == clipName) return c;
            }
        Debug.LogError($"[VenosaurLab] 클립 없음: {fbxPath} (name={clipName ?? "<first>"})");
        return null;
    }
}
