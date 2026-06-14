// Dimaxillosaurus 데이터 셋업 — 클립 복제(이벤트 주입용 사본)·AnimationEvent(ClawHit) 주입·머티리얼 URP 변환·AnimatorController 빌드.
// ★원본 .meta 보존: 이벤트는 원본이 아니라 DimaxRM/ 복제 사본에 박는다(Venodonte 전례 — 사본 import 설정에 events:).
//
// ════════ ★함정 (재발 금지, 핸드오프 §4) ════════
//   1. AnimatorController 디스크 영속화: MCP로 만들면 메모리만 저장·디스크 빈 껍데기 → SaveAssets + ImportAsset(ForceUpdate)
//      + 재로드 검증(상태 개수 로그). (이 에디터는 AssetDatabase.CreateAsset로 직접 디스크 생성 → 영속화는 SaveAssets로 확정.)
//   2. AnimationEvent는 클립 import 설정(ModelImporter.clipAnimations[].events)에 박는다 — ★time은 *정규화(0~1)*
//      (importer가 클립 길이로 곱함). seconds로 넣으면 ×길이 밀려 마지막 이벤트가 클립 밖. 상세는 MakeEvent 위 블록.
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class DimaxillosaurusLabSetup
{
    const string SrcDir = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 7/Dimaxillosaurus/FBX Files";
    const string RmDir = "Assets/_Project/Animations/DimaxRM";
    const string ControllerPath = "Assets/_Project/Animations/DimaxillosaurusBrawler.controller";
    const string MaterialPath = "Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 7/Dimaxillosaurus/Materials/M_Dimaxillosaurus.mat";

    // ════════ ★"벽처럼 오는 클로월" 재설계 (2026-06-14 유저 디렉팅) ════════
    //   "멀리서 발견 → 그 자리에서 포효 → 좌·우·좌·우 클로 반복하며 나한테 와 → 장판 없음 → 벽처럼."
    //   → 상태머신: Idle →(타깃 인지 즉시 attack)→ Roar(오프너 1회) → L_Windup → ★Recovery→반대손 Windup *직행*(Idle 우회) → 무한 좌우.
    //     ★★(2026-06-14 "끊임없는 좌우" 개정) (구) Recovery→Idle→재조준 비트→다음손 경로에서 Idle 클립이 깔리던 "잠시 쉼"을 제거.
    //       Recovery가 끝나면 반대 손 Windup으로 바로 전이(무봉제 연타) + 추적(재조준)을 각 클로의 Windup(cocking)에 접음.
    //     클로 단발 루트모션(각 2.22m)이 전진 = 이동수단. 별도 Approach(run/walk) 상태 없음(제거 — 클로질이 곧 접근).
    //     ★장판 텔레그래프 제거(Dimax 미사용) — TelegraphClaw 이벤트 미주입. ClawHit(컨택 히트 모먼트)만 주입(향후 데미지 훅).
    //   각 단발 1상태=1클립=정체성 동작(CUT 전이). 드라이버가 _nextRight 플래그로 다음 손 교대.
    //
    // ★★단발 클로 _RM 실측(SampleAnimation 본추적, 2026-06-14 — L/R 봉투 동일):
    //   LeftClawsAttackForward_RM:  길이 1.1667s, 전진(z) 2.2177m(1.901 m/s), 측면0·상승0. L발톱 컨택 norm 0.350(≈0.408s).
    //   RightClawsAttackForward_RM: 길이 1.1667s, 전진(z) 2.2177m(1.901 m/s), 측면0·상승0. R발톱 컨택 norm 0.367(≈0.428s).
    // ════════ ★★스윙 = 4구간 이즈 분할 (2026-06-14 유저 디렉팅: "휘두르는 거 더 빠르게 + 앞부분 빠르게·뒷부분 빠르게·자연스럽게 연결, 휘릭휘릭휘릭") ════════
    //   ★방향 정정: (구) "ClawSpeed 1.0 자연 / RecoverySpeed 3.0 / 빨리감기 ❌"는 *flat 균일 2.5× 빨리감기*에 대한 거부였다.
    //     오늘 유저가 "이즈(가속) 곡선으로 빠르게"로 마음 바꿈 — 균일이 아니라 *가속 셰이핑*이면 OK. 그래서 스윙을 4구간으로 쪼개
    //     구간별 정적 state.speed의 *계단형 이즈 근사*를 만든다(per-frame 코드 speed 곡선 = 헌법 위반이라 금지).
    //   한 state의 speed는 클립 전체 균일 → "앞 빠르게·중간 자연·뒤 빠르게"는 단일 클립으로 불가 → 같은 take를 frame 범위만 다르게 4분할:
    //     ① ClawX_Windup    = frame 0~9  : 윈드업/cocking. speed WindupSpeed(1.9, 앞 빠르게 — cocking은 죽은 시간).
    //     ② ClawX_Strike    = frame 9~16 : 컨택(f12)+초기 팔로스루. speed StrikeSpeed(1.35, 읽히는 히트=상대적 느림). ★ClawHit 여기.
    //     ③ ClawX_FollowOut = frame 16~22: 후기 팔로스루. speed FollowSpeed(2.3, 뒤 빠르게 — 휙 빠짐).
    //     ④ ClawX_Recovery  = frame 22~35: 중립 복귀. speed RecoverySpeed(2.5, 빠르게 — FollowOut과 이음매 매끄럽게).
    //   ★연속성: 같은 take에서 잘랐으므로 각 구간 경계 포즈(frame 9/16/22)는 *비트-동일* → CUT(dur0) 전이여도 포즈 점프 0
    //     (한 동작의 4분할 = 매끄러움). crossfade 아님(헌법 준수).
    //   ★루트모션 손실 0: 4구간이 frame 0~9/9~16/16~22/22~35를 각자 운반 → 합 = 풀클립 2.2177m(트림과 달리 전부 *재생*).
    //   ★이즈 램프 = 1.9 → 1.35 → 2.3 → 2.5. (구) 1.0→3.0 하드 점프("휙 채서 어색" 1순위 우려) 소멸: Strike만 상대적 느림(히트 앵커),
    //     2.3→2.5는 거의 이음매 없음. "휘릭" = 빠른 cocking + 크리스피 히트 + 휙 빠지는 팔로스루 + 탁 다음 손.
    //
    //   ★★구간 경계 실측 근거(SampleAnimation L Finger 전방 reach + rootZ, 2026-06-14):
    //     frame 0~9 = 윈드업(발톱 뒤로 cocking, reach 낮음). frame 12 = 컨택(peak reach 2.49). frame 12~16 = 초기 팔로스루(2.49→1.95).
    //     frame 16~22 = 후기 팔로스루(reach 1.95→1.14, rootZ 86% 도달). frame 22~35 = 중립 복귀(발톱이 호를 지나 집으로).
    //     → WindupFrame 9 = 컨택 직전(cocking 끝, 타격 시작 직전). StrikeFrame 16 = 초기 팔로스루 끝. SplitFrame 22 = 후기 팔로스루 끝·회수 시작.
    const int   ClipFrames    = 35;     // ★단발 원본 프레임 수(실측 35프레임/1.1667s = 30fps)
    const int   SrcFps        = 30;
    const int   WindupFrame   = 9;      // ★윈드업/타격 경계(컨택 frame 12 직전 — cocking 끝). 0~9=Windup, 9~16=Strike.
    const int   StrikeFrame   = 16;     // ★타격/후기팔로스루 경계(초기 팔로스루 끝). 9~16=Strike, 16~22=FollowOut.
    const int   SplitFrame    = 22;     // ★팔로스루/회수 경계(실측 — 후기 팔로스루 끝·회수 시작). 16~22=FollowOut, 22~35=Recovery.

    // ★★구간별 정적 speed = 이즈 곡선의 계단형 근사(노브 — 유저 ▶ 판정). 드라이버 const 단일 진실원(state.speed와 desync 차단).
    //   튜닝 = 드라이버 const + SetupData 재실행. "Strike 너무 느림/빠름" → StrikeSpeed, "팔로스루 더 휙" → FollowSpeed, "이음매 거침" → FollowSpeed↔RecoverySpeed 격차 축소.
    const float WindupSpeed   = DimaxillosaurusBrawler.WindupSpeed;    // 1.9 — 앞부분 빠르게(cocking)
    const float StrikeSpeed   = DimaxillosaurusBrawler.StrikeSpeed;    // 1.35 — 읽히는 히트(★ClawHit 구간)
    const float FollowSpeed   = DimaxillosaurusBrawler.FollowSpeed;    // 2.3 — 뒷부분 빠르게(팔로스루)
    const float RecoverySpeed = DimaxillosaurusBrawler.RecoverySpeed;  // 2.5 — 중립 복귀(이음매 매끄럽게)

    // ★★importer 이벤트 time 함정(실측 2026-06-13): ModelImporterClipAnimation.events[].time 은 *정규화(0~1)* 로
    //   해석돼 import 시 클립 길이로 곱해진다(seconds로 넣으면 ×길이 밀려 마지막 이벤트가 클립 밖으로 나감).
    //   ★ClawHit은 이제 *Strike 클립*(frame 9~16)에 박는다 → 정규화 = (컨택절대프레임 − WindupFrame) / (StrikeFrame − WindupFrame).
    //     컨택 절대 프레임(12.25 L / 12.845 R)은 불변 — 어느 sub-clip에 들어가든 그 포즈에서 발화. Strike만 speed 1.35라 절대초는 윈드업 가속으로 앞당겨짐(=더 빠른 히트, 의도대로).
    const float SrcLeftContactFrame  = 0.350f * 35f;  // = 12.25 (원본 절대 프레임, 실측 norm×35프레임)
    const float SrcRightContactFrame = 0.367f * 35f;  // = 12.845
    const float LeftStrikeContactNorm  = (SrcLeftContactFrame  - WindupFrame) / (StrikeFrame - WindupFrame); // = 3.25/7 ≈ 0.464
    const float RightStrikeContactNorm = (SrcRightContactFrame - WindupFrame) / (StrikeFrame - WindupFrame); // = 3.845/7 ≈ 0.549

    // ★사본 내 네 sub-clip 이름(같은 take, frame 범위만 다름). 컨트롤러가 이름으로 로드한다.
    const string LWindupName = "LeftClaw_Windup";
    const string LStrikeName = "LeftClaw_Strike";
    const string LFollowName = "LeftClaw_FollowOut";
    const string LRecovName  = "LeftClaw_Recovery";
    const string RWindupName = "RightClaw_Windup";
    const string RStrikeName = "RightClaw_Strike";
    const string RFollowName = "RightClaw_FollowOut";
    const string RRecovName  = "RightClaw_Recovery";

    [MenuItem("ZombieCrush/Dimaxillosaurus Lab/1. Setup Data (clips+events+material+controller)")]
    public static void SetupData()
    {
        Directory.CreateDirectory(RmDir);

        // ── 1. 머티리얼 URP 변환(Standard → URP/Lit, 마젠타 회피) ──
        ConvertMaterialToURP();

        // ── 2. 클립 사본 복제(이벤트 주입 + 스윙/회수 분할용) ──
        //   ★클로월: 분할 대상 = 단발 클로 L/R(Forward_RM, 각 전진 2.22m). 루트모션은 Animator가 적용(applyRootMotion=true).
        string leftSrc  = $"{SrcDir}/Dimaxillosaurus@LeftClawsAttackForward_RM.fbx";
        string leftDst  = $"{RmDir}/Dimaxillosaurus@LeftClawsAttackForward_RM.fbx";
        string rightSrc = $"{SrcDir}/Dimaxillosaurus@RightClawsAttackForward_RM.fbx";
        string rightDst = $"{RmDir}/Dimaxillosaurus@RightClawsAttackForward_RM.fbx";
        CopyFbx(leftSrc, leftDst);
        CopyFbx(rightSrc, rightDst);

        // 포효·아이들 클립은 원본 직접 참조(복제 불요 — 이벤트 없음).
        AssetDatabase.Refresh();

        // ── 3. 각 사본을 Windup/Strike/FollowOut/Recovery 네 sub-clip으로 분할 + Strike에 ClawHit 주입(컨택 모먼트). ──
        //   ★장판 텔레그래프 제거(TelegraphClaw 미주입).
        SplitClawClip(leftDst,  LWindupName, LStrikeName, LFollowName, LRecovName, LeftStrikeContactNorm);
        SplitClawClip(rightDst, RWindupName, RStrikeName, RFollowName, RRecovName, RightStrikeContactNorm);

        // ── 4. AnimatorController 빌드 ──
        BuildController(leftDst, rightDst);

        Debug.Log("[DimaxLab] 데이터 셋업 완료 — 다음: '2. Build Combat Test'.");
    }

    static void ConvertMaterialToURP()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (mat == null) { Debug.LogWarning($"[DimaxLab] 머티리얼 미발견: {MaterialPath}"); return; }
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogWarning("[DimaxLab] URP/Lit 셰이더 미발견"); return; }
        if (mat.shader == urp) { Debug.Log("[DimaxLab] 머티리얼 이미 URP — 변환 생략"); return; }

        // 빌트인 Standard의 알베도/메인텍스를 URP _BaseMap으로 옮긴다.
        Texture main = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
        Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        mat.shader = urp;
        if (main != null) mat.SetTexture("_BaseMap", main);
        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Smoothness", 0.2f);
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
        Debug.Log("[DimaxLab] 머티리얼 URP 변환 완료.");
    }

    static void CopyFbx(string src, string dst)
    {
        if (!File.Exists(src)) { Debug.LogError($"[DimaxLab] 원본 FBX 미발견: {src}"); return; }
        if (File.Exists(dst)) AssetDatabase.DeleteAsset(dst);
        AssetDatabase.CopyAsset(src, dst);
        AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
    }

    // ════════ ★단발 클로를 Windup(0~9)/Strike(9~16)/FollowOut(16~22)/Recovery(22~35) 네 sub-clip으로 분할 ════════
    //   같은 take에서 네 ModelImporterClipAnimation을 만든다(frame 범위만 다름). Strike에만 ClawHit 주입(컨택 frame 12 포함 구간).
    //   ★각 경계 frame(9/16/22)에서 인접 sub-clip의 lastFrame==다음 firstFrame → 경계 포즈 비트-동일(연속 보장).
    static void SplitClawClip(string fbxPath, string windupName, string strikeName, string followName, string recovName, float strikeContactNorm)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) { Debug.LogError($"[DimaxLab] ModelImporter 없음: {fbxPath}"); return; }

        // 원본 take 이름 — 기존 사본은 단일 클립이라 defaultClipAnimations[0]에서 takeName/internalID 회수.
        var defaults = importer.defaultClipAnimations;
        if (defaults == null || defaults.Length == 0) { Debug.LogError($"[DimaxLab] 기본 클립 없음: {fbxPath}"); return; }
        string takeName = defaults[0].takeName;

        var windup = MakeClipDef(windupName, takeName, 0,           WindupFrame, new AnimationEvent[0]);
        var strike = MakeClipDef(strikeName, takeName, WindupFrame,  StrikeFrame, new[] { MakeEvent("ClawHit", strikeContactNorm, 0) });
        var follow = MakeClipDef(followName, takeName, StrikeFrame,  SplitFrame,  new AnimationEvent[0]);
        var recov  = MakeClipDef(recovName,  takeName, SplitFrame,   ClipFrames,  new AnimationEvent[0]);
        importer.clipAnimations = new[] { windup, strike, follow, recov };

        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        // 재로드 검증 — 네 sub-clip이 디스크에 박혔고 길이·이벤트가 기대대로인가.
        var sb = new System.Text.StringBuilder();
        sb.Append($"[DimaxLab] 4분할 ({System.IO.Path.GetFileName(fbxPath)}):");
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
            keepOriginalPositionY = true,    // 기존 사본과 동일(상승 0 보존)
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
        // 클립 로드 — ★4구간 이즈 분할: 사본당 네 sub-clip을 *이름으로* 로드.
        AnimationClip lWindup = LoadClipByName(leftFbxPath,  LWindupName);
        AnimationClip lStrike = LoadClipByName(leftFbxPath,  LStrikeName);
        AnimationClip lFollow = LoadClipByName(leftFbxPath,  LFollowName);
        AnimationClip lRecov  = LoadClipByName(leftFbxPath,  LRecovName);
        AnimationClip rWindup = LoadClipByName(rightFbxPath, RWindupName);
        AnimationClip rStrike = LoadClipByName(rightFbxPath, RStrikeName);
        AnimationClip rFollow = LoadClipByName(rightFbxPath, RFollowName);
        AnimationClip rRecov  = LoadClipByName(rightFbxPath, RRecovName);
        AnimationClip idle    = LoadClipByName($"{SrcDir}/Dimaxillosaurus@Idle.fbx", null);
        AnimationClip roar    = LoadClipByName($"{SrcDir}/Dimaxillosaurus@Roar.fbx", null);

        if (lWindup == null || lStrike == null || lFollow == null || lRecov == null
         || rWindup == null || rStrike == null || rFollow == null || rRecov == null || idle == null || roar == null)
        { Debug.LogError("[DimaxLab] 클립 로드 실패 — 컨트롤러 빌드 중단"); return; }

        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // ★파라미터(3) — 트리거 3개로 손 명시 라우팅(로코모션 파라미터 없음 → BlendTree 없음 → "Float여야" 함정 자체 소멸).
        ctrl.AddParameter("attack", AnimatorControllerParameterType.Trigger);   // 오프너: Idle→Roar (타깃 인지 1회)
        ctrl.AddParameter("chainL", AnimatorControllerParameterType.Trigger);   // ★좌 단발: Idle→LeftClaw_Windup 직행
        ctrl.AddParameter("chainR", AnimatorControllerParameterType.Trigger);   // ★우 단발: Idle→RightClaw_Windup 직행. 드라이버 _nextRight로 둘 중 하나.

        var sm = ctrl.layers[0].stateMachine;

        // ── Idle (결정 허브: 미교전→포효 / 교전→재조준 비트→다음 단발) ──
        var sIdle = sm.AddState("Idle");
        sIdle.motion = idle;
        sm.defaultState = sIdle;

        // ── Roar (앵티시페이션·위협 텔레그래프, 압축 재생) ──
        var sRoar = sm.AddState("Roar");
        sRoar.motion = roar;
        sRoar.speed = 5.0f;   // Roar 2.833s를 ~0.57s로 압축 — 짧은 위협 rear-up(오프너). 정적 speed라 스크럽 아님.

        // ── 단발 = Windup → Strike → FollowOut → Recovery 네 상태씩 (구간별 정적 speed = 이즈 램프) ──
        //   ★Windup(0~9): cocking. speed WindupSpeed(1.9, 앞 빠르게). ★Strike(9~16): 컨택+초기 팔로스루. speed StrikeSpeed(1.35, 읽히는 히트). ★ClawHit 여기.
        //   ★FollowOut(16~22): 후기 팔로스루. speed FollowSpeed(2.3, 뒤 빠르게). ★Recovery(22~35): 중립 복귀. speed RecoverySpeed(2.5).
        //   ★정적 speed(코드 매프레임 스크럽 금지 — 사고 #3). 각 sub-clip 1상태=정체성 동작의 분할. 4구간 = 이즈 곡선의 계단형 근사.
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
        // Idle → Roar: ★오프너(트리거 attack) — 타깃 인지 1회만.
        var tIdleRoar = sIdle.AddTransition(sRoar);
        tIdleRoar.AddCondition(AnimatorConditionMode.If, 0, "attack");
        tIdleRoar.hasExitTime = false; tIdleRoar.duration = 0f;

        // ★Idle → LeftClaw_Windup / RightClaw_Windup: 체인(트리거 chainL/chainR) — 다음 단발의 *윈드업*으로 직행. ★CUT(dur0).
        var tIdleLWindup = sIdle.AddTransition(sLWindup);
        tIdleLWindup.AddCondition(AnimatorConditionMode.If, 0, "chainL");
        tIdleLWindup.hasExitTime = false; tIdleLWindup.duration = 0f;

        var tIdleRWindup = sIdle.AddTransition(sRWindup);
        tIdleRWindup.AddCondition(AnimatorConditionMode.If, 0, "chainR");
        tIdleRWindup.hasExitTime = false; tIdleRWindup.duration = 0f;

        // Roar → LeftClaw_Windup: 포효 완결 후 자동(ExitTime) — 오프너는 항상 좌 단발로 시작. ★CUT(dur0).
        var tRoarWindup = sRoar.AddTransition(sLWindup);
        tRoarWindup.hasExitTime = true; tRoarWindup.exitTime = 0.95f; tRoarWindup.duration = 0f;

        // ★★단발 4구간 체인 (Windup→Strike→FollowOut→Recovery): 전부 같은 동작의 분할 → 경계 포즈 비트-동일 → CUT(dur0)여도 포즈 점프 0.
        //   각 구간 완결(ExitTime ~1.0)에 다음 구간이 *바로 그 포즈*에서 이어져 구간별 speed로 재생. crossfade 아님(한 동작의 이즈 분할).
        ChainCut(sLWindup, sLStrike);   ChainCut(sLStrike, sLFollow);   ChainCut(sLFollow, sLRecov);
        ChainCut(sRWindup, sRStrike);   ChainCut(sRStrike, sRFollow);   ChainCut(sRFollow, sRRecov);

        // ════════ ★★Recovery → *반대 손* Windup 직행 (2026-06-14 "끊임없는 좌우" 개정 — Idle "잠시 쉼" 제거) ════════
        //   (구) Recovery→Idle→(chainGap 재조준 비트)→다음손에서 Idle 클립이 깔리던 "잠시 쉼"을 제거.
        //   회수가 끝나는 ExitTime(0.98)에 *반대 손 Windup으로 직행*(Idle 미경유) — 드라이버가 Recovery 진입에 다음손 chain trigger를 쏜다.
        //   ★전이 *순서*가 핵심: 트리거-게이트 직행을 **먼저** 추가, Idle 폴백을 **나중**에(같은 exitTime 0.98). Animator는 리스트 순서로 평가 →
        //     trigger 셋이면 직행, 아니면(타깃 소실 폴백) Idle. ★경계 포즈: Recovery 끝(frame35 중립) ≈ 반대손 Windup 시작(frame0 중립)이라 CUT 점프 작음.
        var tLRecovRWindup = sLRecov.AddTransition(sRWindup);   // L 회수 끝 → R 윈드업(chainR)
        tLRecovRWindup.AddCondition(AnimatorConditionMode.If, 0, "chainR");
        tLRecovRWindup.hasExitTime = true; tLRecovRWindup.exitTime = 0.98f; tLRecovRWindup.duration = 0f;

        var tRRecovLWindup = sRRecov.AddTransition(sLWindup);   // R 회수 끝 → L 윈드업(chainL)
        tRRecovLWindup.AddCondition(AnimatorConditionMode.If, 0, "chainL");
        tRRecovLWindup.hasExitTime = true; tRRecovLWindup.exitTime = 0.98f; tRRecovLWindup.duration = 0f;

        // ★폴백(타깃 소실 등 trigger 미셋 시에만): Recovery → Idle. ★직행 전이보다 *나중*에 추가(순서상 후순위 = 트리거 셋이면 직행이 이김).
        var tLRecovIdle = sLRecov.AddTransition(sIdle);
        tLRecovIdle.hasExitTime = true; tLRecovIdle.exitTime = 0.98f; tLRecovIdle.duration = 0f;

        var tRRecovIdle = sRRecov.AddTransition(sIdle);
        tRRecovIdle.hasExitTime = true; tRRecovIdle.exitTime = 0.98f; tRRecovIdle.duration = 0f;

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);

        // ★재로드 검증 — 디스크에 상태머신이 박혔나(빈 껍데기 함정).
        var reloaded = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        int stateCount = reloaded != null && reloaded.layers.Length > 0 ? reloaded.layers[0].stateMachine.states.Length : -1;
        int paramCount = reloaded != null ? reloaded.parameters.Length : -1;
        Debug.Log($"[DimaxLab] 컨트롤러 빌드+영속화 — 재로드 상태 수={stateCount} (기대 10: Idle/Roar + L/R × Windup/Strike/FollowOut/Recovery), 파라미터 수={paramCount} (기대 3: attack/chainL/chainR). ★클로월 4구간 이즈 램프(Windup={WindupSpeed}/Strike={StrikeSpeed}/Follow={FollowSpeed}/Recovery={RecoverySpeed}, 경계 frame {WindupFrame}/{StrikeFrame}/{SplitFrame}/{ClipFrames}).");
    }

    // 단발 4구간 내부 체인 전이 — 같은 동작의 분할이라 경계 포즈 비트-동일 → ExitTime CUT(dur0) 연속(crossfade 아님).
    static void ChainCut(AnimatorState from, AnimatorState to)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true; t.exitTime = 0.99f; t.duration = 0f;
    }

    // 사본의 sub-clip을 *이름으로* 로드(분할로 한 FBX에 2개 클립). name=null이면 첫 비-__ 클립(Idle/Roar 단일).
    static AnimationClip LoadClipByName(string fbxPath, string clipName)
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
            if (o is AnimationClip c && !c.name.StartsWith("__"))
            {
                if (clipName == null || c.name == clipName) return c;
            }
        Debug.LogError($"[DimaxLab] 클립 없음: {fbxPath} (name={clipName ?? "<first>"})");
        return null;
    }
}
