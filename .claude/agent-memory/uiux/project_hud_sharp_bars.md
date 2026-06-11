---
name: HUD 바 샤프화 + 정렬 통일 (2026-06-08)
description: 타원 바 해결책, RectMask2D 패턴, MARGIN 상수, 세그먼트 틱 구현
type: project
---

## 확정 결정

**근본 원인**: `UISprite.psd`(빌트인 9-slice 둥근 스프라이트)를 `Image.Type.Filled`에 쓰면 fill이 짧을 때 타원/알약으로 보임.

**해결책**: `white_square.png` (8×8 흰색, border=0) 에셋을 `Assets/_Project/Art/UI/white_square.png`에 에디터 빌드 시점에 생성+임포트. `EnsureWhiteSquareSprite()`가 idempotent하게 처리.

**Why**: 씬 재로드 후 null이 되는 에디터 베이크 텍스처와 달리, 프로젝트 에셋으로 직렬화된 PNG는 영구적으로 참조 가능.

**How to apply**: 빌더에서 모든 바 트랙/필/패널 배경에 이 스프라이트 + `Image.Type.Simple` 사용. 원형 점(Knob.psd)은 유지.

## RectMask2D 패턴

바 트랙에 `RectMask2D` 컴포넌트 추가 → fill이 트랙 경계 밖으로 렌더되지 않음 (CSS overflow:hidden 근사).
적용 대상: XP_Track, Sync_BarTrack, HP_Track, Boss_BarTrack.

## 세그먼트 틱

`AddSegmentTicks(track, segments, tickColor)` 헬퍼:
- HP: 4분할 (25/50/75% 위치), opacity 0.18 흰색
- SYNC: 3분할 (33/66% 위치), 3단계(30/60/90%) 기준

## 정렬 그리드

`const float MARGIN = 16f` (목업 px) → 실제 31.2px.
코너 패널 anchoredPosition:
- 좌상단: V(MARGIN, -MARGIN)
- 우상단: V(-MARGIN, -MARGIN)
- 좌하단: V(MARGIN, MARGIN)
- 우하단: V(-MARGIN, MARGIN)
- TopCenter: V(0, -MARGIN)
