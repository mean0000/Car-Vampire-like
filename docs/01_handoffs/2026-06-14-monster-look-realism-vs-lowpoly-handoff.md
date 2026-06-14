# 핸드오프 — 몬스터 비주얼 룩: 실사 vs 로우폴리 (★미결)

- **날짜**: 2026-06-14
- **상태**: ★**미결 — 실사·로우폴리 둘 다 고민 중.** 오늘 탐구 끝에 실사로 기울었으나 **완전 확정 아님.** 둘 다 열어둠.
- **관련 메모리**: `project_2026_06_14_monster_lowpoly_shader_limit`

---

## 0. TL;DR

몬스터 비주얼을 **실사(Protofactor PBR 그대로)**로 갈지 **로우폴리(Synty식)**로 갈지 **미결**이다. 오늘 로우폴리를 깊이 파봤고(셰이더 → Blender → UnityMeshSimplifier로 rig 보존 decimate **성공**), 룩 자체는 됐으나 **단색 로우폴리가 "찰흙"처럼 밋밋**해서 실사 쪽으로 기울었다. 단 — 둘 다 장단이 분명해서 **최종 확정은 보류.**

---

## 1. 발단

유저가 "몬스터가 너무 리얼하다"고 느낌. 우리 월드는 Synty(Toon City) 로우폴리인데 몬스터만 Protofactor 하이폴리 PBR이라 **톤 불일치**를 직감. → 로우폴리/Synty 느낌으로 바꾸고 싶어함.

레퍼 이미지: 로우폴리 거미 몬스터(각진 면 + 눈/머리 부위 빨강 발광). `C:\Users\pc\Desktop\화면 캡처 2026-06-14 194126.png` 류.

---

## 2. 로우폴리 탐구 — 전체 시도와 결과

### 2-1. 셰이더로 시도 (전부 실패/어중간)
- **B (노멀맵off + 채도60%↓ + smoothness0.4 + metallic0)**: "리얼함만 뺀" 정도. 그나마 깔끔하나 메시는 그대로.
- **cel (MonsterToon.shader — outline + 밴딩)**: 이상함. Synty엔 외곽선 없음.
- **flat shading (MonsterFlatStylized.shader — face normal ddx/ddy)**: 각진 효과 미묘 + 밝고 평평. 어중간.
- ★**결론**: 셰이더는 라이팅·색만 바꿈. "리얼함"의 근본 = **메시 실루엣(하이폴리) + 사실적 텍스처**라 셰이더로 못 메움. **메시가 근본.**

### 2-2. 메시 변환 (Blender → Unity)
- Blender 5.1 FBX importer가 SK_Venosaur.fbx(7100) 못 읽음(`KeyError: None`, armature_setup). → **OBJ 우회**(메시만, rig 손실).
- Blender decimate 15% → 12274→1841 폴리, flat shading = **각진 로우폴리 됨**(룩은 성공). 단 OBJ라 **rig(애니) 빠짐**.

### 2-3. rig 보존 decimate (성공)
- **UnityMeshSimplifier** (무료 git 패키지) 추가 → **Unity 내 SkinnedMesh decimate + 본 웨이트·bindpose 100% 보존**.
- 결과: `decimate tris 12274→1840 | boneWeights 7590→2033 | bindposes 59→59 | RIG보존=True`.
- 공격 포즈(SampleAnimation) 적용 시 메시가 본 따라 변형 = **애니 작동 확인.**
- 검증 스크립트: `Assets/_Project/Scripts/Editor/VenosaurDecimateTest.cs` (Run/RunPreview).

### 2-4. 찰흙 문제
- decimate 메시 + flat shading + **단색 텍스처**(리얼 알베도 부조화라 단순화) → **"찰흙"처럼 밋밋.** 로우폴리 + 단색 + flat이 어중간한 회녹색 덩어리.
- 부위 발광(눈/가시)로 포인트를 주려 했으나 — Venosaur 단일 메시라 자동 추출(알베도 주황=입)만 됨, 눈/발톱은 수동 마스크/슬롯 분리 필요. + 탑뷰 가시 부위여야(입 안은 안 보임).

---

## 3. 실사 검증

- **우리 그래픽 씬 = `Greybox_ScanLit_v2`** (라이팅 7 + 포스트 Volume + LookdevCloseupCam + 주인공 셀셰이드 + 군중/좀비 + 다크 무드).
- 거기에 Protofactor Venosaur(원본 PBR, 변환 X) 배치 → **위협적·분위기 O. 찰흙과 비교 불가.**
- 주인공(셀셰이드) vs 괴수(실사) = **의도된 대비**(괴수가 사람보다 사실적 → 이질적 위협).
- ⚠️ 단 스타일라이즈드 캐릭터와 **디테일 차이로 몬스터가 약간 튐** → 라이팅/그레이드/림으로 녹일 수 있음.
- 캡처: `_scan_monster.png` (프로젝트 루트).

---

## 4. ★ 두 방향 비교 (판단 자료)

| | **실사 (Protofactor 그대로)** | **로우폴리 (decimate)** |
|---|---|---|
| 작업량 | ★변환 0 (애니/VFX/AI/색규약 다 유효) | 30종 decimate + 텍스처 재제작 + 부위 발광 |
| 월드 톤 | 스타일라이즈드 세계와 약간 튐(라이팅으로 녹임) | ★Synty 월드와 일관 |
| 룩 | 위협적·디테일·분위기(다크 무드에서) | 깔끔하나 단색=찰흙 위험(텍스처 작업 관건) |
| 15m 탑다운 | 리얼/로우폴리 구분 거의 안 됨 (톤게이트 판정) | 〃 |
| rig | 그대로 | ★보존 가능(UnityMeshSimplifier) 검증됨 |
| 원래 아트방향 | ★정합(주인공NPR/괴수PBR, shader-direction §6) | 일탈 |

**오늘 기운 쪽 = 실사** (변환 0 + 다크 무드에서 충분 + 원래 방향 정합). 단 **로우폴리도 rig 보존이 풀려서 기술적으로 가능**해졌으므로, "찰흙"을 텍스처/부위발광으로 극복하면 월드 일관 면에서 더 나을 수도 → **둘 다 미결.**

---

## 5. 기술 발견 (어느 방향이든 재사용)

- ★**UnityMeshSimplifier** = Unity 내 SkinnedMesh decimate + **rig 보존**. (로우폴리 채택 시 양산 도구 / 실사 채택해도 **LOD에 재활용**). manifest: `com.whinarn.unitymeshsimplifier` (git).
- **Blender 5.1 FBX importer 버그** — Unity FBX(7100) `KeyError: None`. 우회 = OBJ export(rig 손실) 또는 Blender 4.2 LTS. → **rig 필요하면 Blender 말고 UnityMeshSimplifier.**
- **부위 발광 방법**(단일 메시): ①Emissive 마스크 텍스처(UV 손수) ②별도 머티리얼 슬롯 분리(Blender) ③본 부착. ★색 자동추출(알베도 주황)은 한계(피부까지/부위 부정확) → **수동 지정 필요.** ★발광 부위 = **탑뷰(15m) 가시 부위**여야(입 안 ❌, 머리/등/눈 ✓).
- **MonsterFlatStylized.shader** (신규, face normal flat + smooth lambert + spec, outline 없음) — 로우폴리 채택 시 사용.

---

## 6. 자산/실험물 위치 (정리 보류 — 무해)

- `Assets/_Project/Scripts/Editor/VenosaurDecimateTest.cs` (decimate + rig 검증 + 미리보기)
- `Assets/_Project/Shaders/MonsterFlatStylized.shader` (flat shading)
- `Assets/_Project/Shaders/MonsterToon.shader` (cel — 기존)
- manifest `com.whinarn.unitymeshsimplifier` (LOD 자산으로 보류)
- 캡처 PNG (프로젝트 루트): `_scan_monster.png`(실사), `_lowpoly_preview.png`/`_lowpoly_simple.png`(로우폴리), `_mat_*.png`(셰이더 시도들)
- 우리 그래픽 씬: `Greybox_ScanLit_v2.unity`

---

## 7. 다음 결정 포인트 + 작업

### 결정 (유저)
**실사 vs 로우폴리** 최종 확정. (현재 실사 우세, 단 미결)

### 실사로 가면
1. 실사 몬스터를 다크 무드 세계에 **녹이기** — 다크 그레이드 + 림 라이트(스타일라이즈드 캐릭터와 톤 통합).
2. **부위 발광** — 눈/가시 Emissive(다크 무드 빨간 눈 = 위협).
3. 1차 9종 애니/VFX, 색규약, VfxDirector 인프라 **그대로 유효.**

### 로우폴리로 가면
1. "찰흙" 극복 — 텍스처(면별 단색 팔레트 or 버텍스 컬러) + **부위 발광(눈/가시 슬롯)**으로 포인트.
2. decimate 30종 양산 파이프라인(UnityMeshSimplifier 배치 스크립트) — `VenosaurDecimateTest.Run` 확장.
3. 비율/텍스처/발광 종별 튜닝.

### 공통 메모
- 15m 탑다운 + 다크 무드면 **메시 리얼/로우폴리 차이가 거의 안 보임**(톤게이트 판정) → 클로즈업(컷신)에서만 차이. 결정 시 "실제 플레이 거리"를 기준으로.
- 우리 원래 방향(주인공 셀셰이드 / 괴수 PBR)은 **실사 쪽과 정합**.

---

*작성: 2026-06-14 몬스터 룩 탐구 세션 종료 시점.*
