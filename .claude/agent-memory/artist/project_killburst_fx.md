---
name: killburst-fx
description: 킬 버스트 프리팹 v1 (시안코어+마젠타엣지) + URP 파티클 머티리얼 코드생성 함정·Vefects 셰이더 실태
metadata:
  type: project
---

처치 순간 화려한 authored 버스트. `Assets/_Project/Resources/VFX/KillBurst.prefab`(일반, ~1.7m), `KillBurstConverged.prefab`(수렴샷, ~2.7m·더 화려). 각 ParticleSystem 루트 + 자식 3 PS(Edge_Magenta / Core_Cyan / Sparks). loop=false, playOnAwake=false, stopAction=None — Gameplay가 풀링 Play/Stop 제어. 자기파괴 스크립트 없음. 색=시안 코어(0.35,0.75,0.85 보상/정화 — ZombieDeathFX 식구) + 마젠타 엣지/스파크(1,0.17,0.84 신호붕괴 캐넌). ⚠️가산 블렌딩이라 시안+마젠타 겹치면 코어가 흰클립 불가피 — 최종 색농도는 유저 인게임 판정 대기.

**소스**: Vefects `Flipbook VFX/Shared/Textures/Explosion/FB_3x4_RGBC_magicExplosion_01.tga`(3x4=12프레임, guid bbb35a7724f3a8440a9562f6c75ddbaf). 알파 마스크가 4방향 별폭발→링와해 시퀀스라 탑뷰 가독성 최적. 스파크=코드생성 소프트 도트. 마스크 텍스처는 원본 알파→흰RGB로 변환해 `Materials/Tex_KillBurstMask.png`로 저장(가산이라 RGB=형태).

**★URP 파티클 머티리얼 함정 (4번 헛돌고 알아낸 근본원인)**:
- Vefects 3팩(Pixel Craft·Flipbook VFX·Combat Flipbook) 셰이더는 전부 `#pragma surface`(BIRP surface shader) — URP에서 마젠타. "Flipbook VFX"의 `SH_Vefects_Unlit_Flipbook_URP`조차 surface라 URP 미작동(파일명만 URP). **이 3팩 URP 변환=실질 불가, 새로 머티리얼 만들어야 함.**
- ★★정정(2026-06-19): **"Anime VFX URP" 팩과 "Stylized VFX URP" 팩은 URP 동등 셰이더가 패키지 내에 이미 존재.** 재질의 m_Shader guid만 BIRP→URP로 교체하면 해결됨. 상세=[[vefects-birp-urp-fix]]
- **`new Material(Shader.Find("URP/Particles/Unlit"))` 코드생성 머티리얼은 텍스처(_BaseMap)가 흰색 폴백** = 파티클이 단색 사각형으로 나옴. AssetVersion MonoBehaviour + 파티클 부속 프로퍼티(_ColorMode/_FlipbookMode/_Mode 등)가 없어서 텍스처 샘플 패스가 안 켜짐.
- **해결 = 검증된 파티클 머티리얼을 `new Material(baseMat)`로 복제 후 텍스처·색만 교체.** 베이스로 `Assets/Feel/FeelDemos/Letters/Materials/FeelLettersDemoSparkMaterial.mat`(URP/Particles/Unlit, 작동확인) 사용. `_BaseMap`+`_MainTex` 둘 다 채우고 _SrcBlend=SrcAlpha/_DstBlend=One/_ZWrite=0/_Surface=1/_Blend=2로 가산 오버라이드.
- URP/Unlit(불투명 베이스)은 MeshRenderer엔 텍스처 정상, **ParticleSystemRenderer엔 단색** — 파티clel엔 반드시 Particles 계열 셰이더.

**RunCommand 함정**: `TextureImporter.SaveAndReimport()`·`AssetDatabase.DeleteAsset()`은 MCP에서 대화상자 유발→즉사("User interactions not supported"). 텍스처는 PNG로 `File.WriteAllBytes`+`AssetDatabase.ImportAsset`(대화상자 없음). 코드생성 `Texture2D.asset`은 셰이더 샘플에서 알파/RGB 흰폴백되니 쓰지 말 것 — PNG로 디스크 기록 필수. 파일 삭제는 bash `rm`+Refresh.

검증=디스크 렌더(임시 STAGE y=500 격리 무대+CapCam 45도+PS.Simulate(t)+cam.Render→PNG→Read). MCP Camera_Capture 금지(죽은 프레임). ⚠️첫 캡처 때 카메라가 열린 씬 지오메트리를 잡으니 STAGE를 멀리(y=500) 격리.

관련: [[cozy-mcp-bypass]] [[telegraph-pad-fx]] [[phase-split-afterimage]]
