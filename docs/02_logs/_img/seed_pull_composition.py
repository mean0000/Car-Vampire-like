# -*- coding: utf-8 -*-
# 끌림 합성(자연스러운 PULL) — 비콘 + 리딩라인 + 콘 릴레이 공개 + 45° 틸트 인셋
import os, math
from PIL import Image, ImageDraw, ImageFont

W,H=1440,980
BG=(15,16,20)
HOT=(150,58,46); MID=(150,104,52); COOL=(34,46,64)
GOLD=(250,206,90); GLOW=(255,196,86); BEACON=(255,150,52)
LANE=(238,206,120); CONE=(118,200,236); LOOP=(86,212,228)
TXT=(230,232,236); DIM=(150,156,164); MON=(186,120,228)
def F(s,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,s)
    except: return ImageFont.load_default()
FT=F(26,True); FS=F(16); FL=F(14,True); FM=F(14); FB=F(18,True)
img=Image.new("RGB",(W,H),BG); d=ImageDraw.Draw(img,"RGBA")
def lerp(a,b,t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))

# ---------- 메인 탑다운 ----------
S=7.0; OX,OY=40,130
def P(mx,my): return (OX+mx*S, OY+my*S)
cx,cy=P(50,50)
# dim heat base
R=58*S
for i in range(56):
    t=i/55; r=R*(1-t)
    col=lerp(COOL,MID,(t/.5)) if t<.5 else lerp(MID,HOT,((t-.5)/.5))
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=col)
d.rectangle([P(2,2),P(98,98)],outline=(78,72,64),width=8)

# 리딩 라인 (코어로 수렴) — 굵은 발광선
for ang in range(0,360,45):
    a=math.radians(ang); ex,ey=P(50+46*math.cos(a),50+46*math.sin(a))
    for w,al in [(11,40),(5,120)]:
        d.line([(ex,ey),(cx,cy)],fill=LANE+(al,),width=w)

# 외곽 카이팅 링
d.ellipse([cx-40*S,cy-40*S,cx+40*S,cy+40*S],outline=LOOP+(150,),width=2)

# 코어 비콘 글로우 (상시 보임) — 큰 헤일로
for r,al in [(20*S,40),(13*S,70),(7*S,150)]:
    d.ellipse([cx-r,cy-r,cx+r,cy+r],fill=GLOW+(al,))
# 비콘 첨탑 + 괴수 윤곽
d.polygon([(cx,cy-9*S),(cx+5*S,cy+4*S),(cx-5*S,cy+4*S)],fill=BEACON,outline=(60,30,8),width=2)
d.ellipse([cx-3*S,cy-12*S,cx+3*S,cy-6*S],fill=MON,outline=(40,20,55),width=2)  # 괴수 머리
d.text((cx,cy+7*S),"코어 비콘",font=FL,fill=GOLD,anchor="mm")
d.text((cx,cy+8.6*S),"발광 첨탑+괴수 = 최대 뽕 약속",font=F(12),fill=GOLD,anchor="mm")

# 플레이어 + 시야 콘 (남측 림에서 안쪽 봄)
ppx,ppy=P(50,90)
d.ellipse([ppx-7,ppy-7,ppx+7,ppy+7],fill=(235,235,240),outline=(20,20,24),width=2)
d.text((ppx,ppy+18),"플레이어",font=F(12),fill=TXT,anchor="mm")
# cone wedge pointing up (-y)
half=math.radians(34); rad=30*S; pts=[(ppx,ppy)]
a0=-math.pi/2-half; a1=-math.pi/2+half
for k in range(25):
    a=a0+(a1-a0)*k/24; pts.append((ppx+rad*math.cos(a),ppy+rad*math.sin(a)))
d.polygon(pts,fill=CONE+(46,))
d.line([pts[1],(ppx,ppy)],fill=CONE+(120,));

# 릴레이 미끼 3비트 (남측 리딩라인 따라)
def beat(mx,my,col,lbl,sub,vis):
    X,Y=P(mx,my); r=10
    d.ellipse([X-r,Y-r,X+r,Y+r],fill=col if vis else (col[0]//2,col[1]//2,col[2]//2),outline=(15,15,18),width=2)
    a=255 if vis else 130
    d.text((X+16,Y-6),lbl,font=FL,fill=col+(a,),anchor="lm")
    d.text((X+16,Y+10),sub,font=F(12),fill=DIM+(a,),anchor="lm")
    return (X,Y)
b1=beat(50,79,(120,230,170),"① 글린트 캐시","지금 콘에 보임",True)
b2=beat(50,64,MON,"② 엘리트 윤곽","콘 가장자리(궁금)",True)
b3=beat(50,50.5,GOLD,"③ 코어 잭팟","아직 디테일 가림",False)
# 릴레이 화살표
def relay(a,b,t):
    d.line([a,b],fill=(245,235,170,150),width=2)
    mx=(a[0]+b[0])/2; my=(a[1]+b[1])/2
    d.text((mx+12,my),t,font=F(11),fill=(245,235,170),anchor="lm")
relay(b1,b2,"가면 ②가 또렷")
relay(b2,b3,"가면 ③ 공개")

d.text((OX,OY-44),"끌림 합성 — 시드 A 광장 (탑다운): 비콘 상시중력 + 리딩라인 + 콘 릴레이 공개",font=FT,fill=TXT)
d.text((OX,OY-12),"플레이어는 '할당량'이 아니라 '저 안의 뽕'을 보고 들어간다. 들어가면 콘이 다음 미끼를 차례로 깐다(젤다 사선 공개).",font=FS,fill=DIM)

# ---------- 45° 틸트 인셋 ----------
ix0,iy0,ix1,iy1=820,150,1410,520
d.rounded_rectangle([ix0,iy0,ix1,iy1],10,fill=(20,22,28),outline=(70,74,82),width=2)
d.text((ix0+18,iy0+12),"45° 틸트 측면 — 왜 비콘이 멀리서도 보이나",font=FB,fill=GOLD)
gy=iy1-70  # ground line
d.line([(ix0+30,gy),(ix1-30,gy)],fill=(90,86,80),width=2)
# 카메라/눈 (좌=플레이어 림)
ex,ey=ix0+60,gy-40
d.ellipse([ex-10,ey-10,ex+10,ey+10],fill=(235,235,240),outline=(20,20,24),width=2)
d.text((ex,ey+24),"플레이어\n(림)",font=F(12),fill=TXT,anchor="ma")
# 림 저클러터
for k,bx in enumerate([ix0+150,ix0+200]):
    d.rectangle([bx,gy-26,bx+34,gy],fill=(40,43,50),outline=(80,80,86))
# 미드 클러터(중간 높이)
for bx in [ix0+300,ix0+360,ix0+420]:
    d.rectangle([bx,gy-58,bx+40,gy],fill=(34,37,44),outline=(80,80,86))
# 코어 비콘 첨탑(키 큼, 우)
btx=ix1-130
for r,al in [(70,40),(46,80),(26,150)]:
    d.ellipse([btx-r,gy-150-r+60,btx+r,gy-150+r+60],fill=GLOW+(al,))
d.polygon([(btx,gy-180),(btx+30,gy),(btx-30,gy)],fill=BEACON,outline=(60,30,8),width=2)
d.ellipse([btx-16,gy-210,btx+16,gy-178],fill=MON,outline=(40,20,55),width=2)
d.text((btx,gy+18),"코어 비콘(키 큼+발광+괴수)",font=F(12),fill=GOLD,anchor="ma")
# 사선(시선)이 클러터 위를 스쳐 비콘 꼭대기에 닿음
d.line([(ex+8,ey-4),(btx,gy-188)],fill=(120,200,236,200),width=2)
for seg in range(0,1):
    pass
d.text((ix0+300,iy0+150),"시선이 클러터 *위로* 비콘을 스침\n→ 지상 미끼는 가려도 비콘은 상시 보임\n= Hyrule 성/산처럼 끄는 중력",font=F(13),fill=(196,200,206))

# ---------- 하단 원리 스트립 ----------
by=560
d.line([(40,by),(W-40,by)],fill=(70,74,82),width=1)
d.text((40,by+10),"젤다식 자연 끌림 = 4겹 (시스템 이유 ❌ → 지각·호기심 ✓)",font=FB,fill=GOLD)
cols=[("상시 중력 (비콘)","틸트로 솟은 발광 코어가 늘 곁눈에.\n'저 안에 대단한 게' = 막연한 큰 끌림."),
      ("리딩 라인","거리·고가·잔해가 시선·발을 안으로 꺾음.\n수렴선 = 무의식 유도."),
      ("콘 릴레이 공개","들어갈수록 콘이 다음 미끼를 깜.\n① 캐시→② 엘리트→③ 잭팟 연쇄(사선 공개)."),
      ("뽕 약속이 미끼","목표(할당량)는 욕망(뽕)을 타고 묻어옴.\n'저거 터뜨리고 싶다'가 진짜 엔진.")]
cw=(W-80)//4
for i,(h,t) in enumerate(cols):
    x=40+i*cw
    d.text((x,by+46),f"{i+1}. {h}",font=F(15,True),fill=TXT)
    d.text((x,by+74),t,font=FM,fill=(196,200,206))

# 시드별 리딩라인/비콘 차이 한 줄
y2=by+160
d.text((40,y2),"시드별 차이 = '리딩라인 기하'만 다름 (비콘·릴레이·뽕약속은 공통 불변식):",font=F(15,True),fill=TXT)
rows=[("A 광장","방사 직선 리딩라인 → 코어 비콘 정조준. 가장 또렷·기준선."),
      ("B 블록","건물이 비콘을 가렸다 골목 끝에서 *왈칵* 드러냄 = 가장 강한 사선 공개 드라마."),
      ("C 나선","리딩라인이 휘어 들어가며 비콘이 점점 커짐 = 압축+점층 끌림(궁금증 최대).")]
for j,(n,t) in enumerate(rows):
    yy=y2+30+j*30
    d.text((54,yy),n,font=F(14,True),fill=GOLD); d.text((180,yy),t,font=FM,fill=(206,210,216))

# 범례
y3=y2+135
d.text((40,y3),"기호:",font=F(14,True),fill=TXT)
d.ellipse([100,y3,118,y3+16],fill=GLOW); d.text((126,y3+8),"비콘 글로우(상시)",font=FM,fill=TXT,anchor="lm")
d.line([(280,y3+8),(330,y3+8)],fill=LANE,width=6); d.text((338,y3+8),"리딩 라인",font=FM,fill=TXT,anchor="lm")
d.polygon([(470,y3+16),(480,y3),(490,y3+16)],fill=CONE); d.text((498,y3+8),"시야 콘(공개 범위)",font=FM,fill=TXT,anchor="lm")
d.ellipse([660,y3,676,y3+16],fill=MON); d.text((684,y3+8),"괴수/엘리트 윤곽",font=FM,fill=TXT,anchor="lm")

d.text((W//2,8),"ZombieCrush — 자연스러운 끌림(PULL) 합성: 비콘 상시중력 + 리딩라인 + 콘 릴레이 공개 (젤다식)",font=F(15,True),fill=TXT,anchor="ma")

out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-pull-composition.png")
img.save(out); print("SAVED:",out,img.size)
