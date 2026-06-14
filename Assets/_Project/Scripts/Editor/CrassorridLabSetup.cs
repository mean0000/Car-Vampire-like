// Crassorrid 데이터 셋업 — ★4번째 몬스터 틀(접근형 브루트 + 텔레그래프 첫 소비자)의 진실원(빌드스크립트).
//   SmashAttack_RM 사본 복제(이벤트 주입용)·3구간 분할(Windup/Strike/Recovery)·SmashHit 이벤트(임팩트 frame 20) 주입
//   ·머티리얼 URP(방어적 — 이미 URP/Lit)·AnimatorController 빌드. ★원본 FBX 불가침 — CrassorridRM/ 사본만.
//
// ════════ ★함정 (재발 금지 — Caniathrox/Dimax 핸드오프 계승) ════════
//   1. AnimatorController 디스크 영속화: CreateAnimatorControllerAtPath로 직접 디스크 생성 + SaveAssets + ImportAsset(ForceUpdate)
//      + 재로드 검증(상태/파라미터 개수 로그). MCP로만 만들면 메모리만 저장·디스크 빈 껍데기.
//   2. AnimationEvent time = ★정규화(0~1)(importer가 클립 길이로 곱함). seconds로 넣으면 ×길이 밀려 클립 밖. MakeEvent 참고.
//   3. 루트모션은 Animator가 적용(applyRootMotion=true). generic 루트모션 측정은 Animator 스텝(정적 커브 거짓) — 본 세션 실측 완료.
//
// ★★스매시 실측 (SampleAnimation hand-Y + Animator 스텝, 2026-06-14):
//   SmashAttack_RM: 길이 1.6667s(50f@30fps), 전진(z) 3.514m, 측면0·상승0(grounded brute — Y float 없음).
//     Windup 0~15f: 양팔 1.9→5.28m 들어올림(탑뷰 가독 = 머리 위 팔 = 읽히는 브루트 예고).
//     Strike 15~30f: 내려찍기 crash(팔 5.18→0.33m), 전진 가속. ★임팩트 frame 20(손 최저 0.334m = 바닥 닿는 순간).
//     Recovery 30~50f: 중립 복귀(팔 1.9m로).
//   Run_RM: 5.744m/0.6s = 9.5728 m/s(질주 사이클 — approachSpeed 5.0으로 감속). Roar 4.6667s(오프너).
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CrassorridLabSetup
{
    const string SrcDir = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack vol 9/Crassorrid/FBX Files";
    const string RmDir = "Assets/_Project/Animations/CrassorridRM";
    const string ControllerPath = "Assets/_Project/Animations/CrassorridBrawler.controller";
    const string MaterialPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack vol 9/Crassorrid/Materials/M_Crassorrid.mat";

    // ════════ ★스매시 3구간 분할 경계 (실측 frame — 드라이버 const와 일치, desync 차단) ════════
    //   ★윈드업/스트라이크 경계 = WindupFrame(드라이버 SSOT). 임팩트 = ImpactFrame(드라이버 SSOT). 회수 시작 = SplitFrame(이 파일).
    const int   ClipFrames    = 50;     // SmashAttack_RM 총 프레임(실측 50f/1.6667s)
    const int   SrcFps        = CrassorridBrawler.SrcFps;       // 30
    const int   WindupFrame   = CrassorridBrawler.WindupFrame;  // 15 — 0~15=Windup, 15~30=Strike
    const int   ImpactFrame   = CrassorridBrawler.ImpactFrame;  // 20 — 내려찍기 닿는 프레임(Strike 구간 내)
    const int   SplitFrame    = 30;     // 15~30=Strike, 30~50=Recovery (회수 시작 = 손이 최저서 복귀 개시)

    // ★구간별 정적 speed = 브루트 무게 셰이핑(드라이버 const 단일 진실원 — state.speed와 desync 차단).
    const float WindupSpeed   = CrassorridBrawler.WindupSpeed;    // 0.5  — 느린 무거운 들어올림(LV4 텔레그래프 윈도)
    const float StrikeSpeed   = CrassorridBrawler.StrikeSpeed;    // 1.25 — 폭발적 내려찍기 commit
    const float RecoverySpeed = CrassorridBrawler.RecoverySpeed;  // 1.4  — 브리스크 회수

    // ★★importer 이벤트 time 함정: ModelImporterClipAnimation.events[].time 은 *정규화(0~1)*(importer가 클립 길이로 곱함).
    //   SmashHit은 *Strike 클립*(frame 15~30)에 박는다 → 정규화 = (임팩트 frame − WindupFrame) / (SplitFrame − WindupFrame).
    //     = (20 − 15) / (30 − 15) = 5/15 ≈ 0.3333. 임팩트 절대 frame 20은 어느 sub-clip이든 그 포즈에서 발화.
    const float SmashContactNorm = (float)(ImpactFrame - WindupFrame) / (SplitFrame - WindupFrame);  // ≈ 0.3333

    // 사본 내 세 sub-clip 이름(같은 take, frame 범위만 다름). 컨트롤러가 이름으로 로드.
    const string WindupName = "Smash_Windup";
    const string StrikeName = "Smash_Strike";
    const string RecovName  = "Smash_Recovery";

    [MenuItem("ZombieCrush/Crassorrid Lab/1. Setup Data (clips+events+material+controller)")]
    public static void SetupData()
    {
        Directory.CreateDirectory(RmDir);

        // ── 1. 머티리얼 URP 변환(이미 URP/Lit이면 생략 — 방어적) ──
        ConvertMaterialToURP();

        // ── 2. SmashAttack_RM 사본 복제(이벤트·분할용) ──
        string smashSrc = $"{SrcDir}/Crassorrid@SmashAttack_RM.fbx";
        string smashDst = $"{RmDir}/Crassorrid@SmashAttack_RM.fbx";
        CopyFbx(smashSrc, smashDst);
        AssetDatabase.Refresh();

        // ── 3. 사본을 Windup/Strike/Recovery 3 sub-clip 분할 + Strike에 SmashHit(임팩트) 주입 ──
        SplitSmashClip(smashDst);

        // ── 4. AnimatorController 빌드 ──
        BuildController(smashDst);

        Debug.Log("[CrassorridLab] 데이터 셋업 완료 — 다음: '2. Build Combat Test'.");
    }

    static void ConvertMaterialToURP()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null) { Debug.LogWarning($"[CrassorridLab] 머티리얼 미발견: {MaterialPath}"); return; }
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogWarning("[CrassorridLab] URP/Lit 미발견"); return; }
        if (mat.shader == urp) { Debug.Log("[CrassorridLab] 머티리얼 이미 URP — 변환 생략."); return; }

        Texture main = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        mat.shader = urp;
        if (main != null) mat.SetTexture("_BaseMap", main);
        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[CrassorridLab] 머티리얼 URP 변환 완료.");
    }

    static void CopyFbx(string src, string dst)
    {
        if (!File.Exists(src)) { Debug.LogError($"[CrassorridLab] 원본 FBX 미발견: {src}"); return; }
        if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
        AssetDatabase.CopyAsset(src, dst);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
    }

    // ════════ ★SmashAttack_RM를 Windup(0~15)/Strike(15~30)/Recovery(30~50) 세 sub-clip 분할 ════════
    //   같은 take에서 세 ModelImporterClipAnimation(frame 범위만 다름). Strike에만 SmashHit 주입(임팩트 frame 20 포함 구간).
    //   ★각 경계 frame(15/30)에서 인접 sub-clip lastFrame==다음 firstFrame → 경계 포즈 비트-동일(연속 보장, CUT 점프 0).
    static void SplitSmashClip(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) { Debug.LogError($"[CrassorridLab] ModelImporter 없음: {fbxPath}"); return; }

        var defaults = importer.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0) { Debug.LogError($"[CrassorridLab] 기본 클립 없음: {fbxPath}"); return; }
        string takeName = defaults[0].takeName;

        var windup = MakeClipDef(WindupName, takeName, 0,           WindupFrame, new AnimationEvent[0]);
        var strike = MakeClipDef(StrikeName, takeName, WindupFrame,  SplitFrame,  new[] { MakeEvent("SmashHit", SmashContactNorm, 0) });
        var recov  = MakeClipDef(RecovName,  takeName, SplitFrame,   ClipFrames,  new AnimationEvent[0]);
        importer.clipAnimations = new[] { windup, strike, recov };

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        // 재로드 검증.
        var sb = new System.Text.StringBuilder();
        sb.Append($"[CrassorridLab] 3분할 ({Path.GetFileName(fbxPath)}):");
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__"))
            {
                sb.Append($"  [{c.name} len={c.length:0.000}s ev={c.events.Length}");
                foreach (var e in c.events) sb.Append($" {e.functionName}@{e.time:0.000}s");
                sb.Append("]");
            }
        float windupLen = (float)WindupFrame / SrcFps, strikeLen = (float)(SplitFrame - WindupFrame) / SrcFps, recovLen = (float)(ClipFrames - SplitFrame) / SrcFps;
        sb.Append($"  (기대: {WindupName} {windupLen:0.000}s ev0 / {StrikeName} {strikeLen:0.000}s ev1 임팩트≈{SmashContactNorm*strikeLen:0.000}s / {RecovName} {recovLen:0.000}s ev0)");
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
            keepOriginalPositionY = true,    // 상승 0 보존(grounded brute)
            keepOriginalPositionXZ = false,  // ★루트모션 전진(XZ) 살림
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

    static void BuildController(string smashFbxPath)
    {
        AnimationClip windup = LoadClipByName(smashFbxPath, WindupName);
        AnimationClip strike = LoadClipByName(smashFbxPath, StrikeName);
        AnimationClip recov  = LoadClipByName(smashFbxPath, RecovName);
        AnimationClip idle   = LoadClipByName($"{SrcDir}/Crassorrid@Idle.fbx", null);
        AnimationClip run    = LoadClipByName($"{SrcDir}/Crassorrid@Run_RM.fbx", null);

        if (windup == null || strike == null || recov == null || idle == null || run == null)
        { Debug.LogError("[CrassorridLab] 클립 로드 실패 — 컨트롤러 빌드 중단"); return; }

        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ★파라미터(2) — 트리거 smash + bool isApproaching. (★Roar 제거 2026-06-14: attack 트리거·오프너 폐기, Idle→Approach 직행.)
        //   (로코모션은 단일 Approach 상태라 BlendTree 없음 → "Float여야" 함정 없음.)
        ctrl.AddParameter("isApproaching", AnimatorControllerParameterType.Bool);    // Idle→Approach, Approach 자기루프 지속
        ctrl.AddParameter("smash", AnimatorControllerParameterType.Trigger);         // Approach→SmashWindup (도착+슬램)

        var sm = ctrl.layers[0].stateMachine;

        // ── Idle (결정 허브) ──
        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        sm.defaultState = sIdle;

        // ── Approach (Run_RM 루트모션 접근, 비루프 → 자기루프로 지속) ──
        //   ★state speed=1.0(배율 누수 방지). 드라이버가 Approach 상태에서만 modelAnimator.speed = approachSpeed/RunNativeSpeed 적용.
        var sApp = sm.AddState("Approach");
        sApp.motion = run; sApp.speed = 1.0f;

        // ── 스매시 3구간 (Windup→Strike→Recovery, 구간별 정적 speed = 브루트 무게 셰이핑) ──
        var sWindup = sm.AddState("SmashWindup");
        sWindup.motion = windup; sWindup.speed = WindupSpeed;     // 0.5 — 느린 무거운 들어올림(텔레그래프 차오름)
        var sStrike = sm.AddState("SmashStrike");
        sStrike.motion = strike; sStrike.speed = StrikeSpeed;     // 1.25 — 폭발적 내려찍기(★SmashHit 여기)
        var sRecov = sm.AddState("SmashRecovery");
        sRecov.motion = recov; sRecov.speed = RecoverySpeed;      // 1.4 — 브리스크 회수

        // ════════ 전이 (정체성 동작 전이 전부 CUT dur0 — 한 동작씩 완결) ════════
        // ★Idle → Approach: 타깃 인지 즉시 직행(bool isApproaching). ★Roar 오프너 폐기 2026-06-14 — 인지하면 바로 접근.
        var tIdleApp = sIdle.AddTransition(sApp);
        tIdleApp.AddCondition(AnimatorConditionMode.If, 0, "isApproaching");
        tIdleApp.hasExitTime = false; tIdleApp.duration = 0f;

        // Approach → Approach: Run_RM 비루프 → 자기루프로 접근 지속(isApproaching 유지 동안).
        var tAppLoop = sApp.AddTransition(sApp);
        tAppLoop.AddCondition(AnimatorConditionMode.If, 0, "isApproaching");
        tAppLoop.hasExitTime = true; tAppLoop.exitTime = 0.98f; tAppLoop.duration = 0f;

        // Approach → SmashWindup: 도착+토큰(트리거 smash). ★CUT.
        var tAppWindup = sApp.AddTransition(sWindup);
        tAppWindup.AddCondition(AnimatorConditionMode.If, 0, "smash");
        tAppWindup.hasExitTime = false; tAppWindup.duration = 0f;

        // ★★스매시 3구간 체인 (Windup→Strike→Recovery): 같은 take 분할 → 경계 포즈 비트-동일 → CUT(dur0) 포즈 점프 0.
        ChainCut(sWindup, sStrike);
        ChainCut(sStrike, sRecov);

        // SmashRecovery → Idle: 회수 완결(ExitTime). 드라이버가 Idle서 다음 접근 결정(호흡 후 isApproaching).
        var tRecovIdle = sRecov.AddTransition(sIdle);
        tRecovIdle.hasExitTime = true; tRecovIdle.exitTime = 0.95f; tRecovIdle.duration = 0f;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);

        // ★재로드 검증.
        var reloaded = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int stateCount = reloaded != null && reloaded.layers.Length > 0 ? reloaded.layers[0].stateMachine.states.Length : -1;
        int paramCount = reloaded != null ? reloaded.parameters.Length : -1;
        Debug.Log($"[CrassorridLab] 컨트롤러 빌드+영속화 — 재로드 상태 수={stateCount} (기대 5: Idle/Approach/SmashWindup/SmashStrike/SmashRecovery — ★Roar 제거), 파라미터 수={paramCount} (기대 2: isApproaching/smash — ★attack 제거). ★스매시 3구간(Windup={WindupSpeed}/Strike={StrikeSpeed}/Recovery={RecoverySpeed}, 경계 frame {WindupFrame}/{SplitFrame}/{ClipFrames}, 임팩트 frame {ImpactFrame}).");
    }

    // 3구간 내부 체인 — 같은 take 분할이라 경계 포즈 비트-동일 → ExitTime CUT(dur0) 연속(crossfade 아님).
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
        Debug.LogError($"[CrassorridLab] 클립 없음: {fbxPath} (name={clipName ?? "<first>"})");
        return null;
    }
}
