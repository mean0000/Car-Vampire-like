---
name: player-turn-in-place-assessment
description: 탑다운 제자리회전 어색함 3접근 견적 + Frank 8way가 facing-relative strafe임을 디스크 검증(인프라 이미 완비)
metadata:
  type: project
---

탑다운 45/15m 마우스조준 회전 시 "발 박힌 채 몸만 턴테이블" 어색함의 접근법별 견적 (2026-06-18 평가).

**디스크 검증 결과 (핵심):** approach1 전제 = 이미 100% 깔려 있다.
- Frank `8Way_S2_Run` = facing-relative strafe 세트(F/B/L/R/FL/FR/BL/BR 8 + idle). animationType:3 Humanoid, loopTime:1, In_Place(loopBlendPositionXZ:0). ⚠️_L이 "앞보며 좌측걸음" vs "좌회전후달리기"는 **모션 실물 봐야** 100%확정(meta=정지데이터). 정황상 strafe 매우유력.
- `KatanaMelee.controller` Locomotion = 이미 2D Freeform Directional(m_BlendType:2), MoveX/MoveY, 9노드 배치완료(_L=guid 6348f411 @MoveX-1/MoveY0).
- `PlayerAnimatorDriver` = 이미 facing프레임 투영(MoveX=right dot·MoveY=fwd dot)+45도 8way스냅. facing=_aim.Direction(마우스), move=별개. line87-88이 transform.rotation=LookRotation(face) **즉시스냅**.

**3접근 견적:**
1. **회전캡+기존strafe — 추천.** 성공률70%(미검증·모션실물안봄). 작업30분~1h(하): line87-88 즉시스냅을 RotateTowards 캡 1줄로. strafe인프라 추가0. 리스크=strafe가 실은 "회전후달리기"면 facing≠move 붕괴(_L 1회재생으로 즉시판명).
2. **Aim IK 상체비틀기.** 성공률80%. 작업0.5~1일(중상): Animation Rigging설치(무료)+Multi-Aim(spine01/02/03+neck+head)+드라이버 회전권한 하체/상체 분리 재배선(line82-97 재작성). 리스크=근접 칼정렬 깨짐(발≠칼방향, 공격 안읽힘=수용성위반).
3. **턴인플레이스 클립.** 성공률40%. 작업1~2일+(상): Frank에 턴클립 **없음**(Step_L/R=사이드스텝≠턴)→조달필요=범위밖. 마우스 연속조준↔이산턴클립 상극=즉응성사망. 장르 불합치.

**추천=1, 단 _L모션 1회확인 선결.** 이유: 인프라 완비라 1줄이면 끝, 모자라면 2를 상체레이어로 적층(1·2 배타아님). 유저 플레이확정=①strafe 진짜 facing-relative인가 ②회전캡 각속도(360~720도/s 노브) ③제자리회전이 15m서 넘어가나(정지캡처로 못봄).

연동=[[project_vexa_humanoid_katana_base]] [[project_frank_katana_kit_and_vexa_rig]]. 핸드오프 §50 미결(facing 8방향스냅 a/b)의 답=b(몸만 8방향)+회전캡.
