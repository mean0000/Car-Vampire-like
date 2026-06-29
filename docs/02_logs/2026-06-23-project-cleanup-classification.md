# 프로젝트 정리 — 분류 기준 & 실행 기록

> 2026-06-23 · 스크립트/씬/에셋/이미지 정리 세션
> 게이트=[직렬화 에셋 이동·다중 파일] → Stab+Codex 병렬 점검 **통과**

---

## 0. 한 줄 요약

흩어진 파일을 **"버전관리 위치 × Unity 참조 위험도"** 두 축으로 분류해, 위험 낮은 것부터(루트 스크래치 삭제) 높은 것까지(GUID 참조 폴더 병합) 순서대로 처리했다. 삭제·이동은 전부 .meta 동반, 병합은 GUID 보존을 전제로 했다.

---

## 1. 분류 기준 (Decision Tree)

파일을 만났을 때 던진 질문 순서:

```
① Unity Assets/ 안인가?
   NO → 버전관리/ignore 여부 확인
        ├ git-ignore된 스크래치(_*.png 등) → [A] 삭제 안전
        └ 추적 안 됨·잡파일                → [F] 정리
   YES ↓
② .meta가 있는 임포트 자산인가? (GUID 보유)
   ③ 다른 자산이 GUID로 참조하는가?  ← 검사 필수
        NO(디버그 캡처 등)            → [B] .meta 동반 삭제
        YES                          → 이동만, 삭제 금지
   ④ 어디에 속해야 맞나? (씬/구조 일관성)
        루즈 씬 → [C] 정규 폴더로 이동(.meta 동반)
        대용량 비런타임(.unitypackage) → [D] Assets 밖으로
        단수/복수 중복 폴더 → [E] 병합(파일+.meta, GUID 보존)
```

### 핵심 원칙
- **GUID는 .meta에 산다.** 파일+.meta를 *함께* 옮기면 경로가 바뀌어도 씬·프리팹·컨트롤러의 참조는 GUID로 살아남는다. → 이동은 안전.
- **경로 기반 참조는 GUID 보존으로 안 지켜진다.** `AssetDatabase.LoadAssetAtPath`, 하드코딩 경로 문자열, 권한 화이트리스트 등은 따로 추적·수정해야 한다. ← 이번 세션 위험점 전부 여기서 나옴.
- **삭제 전 참조 검사**는 재량 아님(게이트). 미참조 확인 후에만 삭제.

---

## 2. 분류 결과 & 처리

| 분류 | 대상 | 위험도 | 처리 |
|---|---|---|---|
| **[A] 루트 스크래치** | `_*.png` ~95, `vfx_*.png`, `_aimDir`, `_caniathrox_export.obj`, `_codex_tasklayers.txt`, `_vidframe/`, `CLAUDE.md.bak` | 없음 (Unity 밖·git-ignore) | **삭제** |
| **[B] Assets 디버그 PNG** | `katana_d*`, `katana_dissolve_*`, `katana_edge_test`, `kd_*`, `ui_preview` (11개) | 낮음 (GUID 미참조 확인) | **.meta 동반 삭제** |
| **[C] 루즈 테스트 씬** | `_PlayerStackTest.unity`, `_ProjectileLookLab.unity` | 낮음 (빌드세팅 미등록) | `_Project/Scenes/Labs/`로 **이동** |
| **[D] 대용량 비런타임** | `Flipbook_VFX_Bundle_URP...`, `Stylized_VFX_Bundle_URP...` (541MB) | 없음 (임포트 완료·git-ignore) | 루트 `_packages/`로 **이동** |
| **[E] 중복 폴더 병합** | `Animation`→`Animations` (4) · `Material`→`Materials` (5) · `Prefab`→`Prefabs` (19) | **높음** (GUID 참조 자산) | 파일+.meta **병합**, 빈 폴더+.meta 삭제 |
| **[F] 경로 정정** | (E·B 이동의 부수효과) | — | 아래 §4 |

**합계: 이미지 108개 삭제 · 폴더 3쌍 병합 · 567MB Assets 밖으로.**

---

## 3. 안 건드린 것 (의도적 제외)

- **스크립트** — `_Project/Scripts/` 아래 카테고리별(Player/Run/Upgrade/Audio/Meta/MapGen/Data/Debug/Editor)로 이미 정리돼 있어 손대지 않음. (198개 .cs, 루즈 .cs 0)
- **벤더 데모 씬·에셋** — Feel/LMHPOLY/POLYBOX/Vefects/Synty 등 패키지 소유물은 정리 대상 아님.
- **`_Project/Image/`** (단수지만 복수 짝 없음), **`_FrankCaptureView/`·`_SidekickTest/`·`__AnimCaptures/`** (스크래치성 캡처 폴더), **`_Project/Setting/`** (단수) — 범위 밖. 다음 세션 후보.

---

## 4. 발견·수정한 경로 기반 참조 (위험점)

GUID로 안 지켜지는 참조들 — 게이트(Stab+Codex)와 자가검사로 색출:

| 파일 | 문제 | 수정 |
|---|---|---|
| `Scripts/Editor/KatanaComboRetimer.cs` | `OutDir = "_Project/Animation"` (단수) — 리타이밍 클립을 옛 폴더에 *쓰는* 상수. 재실행 시 폴더 부활+중복 클립 | → `Animations` |
| `Docs/LowPolyFlat_Shader_Reference.md` | 머티리얼 경로 `_Project/Material/` stale | → `Materials/` |
| `.claude/settings.local.json` (196·198행) | 권한 화이트리스트가 `_Project\Prefab\` 단수 하드코딩 (Stab P1) | → `Prefabs\` ×3 |
| `.claude/agent-memory/Animation/*.md` (5건) | 컨트롤러/마스크 경로 `_Project/Animation/` stale | → `Animations/` |

---

## 5. 게이트 결과 (비누설 — 원문)

- **Codex (크로스프로바이더):** 기능적 클린. SUSPECT 1건 = 셰이더 문서 stale 경로(런타임 위험 0) → 수정.
- **Stab (Unity/C# QA):** P1 1건 = `settings.local.json` 단수 Prefab 경로(런타임 무영향, 재실행 시 빈 결과) → 수정. **나머지 전부 클린**: EditorBuildSettings·GUID 정합(KatanaMelee.controller 내 Retimed/UpperBody guid 일치)·orphan meta 0·삭제 PNG 이름참조 0·병합 동명충돌 0.
- **JSON 무결성:** node 표준 파서로 settings.local.json **VALID** 확정.

---

## 6. 검사 명령 (재현용)

```bash
# 삭제 전 — 디버그 PNG GUID 미참조 확인
for f in katana_d0 kd_solid ui_preview ...; do
  guid=$(grep -m1 'guid:' "Assets/$f.png.meta" | awk '{print $2}')
  grep -rl "$guid" Assets/_Project Assets/Scenes   # 결과 없음 = 안전
done

# 병합 전 — 이름 충돌 검사
comm -12 <(ls Animation|grep -v .meta|sort) <(ls Animations|grep -v .meta|sort)

# 병합 후 — 경로 기반 참조 잔존 색출
grep -rnE '_Project[\\/]+(Animation|Material|Prefab)[\\/]' Assets ProjectSettings .claude \
  | grep -vE 'Animations|Materials|Prefabs'

# orphan/missing .meta
find Assets/_Project -name '*.meta' | while read m; do [ -e "${m%.meta}" ] || echo "ORPHAN $m"; done
```
