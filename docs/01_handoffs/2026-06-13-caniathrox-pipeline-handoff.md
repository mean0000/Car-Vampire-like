# 핸드오프: Caniathrox 파이프라인 — 몬스터 1종 완성 (재사용 틀 + 교훈)

> **세션**: 2026-06-13. 몬스터 공격 구현 첫 수직 슬라이스 — Caniathrox(LV2 추격자, 개[犬]형)를 룩/AI/연출까지 완성하며 **나머지 29종의 작업 틀**을 확립. HP·데미지 같은 전투 스탯은 범위 밖(룩/체감 슬라이스).
> **상위 연계**: [[2026-06-13-topdown-attack-grammar]](공격 문법) · [[2026-06-12-monster-roster-classification]](로스터) · `.claude/agents/animation.md`(애니 전담 에이전트 — 이 세션에서 신설)

## 0. ★북극성 — 모든 몬스터가 향하는 가치 (유저 확정 2026-06-13, 기술보다 먼저 읽어라)

> 아래 §2~6의 틀·함정은 **"어떻게"**(기술)다. 이 여섯은 **"무엇을 향해"**(가치)다. 다음 종(Venodonte)도, 그 다음도 — 기술을 따라하기 전에 이걸 먼저 향하라. **굼뜨거나·바보 같거나·우스우면 기술이 맞아도 실패다.**

1. **진짜 살아있는 생명감** — 애니메이션이 진짜 생물처럼. 애니가 주인, 코드는 연결만. (개구리 폴짝 ❌ / 포식자 돌진 ✓)
2. **속도감·액션성 (유저 1순위)** — 굼뜨면 실패. "빠르고 화려하게."
3. **위협감** — 진짜 위협이어야 한다. 기 모아 폭발, 예측해 요격.
4. **영리함** — 바보 AI 금지. 흔한 표준 기법을 찾아(웹 리서치) 제대로.
5. **장인정신** — "초등학생 게임 수준" 거부. 본질을 이해하고 제대로 만든다.
6. **★플레이어 수용성 — 이 공격을 플레이어가 어떻게 받아들이나** — 적 입장에서만 만들지 마라. 이 공격이 플레이어에게 **어떻게 읽히고(예고가 보이나)·반응 가능하고(피할 수 있나)·체감되는지(공정한가, 긴장되나)**를 깊게 파악하며 짠다. 모든 윈드업·타이밍·장판·탄속·궤적은 "플레이어가 이걸 보고 무엇을 느끼고 어떻게 반응할까"에 답해야 한다. 권위 = [[2026-06-13-topdown-attack-grammar]](모양=영역·채움=타이밍·공정성 캐넌이 전부 플레이어 지각 중심).

> 오늘 Caniathrox가 이 여섯에 도달한 경로가 §6 연출 교훈이다 — 폴짝(생명감 위반)→포식자, 굼뜸(속도감)→모았다가 팍, 직선(위협감)→예측 요격, 바보(영리함)→군중 AI 4기법. 다음 종은 *그 사례*가 아니라 *이 여섯 가치*를 재현하라.

## 1. 완성된 것 (Caniathrox)
플레이 가능한 미니 전투 테스트(`Greybox_CaniathroxLab`, `Build Combat Test` 메뉴). 유저 플레이 판정 통과:
- **상태머신 공격**: IdleAngry → Approach(Run_RM) → [거리분기] → **Bite(근접 BiteForward_RM)** / **Coil(응축)→Lunge(도약 JumpLunge_RM)** → IdleAngry. (Spit 상태는 고아 보존 — 원거리용 미완)
- **군중 AI 4기법**: steering(곡선 추적)·separation(안 겹침)·surround(측면/후방 포위)·attack token(동시 2마리만 공격).
- **거리 분기**: 가까이(<2.5m) 물기 / 멀리 도약 — 도약 오버슈트(가까운 플레이어 지나쳐 뒤로 착지) 해소.
- **속도 2단**: 플레이어 걷기 5.5 / 질주(Shift) 9.0, 적 접근 7.0(그 사이).
- **도약 연출 "모았다가 팍"**: Coil(spd0.4 느린 응축) → Lunge(spd1.8 빠른 발사) 타이밍 대비 + Y bake(상승 0.28m→0, 낮은 돌진 = "개구리 폴짝" 제거).
- **예측 요격**: Coil 중 플레이어 미래 위치(pos + vel×leadTime 0.5s) 조준 → Lunge 발사 시 고정.
- **플레이어 조작**: LabPlayerController(WASD+Shift), LabSimpleCamera(45°/15m 추종), LabCombatSpawner(6마리).

## 2. ★재사용 틀 — 다음 종 파이프라인 (이 순서로)
1. **클립 실측 먼저** — Animator 스텝으로 루트모션 측정(정적 커브 ❌, §4-2). 종별 킷의 전진/상승/길이.
2. **상태머신 구축** — AnimatorController. 상태=클립, 정체성 동작 전이=CUT(dur0), 로코모션 이음새만 블렌드. ★디스크 영속화 검증(§4-1).
3. **드라이버** — `CaniathroxChaser` 패턴 복제: 상태 트리거+추적만, 위치/포즈는 루트모션. 거리분기·토큰·steering 재사용.
4. **군중 AI** — `AttackTokenPool` 공유, separation Roster, surround 슬롯 분배(스포너).
5. **연출 튜닝** — 윈드업/타이밍 대비/Y bake로 "느낌". state speed·노브.
6. **검증** — 디스크 렌더 캡처(정지 골격)→**유저 ▶ 플레이**(모션/속도감/체감은 유저만).

## 3. ★헌법 (애니메이션 — 유저가 가장 비싸게 가르침, `animation.md`)
- **한 동작 진행 중엔 그 애니만** — 정체성 동작(공격·도약·물기)에 crossfade 금지. 동작 완결 후 전환.
- **공격 = 상태 시퀀스** — 접근→정지→공격, Animator 상태머신이 강제.
- **애니가 진실, 코드는 위치/포즈 안 만듦** — 루트모션이 전진/궤적. 코드는 상태전이·트리거·**회전(방향)만**.
- **회전 경계**: 로코모션(Approach)·조준(Coil) 중 회전 O(방향=AI 의도), **발사(Lunge)·물기 중 회전 0**(궤적 보존). 이 경계가 `if/else if` 분기 구조로 강제됨.

## 4. ★기술 함정 (재발 금지 — 이번 세션에서 실제로 당함)
1. **AnimatorController 디스크 영속화** — MCP로 상태머신 만들면 **메모리에만 저장되고 디스크는 빈 껍데기**(`m_StateMachine: {fileID: 0}`)가 된다. 2회 당함. 반드시 `AssetDatabase.SaveAssets()` + `ImportAsset(ForceUpdate)` + **재로드 검증**(LoadAssetAtPath로 상태 개수 로그). 에셋 변경 후엔 항상 ForceUpdate로 디스크↔메모리 동기(MCP 백그라운드는 도메인 리로드를 큐잉만 함).
2. **루트모션 측정** — generic rig는 `AnimationUtility.GetEditorCurve`(정적 커브)가 **거짓**(0/오기) 반환. JumpBite_RM을 "0.28m 상승"으로 오측해 코드 포물선 사고를 냈다. 진짜 루트모션은 **Animator로 실제 스텝해 transform delta 측정**.
3. **MCP 플레이모드 진입 막힘** — 하니스 제약(`isPlaying=true` 즉시 되돌아감). 모션 흐름·속도감·VFX는 **유저 ▶ 플레이로만** 판정. 에디터 Animator 스텝 캡처는 정지 골격(자세·Y)까지만.
4. **서드파티 팩 컴파일 막힘** — `ExplosiveLLC`(SuperCharacterController의 글로벌 `PlayerCamera`가 ithappy를 깸 + RPG Character `CharacterState`)가 18에러로 플레이 차단. 안 쓰는 데모 팩이라 `_DisabledPackages/`로 격리해 해소(git 미추적). 우리 `_Project`는 무참조 확인 후.
5. **static 컬렉션 도메인 리로드 off** — Enter Play Mode Options 켜지면 static(Roster)이 세션 간 잔존. `[RuntimeInitializeOnLoadMethod(SubsystemRegistration)]`로 Clear.
6. **Y bake 방법** — 클립 import `lockRootHeightY=true`(Bake Into Pose Y)로 상승 제거. ★원본 .meta 수정이 걱정되면 **클립 복제 사본**에 적용(JumpLunge_RM처럼). 원본 보존.

## 5. 클립 루트모션 실측값 (Caniathrox — Animator 스텝)
| 클립 | 길이 | 전진 | 상승 | 용도 |
|---|---|---|---|---|
| Run_RM | 0.60s | 2.46m/cyc (4.09m/s) | 0 | 접근(Approach) |
| Jump_RM | 0.83s | 4.67m | **0.28m(폴짝)** | (미사용 — Y가 개구리 원인) |
| **JumpLunge_RM** | 0.83s | 4.67m | **0(bake)** | 도약(Lunge) — Jump_RM 복제+Y bake |
| **JumpCoil** | 0.17s 트림 | 0 | 0 | 응축(Coil) — Jump 첫 구간 in-place 복제 |
| JumpBite_RM | 0.83s | **0(제자리)** | 0 | (미사용) |
| BiteForward_RM | 0.83s | 1.33m | 0 | 근접 물기(Bite) |
| Spit | 0.67s | 0 | 0 | (고아 — 원거리 침 미완) |

## 6. 연출 교훈
- **"개구리 폴짝"의 정체** = ①위로 뜨는 Y ②윈드업 없음 ③착지 후 질질 끄는 꼬리. → Y bake + 응축 비트 + speed 대비로 "포식자 돌진"화.
- **"모았다가 팍"** = 느린 응축(state speed↓) → 빠른 발사(state speed↑)의 **타이밍 대비**. 별개 상태의 정적 speed라 코드가 매 프레임 안 긁음.
- **예측 점프** = target leading(선형 lead, pos+vel×leadTime). 응축 중 조준, 발사 시 고정. 평활(SmoothDamp)로 방향전환 펄럭임 제거.

## 7. 파일 (커밋 대상 — 미커밋 상태)
- `Assets/_Project/Animations/CaniathroxAttack.controller` (6상태) + `CaniathroxRM/Caniathrox@JumpLunge_RM.fbx`·`@JumpCoil.fbx`(복제 사본)
- `Assets/_Project/Scripts/`: `CaniathroxChaser.cs`(드라이버), `AttackTokenPool.cs`, `LabPlayerController.cs`, `LabSimpleCamera.cs`, `LabCombatSpawner.cs`, `CaniathroxAttackDemo.cs`(1마리 데모 원본)
- `Assets/_Project/Scripts/Editor/CaniathroxLabCapture.cs`, `Assets/_Project/Scenes/Greybox_CaniathroxLab.unity`
- `_DisabledPackages/ExplosiveLLC`(격리, git 미추적)
- 검증 캡처: `docs/03_reference/assets/caniathrox_lab/`

## 8. 다음 큐
- **다음 몬스터 선정** — §9에 정리(이 세션에서 결정 중).
- Caniathrox 잔무: Spit(원거리 침) 미완 / HP·데미지·히트박스(전투 스탯) 미착수 / 카메라 쉐이크·히트스탑(게임감 레이어) 게임플레이 단계 보류.
- 톤게이트(몬스터+VFX+도시+주인공 4자 동거 판정) 여전히 대기.

## 9. 다음 몬스터: Venodonte (LV1 군체 — 원거리 사수, ★투사체 축 신설)

> 유저 선정(2026-06-13). **MCP는 다른 세션이 점유** — 이 핸드오프대로 에디터 작업 세션이 진행.

**왜 이 종**: 오늘 근접 돌진 축을 세웠으니, 다음은 **원거리 사수 축**. 근접(Caniathrox)+원거리(Venodonte) **두 틀이 서면 나머지 28종 대부분이 두 틀의 조합**이 된다 → 양산 가속. "정지 사격 처벌"(공격 문법 §2 E형식)은 게임 본업 "에임"의 코어. 투사체 시스템을 세우면 스핏·조준탄·부채탄·유도탄·링탄 다수 종이 재사용.

**스펙** (공격 문법 §6 + 로스터):
- LV1 군체. **산성샷 3연 [E 조준탄]**: 직사 산성 글롭 ×3(0.15s 간격), 탄속 7, 발사 시점 플레이어 위치로. "군체에 섞이는 사선" — 순수 카이터가 아니라 **군체(몸으로 밀려옴)에 섞여 사선을 쏘는 사수**.
- 크기/이동/클립 킷은 작업 시작 시 로스터+FBX 실측으로 확정.

**오늘 틀에서 그대로 재사용** (§2~4):
- 파이프라인 6단계(§2), 애니 헌법(§3 — 사격 모션=정체성 동작, 진행 중 회전 0 / 조준은 회전 허용), 함정 전부(§4 — ★디스크 영속화·루트모션 Animator 스텝 측정·MCP 플레이모드 막힘·서드파티 격리), 검증 루프(디스크 렌더→유저 ▶).
- 코드 재사용: `LabPlayerController`/`LabSimpleCamera`/`LabCombatSpawner`(스포너에 Venodonte 추가), separation(군중), `AttackTokenPool`(사격에도 토큰 적용 검토).

**새로 만들 것** (Caniathrox 근접과 다른 축):
1. **★투사체 시스템 (신설 — 메인 작업, 재사용 설계)**: 직사 글롭 발사→직선 비행(탄속 7)→명중/소멸. **레드오렌지 자작 발광구**(색 캐넌, Caniathrox 스핏 글롭 재활용 가능) 우선 — Vefects Projectile은 URP 비호환이라 변환 필요(톤게이트 함정). `ProjectilePool` 공유 시스템으로 → 나머지 종 재사용 토대.
2. **사수 AI 드라이버 (신규 — `CaniathroxChaser`와 다른 행동)**: 사거리 유지 + 조준 사격. ★**조준탄은 예측 안 함**(발사 시점 플레이어 위치로) — Caniathrox 예측 요격과 **정반대 철학**: 플레이어가 멈춰 쏘면 맞고, 움직이면 빗나감 = "정지 사격 처벌". LV1이라 거리 유지는 약하게(군체에 섞임).
3. **상태머신**: Idle → 위치잡기(사거리 진입) → 조준(윈드업) → 산성샷 3연(0.15s 간격) → 쿨다운 → Idle. 사격 = 정체성 동작(CUT, 회전 0).
4. **클립**: Venodonte 사격/스핏·Idle·이동 FBX 확인 + ★루트모션 Animator 스텝 실측.

**작업 순서**: ①클립 확인·실측 → ②**투사체 시스템 신설**(가장 큰 새 작업) → ③상태머신(사격 시퀀스) → ④사수 AI 드라이버 → ⑤정지사격 처벌 튜닝(탄속·조준 빈도) → ⑥검증(디스크 렌더→유저 ▶).

**핵심 차이 한 줄**: 근접 돌진→원거리 사격 / 추격 AI→사수 AI / 예측 요격→예측 안 함(정지 처벌). 투사체 시스템이 메인 신규.
