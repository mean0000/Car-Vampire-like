# 세션 핸드오프 — 캐릭터 프롬프트 확정(팀장/여성) + 리깅 병목 진단 + Blender MCP 설치 (2026-06-08 오후)

> 다음(클로드)이 이 문서만 읽고 이어갈 수 있게 쓴 핸드오프.
> 이 세션 큰 줄기: **(1) 모델링 트랙 분리(애니/리타깃은 다른 클로드) → (2) 팀장·여성 캐릭터 생성 프롬프트 확정 → (3) 진짜 병목 = "리깅"임을 진단 → (4) Blender MCP 설치(서버 등록 + 애드온 다운로드, Blender쪽 활성화는 유저 대기).**
> 선행 문맥: `2026-06-08-character-pipeline-handoff.md`(AI 이미지→3D→headless 리깅), `2026-06-08-character-animation-handoff.md`(AI리그 파탄→임시캐릭 SM_Casual_Male로 로코모션 완성).

---

## PART 0 — 이 세션의 역할 분담 (중요)
- **이 세션 = 모델링 전용.** 유저 지시: "애니메이션 리깅 작업은 다른 클로드에게 맡길게, 넌 나와 모델링 작업만."
- 그래서 아래 "Pistol 팩"은 **다른 클로드 담당** — 여기선 상태만 기록(중복 작업 금지).
- **Pistol_Handgun Locomotion Pack** (`Assets/_Project/Animations/Pistol_Handgun Locomotion Pack/`) = Mixamo에서 받아 임포트됨. **20개 FBX 전부 Generic(animationType:2)** 상태 → Humanoid(3) 전환 + 아바타 Copy 필요. 유저 요청한 "앞 보며 뒷걸음"의 핵심 클립(walk/run backward, strafe) 전부 있음. **이 전환·블렌드트리·트윈스틱 작업은 다른 클로드가 진행.**

---

## PART 1 — 캐릭터 생성 프롬프트 (확정본 3종)

### 프롬프트 작성 교훈 (다음에도 적용)
- **글자수 한도 = 1000자.** 이미지AI가 1000자 넘으면 잘림. 항상 카운트하며 작성.
- **비율은 명시해야 한다**: AI는 안 잡아주면 사실적으로 뽑음 → `exactly 8 heads tall, full-length legs` 같이 박아야 우리 톤(Synty 툰 월드 + ithappy 툰 좀비)과 정합.
- **"한국형 잘생김" 서술이 비율보다 결과 품질에 더 크게 기여**(유저 검증): `strikingly handsome Korean, refined sharp Korean features, strong jawline` 류를 꼭 넣을 것.
- v3 플레이어 모델의 실제 문제 = 머리 작은 사실적 비율 + 오버사이즈 셸이 상체를 블록화 → 상체 무겁고 다리 짧아 보임. 8등신 명시로 교정.

### 1-A. 팀장 (NPC, 미션 부여자) — ★캐논 재설계됨
역할(캐논): 로컬지점 시니어 관리자, 현장 안 뜀, 책상 패드로 장소·난이도 미션 셀렉트. 본사 완충재 + 엘 보호.
- ⚠️ **외형 캐논 변경**: 기존 `project_story_worldbuilding`의 "지친 50대 툴툴이"에서 → **유저 지시로 "30대 후반 미남 실장님"으로 재설계.** 올백+다크그린(검정에 가까운 진녹) 머리에 흰머리 약간, 동그란 선글라스 내려씀, 와이셔츠 소매 접어 전완근 노출, 정장바지. "낡은 관청 워크웨어/은퇴 현장직" 안 → 세련된 카리스마 팀장.
- 세계관 끈: 사원증 랜야드 1개 + 앰버 액센트만 소량 유지.

```
full-body character, A-pose, front view, plain light-gray background, low-poly stylized cartoon 3D render, well-balanced game proportions, exactly 8 heads tall, full-length legs. A strikingly handsome Korean man in his late thirties, refined sharp Korean features, strong jawline, cool charismatic team-leader charm, faint weariness. Slicked-back all-back near-black hair with a deep dark-green tint and a few white silver strands, round sunglasses worn low on the nose with eyes visible above them, light stubble, bare face. Lean fit build, NOT bulky, broad shoulders. Sharp classic businesswear, not frumpy: a crisp white dress shirt with sleeves rolled up to the elbows showing toned forearms, top button open, tailored dark dress slacks, slim belt, polished dress shoes, a thin lanyard ID card. Near-black and white palette with one small amber-orange accent on the ID. Confident cool team-leader presence, effortlessly stylish, full body head to feet.
```

### 1-B. 여성 플레이어 캐릭터 (선택형)
남성 플레이어와 **동일 테크웨어 키트** 공유(셸+하네스+파우치+앰버스트랩+캐니스터+한글패치). 변형: **짧은 바지 + 정강이까지 오는 레이스업 워커 + 커다란 장갑.** 머리 = **단발**(턱선 길이, 사이드 프린지, 얼굴 감싸는 결 — 유저 레퍼 단발 2장 기반).

```
full-body character, A-pose, front view, plain light-gray background, low-poly stylized cartoon 3D render, well-balanced game proportions, exactly 8 heads tall, full-length legs. A strikingly beautiful young Korean woman, refined sharp Korean features, cool aloof decadent charm, faint weariness, a chin-length dark bob haircut with side-swept fringe and face-framing strands, slightly textured, bare face. Slim athletic build, NOT bulky, long legs. Same techwear kit as the male operative: matte-black nylon techwear shell jacket with a tall funnel stand-collar and half-zip, oversized cool silhouette, chest harness with straps and utility pouches, lanyard ID card, hangul sleeve patches, compact back canister, ONE bold amber-orange utility strap as the single key accent. Oversized large tactical gloves. Short black techwear shorts, bare thighs, tall shin-high lace-up walker combat boots. Near-black palette with one vivid amber-orange accent. Effortlessly cool, full body head to feet.
```

### 1-C. 남성 플레이어 (기존 v3, 참고)
v3 셸형 프롬프트·결과는 `2026-06-08-character-pipeline-handoff.md` 「최신 업데이트」 참조. 바디 합격/얼굴 멜팅/톱다운 무손실. 8등신 비율 미적용본이므로, 재생성 시 위 교훈(8등신 명시) 반영 권장.

> ⚠️ 이미지 생성 상태: 유저가 컨셉/모델 이미지를 뽑은 상태. **저장 경로는 이 세션에서 확정 안 됨**(Downloads 추정). 다음 세션 시작 시 유저에게 GLB/이미지 경로 확인 필요.

---

## PART 2 — ★ 진짜 병목 = 리깅 (진단 확정)

파이프라인 3단계 중 어디가 되고 안 되는지:
1. **AI 이미지 → image-to-3D (Tripo 무료 GLB)** = ✅ 됨. 바디 합격, 얼굴 멜팅(톱다운 무손실)
2. **headless Blender 오토 리깅** = ❌ **실제로 터졌음.** `project_character_pipeline` 메모리는 "검증완료"로 적혀있으나, 그 다음 애니 세션에서 **Player_Rigged 스킨 웨이트 90% 파탄(버텍스 대부분 미할당)** 발견 → 임시캐릭 후퇴의 진짜 원인이 이것.
3. Unity Humanoid 리타깃 = 리그만 멀쩡하면 됨.

**왜 터졌나**: `ARMATURE_AUTO`(본 히트) 자동웨이트는 워터타이트 메시 전제. AI 메시는 non-manifold·교차·떠다니는 부속(백팩·스트랩·캐니스터=분리된 섬)이 많아 본 히트가 조용히 실패. 거기에 **긴 코트**까지 겹쳐 최악이었음.

**★ 결정적 변화 — 새 디자인이 리깅 친화적**:
- 팀장 = 와이셔츠+정장바지(코트 없음)
- 여성 = 자켓+짧은바지+워커(코트 없음, 다리 노출=토폴로지 단순)
- → **원래 막힘의 주범(긴 코트)이 사라짐.** 범용 오토리거가 통할 확률 급상승.

**권장 파이프라인 (리깅 단계 갈아끼움)**:
```
AI 이미지 → Tripo image-to-3D (GLB)
  → headless/MCP Blender '정리만' (decimate·스케일/축 정규화·A포즈·non-manifold 정리·떠다니는 부속 병합/분리)
  → Mixamo 또는 AccuRig 오토리그+웨이트   ← headless 본히트 대신 갈아낌
  → Unity Humanoid 리타깃 → 권총 로코모션 클립 재사용
```
- **Blender = 리깅이 아니라 "업로드용 메시 정리"로만.** 실제 웨이트는 검증된 외부 리거(Mixamo 간단 / AccuRig 품질).
- Mixamo/AccuRig는 웹·GUI라 **업로드·마커는 유저 손** 필요.
- **게임플레이는 안 막힘**: 임시 SM_Casual_Male로 로코모션 이미 돌아가므로, 진짜 캐릭터는 병렬 아트 트랙으로 리깅 완성 후 스왑.

---

## PART 3 — Blender MCP 설치 상태 (절반 완료)

"보면서 작업"을 위해 **`blender-mcp`(ahujasid)** 설치 진행. 헤드리스 배치(눈 감고 → 디스크 렌더 확인)와 달리 켜진 Blender를 실시간 조종+뷰포트 직접 봄. **단, 리깅 알고리즘 문제를 마법처럼 풀진 않음 — 진단/반복 루프만 빨라짐. 최종 웨이트는 여전히 Mixamo/AccuRig 권장.**

### 완료된 것 (이 세션)
- ✅ MCP 서버 등록: `claude mcp add blender -s user -- uvx blender-mcp` → `C:\Users\pc\.claude.json` (user scope)
- ✅ 애드온 다운로드: `_tools/blender_mcp_addon.py` (113KB, ahujasid main, bl_info 최소 Blender 3.0)
- ✅ 서버 기동 테스트: `uvx blender-mcp` → 56패키지 설치 후 정상 기동(포트 9876 대기). 현재는 Blender 애드온 미가동이라 연결만 거부(WinError 10061 = 정상).
- 환경 확인됨: uvx 0.11.15 / uv / Python 3.12.1 / Blender 5.1 (`C:\Program Files\Blender Foundation\Blender 5.1\blender.exe`)

### 남은 것 (유저 손 + 재시작 필요)
1. Blender 5.1 실행 → `Edit > Preferences > Add-ons` → **▼ Install from Disk** → `C:\Users\pc\ZombieCrush\_tools\blender_mcp_addon.py`
2. **"Interface: Blender MCP"** 체크 ON. ⚠️ 애드온이 Blender 3.0 기준 → 5.1에서 활성화 에러 가능. 에러 시 콘솔 메시지 캡처 → 클로드가 5.1 API 패치.
3. 뷰포트 **N키** → **"BlenderMCP"** 탭 → **"Connect to Claude"**(Start MCP Server) → 포트 9876 리슨.
4. **Claude Code 재시작** — 새 `blender` MCP 서버는 재시작해야 도구 로드됨. 재시작 전엔 이 세션/도구에 안 잡힘.

### 참고
- 이 애드온/서버는 **텔레메트리**를 supabase로 전송(기동 시 확인). 원하면 차단법 별도.

---

## PART 4 — 다음 세션 시작점 (권장 순서)
1. **Blender MCP 연결 확인** (애드온 켜고 재시작했다면 `blender` 도구 보이는지) → 안 깔렸으면 PART 3 마저.
2. **end-to-end 리깅 테스트 1회** — 한 캐릭터(팀장 또는 여성)로 새 경로(Tripo→Blender 정리→Mixamo/AccuRig→Unity) 증명. 코트 없는 새 디자인이라 통할 가능성 높음.
3. 성공하면 나머지 캐릭터 양산 + 임시캐릭 SM_Casual_Male 스왑.
4. (병렬) 이미지 미생성분 있으면 PART 1 확정 프롬프트로 생성. GLB/이미지 경로 유저 확인.

## 참고 (메모리 권위)
- `project_character_pipeline` = 캐릭터 제작·리깅 파이프라인. ⚠️ "리깅 검증완료" 서술은 **실제로 파탄남**(본 PART 2). 갱신 필요.
- `project_story_worldbuilding` = 캐릭터 캐논. ⚠️ 팀장 외형 "지친 50대"는 **30대 후반 미남으로 재설계**(본 PART 1-A). 갱신 필요.
- `project_modeling_strategy` / `project_hero_art_pipeline` = 에셋 충당·AI 3D 경로.
- `feedback_docs_structure` = docs 분류 규칙.
