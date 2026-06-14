# -*- coding: utf-8 -*-
# 스폰 디렉터 시각화 — 뱀서류 동적 젠: 밴드=젠존, 착지 즉시교전, 표효 호출, 구심 강몹↑, 시간 번짐
import os, math
IMG="_img/monsters"
BAND={"rim":"#60a0e6","mid":"#e49646","core":"#e45646"}
def P(x,z): return (x+75, 75-z)

def arrow(x0,y0,x1,y1,color,w=1.2,op=0.85,head=2.4):
    ang=math.atan2(y1-y0,x1-x0)
    hx,hy=x1,y1
    p1=(hx-head*math.cos(ang-0.5),hy-head*math.sin(ang-0.5))
    p2=(hx-head*math.cos(ang+0.5),hy-head*math.sin(ang+0.5))
    return (f'<line x1="{x0:.1f}" y1="{y0:.1f}" x2="{x1:.1f}" y2="{y1:.1f}" stroke="{color}" stroke-width="{w}" opacity="{op}"/>'
            f'<polygon points="{hx:.1f},{hy:.1f} {p1[0]:.1f},{p1[1]:.1f} {p2[0]:.1f},{p2[1]:.1f}" fill="{color}" opacity="{op}"/>')

def spawn_icon(i,img,x,z,band,r=5.2,toward=None):
    cx,cy=P(x,z); c=BAND[band]; cid=f"si_{i}"
    s=f'<clipPath id="{cid}"><circle cx="{cx}" cy="{cy}" r="{r}"/></clipPath>'
    s+=f'<image href="{IMG}/{img}.png" x="{cx-r}" y="{cy-r}" width="{2*r}" height="{2*r}" clip-path="url(#{cid})" preserveAspectRatio="xMidYMid slice"/>'
    s+=f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="none" stroke="{c}" stroke-width="1"/>'
    # spawn burst ticks
    for a in range(0,360,45):
        ar=math.radians(a); s+=f'<line x1="{cx+(r+0.6)*math.cos(ar):.1f}" y1="{cy+(r+0.6)*math.sin(ar):.1f}" x2="{cx+(r+2)*math.cos(ar):.1f}" y2="{cy+(r+2)*math.sin(ar):.1f}" stroke="{c}" stroke-width="0.5" opacity="0.5"/>'
    if toward:
        tx,ty=P(*toward); dx,dy=tx-cx,ty-cy; L=math.hypot(dx,dy); dx,dy=dx/L,dy/L
        s+=arrow(cx+dx*(r+2),cy+dy*(r+2),cx+dx*(r+11),cy+dy*(r+11),c,1.1,0.6,2)
    return s

def heatbands():
    cx,cy=P(0,0)
    s=('<defs><radialGradient id="heat" cx="50%" cy="50%" r="52%">'
       '<stop offset="0%" stop-color="#5a2a24"/><stop offset="35%" stop-color="#56402e"/>'
       '<stop offset="70%" stop-color="#283244"/><stop offset="100%" stop-color="#1a2230"/>'
       '</radialGradient></defs>')
    s+=f'<circle cx="{cx}" cy="{cy}" r="73" fill="url(#heat)"/>'
    s+=f'<circle cx="{cx}" cy="{cy}" r="73" fill="none" stroke="#4a443c" stroke-width="2"/>'
    for rr in (22,50):
        s+=f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="none" stroke="#cfd2d8" stroke-width="0.4" stroke-dasharray="2 2" opacity="0.6"/>'
    return s

def beacon():
    cx,cy=P(0,0); s=""
    for rr,op in [(11,0.18),(6,0.35),(3,0.6)]: s+=f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="#ffc456" opacity="{op}"/>'
    s+=f'<text x="{cx}" y="{cy+1.3}" font-size="3.2" fill="#10131a" text-anchor="middle">★</text>'
    return s

# ===== 메인 맵 =====
def main_map():
    s=['<svg viewBox="0 0 150 150" class="map">', heatbands()]
    # 밴드 라벨
    s.append('<text x="75" y="9" font-size="3.4" font-weight="700" fill="#60a0e6" text-anchor="middle">외곽 띠 = LV1 젠존</text>')
    s.append('<text x="75" y="29" font-size="3.4" font-weight="700" fill="#e49646" text-anchor="middle">중간 띠 = LV2~3 젠존</text>')
    s.append('<text x="75" y="62" font-size="3.2" font-weight="700" fill="#e45646" text-anchor="middle">코어 = LV4~5 젠존</text>')
    s.append(beacon())
    # 구심 강몹↑ 화살표 (외곽→코어)
    a0=P(-52,-52); a1=P(-10,-10)
    s.append(arrow(a0[0],a0[1],a1[0],a1[1],"#f0b46a",1.8,0.9,3.2))
    s.append(f'<text x="{P(-40,-40)[0]}" y="{P(-40,-40)[1]-2}" font-size="3.1" font-weight="700" fill="#f0b46a" text-anchor="middle" transform="rotate(45 {P(-40,-40)[0]} {P(-40,-40)[1]})">중심 접근 → 강몹 젠율 ↑</text>')
    # 젠 아이콘 (밴드별, 플레이어로 향함)
    pl=(10,16)
    icons=[("Lacercharias","rim",-40,48),("Venodonte","rim",46,40),("Lacercharias","rim",18,62),
           ("Caniathrox","mid",-26,30),("Kupolojuve","mid",34,18),("Dimaxillosaurus","mid",-8,-30),
           ("Fulgurodonte","core",-16,4),("Venosaur","core",14,-12)]
    for i,(img,band,x,z) in enumerate(icons):
        s.append(spawn_icon(i,img,x,z,band,5.0,toward=pl))
    # 표효 (Caniathrox에)
    rx,ry=P(-26,30)
    for rr,op in [(9,0.5),(13,0.32),(17,0.18)]:
        s.append(f'<circle cx="{rx}" cy="{ry}" r="{rr}" fill="none" stroke="#ffd166" stroke-width="0.9" opacity="{op}"/>')
    s.append(f'<text x="{rx}" y="{ry-19}" font-size="3.4" font-weight="800" fill="#ffd166" text-anchor="middle">표효! → 유사 몬스터 호출</text>')
    # 플레이어 + 수렴 호드
    px,py=P(*pl)
    s.append(f'<circle cx="{px}" cy="{py}" r="3.4" fill="#eef0f4" stroke="#10131a" stroke-width="1"/>')
    s.append(f'<text x="{px}" y="{py-6}" font-size="3.2" font-weight="700" fill="#eef0f4" text-anchor="middle">플레이어</text>')
    for ang in range(0,360,40):
        a=math.radians(ang); ex,ey=px+22*math.cos(a),py+22*math.sin(a)
        s.append(arrow(ex,ey,px+7*math.cos(a),py+7*math.sin(a),"#cf6f6f",0.9,0.55,1.8))
    s.append(f'<text x="{px}" y="{py+9}" font-size="2.8" fill="#cf8f8f" text-anchor="middle">몰려오는 호드 속 액션</text>')
    s.append('</svg>')
    return "\n".join(s)

# ===== 시간 미니맵 (착지 / 중반 / 후반) =====
def mini(phase):
    import random; random.seed({"t0":1,"t1":2,"t2":3}[phase])
    cx,cy=75,75; s=['<svg viewBox="0 0 150 150" class="mini">']
    s.append(f'<circle cx="{cx}" cy="{cy}" r="73" fill="url(#heat)"/>')
    s.append(f'<circle cx="{cx}" cy="{cy}" r="73" fill="none" stroke="#4a443c" stroke-width="2"/>')
    for rr in (22,50): s.append(f'<circle cx="{cx}" cy="{cy}" r="{rr}" fill="none" stroke="#cfd2d8" stroke-width="0.4" stroke-dasharray="2 2" opacity="0.55"/>')
    s.append(beacon())
    def dots(n, rmin, rmax, color, rad=2.2):
        out=""
        for _ in range(n):
            a=random.uniform(0,2*math.pi); rr=random.uniform(rmin,rmax)
            x=cx+rr*math.cos(a); y=cy+rr*math.sin(a)
            out+=f'<circle cx="{x:.1f}" cy="{y:.1f}" r="{rad}" fill="{color}" opacity="0.9"/>'
        return out
    # player position per phase
    if phase=="t0":
        s.append(dots(16,52,70,BAND["rim"]))      # 외곽 LV1 빽빽
        s.append(dots(4,30,46,BAND["mid"]))
        pl=(0,60)
        # 표효 burst
        rx,ry=P(8,58)
        for rr,op in [(7,0.5),(11,0.3)]: s.append(f'<circle cx="{rx}" cy="{ry}" r="{rr}" fill="none" stroke="#ffd166" stroke-width="0.8" opacity="{op}"/>')
        title="① 착지 직후"; desc="즉시 인식 + 표효 → 외곽 LV1 떼 수렴"
    elif phase=="t1":
        s.append(dots(10,52,70,BAND["rim"]))
        s.append(dots(14,26,48,BAND["mid"]))
        s.append(dots(4,8,22,BAND["core"]))
        pl=(0,34)
        title="② 중반 (안으로 전진)"; desc="중간 LV2~3 본격 + 코어 LV4~5 등장"
    else:
        s.append(dots(12,52,70,BAND["rim"]))
        s.append(dots(10,52,70,BAND["mid"]))   # ★중간몹이 외곽서도
        s.append(dots(6,40,70,BAND["core"]))   # ★코어몹이 바깥서도 = 번짐
        s.append(dots(14,8,40,BAND["core"]))
        pl=(0,10)
        title="③ 후반 (번짐 escalation)"; desc="★안쪽 강몹이 바깥 띠에서도 젠 — 안전 외곽 잠식"
    px,py=P(*pl)
    s.append(f'<circle cx="{px}" cy="{py}" r="3" fill="#eef0f4" stroke="#10131a" stroke-width="0.8"/>')
    s.append('</svg>')
    return "\n".join(s), title, desc

m0=mini("t0"); m1=mini("t1"); m2=mini("t2")

rules=[
 ("1 · 착지 = 즉시 교전","드랍하는 순간 주변 몬스터가 바로 인식·어그로. 워밍업 없음 — 첫 발부터 전투."),
 ("2 · 표효 호출","몬스터가 포효하면 주변의 <b>유사 몬스터</b>가 그 방향으로 몰려옴(어그로 전파+호드 형성)."),
 ("3 · 밴드 = 젠 존 (무한)","동심원 각 띠가 자기 LV 풀을 플레이어 주위로 계속 스폰(뱀서류 수렴). 고정 배치 ❌."),
 ("4 · 구심 변조 (공간)","중심에 가까울수록 <b>강몹 젠율 ↑</b>. 코어로 갈수록 위험·밀도↑ = 끌림의 대가."),
 ("5 · 시간 변조 (번짐)","시간이 갈수록 <b>안쪽(강한) 몹이 바깥 띠에서도 스폰</b>. 안전 외곽이 점점 잠식(escalation)."),
]
rule_html="".join(f'<div class="rc"><h4>{h}</h4><p>{p}</p></div>' for h,p in rules)

html=f"""<!DOCTYPE html><html lang="ko"><head><meta charset="UTF-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>스폰 디렉터 — 뱀서류 동적 젠</title>
<style>
:root{{--bg:#0f1014;--panel:#1a1d24;--edge:#3a3f49;--txt:#e6e8ec;--dim:#9aa0a8;--sub:#c2c6cc;--gold:#fac95a}}
*{{box-sizing:border-box;margin:0;padding:0}}
body{{background:radial-gradient(1200px 700px at 50% -10%,#161922,#0f1014 60%);color:var(--txt);
font-family:"Pretendard","Malgun Gothic","Apple SD Gothic Neo","Segoe UI",sans-serif;line-height:1.55;padding:38px 24px 80px}}
.wrap{{max-width:1180px;margin:0 auto}}
.stamp{{display:inline-block;border:1px solid var(--edge);border-radius:4px;padding:3px 10px;font-size:11px;letter-spacing:.12em;color:var(--dim);margin-bottom:14px}}
h1{{font-size:29px;font-weight:800;margin-bottom:6px}} h1 .s{{color:var(--dim);font-weight:600;font-size:17px;margin-left:8px}}
.lede{{color:var(--dim);font-size:14px;max-width:980px;margin-bottom:22px}}
.rules{{display:grid;grid-template-columns:repeat(5,1fr);gap:11px;margin-bottom:26px}}
@media(max-width:900px){{.rules{{grid-template-columns:1fr 1fr}}}}
.rc{{background:var(--panel);border:1px solid var(--edge);border-radius:9px;padding:12px 13px}}
.rc h4{{font-size:13px;color:var(--gold);margin-bottom:5px}} .rc p{{font-size:12px;color:var(--dim)}} .rc b{{color:var(--sub)}}
.maprow{{display:grid;grid-template-columns:1.45fr 1fr;gap:20px;align-items:start}}
@media(max-width:880px){{.maprow{{grid-template-columns:1fr}}}}
.card{{background:var(--panel);border:1px solid var(--edge);border-radius:12px;padding:16px}}
.card h2{{font-size:17px;font-weight:800;margin-bottom:10px}}
svg.map{{width:100%;height:auto;background:#11131a;border:1px solid var(--edge);border-radius:8px;
font-family:"Pretendard","Malgun Gothic","Segoe UI",sans-serif}}
.side h3{{font-size:14px;color:var(--gold);margin-bottom:8px}} .side p{{font-size:12.5px;color:var(--dim);margin-bottom:12px}}
.tl{{margin-top:26px}} .tl h2{{font-size:17px;font-weight:800;margin-bottom:12px}}
.minis{{display:grid;grid-template-columns:repeat(3,1fr);gap:16px}}
@media(max-width:760px){{.minis{{grid-template-columns:1fr}}}}
.mw{{background:var(--panel);border:1px solid var(--edge);border-radius:10px;padding:12px}}
svg.mini{{width:100%;height:auto;background:#11131a;border:1px solid var(--edge);border-radius:7px}}
.mw h4{{font-size:13.5px;margin:9px 0 3px}} .mw p{{font-size:12px;color:var(--dim)}}
.dotleg{{margin-top:18px;font-size:12.5px;color:var(--dim)}} .dotleg b{{color:var(--sub)}}
.warn{{margin-top:22px;border:1px solid #6a5326;background:rgba(250,201,90,.07);border-radius:10px;padding:14px 17px;font-size:12.5px;color:#e7c98a}}
footer{{margin-top:24px;font-size:11px;color:#6c727c;border-top:1px solid var(--edge);padding-top:12px}} footer code{{color:#9aa0a8}}
</style></head><body><div class="wrap">
<div class="stamp">사후처리부 · 1차 처리구역 젠 운용 · 동적 스폰 디렉터 · 2026-06-14</div>
<h1>스폰 디렉터 — 뱀서류 동적 젠<span class="s">원형 = 젠 존 · 몰려오는 호드 속 액션</span></h1>
<p class="lede">정적 토큰 배치 폐기. <b style="color:var(--sub)">동심원 = 젠 존</b>, 플레이어 주위로 무한 수렴. 착지 즉시 교전 + 표효 호출 + 구심 강몹↑ + 시간 번짐. (비콘/잭팟 끌림·릴레이 공개는 <i>지각</i> 레이어로 유지)</p>

<div class="rules">{rule_html}</div>

<div class="maprow">
  <div class="card"><h2>젠 운용도</h2>{main_map()}</div>
  <div class="card side">
    <h3>읽는 법</h3>
    <p>띠 색 = 그 띠가 스폰하는 LV 풀(🟦LV1 / 🟧LV2~3 / 🟥LV4~5). 아이콘 = 젠 출처(주위에서 계속 솟음), 화살표 = 플레이어로 수렴.</p>
    <h3>표효</h3>
    <p>한 마리가 포효하면 같은 종이 그 방향으로 몰림 = 한 곳에서 갑자기 호드가 불어남(액션 무대).</p>
    <h3>구심 = 끌림의 대가</h3>
    <p>가운데로 갈수록 강몹 젠율↑. 잭팟(★)은 욕심을 당기고, 그 대가가 강몹 밀도.</p>
    <h3>단일 평면</h3>
    <p>전부 지상 평면 수렴. 비행(Kupolojuve)도 평면 다이브로 합류.</p>
  </div>
</div>

<div class="tl">
  <h2>시간 변조 — 번짐 (escalation)</h2>
  <div class="minis">
    <div class="mw">{m0[0]}<h4>{m0[1]}</h4><p>{m0[2]}</p></div>
    <div class="mw">{m1[0]}<h4>{m1[1]}</h4><p>{m1[2]}</p></div>
    <div class="mw">{m2[0]}<h4>{m2[1]}</h4><p>{m2[2]}</p></div>
  </div>
  <div class="dotleg">점 색 = 스폰된 몬스터 LV(🟦LV1 🟧LV2~3 🟥LV4~5). <b>후반</b>엔 🟥코어 몹이 외곽 띠에서도 찍힘 = 안쪽 강몹이 바깥서도 젠(안전 외곽 잠식).</div>
</div>

<div class="warn">⚠️ 젠율·표효 쿨·번짐 곡선·동시 alive 캡 = <b>Gameplay 스폰 디렉터 수치</b>(여기선 규칙·의도만). 비콘/잭팟/릴레이 공개는 <i>지각</i> 레이어로 유지(끌림). <b>쫄깃함=게이트0 전투감+종별 애니 선행</b> — 호드가 많아도 한 마리가 안 무서우면 무해함만 많아짐.</div>

<footer>젠 규칙 출처: 유저 디렉팅 2026-06-14 · 좌표/밴드: <code>docs/02_logs/2026-06-14-first-stage-build-spec.md</code> · 독트린 <code>docs/00_authority/2026-06-14-natural-pull-doctrine.md</code> · 이미지 Protofactor Vol.2</footer>
</div></body></html>"""

out=os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)),"..","2026-06-14-spawn-director.html"))
open(out,"w",encoding="utf-8").write(html)
print("SAVED:",out)
