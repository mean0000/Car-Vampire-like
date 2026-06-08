# 우클릭 무기별 보조발사 + 조준 레이저 핸드오프 (2026-06-08)

우클릭을 **정밀조준(구)** → **무기별 보조발사(alt-fire)** 로 재정의하고, 자연스러운 조준선(디제틱 레이저)과
라이플 차지 비주얼(좌우 수렴 브래킷)을 구현한 세션. **스크립트만** 작업(씬 `Greybox_ScanLit.unity`는 값 1건만 저장).
브랜치: `feat/graphics`.

## 0. 방향 결정 (왜 이렇게 했나)

- pm+codex 브레인스토밍 결과: 우클릭은 **정밀 조준을 버리고 다른 스킬로 대체**(사용자 선택지 B).
- **궁극기 ≠ 우클릭**. 궁극기는 따로 계산. 이번엔 **무기 우클릭만** 다룸 → 캐릭터 능력이 아니라 **무기별** 보조발사.
- 조준선 재도입은 "자연스럽게" = **커서가 아닌 실제 탄도 방향(`_aimDir`)을 따르는 상시 레이저**.
  (`_aimDir`은 `aimResponsiveness`로 마우스를 한 박자 늦게 추종하므로, 커서에 그리면 탄착과 어긋남.)

## 1. 데이터 — `Assets/_Project/Scripts/WeaponLoadout.cs`

- 신규 enum: `AltFire { None=0, FanFire=1, ChargePierce=2, StockBash=3 }` + 구조체 `altFire` 필드.
- 죽은 필드 `aimSpread` 제거. `spread`의 의미 = **좌클릭 산포 반각(도)**.
- **산포 축소**: 리볼버 7°, 라이플 3°, 샷건 10°(펠릿8). 근접은 0.

| 무기(idx) | Kind | dmg | cd | range | noise | pellet | spread | altFire |
|---|---|---|---|---|---|---|---|---|
| 리볼버(0) | Ranged | 3 | 0.5 | 20 | 95 | 1 | 7 | FanFire |
| 야구방망이(1) | Melee | 2 | 0.5 | 2.2 | 25 | 0 | 0 | – |
| 쇠지렛대(2) | Melee | 6 | 0.45 | 2.5 | 32 | 0 | 0 | – |
| 라이플(3) | Ranged | 2 | 0.12 | 26 | 60 | 1 | 3 | ChargePierce |
| 샷건(4) | Ranged | 1 | 0.85 | 11 | 105 | 8 | 10 | StockBash |

- 핫스왑 접근자: `Revolver`=[0], `Rifle`=[3], `Shotgun`=[4]. `BaseBat`=[1], `EvolvedCrowbar`=[2].

## 2. 보조발사 3종 — `Assets/_Project/Scripts/PlayerCombat.cs`

좌클릭(주발사)은 두 무기 공통 "홀드=쿨마다 반복" 유지. 우클릭은 `WeaponLoadout.AltFire`로 분기(`HandleAltFire`).
**알트 공용 쿨다운 = `_altCooldownTimer`.** 튜닝값은 전부 SerializeField(게임감 반복용).

- **리볼버 = 패닝(FanFire)** — 즉발 트리거. `_fanShotsLeft`=3발을 `fanInterval`(0.07s)마다 난사(`fanSpread`13°),
  끝나면 `fanReload`(0.9s) 장전 공백으로 **좌·우 모두 잠금**. 낮은 연사를 패닉 클리어로 보완.
- **라이플 = 관통 차지샷(ChargePierce)** — 우클릭 홀드 차징. `chargeMinTime`(0.12s) 미만에 떼면 **오발 없이 취소**.
  `ReleaseCharge`: dmg = `damage × Lerp(1, pierceDamageMult3, charge01)`, `range×pierceRangeMult1.3`로 **경로상 전원 관통**
  (`SphereCastAll`+`HashSet` 중복방지). 발사 후 `pierceCooldown`0.5s.
- **샷건 = 개머리판 밀치기(StockBash)** — 즉발. `OverlapSphere(bashRange3)` 전방 부채꼴(`bashArc`60°, 0.9m초근접 각도무시,
  벽 LOS 체크) → `TakeMeleeHit(bashDamage1, …, bashKnockback9, bashStagger0.5, None)`. 밀착 위기를 "꺼져" 버튼으로 보완.

좌클릭은 `altBusy = _fanShotsLeft>0 || _charging` 동안 잠금(같은 무기 좌·우 동시발사 방지).
히트스캔 공용 코어 = `FireShot(spread, dmg, rng, pellets, pierce, noise)`.

## 3. 조준 비주얼 (이번 세션 후반)

- **상시 조준 레이저** — `UpdateLaser()`. 총구→`_aimDir`따라 `(zombieMask|obstacleMask)` 첫 히트까지 얇고 어두운 선
  (`_laserLine`) + 밝은 착탄 도트(`_laserDot`, 짧은 선+둥근 캡). 매 프레임 갱신. **제작 중 숨김, 근접은 미호출**.
- **라이플 차지 = 좌우 수렴 브래킷** — `ShowChargeBrackets()`. (구) 단일 게이지선(`_aimLine`) **완전 제거**하고
  두 선(`_chargeL/R`)이 `±Vector3.Cross(up,_aimDir)*off`에서 시작, 차지가 오를수록 **레이저로 수렴**(off→0.02),
  **흰 반투명→빨강 변색**(`chargeColorLow→High`), **두꺼워짐**(`chargeWidthMin0.03→Max0.16`). 풀차지=한 줄기 굵은 빔.
  레이저는 차징 중에도 켜둬서 브래킷이 그 좌우로 모이는 기준선이 된다.
- LineRenderer 4개 전부 Awake `CreateLine()` 생성, `OnDestroy`에서 `.material` 해제.

## 4. 수정한 버그

1. **우클릭 무반응** — 무기 미선택 시 `_altFire=None`이라 switch no-op. → Awake 미선택 기본값 `FanFire`로.
2. **차징+제작 데드락**(Stab+Codex 동시 지적) — `_charging` 잔류로 좌클릭 영구잠금. → ChargePierce에서 `crafting`이면 차징 강제취소.
3. **CS0234** — `MMSpringScale`는 `MoreMountains.Tools` 아님. → `MoreMountains.Feedbacks.MMSpringScale`로 수정.
4. 산포 과다 → 씬 `hipfireSpread` 18→7 저장.

## 5. 검증 / 주의

- 보조발사·조준선 각각 Stab+Codex 병렬 리뷰 완료. 조준선 리뷰: **상태 펜싱 정상·힙 할당 0**, 실버그 없음.
- ⚠️ **플레이어 GameObject가 zombie(7)/obstacle(8) 레이어에 있으면 레이저 자기히트** 가능(이번 변경 이전부터 동일 조건).
- MCP RunCommand는 stale 어셈블리로 컴파일 → 새 심볼 검증은 **에디터 포커스 후 도메인 리로드** 필요. 콘솔 에러 0 확인.
- 레이저는 `QueryTriggerInteraction.Collide`(좀비 트리거 콜라이더 착탄 우선). 벽 막힘은 `FireShot`가 `.Ignore`로 별도 처리.

## 6. 남은 작업

- **차지 브래킷 게임감 튜닝**(수렴 속도·색·두께) 플레이테스트로 다듬기 — 1순위.
- 궁극기(캐릭터 능력) 설계는 별도로 보류 중.
- 핫스왑 장전 우회는 디버그 한정(스코프 밖). `_altHits`는 관통/밀치기 공유(같은 프레임 충돌 경로 없음, 의도됨).
