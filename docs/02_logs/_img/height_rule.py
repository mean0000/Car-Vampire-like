# -*- coding: utf-8 -*-
# 높이 규칙 (측면도) — 단일 전투 평면 ✓ / 옥상 저격·층 등반 ❌
import os, math
from PIL import Image, ImageDraw, ImageFont
W,H=1440,760
BG=(15,16,20)
GROUND=(96,92,82); OKC=(110,210,140); NOC=(228,90,80)
GOLD=(250,206,90); GLOW=(255,196,86); CONE=(120,200,236); MON=(190,120,228)
TXT=(230,232,236); DIM=(150,156,164); STEEL=(150,150,158)
def F(s,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,s)
    except: return ImageFont.load_default()
FT=F(24,True); FS=F(15); FL=F(15,True); FM=F(13)
img=Image.new("RGB",(W,H),BG); d=ImageDraw.Draw(img,"RGBA")
def fig(x,gy,col,h=34,label=None):
    d.ellipse([x-8,gy-h-8,x+8,gy-h+8],fill=col,outline=(15,15,18),width=2)
    d.line([(x,gy-h+6),(x,gy-8)],fill=col,width=4)
    if label: d.text((x,gy+12),label,font=FM,fill=col,anchor="ma")
def star(x,y,R,c=GOLD):
    r=R*0.42; pts=[]
    for i in range(10):
        a=math.radians(-90+i*36); rr=R if i%2==0 else r; pts.append((x+rr*math.cos(a),y+rr*math.sin(a)))
    d.polygon(pts,fill=c,outline=(60,45,10),width=2)

GY=560
# 분할선
d.line([(940,90),(940,690)],fill=(70,74,82),width=2)

# ===== ✓ 허용 =====
d.text((50,40),"✓ 우리가 할 수 있는 것 — 단일 전투 평면",font=FT,fill=OKC)
d.line([(50,GY),(900,GY)],fill=GROUND,width=5)
d.text((50,GY+34),"단일 전투 평면 (모든 적·플레이어·조준이 이 한 면 위)",font=FL,fill=TXT)
# 플레이어
fig(150,GY,(235,235,240),label="플레이어")
# 엄폐 사격 적(지상)
d.rectangle([300,GY-46,360,GY],fill=(40,43,50),outline=(82,82,88),width=2)  # 컨테이너=엄폐
fig(330,GY,MON)
for sx in range(180,300,22):
    d.line([(sx,GY-22),(sx+12,GY-22)],fill=(232,120,90),width=3)
d.text((330,GY-66),"엄폐 사격(지상)",font=FM,fill=MON,anchor="ma")
# 비행체 = 내려와서 침
fx,fy=540,GY-150
d.line([(fx-12,fy),(fx,fy+10),(fx+12,fy)],fill=MON,width=3)  # 날개
d.line([(fx,fy+10),(fx+30,GY-6)],fill=CONE+(200,),width=3)   # 다이브
d.polygon([(fx+30,GY-6),(fx+20,GY-22),(fx+38,GY-20)],fill=CONE)
d.ellipse([fx+10,GY-10,fx+90,GY+6],outline=(232,120,90,200),width=3)  # 평면 텔레그래프
d.text((fx+4,fy-22),"비행 = 내려와 치거나 평면에 투사(텔레그래프)",font=FM,fill=CONE,anchor="la")
# 코어 비콘 = 키 큼(끌림) / 보상은 발밑
bx=770
for r,al in [(64,40),(40,80),(22,150)]:
    d.ellipse([bx-r,GY-150-r+30,bx+r,GY-150+r+30],fill=GLOW+(al,))
d.line([(bx,GY),(bx,GY-180)],fill=(150,150,158),width=5)        # 마스트
d.line([(bx-26,GY-180),(bx+16,GY-180)],fill=(150,150,158),width=4)
d.ellipse([bx-14,GY-210,bx+14,GY-182],fill=MON,outline=(40,20,55),width=2)
star(bx,GY-14,15)                                               # 보상=발밑
d.text((bx,GY+12),"비콘=높이(끌림)\n보상·코어 엘리트=발밑(평면)",font=FM,fill=GOLD,anchor="ma")
d.line([(bx+30,GY-120),(bx+30,GY-30)],fill=(250,250,250,120),width=1)
d.text((bx+40,GY-90),"높이는 실루엣/끌림용\n전투는 발밑에서",font=FM,fill=(200,204,210))

# ===== ❌ 함정 =====
d.text((965,40),"❌ 우리가 못 하는 것 (매몰 함정)",font=FT,fill=NOC)
d.line([(965,GY),(1390,GY)],fill=GROUND,width=5)
# 옥상 저격
d.rectangle([1010,GY-150,1090,GY],fill=(38,41,48),outline=(82,82,88),width=2)
fig(1050,GY-150,NOC,h=28)
fig(1180,GY,(235,235,240))
d.line([(1058,GY-168),(1172,GY-30)],fill=(232,120,90),width=3)
d.text((1050,GY-200),"옥상 저격수",font=FM,fill=NOC,anchor="ma")
# 큰 X
d.line([(1010,GY-185),(1200,GY-15)],fill=NOC+(230,),width=5); d.line([(1200,GY-185),(1010,GY-15)],fill=NOC+(230,),width=5)
d.text((1105,GY+34),"근접 닿지 않음 · 평면 조준 불가\n수용성↓(내 페이스로 못 받아침)",font=FM,fill=NOC,anchor="ma")
# 층 등반
bx2=1320
d.rectangle([bx2-44,GY-200,bx2+44,GY],fill=(34,37,44),outline=(82,82,88),width=2)
for fy2 in [GY-50,GY-100,GY-150]:
    d.line([(bx2-44,fy2),(bx2+44,fy2)],fill=(82,82,88),width=2)
d.line([(bx2,GY-10),(bx2,GY-185)],fill=(120,200,236),width=3)
d.polygon([(bx2,GY-185),(bx2-9,GY-168),(bx2+9,GY-168)],fill=(120,200,236))
d.line([(bx2-50,GY-205),(bx2+50,GY+5)],fill=NOC+(230,),width=5); d.line([(bx2+50,GY-205),(bx2-50,GY+5)],fill=NOC+(230,),width=5)
d.text((bx2,GY+34),"층 등반",font=FM,fill=NOC,anchor="ma")
d.text((bx2,GY+58),"다른 장르(3D 액션)\n멀티 navmesh=솔로 매몰",font=FM,fill=NOC,anchor="ma")

# 헤더 + 하단 규칙
d.text((W//2,8),"ZombieCrush — 높이 규칙 (탑뷰): 단일 전투 평면 · 높이=가독성/비행 전용",font=F(16,True),fill=TXT,anchor="ma")
d.text((50,700),"규칙: ①모든 전투는 한 지상 평면. ②높이는 비콘 실루엣/끌림·오클루전용(올라가지 않음, 보상은 발밑). ③비행체만 예외 — 평면으로 내려와 치거나 평면에 텔레그래프 투사. ④얕은 단차/경사(나선 보울)는 OK(한 navmesh).",font=FM,fill=(200,204,210))
out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-height-rule.png")
img.save(out); print("SAVED:",out,img.size)
