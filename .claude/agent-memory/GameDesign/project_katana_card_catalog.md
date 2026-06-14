---
name: project-katana-card-catalog
description: 2026-06-14 카타나 전체 카드 카탈로그 구현 스펙(65장) — Gameplay가 ScriptableObject로 구현해 증명 슬라이스 플레이. 척추7+시너지6+연료9+잎변형36+신화2
metadata:
  type: project
---

2026-06-14 카타나 *전체* 카드 카탈로그 구현 스펙 작성 (`docs/03_reference/2026-06-14-katana-card-catalog.md`).

**산출물 = 증명 슬라이스용 구현 스펙**(종이 설계 아님). Gameplay가 읽고 ScriptableObject(`RunUpgradeDef` 계열)로 구현해 *실제 플레이*. 상태 = 🟡 제안 + Gameplay 인계용.

**총 65장**:
- **척추 7**: 뿌리1(kat_root) + 분기2(kat_geh_branch 거합/kat_cham_branch 참격) + 잎4(byeok 벽력일섬·counter 반격베기·flicker 플리커·beam 검기발사).
- **시너지 묶음 6**: 거합{chargen·reflux·weakpoint} 환류사슬 / 참격{combospd·killgain·keeptol} 가속사슬. G3 사슬 결선(chargen→reflux→weakpoint / combospd→killgain→keeptol). ★①숫자 등급 롤 적격.
- **연료 9**: 뿌리중립3(dmg/speed/dash) + 거합방향3(chargespd/drawdmg/execthresh) + 참격방향3(combospd/beamdmg/window). ★①숫자 등급 롤 적격.
- **잎 변형 36**: 4잎 × [변형3 + 재분기6(변형 3×2갈래)] = 4×9. 전부 ②결정론(롤 ❌). 예: 벽력일섬 → 중심과부하/귀환라인/2차기폭 + 각 재분기 2.
- **신화 2**: M-1[전개식 모노필라멘트](🔴 데모 앵커·기동빌드 결선) + M-3[압류 집행](🟢 싸고 디제틱·약점/면 결선). 트리 밖 풀, ③ 화려함=돈 가중 RNG.

**등급 롤 위치(엄수)**: ①숫자 C/R/E = 시너지6+연료9 = **15장에만**. ②결정론(롤❌) = 분기2+잎4+변형12+재분기24 = 42장. ③신화 = 2장. 베이스(롤없음) = 뿌리1.

**Why**: 카드시스템 v2.0 등급 3종 하이브리드를 카타나 §17.1.D 트리에 *실제 카드 스키마*로 박은 첫 구현 스펙. "잎=결정론·신화=이벤트는 Rarity 붙이면 캐넌 위반"이 load-bearing 가드.

**How to apply**: Gameplay 인계 시 §9 주의점 8개(등급 롤 위치·prereq held-predicate·잎 변형 게이트·시너지 사슬 G3 빈칸·신화 별도 타입·수치=노브·메카닉 베이스는 §17.1.A/B 확정·이름 가제) 전달. 수치 전부 노브 시작값(동결❌). 잎 변형 *per-잎 전면 작성*이 카드시스템 §2.3에서 "후속"이었는데 본 문서가 그 후속을 완료(4잎 다 동형 3변형+재분기).

관련: [[project-card-grammar]], [[project-card-system-research]], [[feedback-pong-over-systematization]], [[project-katana-cascade-design]]
