# -*- coding: utf-8 -*-
# 맵 컨셉 4종 — 자연스러운 끌림 합성 비교 (lv 공간 + st 디제틱 융합)
import os, math
from PIL import Image, ImageDraw, ImageFont

S=5.6; HEADER=44; TITLE=78; MAPpx=560
PW=660; PH=TITLE+MAPpx+44
IMG_W=PW*2; IMG_H=HEADER+PH*2+120
BG=(15,16,20)
HOT=(150,58,46); MID=(150,104,52); COOL=(34,46,64)
GOLD=(250,206,90); GLOW=(255,196,86); BEACON=(255,150,52)
LANE=(238,206,120); LOOP=(86,212,228); MON=(190,120,228)
GREENC=(120,230,170); LZC=(88,208,118); WALL=(78,72,64); BLOCK=(30,33,40); BLOCK_E=(90,86,80)
TXT=(230,232,236); DIM=(150,156,164)
def F(s,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,s)
    except: return ImageFont.load_default()
FT=F(22,True); FS=F(14); FL=F(12,True); FC=F(12)
img=Image.new("RGB",(IMG_W,IMG_H),BG); d=ImageDraw.Draw(img,"RGBA")
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))
def org(i):
    col=i%2; row=i//2
    return col*PW+50, HEADER+row*PH+TITLE
def P(i,mx,my):
    ox,oy=org(i); return (ox+mx*S, oy+my*S)
def heat(i,g=1.0):
    cx,cy=P(i,50,50); R=58*S
    for k in range(50):
        t=k/49; r=R*(1-t)
        col=lerp(COOL,MID,(t/.5)**g) if t<.5 else lerp(MID,HOT,((t-.5)/.5)**g)
        d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col)
def walls(i): d.rectangle([P(i,2,2),P(i,98,98)],outline=WALL,width=7)
def lead(i,ang,r0=46,r1=8,broken=False):
    a=math.radians(ang); cx,cy=P(i,50,50)
    x0,y0=cx+r0*S*math.cos(a),cy+r0*S*math.sin(a); x1,y1=cx+r1*S*math.cos(a),cy+r1*S*math.sin(a)
    if broken:
        for seg in [(0.0,0.35),(0.5,0.8)]:
            sx=x0+(x1-x0)*seg[0]; sy=y0+(y1-y0)*seg[0]; ex=x0+(x1-x0)*seg[1]; ey=y0+(y1-y0)*seg[1]
            d.line([(sx,sy),(ex,ey)],fill=LANE+(150,),width=6)
    else:
        d.line([(x0,y0),(x1,y1)],fill=LANE+(45,),width=10); d.line([(x0,y0),(x1,y1)],fill=LANE+(150,),width=5)
def loop(i,cx,cy,rx,ry,c=LOOP,cw=True):
    X,Y=P(i,cx,cy); d.ellipse([X-rx*S,Y-ry*S,X+rx*S,Y+ry*S],outline=c+(170,),width=2)
def glowball(i,mx,my,rm):
    X,Y=P(i,mx,my)
    for r,al in [(rm*S,45),(rm*0.6*S,80),(rm*0.32*S,160)]:
        d.ellipse([X-r,Y-r,X+r,Y+r],fill=GLOW+(al,))
def monhead(i,mx,my,s=3.2):
    X,Y=P(i,mx,my); d.ellipse([X-s*S,Y-s*S,X+s*S,Y+s*S],fill=MON,outline=(40,20,55),width=2)
def chevron(i,mx,my,s=1.4):
    X,Y=P(i,mx,my); d.line([(X-s*S,Y),(X,Y+s*S),(X+s*S,Y)],fill=MON,width=2)
def block(i,a,b,c,e): d.rectangle([P(i,a,b),P(i,c,e)],fill=BLOCK,outline=BLOCK_E,width=1)
def beat(i,mx,my,col,lbl,vis=True):
    X,Y=P(i,mx,my); r=8
    cc=col if vis else tuple(v//2 for v in col)
    d.ellipse([X-r,Y-r,X+r,Y+r],fill=cc,outline=(14,14,18),width=2)
    d.text((X+13,Y),lbl,font=FL,fill=col+(255 if vis else 150,),anchor="lm")
def lz(i,mx,my):
    X,Y=P(i,mx,my); d.ellipse([X-13,Y-13,X+13,Y+13],fill=LZC,outline=(8,10,8),width=2)
    d.text((X,Y),"퇴",font=FL,fill=(8,28,12),anchor="mm"); d.text((X,Y-22),"LZ",font=FL,fill=LZC,anchor="mm")
def chips(i):
    for mx,my,t,c in [(15,14,"외곽 LV1",(96,150,210)),(30,30,"LV2~3",(228,150,70)),(50,67,"코어 LV4~5",(228,86,70))]:
        X,Y=P(i,mx,my); w=d.textlength(t,font=FL)+12
        d.rounded_rectangle([X-w/2,Y-10,X+w/2,Y+10],5,fill=(18,20,26,230),outline=c,width=2)
        d.text((X,Y),t,font=FL,fill=c,anchor="mm")
def title(i,bad,gong,sub):
    ox,oy=org(i)
    d.text((ox,oy-TITLE+10),bad,font=FT,fill=GOLD)
    d.text((ox+d.textlength(bad,font=FT)+12,oy-TITLE+16),f"· {gong}",font=FS,fill=DIM)
    d.text((ox,oy-TITLE+44),sub,font=FS,fill=(196,200,206))

# ===== 0 입체교차로 「고가 잔무」 (블록·8자) =====
i=0; heat(i); walls(i)
for a,b,c,e in [(16,16,38,38),(62,16,84,38),(16,62,38,84),(62,62,84,84)]: block(i,a,b,c,e)
for ang in [45,135,225,315]: lead(i,ang,broken=True)        # 끊긴 도로 4
loop(i,30,50,18,36,cw=False); loop(i,70,50,18,36)            # 8자
glowball(i,50,50,16)                                          # 오염 웅덩이
d.line([P(i,40,40),P(i,60,60)],fill=BEACON,width=10); d.line([P(i,40,60),P(i,60,40)],fill=BEACON,width=10)  # 무너진 X 고가
monhead(i,50,50,4)                                            # 램 엘리트
beat(i,72,72,GREENC,"① 캐시"); beat(i,60,60,MON,"② 램 윤곽"); beat(i,50,50,GOLD,"③ 잭팟",False)
chips(i); lz(i,92,50)
title(i,"고가 잔무","제2순환 입체교차로","무너진 X 고가에 박힌 램 엘리트 · 블록 가림→드러냄")

# ===== 1 집하장 「집하 중단」 (회랑·코너 왈칵) ★추천 =====
i=1; heat(i); walls(i)
for r in range(22,80,16):                                     # 컨테이너 행 = 격자 회랑
    block(i,r,18,r+8,40); block(i,r,60,r+8,82)
for ang in [0,90,180,270]: lead(i,ang)                        # 회랑 수렴
loop(i,50,50,40,40)
# 크레인 비콘 (키 15m+)
cx,cy=P(i,50,50); d.line([(cx,cy),(cx,cy-22*S)],fill=(150,150,158),width=5)      # 마스트
d.line([(cx-12*S,cy-22*S),(cx+8*S,cy-22*S)],fill=(150,150,158),width=4)          # 지브
glowball(i,50,28,7)                                                               # 보급 비콘등
monhead(i,50,50,3.2)
beat(i,86,40,GREENC,"① 캐시"); beat(i,66,46,MON,"② 코너 왈칵!"); beat(i,50,50,GOLD,"③ 보급 잭팟",False)
chips(i); lz(i,50,92)
title(i,"집하 중단 ★","제7보급거점 운영중단","크레인 15m 보급등 · 회랑 코너 돌면 야드 왈칵(최강)")

# ===== 2 분화구 「발원 격리」 (나선·점층) =====
i=2; heat(i,1.7); walls(i)
cx,cy=P(i,50,50)
for r,al in [(30,90),(22,150),(14,210),(7,255)]:             # 함몰 동심 글로우
    rr=r*S; col=lerp((40,40,52),GLOW,al/255); d.ellipse([cx-rr,cy-rr,cx+rr,cy+rr],outline=col+(al,),width=4)
for ang in range(0,360,45): lead(i,ang,r0=44,r1=14)
# 나선 호
for r,a0,a1 in [(38,30,300),(26,200,140)]:
    rr=r*S; d.arc([cx-rr,cy-rr,cx+rr,cy+rr],a0,a1,fill=LOOP+(170,),width=2)
for mx,my in [(50,44),(46,54),(55,52),(50,57)]: chevron(i,mx,my)   # 유생
beat(i,68,68,GREENC,"① 캐시"); beat(i,58,58,MON,"② 유생 꿈틀"); beat(i,50,50,GOLD,"③ 발원 코어",False)
chips(i); lz(i,8,50)
title(i,"발원 격리","발생원 미상 함몰지","발광 싱크홀+유생 · 나선 점층(궁금증 최대) · 이름이 거짓말")

# ===== 3 분수광장 「광장 집회 해산」 (링·방사·공중 비콘) =====
i=3; heat(i); walls(i)
for ang in range(0,360,45): lead(i,ang)                       # 방사 직선
loop(i,50,50,40,40)
cx,cy=P(i,50,50)
d.rectangle([cx-4*S,cy-2*S,cx+4*S,cy+4*S],fill=BLOCK,outline=BLOCK_E,width=1)   # 단상(프록시)
glowball(i,50,48,6)
for dx,dy in [(-5,-9),(0,-12),(5,-9),(-8,-6),(8,-6),(0,-6),(-3,-9),(3,-9)]:     # 공중 군집=비콘
    chevron(i,50+dx,42+dy,1.2)
for mx,my in [(30,30),(70,30),(30,70),(70,70)]: beat(i,mx,my,GREENC,"")          # 반짝이 캐시 산포
beat(i,50,82,GREENC,"① 캐시"); beat(i,50,64,MON,"② 군집 선회"); beat(i,50,50,GOLD,"③ 광역 학살",False)
chips(i); lz(i,50,8)
title(i,"광장 집회 해산","중앙광장 집단발생","공중 군집=비콘(나는 윤곽) · 방사 정조준 기준선 · 광역 뽕")

# 헤더 + 범례
d.text((IMG_W//2,8),"ZombieCrush — 자연스러운 끌림 맵 컨셉 4종 (LevelDesign 공간 + Story 디제틱 융합 · 폐허 도심 1차 바이옴)",font=F(16,True),fill=TXT,anchor="ma")
ly=IMG_H-96
d.text((50,ly),"공통 불변식: 비콘 상시중력(틸트) + 리딩라인 수렴 + 콘 릴레이 공개(①캐시→②윤곽→③잭팟) + 데스트랩0 + 단일 LZ + 동심원 LV. 시드차이 = 공개 드라마.",font=FS,fill=DIM)
items=[(GLOW,"비콘 글로우(상시)"),(LANE,"리딩 라인"),(LOOP,"카이팅 루프"),(MON,"괴수/엘리트 윤곽"),(GREENC,"글린트 캐시"),(GOLD,"코어 잭팟"),(LZC,"추출 LZ")]
x=50; yy=ly+34
for c,t in items:
    d.rectangle([x,yy,x+22,yy+15],fill=c,outline=(80,80,86)); d.text((x+30,yy+7),t,font=FC,fill=TXT,anchor="lm")
    x+=30+d.textlength(t,font=FC)+34

out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-map-concepts-4.png")
img.save(out); print("SAVED:",out,img.size)
