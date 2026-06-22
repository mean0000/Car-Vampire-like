# 2026-06-18 핸드오프 — 몬스터 Synty/로우폴리 룩 (각진 + 외곽선 + 부위색)

**연결**: `2026-06-14-monster-look-realism-vs-lowpoly-handoff.md` (직전 갈림길) · 메모리 `project_2026_06_14_monster_lowpoly_shader_limit` · `project_2026_06_18_player_stack_rebuild`

---

## TL;DR
유저가 **Human Resources**(레퍼)식 "각진 로우폴리 + 가는 외곽선 + 부위별 플랫 컬러" 몬스터 룩을 원함. 06-14엔 *"셰이더만으론 안 됨(하이폴리 메시 한계) → 실사 우세"* 결론이었으나, 이번에 **그 빈칸을 채움**:

> **decimate(로우폴리화) + flat 셰이딩 + 외곽선 = 각진 룩 성립.**

블렌더에서 컨셉 증명 완료. **미결 = 색/부위 방향 + Unity 정합 + 양산 비용** (전부 유저 판정/내일 작업).

---

## 목표 / 레퍼런스
- 레퍼: **Human Resources** (각진 로우폴리 메카 + 가는 검은 외곽선 + 부위별 플랫 컬러). 우리 도심 블록아웃 레퍼이기도 함.
- 대상: Protofactor Monster Full Pack Vol 2 (Vol 7~12, 수십 종). 테스트 종 = **Caniathrox**.

## ★ 결정적 발견 (06-14 빈칸 채움)
- 06-14: cel/flat **셰이더만** 씌움 → 어중간. "메시가 하이폴리라 셰이더로 못 메움" 결론.
- 06-18 정정: **flat 셰이딩이 "각진 룩"을 만들려면 폴리곤이 충분히 커야 함**(면이 보일 만큼). 하이폴리는 면이 작아 flat 씌워도 각이 안 보임. → **decimate로 메시를 줄여(면을 키워)야 각진 면이 살아난다.** 유저 직관("각지게 만들고")이 옳았음.
- Caniathrox 9626 tris → decimate 10% → **962 tris** → flat → 각진 면 확실히 보임.

## 워크플로우 (블렌더 씬은 미저장·휘발 — 재현용)
1. **Unity에서 메시 OBJ export** (RunCommand): `SK_Caniathrox.fbx`의 최대 메시 → `_caniathrox_export.obj` (**디스크 보존됨**, 5788v/9626t, rig 없음 = 룩 테스트용).
   - ⚠️ `Mesh`는 이 프로젝트에서 네임스페이스 충돌 → `UnityEngine.Mesh`로 명시.
2. **블렌더 OBJ import** (`bpy.ops.wm.obj_import`) → Decimate 모디파이어(COLLAPSE, ratio 0.1) apply → `poly.use_smooth=False`(flat).
3. **freestyle 외곽선**: `render.use_freestyle=True`, lineset `linestyle.thickness=0.6`(유저 "얇게" 확정), color 거의 검정.
4. **EEVEE 렌더**(`BLENDER_EEVEE`): 카메라 bound 기반 배치 + sun + world ambient.

## ★ 함정 (재발 방지)
- **블렌더 5.1 FBX import 버그**: Protofactor 스킨드 FBX를 `import_scene.fbx`하면 `KeyError: None` (`armature_setup`, `link_hierarchy`). 옵션(`use_anim`/`ignore_leaf_bones`/`automatic_bone_orientation`) 무관하게 실패. → **OBJ 우회**(Unity export). 룩 테스트엔 rig 불필요.
- **PolyHaven 부적합**: ①기본 꺼짐(블렌더 사이드바 수동 활성화 필요) ②켜도 **표면 타일러블 텍스처**(돌/금속)라 캐릭터 UV에 안 맞음 ③**HR/Synty는 텍스처가 아니라 "부위별 플랫 컬러"** — "텍스처 구해와 입히기"가 이 룩과 **반대 방향**.
- 블렌더 5.1 머티리얼: `nodes.get("Principled BSDF")`가 None일 수 있음 → `next(n for n if n.type=='BSDF_PRINCIPLED')` 타입 검색.

## 캡처 진행 (전부 워크스페이스 루트, 임시)
- `_blender_caniathrox.png` — 회색 flat (각짐 증명, 외곽선 없음)
- `_blender_caniathrox2/3.png` — 베이지 + freestyle 외곽선 (1.8 → **0.6** 얇게)
- `_blender_caniathrox6.png` — **원본 텍스처**(`T_Caniathrox_BaseColor`) = 어두운 호러 부위색
- `_blender_caniathrox7.png` — **절차 부위색**(높이층, 청회+주황, 색상 대비) ← 최신
- `_lookdev_caniathrox4.png` — Unity URP MonsterToon/Flat **4변형**(셰이더만, 하이폴리라 각 안 보임 = 위 발견의 반증 근거)

## 미결 (내일 판정/작업)
1. **색/부위 방향** (유저 판정): 청회+주황 절차색 / 어두운 호러(원본텍스처) / 다른 팔레트 / 더 컬러풀.
2. **진짜 부위 분할**: 현재는 높이 기반 "층" 색(같은 높이=같은 색). 진짜 머리/다리 따로면 위치 클러스터(K-means류) or 수동 머티리얼 할당 필요.
3. **★ Unity 정합 (진짜 관문)**: 지금까지 전부 블렌더 EEVEE **정적** 렌더. 게임은 Unity URP + rig + 애니.
   - rig 보존 decimate = **UnityMeshSimplifier**(현재 프로젝트에 **미설치** — 06-14 메모리엔 설치라 했으나 지금 Assets에 없음, 재설치 필요).
   - URP flat = `MonsterFlatStylized`(face normal — 이미 decimate된 메시면 진짜 각짐) + 가는 외곽선(`MonsterToon` outline pass, width 0.6 수준).
   - 부위색 → 서브메시 or 버텍스컬러로 Unity 이관.
4. **양산 비용**: 한 마리 컨셉 OK여도 수십 종 반복(06-14 "로우폴리 전환 = 30종 + 애니/VFX 재적용 큰 작업"). 착수 전 결정.
5. **종 적합성**: Caniathrox는 유기 짐승이라 각지면 "돌덩이/크리스탈 짐승"처럼 형태 애매. **갑각류/곤충형(Crustaspikan 등)이 각진 룩에 더 맞을 수 있음** — 비교 권장.

## 다음 작업 체크리스트 (내일)
- [ ] 색/부위 방향 유저 판정 (미결 1)
- [ ] (선택) 진짜 부위 분할 or 갑각류 종 비교
- [ ] 룩 "이거다" 확정
- [ ] **UnityMeshSimplifier 재설치 → rig 보존 decimate 검증** (Unity 정합 핵심)
- [ ] URP flat + 외곽선 + 부위색 이관 → Unity 캡처로 **게임 실제 룩** 확인
- [ ] 양산 비용 결정

## 자산 위치
- OBJ(보존): `_caniathrox_export.obj` (워크스페이스 루트)
- 캡처(임시): `_blender_caniathrox*.png`, `_lookdev_caniathrox*.png` (루트 — 내일 정리/`.gitignore` 검토)
- 셰이더: `Assets/_Project/Shaders/MonsterToon.shader`, `MonsterFlatStylized.shader`, `LowPolyFlat.shader`
- 원본: `Assets/Protofactor/Monster Full Pack Vol 2/Monster Pack Vol 8/Caniathrox/`
- 블렌더 씬: **미저장(휘발)** — 위 워크플로우로 재현

---

## 갈래 A (별도 트랙, 같은 날): 플레이어 스택 작업 분해
세션 전반부. 이동/공격/히트 8파일(`Assets/_Project/Scripts/Player/`) 작업 계층 지도 + codex 교차 검증. 통합 우선순위:
- **0. (선행, D와 병행)** 공격 컨텍스트 struct + `IDamageable.TakeHit` 반환값 시그니처
- **1. D** 프레임데이터 (Startup/Active/Recovery + active '구간' 판정 + cancel window)
- **2. A** input buffer (선입력)
- **3. G** 히트스탑 + 슬래시 VFX
- **4. B/C** 공격 중 이동 감속 + facing 잠금
- **5. F** 적 `IDamageable` 구현체 + 플레이어 피격/HP

상세 = 메모리 `project_2026_06_18_player_stack_rebuild` + 이 세션의 codex 교차 응답.
