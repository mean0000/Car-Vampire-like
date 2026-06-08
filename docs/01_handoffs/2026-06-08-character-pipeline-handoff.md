# 세션 핸드오프 — 주인공 캐릭터 AI 제작 + headless Blender 리깅 (2026-06-08)

> 다음(클로드/본인)이 이 문서만 읽고 이어갈 수 있게 쓴 핸드오프.
> 이 세션 큰 줄기: **(1) 플레이어 캐릭터 컨셉 확정(얼굴 보이는 퇴폐미 한국남) → (2) AI 이미지→무료 image-to-3D(GLB) → (3) 클로드가 headless Blender로 정리·리깅까지 완수, 코트 폭발 우회 → (4) 1차 검증(코트형 Player_Rigged.fbx) → (5) 컨셉 교체(코트→후드없는 테크 셸)로 진짜 히어로 v3 재생성 → Player_Hero_Rigged.fbx 확정.**
> 핵심 성과: **"리깅 못해서 캐릭터 못 만든다"는 더 이상 막힘이 아님.** Mixamo가 코트 때문에 터뜨린 걸 클로드 headless로 풀었다.
> ⚠️ **현행 히어로 = `Player_Hero_Rigged.fbx` (v3, 후드없는 셸).** 아래 「🔵 최신 업데이트」부터 읽을 것. PART 1~5는 1차(코트형) 기준 — 파이프라인·전략은 동일 유효, 의류만 셸로 바뀜.

---

## 🔵 최신 업데이트 (세션 후반 — 진짜 히어로 v3 + 확정 전략)

1차 모델(코트형, `Player_Rigged.fbx`)은 **파이프라인 검증용**이었고, 후반에 **컨셉을 코트→후드없는 테크 셸로 교체**해 진짜 히어로를 다시 뽑았다. 아래가 현행.

### 의류 방향 교정 (유저 지적)
- "테크웨어는 코트 안 입는다 → 후드/바람막이 재질" → 코트 폐기, **기능성 셸/바람막이(나일론 립스탑)**로.
- **후드도 뺐다** (클로드 판단·유저 동의): AI image→3D에서 후드가 목/어깨 뒤로 뭉개지고 얼굴 가림 + 톱다운 보상 작음 → **하이 퍼널 스탠드칼라(무드보드 3번)**가 더 안전·샤프·얼굴 깔끔.
- 추가 디렉션: **한국인 표현 명확화** + **몸 자체 샤프**(마른 체형, NOT bulky).

### v3 최종 생성 프롬프트 (949자, 이미지AI 1000자 제한 대응)
```
full-body character, A-pose, front view, plain gray background, low-poly stylized cartoon 3D render, clean bold shapes. A strikingly handsome young Korean man, Korean features, sharp jawline, high cheekbones, weary half-lidded eyes, dark circles, decadent melancholic charm, messy black hair over one eye, light stubble, bare face, no helmet/goggles/mask. Tall lean sharp physique, broad shoulders, narrow waist, NOT bulky. A near-future Korean disaster-cleanup operative. Oversized matte-black nylon techwear shell jacket, tall funnel stand-collar, no hood, half-zip, hip-to-thigh length, over a high turtleneck. Chest harness with straps, utility pouches, lanyard ID, gloves, cargo techwear pants, heavy boots, back canister, korean hangul sleeve patches. ONE bold high-visibility amber-orange utility strap as the key accent, near-black palette with a single vivid amber accent, field-worn, effortlessly cool oversized silhouette, full body head to feet.
```

### 결과 — 바디 합격, 얼굴 멜팅
| 컨셉아트(2D, 얼굴 멀쩡) | 생성 3D 바디(합격) | ★얼굴 멜팅(3D 약점) |
|---|---|---|
| ![컨셉](_img/2026-06-08-character/hero_concept_v3.png) | ![바디](_img/2026-06-08-character/hero_model_front.png) | ![멜팅](_img/2026-06-08-character/hero_face_melt.png) |

- 바디: 셸·퍼널칼라·하네스·앰버스트랩·카고팬츠·부츠·등캐니스터 전부 정확. T-pose라 리깅 친화.
- **얼굴 멜팅** = 단일 정면뷰 image-to-3D의 구조적 한계(눈 비대칭·기하 붕괴). 바디만 좋고 얼굴만 무너짐.

### ★ 확정 전략 — 얼굴/3D 분리
- **인게임 3D**: 이 바디 사용. **톱다운이라 정면 얼굴 안 보임**(정수리만) → 멜팅 무손실.
- **선택화면/초상화**: 3D 렌더 대신 **2D 컨셉아트(`hero_concept_v3.png`)를 초상화로** 사용. 잘생긴 얼굴을 2D로 살림. (뱀서 등 업계 표준)

### ★ "클로드가 블렌더로 처음부터 손모델링" — 기각 (중요)
유저 제안 "이미지 있으니 클로드가 Blender로 직접 모델링?" → **최악의 길이라 기각.** 이유:
- bpy 스크립트(좌표찍기)는 **모듈러/하드서피스엔 강하나 유기적 캐릭터+얼굴엔 최약체.** 사람이 뷰포트에서 스컬핑하는 것보다 훨씬 어렵고 결과 나쁨.
- **우리 문제(얼굴)가 정확히 손모델링 최약점** → 멜팅보다 못한 얼굴 나옴.
- 이미 좋은 AI 바디를 버리고 약점으로 재시작 = 이중 손해. "이미지 있음"은 image→3D에 유리한 조건이지 손모델링에 유리한 게 아님.
- 결론: 손모델링은 옵션에서 제외. 얼굴 욕심나면 **멀티뷰 재생성**(정면+측면+후면)이 정답.

### 현행 산출물 (오늘 최종)
- **`Assets/_Project/Characters/Player/Player_Hero_Rigged.fbx`** ← **현행 히어로**, 21본 리깅, 셸 하단 4886버텍스 hip-clamp, 톱다운 검증 ✅
- 소스 GLB(당시): `C:\Users\pc\Downloads\70dedb37-0b65-4c0b-b802-12f0210bc341\base_basic_pbr.glb`
- 컨셉아트: `C:\Users\pc\Downloads\image_1.png` (= `_img/.../hero_concept_v3.png`)

| 리깅 검증 정면(스트라이드) | ★톱다운(실제 게임 카메라) |
|---|---|
| ![리깅정면](_img/2026-06-08-character/hero_rig_front.png) | ![리깅톱다운](_img/2026-06-08-character/hero_rig_top.png) |

> 짧은 셸이라 다리 자유 스트라이드, 안 터짐. 톱다운에서 완벽 + 얼굴 멜팅 비가시 확인.

### 다음 세션 (내일) 시작점
- 손모델링 NO. **추천: 이 `Player_Hero_Rigged.fbx`로 Unity까지 끝까지 굴려** 인게임 톱다운 실제 화면 확인 → 얼굴 정말 안 보이는지 눈으로 검증 → 그래도 욕심나면 멀티뷰 재생성.
- Unity 단계 = 아래 PART 4 그대로.

---

## PART 1 — 캐릭터 컨셉 (확정)

플레이어가 **선택 가능한** 주인공 캐릭터. 그래서 **얼굴이 보여야 함**(헬멧/고글/바이저 금지 — 가리면 선택형 의미 소멸).

- **인물**: 잘생겼지만 약간 지친 **퇴폐미** 있는 전형적 한국 남자. 날카로운 이목구비 + 피곤한 눈(다크서클) + 헝클어진 머리.
- **복장**: 지급형 필드 테크웨어 = 오버사이즈 비대칭 지퍼 코트 + 스탠드칼라 베이스 + 체스트 하네스/유틸리티 리그 + 사원증 랜야드 + 한글 회사 패치 + **앰버(amber) 액센트 1포인트**(슬링백/스트랩 — 얼굴 가리는 바이저에서 옮김).
- **톤 근거**: SANABI식 "뽕맛"은 **모델/매체가 아니라 연출(Feel 주스 + 그래픽 처리: 헤이즈/그레이드/림)**이 만든다. 한국 액션웹툰(킬러 베드로) DNA = 갭(평범한 외양 + 치명적 깊이)·시크한 포스·옷빨·불필요한 선 없음(로우폴리와 정합).

### 사용한 생성 프롬프트 (얼굴 노출 최종본)
```
single full-body character, front view, A-pose, plain light-gray background,
low-poly stylized cartoon 3D render, bold clean shapes, no micro-detail noise,
cinematic dark techwear aesthetic, a handsome young Korean man, sharp attractive
features, tired weary eyes with faint dark circles, decadent melancholic charm,
slightly unkempt messy hair, bare face fully visible, no helmet, no goggles, no mask,
a cool veteran disaster-cleanup field operative in a near-future Korean dystopia,
the "ordinary worker hiding a lethal past" gap,
black and dark-slate techwear: an oversized asymmetric zip technical coat over a
stand-collar coverall / turtleneck base, a chunky chest harness and utility rig with straps,
a few modular utility pouches, a hi-vis amber sling bag, lanyard ID card, gloves,
heavy techwear boots, compact back canister, bold korean hangul company text patches,
a single high-visibility amber accent strap, muted desaturated black palette with
one amber glow accent, field-worn scuffed, striking oversized silhouette,
effortlessly cool nonchalant presence, subtle rim light,
centered, full body head to feet visible
```
- 이전 버전엔 `half-visor work helmet / glowing amber visor`가 있었음 → **제거**. 앰버 발광은 슬링백·스트랩으로 이전.
- 튜닝 노브: 얼굴이 너무 미형/노안이면 `tired eyes` ↔ `young` 비중만 조정.

### 의상 참고자료 (방향 잡을 때 본 실제 의류 — 클로드 수집)
> "약간 양복 같으면서도 작업복" = 실존 의류에 근거. 가공의 디자인 X (일관성 위해).

| | | | |
|---|---|---|---|
| ![작업복 커버올](_img/2026-06-08-character/garment_ref_1.jpg) | ![참고2](_img/2026-06-08-character/garment_ref_2.jpg) | ![참고3](_img/2026-06-08-character/garment_ref_3.jpg) | ![참고4](_img/2026-06-08-character/garment_ref_4.jpg) |
| 테일러드 커버올/보일러수트 | bleu de travail(작업자 블루) | 서비스/유니폼 톤 | 테크웨어 구조 |

### ★ 테크웨어 무드보드 (유저가 직접 고른 방향 레퍼 5장)
> 이 5장이 최종 방향의 근거. 오버사이즈 다크 테크웨어 + 스트랩/하네스 + 한글·한자 텍스트 패치 + 멜랑콜리/퇴폐 무드.

| | | |
|---|---|---|
| ![테크웨어1](_img/2026-06-08-character/253b776bb709acab442bc219e180cc2c.jpg) | ![테크웨어2](_img/2026-06-08-character/3654f218902a1dc5e4feb8945cbc3a3d.jpg) | ![테크웨어3](_img/2026-06-08-character/4848f972e16ecea54a3ed6355d77c1fd.jpg) |
| 백발+카타나, 오버사이즈 지퍼코트·스트랩, 멜랑콜리 | ★**컨셉 최근접** — 흑발 잘생긴 청년, 오버사이즈 코트+오렌지(앰버) 스트랩+패치, 갭/퇴폐 | 백발 안경 캐릭터시트(3뷰), 다포켓·스트랩 테크재킷 |

| | |
|---|---|
| ![테크웨어4](_img/2026-06-08-character/a2290e06ea900c620bff494d9442f2ac.jpg) | ![테크웨어5](_img/2026-06-08-character/d6929cdf1fb1b324848c94c7b7b9baa3.jpg) |
| DEMON-77 팀, 한자/한글 텍스트 패치+흑복 테크웨어(우리 한글패치 근거) | 레드 오버사이즈 자켓+흑복+한자 스트랩, 스트리트 테크 |

> 채택 요소: 오버사이즈 비대칭 실루엣 / 체스트 하네스·스트랩 / **앰버(오렌지) 액센트 1포인트** / 한글 텍스트 패치 / 멜랑콜리·퇴폐 표정. 칼(카타나)·과한 체인 등은 우리 톤(현대 한국 재난처리직)과 안 맞아 제외.

---

## PART 2 — 검증된 제작 파이프라인 (재현 가능)

| 단계 | 도구 | 비고 |
|---|---|---|
| 1. 컨셉 이미지 | AI 이미지 생성 | 위 프롬프트 |
| 2. image-to-3D | **Tripo 무료** / Hunyuan3D / TRELLIS | Tripo 무료는 **GLB만** 익스포트(HD·FBX·쿼드는 유료벽). GLB로 충분 |
| 3. 정리+리깅 | **클로드 headless Blender** | `_tools/rig_character.py` |
| 4. Unity | Humanoid 전환 → 툰셰이더 → 애니 리타깃 → 프리팹 | **← 다음 작업(미완)** |

### AI 3D 생성 퀄리티 예시 (유저 설득용으로 보여줬던 레퍼)
> "AI로 게임용 캐릭터 모델 뽑기" 실제 퀄. 픽셀아트보다 게임/1인개발에 유리하다는 근거 이미지.

| | | | |
|---|---|---|---|
| ![예시1](_img/2026-06-08-character/ai3d_example_1.jpg) | ![예시2](_img/2026-06-08-character/ai3d_example_2.jpg) | ![예시3](_img/2026-06-08-character/ai3d_example_3.jpg) | ![예시4](_img/2026-06-08-character/ai3d_example_4.jpg) |

### 2-1. Tripo 무료 익스포트 결과
`base_basic_pbr.glb`(13.8MB) + `base_basic_shaded.glb`(9.5MB) 두 개 받음.
- **`pbr` 버전이 키퍼**: PBR 텍스처 분리됨(diffuse/metallic-roughness/normal, 각 2048). 우리처럼 직접 라이팅하는 게임용.
- **`shaded` 버전은 버림**: 조명이 텍스처에 구워져 있음 → 동적 라이팅 게임엔 독.
- 원본: 단일 메시 120,000 tris / 111k verts, 높이 1.808, 리깅 없음.
- 다운로드 위치(당시): `C:\Users\pc\Downloads\80d52c59-965c-4713-95a0-6dcd57cfbec6\`

**생성 모델 멀티앵글 (정리 후 EEVEE 렌더):**

| 정면 | 3/4 | 측면 | 얼굴(40k 감폴리 후) |
|---|---|---|---|
| ![정면](_img/2026-06-08-character/model_front.png) | ![3/4](_img/2026-06-08-character/model_quarter.png) | ![측면](_img/2026-06-08-character/model_side.png) | ![얼굴](_img/2026-06-08-character/model_face_decimated.png) |

> 얼굴은 120k→40k로 줄여도 노멀맵 덕에 인상 유지. 측면에 등 캐니스터, 팔에 한글 패치, 앰버 슬링 확인.

### 2-2. headless Blender 정리+리깅 (`_tools/rig_character.py`)
실행: `blender.exe --background --python _tools/rig_character.py -- <glb> <out_fbx> <render_base>`
1. GLB import → 노멀 재계산 → 트랜스폼 적용
2. **Decimate 120k→40k** (얼굴은 노멀맵이 디테일 보존 → 40k에서도 인상 유지 확인)
3. 1.8m 정규화 + XY 센터 + 발바닥 원점
4. **21본 표준 휴머노이드 스켈레톤** 비례 배치 (Unity Humanoid 호환 이름: Hips/Spine/Chest/Neck/Head/L·R Shoulder·UpperArm·LowerArm·Hand·UpperLeg·LowerLeg·Foot·Toes)
   - 좌표계: Z up, X 좌우(+X=캐릭터 LEFT), Y 깊이(-Y=정면). 좌우 매핑 어긋나면 Unity Avatar에서 교정.
5. **자동 웨이트** `ARMATURE_AUTO`(본히트) — 성공. 실패 시 envelope 폴백 코드 있음.
6. ★**코트 폭발 우회**: 가랑이 아래(z<0.94) & 다리축에서 먼(>0.15m) "코트 스커트" 버텍스(6244개)를 **Hips본 weight 1.0 강제고정** → 빳빳한 스커트화. 다리 벌어져도 안 터짐.
7. rest 포즈로 FBX 익스포트(텍스처 임베드, add_leaf_bones=False, bake_anim=False)
8. 스트라이드 테스트 포즈로 front/quarter/top 렌더 → 검증

### 2-3. ★핵심 깨달음 — 톱다운 관대함
톱다운 카메라라 **다리-코트 변형 디테일이 거의 안 보임.** 영화급 웨이트 불필요, 목표는 "**안 터지면 된다**"뿐. 그래서 Mixamo/AccuRig가 코트에 체하는 문제를 클로드 headless로 우회 가능.
- **Mixamo 코트폭발 원인** = 긴 코트가 양다리를 잇는 토폴로지 + 발 붙음으로 다리 부피가 한 덩어리로 융합 → 다리 벌어질 때 천이 찢어짐.
- **클로드 약점(정직)**: 본 정밀배치(관절을 눈으로 못 봐 비례 추정)·섬세한 웨이트 페인팅. → 톱다운에서 안 보이는 선까지만 품질 보장.

---

## PART 3 — 산출물

- `Assets/_Project/Characters/Player/Player_Base.fbx` — 리깅 전, 40k tris, 1.8m
- `Assets/_Project/Characters/Player/Player_Rigged.fbx` — **21본 리깅 완료, 톱다운 3앵글 검증** ✅
- `_tools/rig_character.py` — 정리+스켈레톤+웨이트+코트클램프+테스트렌더 일괄
- `_tools/inspect_glb.py` — 폴리수/스케일/텍스처 점검
- `_tools/preview_glb.py`, `_tools/preview_fbx.py` — 멀티앵글 렌더
- 테스트 렌더: `_tools/rigtest_stride_{front,q,top}.png`

**리깅 검증 — 스트라이드 포즈(다리 벌림)에서 코트 안 터짐:**

| 정면 | 3/4 | ★톱다운(실제 게임 카메라) |
|---|---|---|
| ![리깅정면](_img/2026-06-08-character/rigtest_stride_front.png) | ![리깅3/4](_img/2026-06-08-character/rigtest_stride_quarter.png) | ![리깅톱다운](_img/2026-06-08-character/rigtest_stride_top.png) |

> 코트가 빳빳한 스커트로 유지(엉덩이 고정), 다리만 스트라이드. 톱다운에선 완벽.
- **Blender**: `C:\Program Files\Blender Foundation\Blender 5.1\blender.exe` (5.1.2), headless `--background --python` 정상. EEVEE 엔진명은 `BLENDER_EEVEE`(NEXT 아님).

---

## PART 4 — 다음 작업 (미완, Unity 측)

1. Unity가 `Player_Rigged.fbx` 임포트 → **Rig을 Humanoid로 전환** + Avatar 자동 구성(본 이름 표준이라 자동매핑 기대). 좌우 뒤집힘만 확인.
2. 머티리얼 **URP 툰셰이더** 적용 (현재 PBR).
3. **애니 리타깃**: 보유 **Kevin Kiselias Human Basic Motions**(톤 일치) 또는 Mixamo 휴머노이드 애니 → Unity Humanoid 리타깃.
4. 프리팹화 + 애니메이터 컨트롤러 연결.
- ⚠️ Unity 에디터가 열려 있어야 임포트 설정·아바타 구성 가능.

## PART 5 — 향후 적용

캐릭터(좀비 변종 포함) 신규 제작·리깅 요청 = 이 파이프라인 그대로. "리깅 못한다"는 더 이상 막힘 아님. 단 긴 코트=오토리거의 적이지만 hip-clamp로 해결. 메모리: `project_character_pipeline.md`.
