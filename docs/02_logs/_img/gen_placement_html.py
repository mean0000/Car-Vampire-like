# -*- coding: utf-8 -*-
# 배치 인카운터 맵 HTML 생성 — 집하장 + 분수광장, 몬스터 토큰(프리뷰 이미지) 좌표 배치
import os

BAND = {"rim":"#60a0e6","mid":"#e49646","core":"#e45646"}
IMG = "_img/monsters"   # HTML이 docs/02_logs 기준

def P(x,z): return (x+75, 75-z)   # (X,Z 미터) -> svg (0..150), +Z=위

def lvr(band):  # 토큰 반경 (LV 클수록 큼)
    return {"rim":5.6,"mid":6.6,"core":8.2}[band]

def token(i, sp, img, x, z, n, band, label):
    cx,cy=P(x,z); r=lvr(band); c=BAND[band]; cid=f"clip_{i}"
    s=f'<clipPath id="{cid}"><circle cx="{cx}" cy="{cy}" r="{r}"/></clipPath>'
    s+=f'<image href="{IMG}/{img}.png" x="{cx-r}" y="{cy-r}" width="{2*r}" height="{2*r}" clip-path="url(#{cid})" preserveAspectRatio="xMidYMid slice"/>'
    s+=f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{c}" stroke-width="1.1"/>'
    if n>1:
        bx,by=cx+r-1.5,cy-r+0.5
        s+=f'<circle cx="{bx}" cy="{by}" r="3.4" fill="#10131a" stroke="{c}" stroke-width="0.7"/>'
        s+=f'<text x="{bx}" y="{by+1.3}" font-size="3.4" font-weight="700" fill="{c}" text-anchor="middle">{n}</text>'
    s+=f'<text x="{cx}" y="{cy+r+4}" font-size="3.1" font-weight="700" fill="#dfe2e6" text-anchor="middle">{label}</text>'
    return s

def beat(x,z,num):
    cx,cy=P(x,z)
    return (f'<circle cx="{cx}" cy="{cy}" r="3.6" fill="#faca5a" stroke="#10131a" stroke-width="0.8"/>'
            f'<text x="{cx}" y="{cy+1.4}" font-size="4" font-weight="800" fill="#10131a" text-anchor="middle">{num}</text>')

def beacon(x,z,kind):
    cx,cy=P(x,z); s=""
    for rr,op in [(13,0.16),(8,0.28),(4,0.55)]:
        s+=f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="#ffc456" opacity="{op}"/>'
    if kind=="crane":
        s+=f'<line x1="{cx}" y1="{cy}" x2="{cx}" y2="{cy-1}" stroke="#9aa" stroke-width="2"/>'
        s+=f'<rect x="{cx-1.4}" y="{cy-1.4}" width="2.8" height="2.8" fill="#caa" transform="rotate(45 {cx} {cy})"/>'
        s+=f'<text x="{cx}" y="{cy+1.4}" font-size="3" fill="#10131a" text-anchor="middle">⌁</text>'
    else:
        s+=f'<rect x="{cx-4}" y="{cy-3}" width="8" height="6" rx="1" fill="#2a2f3a" stroke="#caa" stroke-width="0.6"/>'
        # swarm chevrons above platform
        import math
        for k in range(7):
            a=-1.6+0.45*k; px=cx+ (k-3)*2.0; py=cy-6-abs(k-3)*0.6
            s+=f'<path d="M{px-1.4},{py} L{px},{py+1.4} L{px+1.4},{py}" stroke="#b88fd0" stroke-width="0.9" fill="none"/>'
    s+=f'<text x="{cx}" y="{cy+19}" font-size="3.2" font-weight="700" fill="#faca5a" text-anchor="middle">코어 비콘</text>'
    return s

def lz(x,z):
    cx,cy=P(x,z)
    return (f'<circle cx="{cx}" cy="{cy}" r="5" fill="#58d076" stroke="#0c100c" stroke-width="1"/>'
            f'<text x="{cx}" y="{cy+1.6}" font-size="4.2" font-weight="800" fill="#0c180e" text-anchor="middle">퇴</text>'
            f'<text x="{cx}" y="{cy+10}" font-size="3.1" font-weight="700" fill="#58d076" text-anchor="middle">추출 LZ</text>')

def entry(x,z):
    cx,cy=P(x,z)
    return (f'<circle cx="{cx}" cy="{cy}" r="3" fill="#e6e8ec"/>'
            f'<text x="{cx}" y="{cy-5}" font-size="3" fill="#e6e8ec" text-anchor="middle">진입</text>')

def svg_map(cfg):
    W=150; out=[f'<svg viewBox="0 0 {W} {W}" class="map">']
    out.append('<defs><radialGradient id="heat" cx="50%" cy="50%" r="52%">'
        '<stop offset="0%" stop-color="#5a2a24"/><stop offset="38%" stop-color="#5a4530"/>'
        '<stop offset="72%" stop-color="#2a3445"/><stop offset="100%" stop-color="#1a2230"/>'
        '</radialGradient></defs>')
    cx,cy=P(0,0)
    out.append(f'<circle cx="{cx}" cy="{cy}" r="73" fill="url(#heat)"/>')
    # 봉쇄벽
    out.append(f'<circle cx="{cx}" cy="{cy}" r="73" fill="none" stroke="#4a443c" stroke-width="2"/>')
    # 밴드 링
    for rr,c in [(22,"#e45646"),(50,"#e49646")]:
        out.append(f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="none" stroke="{c}" stroke-width="0.5" stroke-dasharray="2 2" opacity="0.7"/>')
    # 리딩 라인
    for (x0,z0,x1,z1) in cfg["lead"]:
        a=P(x0,z0); b=P(x1,z1)
        out.append(f'<line x1="{a[0]}" y1="{a[1]}" x2="{b[0]}" y2="{b[1]}" stroke="#f0d27a" stroke-width="1.4" opacity="0.30"/>')
    # 컨테이너/파사드 블록
    for (x,z,w,h) in cfg.get("blocks",[]):
        bx,bz=P(x,z); out.append(f'<rect x="{bx-w/2}" y="{bz-h/2}" width="{w}" height="{h}" rx="0.8" fill="#23272f" stroke="#555c66" stroke-width="0.4"/>')
    # 카이팅 링
    for rr in cfg["loops"]:
        out.append(f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="none" stroke="#56d4e4" stroke-width="0.7" opacity="0.55"/>')
    out.append(beacon(*cfg["beacon"]))
    for (x,z,num) in cfg["beats"]: out.append(beat(x,z,num))
    out.append(lz(*cfg["lz"])); out.append(entry(*cfg["entry"]))
    for i,t in enumerate(cfg["tokens"]):
        out.append(token(f'{cfg["id"]}_{i}', *t))
    out.append('</svg>')
    return "\n".join(out)

# ---- 집하장 ----
JIP = dict(id="A", beacon=(0,0,"crane"), lz=(0,-68), entry=(0,70),
  lead=[(0,70,0,18),(0,-70,0,-18),(70,0,18,0),(-70,0,-18,0)],
  blocks=[(28,52,9,6),(-28,52,9,6),(40,30,6,9),(-40,30,6,9),(28,-30,9,6),(-28,-30,9,6),(45,-12,6,9),(-45,-12,6,9),(20,18,7,5),(-20,18,7,5)],
  loops=[62,36,13],
  beats=[(0,62,"1"),(0,40,"2"),(8,18,"3")],
  tokens=[
    ("Lacercharias","Lacercharias",0,60,3,"rim","Lacercharias"),
    ("Venodonte","Venodonte",12,54,1,"rim","Venodonte"),
    ("Caniathrox","Caniathrox",-2,40,4,"mid","Caniathrox"),
    ("Dimaxillosaurus","Dimaxillosaurus",-13,32,2,"mid","Dimax."),
    ("Kupolojuve","Kupolojuve",16,22,2,"mid","Kupolojuve"),
    ("Venosaur","Venosaur",10,7,4,"core","Venosaur"),
    ("Fulgurodonte","Fulgurodonte",-18,0,1,"core","Fulguro."),
  ])

# ---- 분수광장 ----
import math
spokes=[]
for ang in range(0,360,45):
    a=math.radians(ang); spokes.append((70*math.cos(a),70*math.sin(a),10*math.cos(a),10*math.sin(a)))
GWANG = dict(id="B", beacon=(0,0,"plaza"), lz=(0,-70), entry=(0,70),
  lead=spokes,
  blocks=[(30,55,8,7),(-30,55,8,7),(55,30,7,8),(-55,30,7,8),(55,-30,7,8),(-55,-30,7,8),(30,-55,8,7),(-30,-55,8,7)],
  loops=[62,36,13],
  beats=[(0,62,"1"),(0,30,"2"),(0,12,"3")],
  tokens=[
    ("Lacercharias","Lacercharias",-2,60,3,"rim","Lacercharias"),
    ("Venodonte","Venodonte",14,48,1,"rim","Venodonte"),
    ("Caniathrox","Caniathrox",-12,32,5,"mid","Caniathrox"),
    ("Dimaxillosaurus","Dimaxillosaurus",17,26,2,"mid","Dimax."),
    ("Kupolojuve","Kupolojuve",0,20,3,"mid","Kupolojuve"),
    ("Fulgurodonte","Fulgurodonte",40,0,1,"core","Fulguro.램"),
    ("Carcinoptera","Carcinoptera",-9,9,1,"core","Carcinop."),
  ])

def placement_list(items):
    return "".join(f'<li><span class="d {b}"></span><b>{s}</b> ×{n} — {note}</li>' for s,n,b,note in items)

JIP_LIST=[("Lacercharias",3,"rim","회랑 입구 (외곽)"),("Venodonte",1,"rim","컨테이너 뒤 지상 엄폐 사격"),
 ("Caniathrox",4,"mid","회랑 깔때기 직선 돌진(떼)"),("Dimaxillosaurus",2,"mid","컨테이너 틈 매복=③왈칵"),
 ("Kupolojuve",2,"mid","상공→평면 다이브"),("Venosaur",4,"core","코어 잭팟 호위"),
 ("Fulgurodonte",1,"core","야드 가장자리→잭팟 램(벽그로기)")]
GWANG_LIST=[("Lacercharias",3,"rim","방사 거리 입구"),("Venodonte",1,"rim","차량/바리케이드 뒤 엄폐"),
 ("Caniathrox",5,"mid","방사 거리로 쇄도"),("Dimaxillosaurus",2,"mid","파사드 코너 매복"),
 ("Kupolojuve",3,"mid","공중 군집=비콘→다이브"),("Fulgurodonte",1,"core","개활 런웨이 직선 램"),
 ("Carcinoptera",1,"core","공중 정예 다이브(분수광장 전용)")]

html=f"""<!DOCTYPE html><html lang="ko"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>몬스터 배치 — 1차 스테이지 인카운터 맵</title>
<style>
:root{{--bg:#0f1014;--panel:#1a1d24;--edge:#3a3f49;--txt:#e6e8ec;--dim:#9aa0a8;--sub:#c2c6cc;--gold:#fac95a;--rim:#60a0e6;--mid:#e49646;--core:#e45646}}
*{{box-sizing:border-box;margin:0;padding:0}}
body{{background:radial-gradient(1200px 700px at 50% -10%,#161922,#0f1014 60%);color:var(--txt);
font-family:"Pretendard","Malgun Gothic","Apple SD Gothic Neo","Segoe UI",sans-serif;line-height:1.55;padding:38px 24px 80px}}
.wrap{{max-width:1280px;margin:0 auto}}
.stamp{{display:inline-block;border:1px solid var(--edge);border-radius:4px;padding:3px 10px;font-size:11px;letter-spacing:.12em;color:var(--dim);margin-bottom:14px}}
h1{{font-size:29px;font-weight:800;margin-bottom:6px}} h1 .s{{color:var(--dim);font-weight:600;font-size:17px;margin-left:8px}}
.lede{{color:var(--dim);font-size:14px;max-width:980px;margin-bottom:6px}}
.logic{{display:grid;grid-template-columns:repeat(4,1fr);gap:12px;margin:18px 0 26px}}
@media(max-width:860px){{.logic{{grid-template-columns:1fr 1fr}}}}
.logic .b{{background:var(--panel);border:1px solid var(--edge);border-radius:8px;padding:11px 13px}}
.logic h4{{font-size:13px;color:var(--gold);margin-bottom:4px}} .logic p{{font-size:12px;color:var(--dim)}}
.maps{{display:grid;grid-template-columns:1fr 1fr;gap:22px}} @media(max-width:900px){{.maps{{grid-template-columns:1fr}}}}
.mapcard{{background:var(--panel);border:1px solid var(--edge);border-radius:12px;padding:16px 17px}}
.mapcard h2{{font-size:18px;font-weight:800;margin-bottom:2px}} .mapcard .one{{font-size:12.5px;color:var(--dim);margin-bottom:12px}}
svg.map{{width:100%;height:auto;background:#11131a;border:1px solid var(--edge);border-radius:8px;display:block;
font-family:"Pretendard","Malgun Gothic","Segoe UI",sans-serif}}
.relay{{font-size:12px;color:var(--sub);margin:12px 0 6px}} .relay b{{color:var(--gold)}}
ul.place{{list-style:none;display:flex;flex-direction:column;gap:6px;margin-top:6px}}
ul.place li{{font-size:12.5px;color:var(--sub)}} ul.place b{{color:var(--txt)}}
.d{{display:inline-block;width:9px;height:9px;border-radius:50%;margin-right:7px;vertical-align:middle}}
.d.rim{{background:var(--rim)}} .d.mid{{background:var(--mid)}} .d.core{{background:var(--core)}}
.leg{{margin-top:24px;background:var(--panel);border:1px solid var(--edge);border-radius:10px;padding:14px 18px;font-size:12.5px;color:var(--dim)}}
.leg b{{color:var(--sub)}}
.warn{{margin-top:20px;border:1px solid #6a5326;background:rgba(250,201,90,.07);border-radius:10px;padding:14px 17px;font-size:12.5px;color:#e7c98a}}
footer{{margin-top:26px;font-size:11px;color:#6c727c;border-top:1px solid var(--edge);padding-top:12px}}
footer code{{color:#9aa0a8}}
</style></head><body><div class="wrap">
<div class="stamp">사후처리부 · 1차 처리구역 배치 운용도 · 2026-06-14</div>
<h1>몬스터 배치 — 인카운터 맵<span class="s">집하장 · 분수광장 (150×150)</span></h1>
<p class="lede">빌드 스펙 좌표 그대로. 토큰=실제 에셋 프리뷰, 링 색=밴드(LV), ×N=동시 수, ①②③=콘 릴레이 공개 순서. 전부 단일 평면(비행=다이브).</p>

<div class="logic">
<div class="b"><h4>구심 전진</h4><p>외곽 LV1 파밍→레벨업→안으로 밀어 코어 LV4~5 잭팟. 밴드=난이도=보상.</p></div>
<div class="b"><h4>릴레이 공개</h4><p>진입(0,+70)→코어. 콘이 ①캐시→②중간 떼→③코어 정예를 차례로 깐다.</p></div>
<div class="b"><h4>역할 분담</h4><p>외곽=fodder+엄폐 사수 / 중간=러셔·비행·매복 / 코어=램 엘리트+호위.</p></div>
<div class="b"><h4>단일 평면</h4><p>고지 사격·등반 0. 비행(Kupolojuve·Carcinoptera)=평면 다이브/텔레그래프.</p></div>
</div>

<div class="maps">
 <div class="mapcard">
  <h2>집하장 <span style="color:var(--gold);font-size:13px">★추천</span></h2>
  <div class="one">회랑 코너 왈칵 — 컨테이너 미로를 누비다 ③에서 야드가 터진다. 강한 reveal.</div>
  {svg_map(JIP)}
  <div class="relay"><b>① (0,+62)</b> 크레인등만 머리 위 + 회랑 글린트 → <b>② (0,+40)</b> 컨테이너 틈으로 정예 윤곽 슬쩍 → <b>③ (±8,+18) 왈칵</b> 야드 전모(잭팟+Fulguro+Venosaur)</div>
  <ul class="place">{placement_list(JIP_LIST)}</ul>
 </div>
 <div class="mapcard">
  <h2>분수광장 <span style="color:var(--dim);font-size:13px">대조군</span></h2>
  <div class="one">방사 정조준 — 단상+공중 군집이 처음부터 또렷. 트임 reveal·광역 학살 무대.</div>
  {svg_map(GWANG)}
  <div class="relay"><b>① (0,+62)</b> 단상·군집 또렷(기준선) → <b>② (0,+30) 트임</b> 콘 확 넓어짐·광장 개활 → <b>③ (0,+12)</b> 단상 도달·정예 전모</div>
  <ul class="place">{placement_list(GWANG_LIST)}</ul>
 </div>
</div>

<div class="leg"><b>범례</b> &nbsp; 🟦 외곽 LV1 (파밍/안전) &nbsp; 🟧 중간 LV2~3 (카이팅 본전장) &nbsp; 🟥 코어 LV4~5 (잭팟) &nbsp;|&nbsp; 토큰 크기 = LV(클수록 큼) &nbsp;|&nbsp; 시안 원=카이팅 루프(림62/중간36/코어13) &nbsp;|&nbsp; 금색 점선=밴드 경계 &nbsp;|&nbsp; 노란 선=리딩 라인 &nbsp;|&nbsp; ⌁=크레인·단상 비콘 &nbsp;|&nbsp; 퇴=추출 LZ</div>

<div class="warn">⚠️ 이 수(×N)는 <b>한 비트의 동시 노출 의도</b>지 총 스폰이 아님 — 무한 스폰은 디렉터가 밴드 인구예산·스태거로 채움. 정면 팝인 0(디제틱 출처). 실제 동시 alive 캡·스태거·풀사이즈 = Gameplay. <b>쫄깃함은 게이트0 전투감 선행 판정</b>(맵=무대).</div>

<footer>좌표 출처: <code>docs/02_logs/2026-06-14-first-stage-build-spec.md</code> · 이미지: <code>docs/03_reference/assets/monster_previews/</code> (Protofactor Vol.2) · 독트린 <code>docs/00_authority/2026-06-14-natural-pull-doctrine.md</code></footer>
</div></body></html>"""

out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"..","2026-06-14-monster-placement.html")
open(os.path.abspath(out),"w",encoding="utf-8").write(html)
print("SAVED:",os.path.abspath(out))
