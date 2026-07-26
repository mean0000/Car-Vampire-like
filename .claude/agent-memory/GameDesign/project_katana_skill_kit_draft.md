---
name: project-katana-skill-kit-draft
description: 카타나 기술 키트(E/R 슬롯) 설계 드래프트 — 역할 삼각(RMB/E/R)·E 3안·R 게이지 2안. 유저 판정 대기 (2026-07-05)
metadata:
  type: project
---

2026-07-05. 카타나 빈 슬롯 2개(E 스킬·R 궁) 채우는 드래프트. 산출=`docs/02_logs/2026-07-05-katana-skill-kit-draft.md` (🟡 제안, 동결권 없음, Q1~Q9).

**★기술 프레임 실측(중요·재사용):** E/R 슬롯은 **코드가 이미 끝까지 배선됨** — `KatanaWeapon.OnTick`이 `input.skillDown→TryBeginInstantAction(_skillRt)`(E), `input.ultimateDown→_ultimateRt`(R). `WeaponActionSet` SO 에셋만 슬롯(skillAction/ultimateAction)에 꽂으면 발동.
- **데이터만으로 되는 판정** = `DoActionHit` 부채꼴(range·arcHalfAngle·forwardOffset·damage·knockback). ★**arcHalf=180 → 360° 방사(노바)가 데이터만으로 성립.** 광역 스윙·넉백 노바·집중 부채꼴 전부 데이터.
- **코드 필요:** 전진 이동(단 `PlayerMotor.AddGlide` 재활용=소폭 배선, 파생 아님) · 관통 절단선(lineCut은 콤보 전용, 액션 노출=소폭 코드) · 타겟 홈잉/투사체/지속 멀티히트=파생 클래스. 액션은 `_hitDone` 가드로 활성당 1히트.
- 재활용: 대시 시안 잔상 · ParrySlowMotion(프리즈) · NotifyHit 투스테이트 줌 · purge-snapshot/killburst · 죽음 즉사 디졸브 티어. → R 스펙터클 절반 이미 존재.

**★역할 삼각(비겹침·07-04 정합):** 세 기술 슬롯 다 "뚫기(전진)"하되 케이던스·표현·조준 정반대.
- **RMB 차징(거합·기존)** = 모아서 한 줄 뚫기(정지 충전→관통 집중 화력·고수, 정지=피격 대가).
- **E(신규)** = 몸 던져 앞을 연다(순간 전진 돌파). ★**카이팅의 답** = "뒤로 갈 수 없는 파워 버튼"(조준 방향으로만 변위)+밀도 보상(많이 맞힐수록 큰 효과=호드 속으로 파고들 인센티브). 제자리 노바는 카이팅 못 막아 기각→06-26 "숨통트기/포위일소"를 *전진형*으로 재정합.
- **R(신규)** = 한 번에 다 벤다(수평 반토막 스펙터클, 게이지 정점).

**★E 후보 3안:** A 일섬(一閃, 거합/고수·관통 돌진 베기, 카이팅 답 최강·간지 최고·저비용=AddGlide 재활용, MGR 잔타츠/Hades대시Aspect/Katana Zero — **gd 리드 권고**, 06-17"팍"·거합 발도 계보 제품화) / B 선풍참(旋風斬, 참격/뉴비·전진 회전 광역·무조준 관용·데이터 대부분, 단발로 시작-지속회전은 코드, 카이팅 답 A보다 약함=관용 대안) / C 인(引, 하이브리드·처형 낚아채기·읽고-베기 크리 재활용·파생 클래스=최고비용·R과 톤중복=파킹).

**★R 게이지 2안:** 안1 스타일 게이지(화려함 충전=키스톤 계보, ⚠️VS 피벗 정산 카빙 후 생존 재정합) / 안2 킬 카운트(피벗 보스게이지 문법 정합·구현 확실). **권고=하이브리드**(킬 뼈대+화려함 가속). 순수 쿨다운 ❌(정점을 시간으로 주면 뽕 죽음). 화이트박스=오늘 가짜 반토막(긴 라인 즉사+디졸브+줌/화이트아웃)+게이지 컴포넌트 1개(유일 신규) / 진짜 절단=RayFire 구매 후(7월 미검증).

**충돌 감사:** 카드=결합 지점(런타임 modifier 레이어, SO 오염 금지, 이 드래프트는 손잡이만) · 06-14(E=궁/Tab)↔06-26(R=궁/E스킬) 이미 재정합(06-26이 이김) · 억제 스택=시너지(E 넉백→도미노, 다수킬→멀티킬 프리즈) · 평타 전진금지 보존(전진을 기술층에만).

**Why:** 슬롯 2개 채우기지 시스템 신설 아님(과잉설계 금지). E/R 코드 배선은 이미 존재→에셋+소폭 배선 문제. **How to apply:** 유저 Q3(E 리드안)·Q5(R 게이지) 판정 후 동결→노드명 Story 렉시콘 의뢰+WeaponActionSet 에셋 스펙 Gameplay 인계. 06-25 LOCK(스펙터클·무기불가지론·≠타이밍듀얼)과 항상 먼저 대조.

관련: [[project-katana-cascade-design]], [[reference-sword-action-pong-engines]], [[feedback-pong-over-systematization]], [[reference-roguelike-systems]]
