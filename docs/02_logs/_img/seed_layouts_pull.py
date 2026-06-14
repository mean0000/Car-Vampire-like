# -*- coding: utf-8 -*-
# 수동 시드 v3 — LV 배치 + PULL 엔진 (왜 중앙으로 갈 수밖에 없는가)
import os, math
from PIL import Image, ImageDraw, ImageFont

S = 5; MAP = 100; MPX = 500
TITLE_H = 128; PW = 600; PH = TITLE_H + MPX + 20
BOT_H = 360
IMG_W = PW*3; IMG_H = PH + BOT_H

BG=(16,18,22); WALL=(80,73,64); BLOCK=(28,31,38); BLOCK_E=(92,88,80)
LOOP=(86,212,228); LANE=(240,222,120); LZC=(88,208,118)
HOT=(202,72,54); MID=(208,142,66); COOL=(42,56,80)
GOLD=(247,205,84); TXT=(228,230,234); DIM=(150,156,164)
RIM_C=(96,150,210); MID_C=(228,150,70); CORE_C=(228,86,70)

def font(sz,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,sz)
    except: return ImageFont.load_default()
F_T=font(25,True); F_S=font(16); F_L=font(13,True); F_LG=font(15); F_B=font(19,True); F_BL=font(14)

img=Image.new("RGB",(IMG_W,IMG_H),BG); d=ImageDraw.Draw(img,"RGBA")
def P(p,mx,my): return (p*PW+50+mx*S, TITLE_H+my*S)
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))

def heat(p,g=1.0):
    cx,cy=P(p,50,50); R=56*S
    for i in range(60):
        t=i/59; r=R*(1-t)
        col=lerp(COOL,MID,(t/.5)**g) if t<.5 else lerp(MID,HOT,((t-.5)/.5)**g)
        d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col)
def walls(p): d.rectangle([P(p,2,2),P(p,98,98)],outline=WALL,width=9)
def block(p,a,b,c,e): d.rectangle([P(p,a,b),P(p,c,e)],fill=BLOCK,outline=BLOCK_E,width=2)
def lane(p,a,b,c,e,w=5):
    d.line([P(p,a,b),P(p,c,e)],fill=LANE+(150,),width=w*S//2)
def dotted_circle(p,r,color):
    cx,cy=P(p,50,50); R=r*S
    for ad in range(0,360,7):
        a=math.radians(ad); x=cx+R*math.cos(a); y=cy+R*math.sin(a)
        d.ellipse([x-2,y-2,x+2,y+2],fill=color)
def arrowhead(px,py,ang,c=LOOP,L=11):
    dx,dy=math.cos(ang),math.sin(ang); tip=(px+L*dx,py+L*dy); bx,by=px-dx*4,py-dy*4
    d.polygon([tip,(bx-dy*7,by+dx*7),(bx+dy*7,by-dx*7)],fill=c)
def loop_e(p,cx,cy,rx,ry,c=LOOP,cw=True,arr=True):
    X,Y=P(p,cx,cy); RX,RY=rx*S,ry*S
    d.ellipse([X-RX,Y-RY,X+RX,Y+RY],outline=c+(235,),width=3)
    if arr:
        for k,(ax,ay) in enumerate([(X,Y-RY),(X+RX,Y),(X,Y+RY),(X-RX,Y)]):
            ang=[0,math.pi/2,math.pi,-math.pi/2][k]
            if not cw: ang+=math.pi
            arrowhead(ax,ay,ang,c)
def arc(p,r,a0,a1,c=WALL,w=9):
    X,Y=P(p,50,50); R=r*S; d.arc([X-R,Y-R,X+R,Y+R],a0,a1,fill=c,width=w)
def star(p,mx,my,rm,c=GOLD):
    X,Y=P(p,mx,my); R=rm*S; r=R*0.42; pts=[]
    for i in range(10):
        ang=math.radians(-90+i*36); rr=R if i%2==0 else r
        pts.append((X+rr*math.cos(ang),Y+rr*math.sin(ang)))
    d.polygon(pts,fill=c,outline=(60,45,10),width=2)
def lz(p,mx,my):
    X,Y=P(p,mx,my); d.ellipse([X-15,Y-15,X+15,Y+15],fill=LZC,outline=(10,12,10),width=2)
    d.text((X,Y),"퇴",font=F_L,fill=(8,28,12),anchor="mm"); d.text((X,Y-24),"추출 LZ",font=F_L,fill=LZC,anchor="mm")
def chip(p,mx,my,txt,col):
    X,Y=P(p,mx,my); w=d.textlength(txt,font=F_L)+14
    d.rounded_rectangle([X-w/2,Y-11,X+w/2,Y+11],6,fill=(18,20,26,235),outline=col,width=2)
    d.text((X,Y),txt,font=F_L,fill=col,anchor="mm")
def bands_overlay(p):
    dotted_circle(p,36,(230,230,238,160))   # 중간/외곽 경계
    dotted_circle(p,18,(255,235,180,200))   # 코어/중간 경계
    chip(p,16,15,"외곽 LV1",RIM_C)
    chip(p,30,30,"중간 LV2~3",MID_C)
    chip(p,50,68,"코어 LV4~5",CORE_C)
def core_pull(p):
    star(p,50,50,7)
    X,Y=P(p,50,50); d.text((X,Y+22),"할당량·돈·XP 잭팟",font=F_L,fill=GOLD,anchor="mm")
def title(p,t,sub):
    x0=p*PW+50; d.text((x0,28),t,font=F_T,fill=TXT); d.text((x0,64),sub,font=F_S,fill=DIM)
    d.text((x0,90),"외곽=숨고르기·정비  →  코어 급습(잭팟)  →  후퇴  →  재급습  (진자운동)",font=font(12),fill=(112,118,126))

# A 광장
p=0; heat(p); walls(p)
for x,y in [(24,24),(70,24),(24,70),(70,70)]: block(p,x,y,x+6,y+6)
lane(p,24,24,76,76); lane(p,24,76,76,24); loop_e(p,50,50,40,40)
bands_overlay(p); core_pull(p); lz(p,50,92)
title(p,"시드 A — 광장","링 카이팅 · 가장 열림 · 기준선")
# B 블록
p=1; heat(p); walls(p)
for a,b,c,e in [(18,18,40,40),(60,18,82,40),(18,60,40,82),(60,60,82,82)]: block(p,a,b,c,e)
lane(p,50,8,50,92); lane(p,8,50,92,50); loop_e(p,29,50,20,38,cw=False); loop_e(p,71,50,20,38)
bands_overlay(p); core_pull(p); lz(p,90,50)
title(p,"시드 B — 블록","8자 순환 · 테크니컬 카이팅")
# C 나선
p=2; heat(p,1.7); walls(p)
arc(p,38,35,325); arc(p,24,200,150)
loop_e(p,50,50,38,38); loop_e(p,50,50,24,24,arr=False)
for ad in [60,150,240,330]:
    a=math.radians(ad); X,Y=P(p,50,50); arrowhead(X+44*math.cos(a),Y+44*math.sin(a),a+math.pi,(234,120,90),13)
bands_overlay(p); core_pull(p); lz(p,50,8)
title(p,"시드 C — 나선","압축 카빙 · 공격적 · 최고 페이오프")

# ===== 하단: PULL 엔진 + LV 배치 + 범례 =====
by=PH+8
d.rectangle([30,by,IMG_W-30,by+1],fill=(70,74,82))
d.text((50,by+12),"왜 중앙으로 갈 수밖에 없는가 — PULL 엔진",font=F_B,fill=GOLD)
d.text((50,by+44),"맵(밀도)은 무대일 뿐. 끌림의 이유 = \"값어치가 위험한 곳에만 있다.\"  외곽 무한 카이팅이 '편한 함정'인 걸 세 힘이 깬다:",font=F_BL,fill=DIM)

cols=[("① 필요 (진척이 중앙에만)",
       ["할당량 타겟 = LV4~5 이상개체 = 코어에만 산다.",
        "외곽 LV1 fodder는 죽여도 진척 미미.",
        "→ 일을 끝내려면 코어로 들어가야 함."]),
      ("② 욕심 (돈·파워가 중앙에만)",
       ["화려함=돈(고위협·화려처형=등급 S=현금) 집중.",
        "인-런 XP 잭팟=레벨업도 코어 집중.",
        "→ 외곽서 버티면 가난·약체로 늙는다."]),
      ("③ 압박 (외곽 안전은 일시적)",
       ["무한 스폰 + 끌수록 강해짐(escalation).",
        "안전 외곽이 점점 수축(hot이 바깥 번짐).",
        "→ 일찍 털어 채우는 게 이득, 미루면 전멸."])]
cw=(IMG_W-100)//3
for i,(h,lines) in enumerate(cols):
    cx=50+i*cw
    d.text((cx,by+78),h,font=font(16,True),fill=TXT)
    for j,ln in enumerate(lines):
        d.text((cx,by+108+j*26),ln,font=F_BL,fill=(196,200,206))

# LV 배치 행
ly=by+200
d.text((50,ly),"LV 배치 (동심원 = LV 그라디언트, 셋 다 공통):",font=font(16,True),fill=TXT)
lvrows=[("외곽 LV1",RIM_C,"Lacercharias 산발 · Venodonte 산성 견제  (숨고르기·소액 XP)"),
        ("중간 LV2~3",MID_C,"Caniathrox 돌진떼 · Kupolojuve 비행 · Dimaxillosaurus 클로  (카이팅 본전장)"),
        ("코어 LV4~5",CORE_C,"Fulgurodonte 램 엘리트+Venosaur 호위 · Crustaspikan 유생(보스 예고)  (할당량·돈·XP)")]
for j,(name,col,desc) in enumerate(lvrows):
    yy=ly+30+j*30
    d.rectangle([50,yy,76,yy+20],fill=col,outline=(40,40,46)); d.text((58,yy+10),"",anchor="lm")
    d.text((86,yy+10),name,font=font(14,True),fill=col,anchor="lm")
    d.text((200,yy+10),desc,font=F_BL,fill=(206,210,216),anchor="lm")

# 범례 (우측)
lx=IMG_W-560; ry=ly
d.text((lx,ry),"기호:",font=font(15,True),fill=TXT)
yy=ry+30
d.ellipse([lx,yy,lx+22,yy+14],outline=LOOP,width=3); d.text((lx+32,yy+7),"카이팅 루프",font=F_BL,fill=TXT,anchor="lm")
d.rectangle([lx+150,yy,lx+176,yy+14],fill=LANE); d.text((lx+184,yy+7),"카빙 레인",font=F_BL,fill=TXT,anchor="lm")
# star legend
sx=lx+300; sy=yy+7; R=10; r=4; pts=[]
for i in range(10):
    ang=math.radians(-90+i*36); rr=R if i%2==0 else r; pts.append((sx+rr*math.cos(ang),sy+rr*math.sin(ang)))
d.polygon(pts,fill=GOLD,outline=(60,45,10)); d.text((sx+18,sy),"코어 잭팟",font=F_BL,fill=TXT,anchor="lm")
yy2=yy+30
d.ellipse([lx,yy2,lx+18,yy2+18],fill=LZC); d.text((lx+26,yy2+9),"단일 추출 LZ(할당량 채우면 점등)",font=F_BL,fill=TXT,anchor="lm")
d.rectangle([lx+300,yy2+2,lx+322,yy2+16],fill=BLOCK,outline=BLOCK_E); d.text((lx+330,yy2+9),"건물/엄폐",font=F_BL,fill=TXT,anchor="lm")

d.text((IMG_W//2,6),"ZombieCrush — 수동 시드 v3: LV 배치 + PULL 엔진 (서바이버즈 농성·할당량+무한스폰+15분+)",font=font(15,True),fill=TXT,anchor="ma")

out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-seed-layouts-v3.png")
img.save(out); print("SAVED:",out,img.size)
