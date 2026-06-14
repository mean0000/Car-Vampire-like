# -*- coding: utf-8 -*-
# 맵 척도 + 구심 전진 구조 — 150×150 기준, 카메라 가시범위 대비, 3밴드 카이팅 루프
import os, math
from PIL import Image, ImageDraw, ImageFont
W,H=1180,1000
BG=(15,16,20)
HOT=(150,58,46); MID=(150,104,52); COOL=(36,52,74)
GOLD=(250,206,90); GLOW=(255,196,86); LOOP=(86,212,228)
CAM=(120,210,150); TXT=(230,232,236); DIM=(150,156,164); ARR=(240,150,90)
RIM_C=(96,150,210); MID_C=(228,150,70); CORE_C=(228,86,70)
def F(s,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,s)
    except: return ImageFont.load_default()
FT=F(24,True); FS=F(15); FL=F(14,True); FM=F(13)
img=Image.new("RGB",(W,H),BG); d=ImageDraw.Draw(img,"RGBA")
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
MAP=150; S=4.6; OX=60; OY=150
def P(mx,my): return (OX+mx*S, OY+my*S)  # mx,my in meters, 0..150
cx,cy=P(75,75)
# heat (radius 75)
R=75*S
for k in range(60):
    t=k/59; r=R*(1-t)
    col=lerp(COOL,MID,(t/.5)) if t<.5 else lerp(MID,HOT,((t-.5)/.5))
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col)
# band boundary rings (radii from center, meters)
def ring(rm,col,w=2,dash=True):
    rr=rm*S
    if dash:
        for ad in range(0,360,5):
            a=math.radians(ad); x=cx+rr*math.cos(a); y=cy+rr*math.sin(a)
            d.ellipse([x-2,y-2,x+2,y+2],fill=col)
    else:
        d.ellipse([cx-rr,cy-rr,cx+rr,cy+rr],outline=col,width=w)
ring(74,(78,72,64),3,dash=False)  # 봉쇄벽
ring(50,(230,230,238,150)); ring(22,(255,235,180,200))
# kiting loops
def kloop(rm,lbl):
    rr=rm*S; d.ellipse([cx-rr,cy-rr,cx+rr,cy+rr],outline=LOOP+(180,),width=2)
kloop(62,"림 루프"); kloop(36,"중간 루프"); kloop(13,"코어")
# centripetal advance arrows (rim -> core)
for ang in [25,150,265]:
    a=math.radians(ang)
    for rr in [60,42,26]:
        x=cx+rr*S*math.cos(a); y=cy+rr*S*math.sin(a)
        dx,dy=-math.cos(a),-math.sin(a)
        d.polygon([(x+dx*10,y+dy*10),(x-dy*6,y+dx*6),(x+dy*6,y-dx*6)],fill=ARR+(220,))
# beacon
for r,al in [(13*S,45),(8*S,80),(4*S,170)]:
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=GLOW+(al,))
sx,sy=cx,cy; RR=5*S; rr=RR*0.42; pts=[]
for i in range(10):
    ang=math.radians(-90+i*36); rad=RR if i%2==0 else rr; pts.append((sx+rad*math.cos(ang),sy+rad*math.sin(ang)))
d.polygon(pts,fill=GOLD,outline=(60,45,10),width=2)
d.text((cx,cy+7*S),"코어 비콘/잭팟",font=FL,fill=GOLD,anchor="mm")
# 카메라 가시범위 (플레이어 림 위치)
ppx,ppy=P(75,137)
d.ellipse([ppx-6,ppy-6,ppx+6,ppy+6],fill=(235,235,240),outline=(20,20,24),width=2)
camR=16*S
d.ellipse([ppx-camR,ppy-camR,ppx+camR,ppy+camR],outline=CAM+(230,),width=3)
d.text((ppx,ppy+camR+14),"카메라 가시범위 ≈ Ø30m\n(코어까지 약 3카메라)",font=FM,fill=CAM,anchor="ma")
# band LV labels
def lab(mx,my,t,c):
    X,Y=P(mx,my); w=d.textlength(t,font=FL)+12
    d.rounded_rectangle([X-w/2,Y-11,X+w/2,Y+11],6,fill=(18,20,26,235),outline=c,width=2)
    d.text((X,Y),t,font=FL,fill=c,anchor="mm")
lab(75,12,"외곽 LV1 · 파밍/안전 (r55~74)",RIM_C)
lab(40,42,"중간 LV2~3 · 본전장 (r22~50)",MID_C)
lab(75,62,"코어 LV4~5 · 잭팟 (r0~22)",CORE_C)
# 치수선 150m
d.line([P(2,148),P(148,148)],fill=(150,156,164),width=1)
d.text(((P(2,148)[0]+P(148,148)[0])//2,P(148,148)[1]+10),"150 m",font=FL,fill=TXT,anchor="ma")
d.line([P(150,2),P(150,148)],fill=(150,156,164),width=1)
# 옛 100×100 비교(점선 박스)
o0=P(25,25); o1=P(125,125)
for x in range(int(o0[0]),int(o1[0]),10): d.line([(x,o0[1]),(x+5,o0[1])],fill=(120,120,128),width=1); d.line([(x,o1[1]),(x+5,o1[1])],fill=(120,120,128),width=1)
for y in range(int(o0[1]),int(o1[1]),10): d.line([(o0[0],y),(o0[0],y+5)],fill=(120,120,128),width=1); d.line([(o1[0],y),(o1[0],y+5)],fill=(120,120,128),width=1)
d.text((o0[0]+6,o0[1]+6),"옛 100×100\n(구심 전진엔 작음)",font=FM,fill=(150,150,158))

# 타이틀 + 우측 설명
d.text((OX,40),"맵 척도 + 구심 전진 구조 — 기준 150×150 m",font=FT,fill=TXT)
d.text((OX,78),"외곽서 파밍·레벨업 → 강해지면 프런티어를 안으로 밀어 코어로 전진. 시간 escalation이 외곽까지 데움 = 안으로 짜내짐.",font=FS,fill=DIM)
d.text((OX,104),"동심원 3밴드 각각에 카이팅 루프가 들어갈 크기. 코어=림에서 ~3카메라 거리라 비콘이 '멀리서 솟아' 보이고 릴레이 공개가 단계로 작동.",font=FS,fill=(150,156,164))

# 우측 패널 (텍스트 요약)
px=OX+760; py=180
d.text((px,py),"왜 150 인가",font=F(16,True),fill=GOLD);
rows=["• 카메라 Ø30m → 150은 ~5카메라 폭",
      "• 코어가 림에서 ~3카메라 = '여정' 성립",
      "• 100이면 코어가 늘 화면 안 → 릴레이",
      "   공개·비콘 솟음이 무의미(반려 사유)",
      "• 밴드3 각각 카이팅 루프 들어감",
      "• 너무 크면 호드 흩어짐 → 밀도",
      "   그라디언트가 '항상 스웜' 유지",
      "• 솔로 비용 = 모듈 조립으로 흡수"]
for j,t in enumerate(rows): d.text((px,py+34+j*26),t,font=FM,fill=(200,204,210))
py2=py+34+len(rows)*26+24
d.text((px,py2),"난이도 2축 (인-런 레벨업이 경주)",font=F(16,True),fill=GOLD)
rows2=["① 공간축 — 안으로 갈수록 강함(LV4~5)",
       "② 시간축 — escalation(능력·밀도·변이)",
       "→ 레벨업으로 둘 다 따라잡으며 전진",
       "→ 못 따라잡으면 외곽도 치명=짜내짐"]
for j,t in enumerate(rows2): d.text((px,py2+34+j*26),t,font=FM,fill=(200,204,210))
out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-map-scale.png")
img.save(out); print("SAVED:",out,img.size)
