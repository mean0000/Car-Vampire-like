# -*- coding: utf-8 -*-
# 오브젝트 배치 독트린 — 3단 높이 위계 + 밀도 역구배 (탑다운 + 측면 인셋)
import os, math
from PIL import Image, ImageDraw, ImageFont
W,H=1280,980
BG=(15,16,20)
COOL=(36,52,74); MID=(150,104,52); HOT=(150,58,46)
TALL=(58,63,72); TALL_E=(120,126,136)   # 골격(키큰 건물)
PEG=(74,80,90); PEG_E=(120,126,136)      # 중간 페그
GOLD=(250,206,90); GLOW=(255,196,86)
LOOP=(86,212,228); LANE=(238,206,120)
TXT=(230,232,236); DIM=(150,156,164); SUB=(196,200,206)
def F(s,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,s)
    except: return ImageFont.load_default()
FT=F(24,True); FS=F(15); FL=F(14,True); FM=F(13)
img=Image.new("RGB",(W,H),BG); d=ImageDraw.Draw(img,"RGBA")
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))

# ===== 탑다운 =====
S=3.6; OX,OY=50,150
def P(x,z): return (OX+(x+75)*S, OY+(75-z)*S)
cx,cy=P(0,0)
R=75*S
for k in range(56):
    t=k/55; r=R*(1-t)
    col=lerp(COOL,MID,(t/.5)) if t<.5 else lerp(MID,HOT,((t-.5)/.5))
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col)
d.ellipse([cx-R,cy-R,cx+R,cy+R],outline=(74,68,60),width=3)
for rr in (22,50):
    d.ellipse([cx-rr*S,cy-rr*S,cx+rr*S,cy+rr*S],outline=(210,214,222,120),width=1)

def rrect(x,z,w,h,ang,fill,outline,wd=1):
    cxp,cyp=P(x,z); a=math.radians(ang); hw,hh=w*S/2,h*S/2
    pts=[(-hw,-hh),(hw,-hh),(hw,hh),(-hw,hh)]
    rot=[(cxp+px*math.cos(a)-py*math.sin(a), cyp+px*math.sin(a)+py*math.sin(0)+py*math.cos(a)) for px,py in pts]
    rot=[(cxp+px*math.cos(a)-py*math.sin(a), cyp+px*math.sin(a)+py*math.cos(a)) for px,py in pts]
    d.polygon(rot,fill=fill,outline=outline,width=wd)

# 외곽 골격 = 리딩라인 건물 줄 (8방위, 장축 radial)
for ang in range(0,360,45):
    a=math.radians(ang); rx,rz=62*math.cos(a),62*math.sin(a)
    rrect(rx,rz,7,20,ang,TALL,TALL_E,1)
    rx2,rz2=52*math.cos(a+22),52*math.sin(a+22)
    rrect(rx2,rz2,6,12,ang+22,TALL,TALL_E,1)
# 중간 페그 클러스터 (loop-able, 갭)
for ang in range(15,360,60):
    a=math.radians(ang)
    for off in (-7,7):
        px,pz=36*math.cos(a)+off*math.sin(a),36*math.sin(a)-off*math.cos(a)
        rrect(px,pz,5,5,ang,PEG,PEG_E,1)
# 코어 = 트인 아레나 (오브젝트 0) + 비콘
for r,al in [(12*S,40),(7*S,75),(3.5*S,160)]:
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=GLOW+(al,))
d.text((cx,cy),"★",font=FL,fill=(60,45,10),anchor="mm")
# 카이팅 루프
for rr in (62,36,13):
    d.ellipse([cx-rr*S,cy-rr*S,cx+rr*S,cy+rr*S],outline=LOOP+(150,),width=2)
# 카빙 레인 (방위 갭 = radial)
for ang in range(45,360,90):
    a=math.radians(ang); p0=P(70*math.cos(a),70*math.sin(a)); p1=P(16*math.cos(a),16*math.sin(a))
    d.line([p0,p1],fill=LANE+(110,),width=5)

# 라벨
d.text((cx,cy+15*S),"코어 = 트인 학살 아레나\n(오브젝트 0)",font=FM,fill=SUB,anchor="ma")
lx,lz=P(-58,40); d.text((lx,lz),"외곽 = 빽빽 폐허\n(골격 건물 줄)",font=FM,fill=SUB,anchor="lm")
mx,mz=P(40,-44); d.text((mx,mz),"중간 = 페그 클러스터\n(위빙·카빙)",font=FM,fill=SUB,anchor="ma")
# 밀도 역구배 화살표
a0=P(-66,-66); a1=P(-14,-14)
d.line([a0,a1],fill=(240,150,90,220),width=2)
d.polygon([a1,(a1[0]-9,a1[1]-2),(a1[0]-2,a1[1]-9)],fill=(240,150,90))
d.text((P(-44,-44)[0],P(-44,-44)[1]+12),"오브젝트 밀도 ↓ (안으로)",font=FM,fill=(240,170,120),anchor="ma")

d.text((OX,40),"오브젝트 배치 독트린 — 3단 높이 위계 + 밀도 역구배",font=FT,fill=TXT)
d.text((OX,76),"중심 문제 = 가림(매복·공개 드라마) ↔ 가독(호드를 봐야 함)의 충돌. 해법 = 높이로 역할을 나눈다.",font=FS,fill=DIM)
d.text((OX,102),"★몬스터 밀도는 중심으로 ↑, 오브젝트 밀도는 중심으로 ↓ (싸우는 곳을 트이게 = 뱀서 가독).",font=FS,fill=(150,156,164))

# ===== 측면 인셋 (3단 높이) =====
ix0,iy0,ix1,iy1=770,150,1240,560
d.rounded_rectangle([ix0,iy0,ix1,iy1],10,fill=(20,22,28),outline=(70,74,82),width=2)
d.text((ix0+16,iy0+12),"3단 높이 위계 (측면 — 45° 틸트 가림 해소)",font=FL,fill=GOLD)
gy=iy1-58
d.line([(ix0+24,gy),(ix1-24,gy)],fill=(96,92,82),width=3)
d.text((ix0+24,gy+8),"단일 전투 평면",font=FM,fill=DIM)
# 카메라 시선
ex,ey=ix0+44,gy-70
d.ellipse([ex-9,ey-9,ex+9,ey+9],fill=(235,235,240),outline=(20,20,24),width=2)
d.text((ex,ey-22),"45° 카메라",font=FM,fill=TXT,anchor="ma")
# 골격(키큰)
btx=ix1-110
for r,al in [(40,40),(24,80),(13,150)]: d.ellipse([btx-r,gy-120-r+50,btx+r,gy-120+r+50],fill=GLOW+(al,))
d.rectangle([btx-16,gy-120,btx+16,gy],fill=TALL,outline=TALL_E,width=2)
d.text((btx,gy+16),"골격 3m+\n(비콘·리딩라인·벽)",font=FM,fill=SUB,anchor="ma")
# 페그(중간)
pgx=ix0+250
d.rectangle([pgx-20,gy-34,pgx+20,gy],fill=PEG,outline=PEG_E,width=2)
d.text((pgx,gy+10),"페그 1~2m\n(위빙·엄폐)",font=FM,fill=SUB,anchor="ma")
# 드레싱(낮)
dsx=ix0+150
d.rectangle([dsx-16,gy-10,dsx+16,gy],fill=(48,52,60),outline=(90,94,102),width=1)
d.text((dsx,gy+10),"드레싱 <0.5m",font=FM,fill=DIM,anchor="ma")
# 시선: 골격은 뒤 가림(공개 드라마), 페그는 멀리 바닥 안 가림
d.line([(ex+8,ey-2),(btx-16,gy-122)],fill=(120,200,236,200),width=2)
d.text((ix0+24,iy0+44),"골격=가리고 '넘어가면 공개'(드라마) · 페그=낮아 멀리 바닥 안 가림(호드 가독) · 드레싱=무해",font=FM,fill=(196,200,206))

# ===== 5원칙 (하단) =====
by=710
d.line([(50,by),(W-50,by)],fill=(70,74,82),width=1)
d.text((50,by+12),"배치 5원칙",font=F(17,True),fill=GOLD)
rules=[("A · 밀도 ⟂ 전투 강도","크게 싸우는 곳(카이팅 링·코어)은 트이게. 클러터는 접근로·외곽에. (싸우는 데 엄폐 깔면 죽음·안 보임)"),
 ("B · 밀도 = 중심으로 ↓","외곽 빽빽 폐허(누비기·안전) → 코어 트인 학살 아레나. 몬스터 밀도와 반대. 디제틱: 코어=무너진/청소된 곳."),
 ("C · 오브젝트 = 리딩라인","건물·컨테이너 줄을 코어로 정렬 = 끌림 합성 그 자체. 배치가 곧 유도선."),
 ("D · 페그지 벽 아님","모든 군집 루프 가능·≥2 통로. 데스트랩0 + 한 방향 완전 차단 금지(적이 빙 돌아감=버그)."),
 ("E · 디제틱 스폰 가림막","오브젝트가 스폰 출처(뒤·문 솟음, 정면 팝인 0). 매복 포켓은 콘이 안 덮는 측·후방만.")]
cw=(W-100)//2
for i,(h,p) in enumerate(rules):
    col=i//3; row=i%3 if i<3 else i-3
    if i<3: x=50; y=by+44+i*54
    else: x=50+cw+20; y=by+44+(i-3)*54
    d.text((x,y),h,font=F(15,True),fill=TXT)
    # wrap
    words=p.split(" "); lines=[]; cur=""
    for w_ in words:
        t=(cur+" "+w_).strip()
        if d.textlength(t,font=FM)<=cw-10: cur=t
        else: lines.append(cur); cur=w_
    if cur: lines.append(cur)
    for li,ln in enumerate(lines[:2]): d.text((x,y+22+li*19),ln,font=FM,fill=(196,200,206))

d.text((W//2,8),"ZombieCrush — 오브젝트(건물 등) 배치 독트린 (탑뷰 뱀서류 농성)",font=F(15,True),fill=TXT,anchor="ma")
out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-object-placement.png")
img.save(out); print("SAVED:",out,img.size)
