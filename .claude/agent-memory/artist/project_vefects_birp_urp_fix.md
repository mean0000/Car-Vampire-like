---
name: vefects-birp-urp-fix
description: Vefects 슬래시/임팩트 BIRP→URP 셰이더 교체 완료(마젠타 제거) — guid 매핑 + 캡처검증
metadata:
  type: project
---

## 결론
Vefects 팩들이 "URP" 표기임에도 재질이 BIRP 셰이더를 참조해 핑크(마젠타)가 뜬다. URP 동등 셰이더가 같은 팩 안에 존재하므로 재질의 m_Shader guid만 교체하면 해결된다. 변환 도구 없음 — 수동 YAML 교체.

**Why:** Vefects가 BIRP·URP 버전을 같은 패키지에 혼재 배포. 에셋 임포트 시 재질이 BIRP 셰이더 guid를 기본 참조해 URP 프로젝트에서 핑크.

**How to apply:** 새 Vefects 재질 추가 시 m_Shader guid 확인 필수. 아래 매핑 그대로 재사용.

---

## 완료된 교체 목록 (2026-06-19)

### Anime VFX URP — Shared/Materials/
| 재질 파일 | BIRP guid(교체 전) | URP guid(교체 후) | 셰이더 |
|---|---|---|---|
| M_VFX_Slash_01.mat | 9ca8d8af065f33e40a23f679979ce2d3 | c1638a09fe0e679499da744f3dbb2d7f | SH_VFX_Stylized_Slash_01 |
| M_VFX_Slash_02.mat | 9ca8d8af065f33e40a23f679979ce2d3 | c1638a09fe0e679499da744f3dbb2d7f | SH_VFX_Stylized_Slash_01 |
| M_VFX_Slash_03.mat | 9ca8d8af065f33e40a23f679979ce2d3 | c1638a09fe0e679499da744f3dbb2d7f | SH_VFX_Stylized_Slash_01 |
| M_VFX_Slash_04.mat | b8316386d8e5ff84093866f81e518acc | 279d9de83fd0f574ca6b4430e9fec833 | SH_VFX_Stylized_Dissolve |
| M_VFX_Distortion_Slash New.mat | a00711177f21b2147bdb04e74de13c37 | 226fc7e2bc3f50a478ac7dde176d3360 | SH_VFX_Vefects_Distortion_Slash_01 |

### Stylized VFX URP — Generic + Electric
| 재질 파일 | BIRP guid | URP guid | 셰이더 |
|---|---|---|---|
| M_VFX_Slash_Generic.mat | d5c9a5274bb74e54bab3a8110e50193d | 65456828ad9100f4c9466db6547093e9 | SH_VFX_Vefects_Slash_URP_New |
| M_VFX_Slash_Generic_Add.mat | d5c9a5274bb74e54bab3a8110e50193d | 65456828ad9100f4c9466db6547093e9 | SH_VFX_Vefects_Slash_URP_New |
| M_VFX_Slash_Circle_Generic.mat | d5c9a5274bb74e54bab3a8110e50193d | 65456828ad9100f4c9466db6547093e9 | SH_VFX_Vefects_Slash_URP_New |
| M_VFX_Piercing_Generic.mat | f4a25a78ebfcce646be88bccf6896579 | a22eefb8bd3d65845b2a333a238d3432 | SH_VFX_Vefects_Piercing_URP_New |
| M_VFX_Slash_Electric.mat | d5c9a5274bb74e54bab3a8110e50193d | 65456828ad9100f4c9466db6547093e9 | SH_VFX_Vefects_Slash_URP_New |
| M_VFX_Piercing_Electric.mat | f4a25a78ebfcce646be88bccf6896579 | a22eefb8bd3d65845b2a333a238d3432 | SH_VFX_Vefects_Piercing_URP_New |

---

## 전체 BIRP→URP guid 매핑 (Anime VFX URP 팩)
- Slash BIRP: 9ca8d8af065f33e40a23f679979ce2d3 → URP: c1638a09fe0e679499da744f3dbb2d7f
- DistortionSlash BIRP: a00711177f21b2147bdb04e74de13c37 → URP: 226fc7e2bc3f50a478ac7dde176d3360
- Dissolve BIRP: b8316386d8e5ff84093866f81e518acc → URP: 279d9de83fd0f574ca6b4430e9fec833

## 전체 BIRP→URP guid 매핑 (Stylized VFX URP 팩)
- Slash BIRP: d5c9a5274bb74e54bab3a8110e50193d → URP: 65456828ad9100f4c9466db6547093e9
- Piercing BIRP: f4a25a78ebfcce646be88bccf6896579 → URP: a22eefb8bd3d65845b2a333a238d3432

---

## 캡처 검증 결과
- vfx_urp_check.png: VFX_Basic_Attack + VFX_Slash_Generic — 마젠타 없음, 흰/회 슬래시 정상
- vfx_electric_check.png: VFX_Slash_Electric + VFX_Piercing_Electric — 마젠타 없음, 정상 렌더

## 남은 BIRP 재질 (카타나 범위 밖 — 미처리)
Stylized 팩의 원소별 슬래시(Fire/Ice/Water/Dark/Void/Nature/Earth/Sound) 재질들은 동일 BIRP 패턴. 필요 시 위 매핑으로 동일하게 교체 가능.

## 참고 함정
- _GrabTexture 에러: URP에서 GrabPass 없어도 씬 내 다른 BIRP 잔류 오브젝트에서 발생 가능 — VFX 렌더 자체와 무관
- Stylized VFX URP 팩의 슬래시 Circle 계열도 Slash BIRP 사용 → 동일 매핑
