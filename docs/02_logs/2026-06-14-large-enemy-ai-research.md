# 중형·거대형 적 AI 설계 — 웹 리서치 + 결정 (2026-06-14)

> **경위**: Crassorrid(LV4 7m 브루트) 튜닝 중 "거구가 위협적이나 행동이 사냥개 차용이라 안 거구답다" + "소수 거구는 동시 공격해도 됨, 토큰 대기 기웃거림이 어색"(유저). → 임의설계 금지·레퍼런스 추적 원칙대로 웹 리서치(에이전트 2기) → 본 로그 = 권위.
> **상태**: 리서치 동결. 결정 = 공격 조율 **Option 1(토큰 의미 변경)** 유저 확정. 이동(Arrive 스티어링) 재설계는 **다음 레이어**(미착수).

## A. 거구다움 = 이동 / 압박 / 커밋 (출처)

1. **이동 무게 = Reynolds 스티어링: 높은 mass + 낮은 max_force.** `accel = steering_force / mass` — mass↑면 같은 조향력에도 못 꺾고 *호를 그리며* 미끄러짐. 비라인(매프레임 순간 회전)이 "사냥개 같다"의 코드적 정체. **Arrive** 행동이 "접근→감속 정지"를 공짜로 줌(우리 명세와 일치). → https://www.red3d.com/cwr/steer/gdc99/ · https://github.com/libgdx/gdx-ai/wiki/Steering-Behaviors
2. **압박 = 추격 아니라 *장판 면적 점유*.** 못 잡는 거구는 바닥을 뺏어 재배치를 강요(RoR2 엘리트·DRG Dreadnought "예측가능 사거리 장판"). → https://parryeverything.com/2021/08/13/the-elites-of-risk-of-rain-2-efficient-design-and-the-fundamentals-of-real-time-combat/
3. **슬램 = 풀 커밋 상태 시퀀스(Animation Priority).** 강공일수록 startup/recovery 길고 시전 중 취소 불가, 빗맞으면 긴 회복으로 처벌=반격창. 유저 헌법 "한 동작만 돈다"와 동일. → https://www.gamedeveloper.com/design/enemy-attacks-and-telegraphing
4. **거구 = 페이싱을 *바꾸는 사건*, 밀도 디렉터서 제외**(L4D AI Director: "보스는 적응 페이싱 대상 아님"). 유저 "수 적게 나온다"와 일치. → https://steamcdn-a.akamaihd.net/apps/valve/2009/ai_systems_of_l4d_mike_booth.pdf · https://left4dead.fandom.com/wiki/The_Tank

## B. 다중 적 공격 조율 + 기웃거림 (출처)

5. **어택 토큰 풀 = DOOM 2016 원조.** 공격 타입별 토큰, 받아야 침. ★DOOM이 우리 문제를 **토큰 *빼앗기*로 해결** — "플레이어 앞 데몬이 공격하게 해서 *멍청하게 서 있지 않게(stand around looking stupid)*". → https://www.gamedeveloper.com/design/cyber-demons-the-ai-of-doom-2016-
6. **Battle Circle / 동시 공격자 상한** (AC·Arkham=시네마틱 대기, Souls=위험형). 허가 거부 시 1~2s 스트레이프 후 재요청. → https://code.tutsplus.com/battle-circle-ai-let-your-player-feel-like-theyre-fighting-lots-of-enemies--gamedev-13535t
7. **기웃거림 해법**: ①토큰 재분배(DOOM) ②적 타입 다양화 ③페인트/재배치 ④대기 모션 존재감 ⑤문맥 정당화. ★합의: "*대기를 숨기는 것보다 줄이는 것*이 본질." → https://www.resetera.com/threads/enemies-waiting-to-attack-the-debate.233842/
8. **소수 적 = 게이팅 이득(카오스 방지) 거의 없음 → 기웃거림이 순비용**(TLoU는 2기 초과부터 일부러 압도). **단 완전 무제한은 For Honor 갱=불공정**. **Aztez 규칙: 동시 공격 OK, 단 같은 각에서 둘 안 옴.** Dark Souls 합의 "겹쳐 안 읽히면 스킬→운". → https://www.gamedeveloper.com/design/enemy-design-and-enemy-ai-for-melee-combat-systems · https://www.neogaf.com/threads/boss-fights-with-multiple-enemies-are-cheap.1219272/ · https://steamcommunity.com/app/304390/discussions/0/2860219962102962025/

## C. 결정 — Crassorrid 공격 조율 (유저 확정 = Option 1)

**토큰을 "몇 기가 치냐" 게이트 → "어느 *각·박자*로 치냐" 분산으로 의미 변경:**
- **동시 슬램 허용**(수 게이팅 제거 — 소수 거구).
- **각 분산**: 동시 슬램하는 브루트는 플레이어 기준 서로 다른 방위(≥~90°). 같은 각이면 한쪽 지연/재배치.
- **미세 스태거 0.2~0.4s**: 완전 동시 내려찍기 금지(회피 가능 유지). 동시감은 유지.
- **기웃거림 → 플랭킹 재배치**: 대기(각/스태거 막힘) 브루트는 맴돌지 말고 빈 방위로 이동. 굼뜬 거구라 이동 자체가 위협적.
- **공정 룰 유지**: 플레이어 경직 회복 중 새 슬램 텔레그래프 안 시작.

(수치 90°·0.2~0.4s는 제안값 — 플레이테스트 튜닝. 출처는 "각 분산·스태거 필요" 원칙만 지지.)

## D. 다음 레이어 (미착수, 권고)

**이동 = Arrive 스티어링 재설계**(높은 mass·낮은 max_force·회전캡, 시전 중 회전캡 0). 현재 비라인 추격 → 호를 그리며 미끄러지는 무게. 이게 "거구다움"의 코드적 본진이나, 이번 범위(공격 조율) 밖 — 별도 레이어로.
