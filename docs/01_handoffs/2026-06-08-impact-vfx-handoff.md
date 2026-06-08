# 날아가는 탄(트레일) + 명중 임팩트 VFX 핸드오프 (2026-06-08)

리볼버/라이플/샷건 사격을 **즉발 히트스캔 → 실제 날아가는 발사체**로 바꾸고, 명중점에
**코드 생성 임팩트 플래시**(+ 선택 프리팹 오버라이드)를 붙인 세션. **스크립트만** 작업
(`Assets/_Project/Scripts/PlayerCombat.cs`). 브랜치: `feat/graphics`. 작업 씬: `Greybox_ScanLit.unity`.

## 0. 방향 결정 (왜 이렇게 했나)

- **히트스캔 → 발사체**: 사용자 요구 "맞아야 죽게". 데미지 판정을 발사 즉시가 아니라
  **비행 중**에 한다 — 탄이 좀비에 물리적으로 닿아야 명중. 탄속은 **옵션 A(빠름, 기본 90 m/s)**
  로 기존 밸런스 보존(거의 즉발처럼 보이되 회피 가능).
- **명중 이펙트**: 보유 **Vefects 임팩트 팩 2종이 전부 BIRP 서피스 셰이더** → 우리 URP
  프로젝트에서 **통째로 마젠타(핑크)**. (아래 1-B 참조) 그래서 3rd파티 프리팹 대신
  **코드 기반 자체 플래시**로 결정(사용자 선택). 단 **나중에 자기 이펙트로 교체 가능하게**
  오버라이드 훅도 같이 넣음.

## 1. 발사체(트레이서) — 즉발 → 날아가는 탄

- **풀**: `BulletPoolSize=48` 동시 비행 총알. `struct Bullet{active,pos,dir,remaining,damage,pierce}`,
  슬롯별 관통 중복방지 `HashSet _bulletHits[i]`. 고갈 시 `_bulletEvict` 라운드로빈 재사용
  (펠릿 수 풀이면 비행시간>쿨다운일 때 슬롯이 덮여 탄이 멀리 못 가던 **버그**를 48 풀로 차단).
- **트레일 비주얼**: `TrailRenderer[]` 풀. 매 프레임 위치만 옮기면 streak 자동 생성.
  공유 가산 HDR 머티리얼(`_tracerMat`, `CreateAdditiveMaterial`) → **메인 씬 블룸에서 빛남**.
  `tracerColor`(HDR), `bulletSpeed`(90), `trailTime`(0.06), `trailWidth`(0.16) 인스펙터 노브.
- **판정 코어 `UpdateBullets()`**: 매 프레임 `bulletSpeed*dt`만큼 전진하며 그 **구간(이전→현재)을 캐스트**
  (segment 캐스트라 빠른 탄이 좀비를 건너뛰는 터널링 없음). 벽=`Raycast(obstacleMask)`,
  좀비=`SphereCast(zombieMask, hitRadius)`. 초근접 시작점 겹침 방지로 `castFrom = from - dir*hitRadius`,
  벽 너머로 새지 않게 `castLen = Min(travel+hitRadius, wallDist)`. 관통은 `SphereCastAll`+슬롯 HashSet.

### 1-B. ★중요 — Vefects 임팩트 팩은 URP에서 못 쓴다 (검증 완료)

- `Assets/Vefects/Combat Flipbook VFX` 와 `Assets/Vefects/Flipbook VFX` **둘 다**
  머티리얼이 **`#pragma surface ... ` Amplify 서피스 셰이더** = **BIRP 전용**.
  URP는 서피스 셰이더를 못 돌려 **마젠타**로 렌더된다.
- 함정: `shader.isSupported == True`, `ShaderHasError == False` 로 떠도 **실제로는 핑크**.
  서피스 셰이더가 여러 서브셰이더를 생성해 생긴 **오탐**. → 판정은 반드시 **실제 렌더(PNG)로**.
- 이름이 `..._URP.shader` 여도 내용이 서피스 셰이더면 BIRP다(`SH_Vefects_Unlit_Flipbook_URP`가 그 예).
  색감(_R/_G/_B HDR 채널 리맵)이 셰이더에 묶여 있어 `URP/Particles/Unlit`로 단순 교체하면 룩이 깨짐.
- 팩에 URP 변환 패키지·셰이더그래프 **없음**. 살리려면 Amplify로 URP 템플릿 재타깃(무거움, MVP 과함).

## 2. 명중 임팩트 — 코드 생성 플래시 (현재 기본)

`PlayerCombat.cs`, **원거리 무기 전용**(Awake의 `Kind.Ranged` 분기에서만 풀 생성).

- **풀**: `FlashPoolSize=16` 빌보드 쿼드. 각 GameObject = `MeshFilter(_quadMesh)+MeshRenderer(_impactMat)`,
  평소 비활성. 인스턴스별 색은 머티리얼 복제 없이 `MaterialPropertyBlock _flashMPB`로 주입.
- **에셋 전부 코드 생성**:
  - `BuildQuadMesh()` — 중심원점·XY평면·+Z노멀 1x1 쿼드(인스턴스 소유, static 아님).
  - `BuildRadialTexture(64)` — 중심 밝고 가장자리 투명한 부드러운 원형 글로우(사각 티 제거·블룸 친화).
  - `CreateAdditiveMaterial` — 트레이서와 공유하는 가산 HDR(`Src=SrcAlpha,Dst=One,ZWrite=0`),
    `_impactMat`은 여기에 라디얼 텍스처(_BaseMap/_MainTex) + `_Cull=0`(양면, 빌보드 백페이스 안전).
- **재생 `PlayFlash`/`UpdateImpactFlashes`**: 명중점에서 `localScale=0` 시작 → **팝(ease-out 확대)**
  + **페이드(알파↓ → 가산이라 글로우 기여도↓)**, 매 프레임 카메라 빌보드, `impactFlashTime`(0.12s) 후 비활성.
- **색·크기 분기**: 좀비=`zombieFlashColor`(따뜻 살점)/`zombieFlashSize`(0.7),
  벽=`wallFlashColor`(차가운 스파크)/`wallFlashSize`(0.5). 둘 다 HDR.
- **명중점**: 좀비=히트 지점(초근접 point=0이면 `from` 폴백), 벽=`wallHit.point`(벽면).

## 3. 다른 이펙트로 교체 — 오버라이드 훅 (사용자 요청)

- 인스펙터 **Impact Override** 섹션:
  `Zombie Hit Override` / `Wall Hit Override`(GameObject) + `Override Lifetime`(2s).
- **로직**: `PlayImpact(pos, dir, zombie)` → 해당 오버라이드가 **꽂혀 있으면 그 프리팹 스폰**(교체,
  표면서 튀어나오게 `-dir` 정렬, 수명 후 자동 소멸), **비우면 코드 플래시로 폴백**.
- ⚠️ 꽂는 프리팹은 **URP 호환 셰이더만**(BIRP면 1-B처럼 핑크).

## 4. Stab+Codex 병렬 리뷰 → 수정한 실버그

1. **(HIGH)** 점블랭크에서 `LookRotation(zero)` 에러 스팸+오방향 → `toFlash.sqrMagnitude>1e-6` 가드,
   `_cam` null이면 재취득.
2. **(MED)** 벽 플래시가 벽면 앞 `hitRadius`에 떴음 → `UpdateBullets`에서 `wallHit.point` 캡처해 그 점에 스폰.
- 나머지 지적(풀 16 고갈 시 라운드로빈 재사용 팝, 루트 하이라키 노이즈 등)은 비이슈/MVP 범위 밖이라 보류.

## 5. 검증 / 주의

- 콘솔 에러 0. 단 **MCP RunCommand는 stale 어셈블리로 컴파일** → 새 SerializeField/메서드 최종 확정은
  **에디터 포커스 후 도메인 리로드** 필요(기존 함정과 동일).
- MCP `Camera_Capture`는 죽은 프레임/오프스크린 함정 → VFX 핑크 판정은 **씬 메인 카메라로 렌더한 PNG**로 확인함.
- 임시 디버그 PNG(`_vfx_*`)는 정리 완료.

## 6. 남은 작업 (내일)

1. **에디터 포커스 → 도메인 리로드 → 콘솔 에러 0 재확인** → 플레이.
2. **게임감 튜닝(인스펙터)** — `impactFlashTime`/`zombieFlashSize`/`wallFlashSize`/HDR 색 세기.
   탑다운에서 너무 작/크/연한지 느껴보고 값만 조정. 1순위.
3. (선택) **URP 호환 임팩트 프리팹** 찾으면 Override 슬롯에 꽂아 교체 — Vefects 팩은 1-B로 제외.
4. (보류) Vefects 팩을 정말 쓰려면 Amplify URP 재타깃이 필요(무거움). MVP에선 코드 플래시로 충분.
