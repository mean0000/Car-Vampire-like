# -*- coding: utf-8 -*-
# 1차 스테이지 몬스터 배역표 — 9종 (lv 역할 + st 디제틱 융합)
import os
from PIL import Image, ImageDraw, ImageFont
W,H=1480,980
BG=(15,16,20); CARD=(26,29,36); CARD_E=(60,64,72)
RIM_C=(96,150,210); MID_C=(228,150,70); CORE_C=(228,86,70)
GOLD=(250,206,90); TXT=(230,232,236); DIM=(156,162,170); SUB=(190,194,200)
def F(s,b=False):
    p="C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p,s)
    except: return ImageFont.load_default()
FT=F(24,True); FH=F(18,True); FN=F(17,True); FR=F(13,True); FM=F(12); FMI=F(12)
img=Image.new("RGB",(W,H),BG); d=ImageDraw.Draw(img,"RGBA")

cols=[
 ("외곽 LV1 · 파밍/안전",RIM_C,[
   dict(n="Lacercharias",lv="1",role="fodder / 스웜",call="「하급 이상개체」 · 잡것들",
        mut="보행자 인파 → 무리로만 존재하던 게 무리로만 위협",star=False),
   dict(n="Venodonte",lv="1",role="원거리 / 조너 (산성·지상엄폐)",call="「산성 분사형」 · 소독반",
        mut="★방역 소독반 → 분무하던 손이 산성을 뱉음 (주인공 직종 거울상)",star=True),
 ]),
 ("중간 LV2~3 · 카이팅 본전장",MID_C,[
   dict(n="Caniathrox",lv="2",role="근접 러셔 (속도감 1순위)",call="「쾌속 추격형」 · 순찰개",
        mut="경비·순찰 → 두 발로 돌던 자가 네 발로 쫓음",star=False),
   dict(n="Kupolojuve",lv="2",role="비행 / 하라서 (다이브·텔레그래프)",call="「부유 전격형」 · 전기 박쥐",
        mut="배전·고소 인력 → 전선에 매달리던 자가 전기 체내화·부유",star=False),
   dict(n="Dimaxillosaurus",lv="3",role="브루저 / 클로 매복",call="「직립 포식형」 · 양복",
        mut="★사무직 → 입으로 일하던 자의 입이 무기가 됨",star=True),
 ]),
 ("코어 LV4~5 · 잭팟",CORE_C,[
   dict(n="Venosaur",lv="3",role="호위 / 물량",call="「중장 호위형」 · 조끼들",
        mut="건설 작업조 → 도심을 짓던 손",star=False),
   dict(n="Fulgurodonte",lv="4",role="엘리트 / 헤비 (램·벽그로기=뽕타겟)",call="「돌진 정예」 · 기사님",
        mut="★운전·중장비 → 몰던 쇳덩이가 제 몸이 됨 (코어 비콘)",star=True),
   dict(n="Carcinoptera",lv="4",role="공중 정예 (분수광장 전용)",call="「공중 정예」 · 말벌",
        mut="★항공 방역·통신 → 본사 드론과 시각 혼선 (소급 떡밥)",star=True),
   dict(n="Crustaspikan 유생",lv="5",role="보스 스포너 (발원 격리)",call="「특별관리대상 — 유생」",
        mut="정체성 소실 — 변이 말기 종착, 누구였는지 추적 불가",star=False),
 ]),
]

d.text((40,26),"ZombieCrush — 1차 스테이지(폐허 도심) 몬스터 배역표 · 9종",font=FT,fill=TXT)
d.text((40,60),"동심원 = LV = 변이 진행도(=인간성 잔존도). 좌(외곽·사람 잔존) → 우(코어·완전 이형). 역할 겹침 0 · 전부 단일 평면.",font=FM,fill=DIM)

CW=460; CX0=40; CY0=98; CARDH=150; GAP=14
for ci,(hdr,col,cards) in enumerate(cols):
    cx=CX0+ci*(CW+10)
    d.rounded_rectangle([cx,CY0,cx+CW,CY0+26],6,fill=col+(40,),outline=col,width=2)
    d.text((cx+12,CY0+13),hdr,font=FH,fill=col,anchor="lm")
    for i,c in enumerate(cards):
        y=CY0+40+i*(CARDH+GAP)
        ec=GOLD if c["star"] else CARD_E
        d.rounded_rectangle([cx,y,cx+CW,y+CARDH],8,fill=CARD,outline=ec,width=2 if c["star"] else 1)
        # name + LV chip
        d.text((cx+14,y+14),c["n"],font=FN,fill=GOLD if c["star"] else TXT)
        lvw=d.textlength("LV"+c["lv"],font=FR)+16
        d.rounded_rectangle([cx+CW-lvw-12,y+12,cx+CW-12,y+34],5,fill=col+(60,),outline=col,width=2)
        d.text((cx+CW-12-lvw/2,y+23),"LV"+c["lv"],font=FR,fill=col,anchor="mm")
        # role
        d.text((cx+14,y+44),"["+c["role"]+"]",font=FR,fill=SUB)
        # 통칭
        d.text((cx+14,y+70),c["call"],font=FM,fill=DIM)
        # 변이 알리바이 (wrap to 2 lines)
        mut=c["mut"]; maxw=CW-28
        words=mut.split(" "); lines=[]; cur=""
        for w_ in words:
            t=(cur+" "+w_).strip()
            if d.textlength(t,font=FMI)<=maxw: cur=t
            else: lines.append(cur); cur=w_
        if cur: lines.append(cur)
        for li,ln in enumerate(lines[:2]):
            d.text((cx+14,y+94+li*20),ln,font=FMI,fill=(206,210,216) if c["star"] else (186,190,196))

# footer
fy=CY0+40+4*(CARDH+GAP)+6
d.line([(40,fy),(W-40,fy)],fill=(70,74,82),width=1)
d.text((40,fy+10),"트림 5종 (1차 제외 → 2차):",font=FR,fill=TXT)
d.text((250,fy+10),"Arathrox·Horridomorph(원거리/공중 과포화) · Hexateuthis(그랩=학습부담) · Cephalonops(LV2 잉여) · Occisodonte(수변=도심 디제틱 부재)",font=FM,fill=DIM)
d.text((40,fy+38),"응집 2축:",font=FR,fill=TXT)
d.text((250,fy+38),"① 직종 = 한 도시 노동 인구 단면(소독·경비·배전·사무·건설·운전·항공+보행자)  ② 변이 진행도 = 인간성 잔존(직립+팔→이형, 카피0·실루엣만)",font=FM,fill=DIM)
d.text((40,fy+64),"escalation:",font=FR,fill=TXT)
d.text((250,fy+64),"로스터 내 4단계(밀도↑→능력치↑→유생 분출→Crustaspikan 보스). 신규 종 발명 0.",font=FM,fill=DIM)
d.text((40,fy+90),"★ = 회수 떡밥",font=FR,fill=GOLD)
d.text((250,fy+90),"⚠️ 로스터=배역표. '쫄깃한가'는 게이트0 전투감 + 종별 애니(Animation) 선행 판정. 캐넌 충돌 0건.",font=FM,fill=(220,180,120))

out=os.path.join(os.path.dirname(os.path.abspath(__file__)),"2026-06-14-stage1-roster.png")
img.save(out); print("SAVED:",out,img.size)
