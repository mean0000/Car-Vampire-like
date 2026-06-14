# -*- coding: utf-8 -*-
# 수동 시드 3장 v2 — 서바이버즈 농성 버전 (할당량+무한스폰+15분+)
# 조직 원리: 밀도 그라디언트 사냥터 + 카이팅 루프/카빙 레인 + 단일 추출 LZ(끝맺음)
import os, math
from PIL import Image, ImageDraw, ImageFont

S = 5                 # px/m
MAP = 100
MPX = MAP * S         # 500
TITLE_H = 132
LEG_H = 168
PW = MPX + 100        # 600
PH = TITLE_H + MPX + 26
IMG_W = PW * 3
IMG_H = PH + LEG_H

BG       = (16, 18, 22)
WALL     = (80, 73, 64)
BLOCK    = (28, 31, 38)
BLOCK_E  = (92, 88, 80)
LOOP     = (86, 212, 228)   # 카이팅 루프 (시안)
LANE     = (240, 222, 120)  # 카빙 레인
LMARK    = (158, 122, 80)
LZC      = (88, 208, 118)   # 단일 추출 LZ
HOT      = (200, 74, 56)
MID      = (208, 142, 66)
COOL     = (42, 56, 80)
TXT      = (228, 230, 234)
DIM      = (150, 156, 164)

def font(sz, b=False):
    p = "C:/Windows/Fonts/malgunbd.ttf" if b else "C:/Windows/Fonts/malgun.ttf"
    try: return ImageFont.truetype(p, sz)
    except: return ImageFont.load_default()

F_T  = font(25, True); F_S = font(16); F_L = font(14, True); F_LG = font(15)

img = Image.new("RGB", (IMG_W, IMG_H), BG)
d = ImageDraw.Draw(img, "RGBA")

def P(p, mx, my):
    return (p*PW + 50 + mx*S, TITLE_H + my*S)

def lerp(a, b, t): return tuple(int(a[i]+(b[i]-a[i])*t) for i in range(3))

def heat(p, gamma=1.0):
    cx, cy = P(p, 50, 50)
    R = 56 * S
    steps = 60
    for i in range(steps):
        t = i/(steps-1)
        r = R*(1-t)
        # color: outer(cool) -> mid -> hot(center)
        if t < 0.5: col = lerp(COOL, MID, (t/0.5)**gamma)
        else:       col = lerp(MID, HOT, ((t-0.5)/0.5)**gamma)
        d.ellipse([cx-r, cy-r, cx+r, cy+r], fill=col)

def walls(p):
    d.rectangle([P(p,2,2), P(p,98,98)], outline=WALL, width=9)

def block(p, x1, y1, x2, y2):
    d.rectangle([P(p,x1,y1), P(p,x2,y2)], fill=BLOCK, outline=BLOCK_E, width=2)

def lane(p, x1, y1, x2, y2, w=5):
    a = P(p,x1,y1); b = P(p,x2,y2)
    d.line([a,b], fill=LANE+(150,), width=w*S//2)

def arrowhead(px, py, ang, c=LOOP, L=11):
    dx, dy = math.cos(ang), math.sin(ang)
    tip = (px+L*dx, py+L*dy)
    bx, by = px-dx*4, py-dy*4
    p1 = (bx - dy*7, by + dx*7); p2 = (bx + dy*7, by - dx*7)
    d.polygon([tip, p1, p2], fill=c)

def loop_ellipse(p, cmx, cmy, rmx, rmy, c=LOOP, arrows=True, cw=True):
    cx, cy = P(p, cmx, cmy); rx, ry = rmx*S, rmy*S
    d.ellipse([cx-rx, cy-ry, cx+rx, cy+ry], outline=c+(235,), width=3)
    if arrows:
        s = 1 if cw else -1
        # top, right, bottom, left travel dirs for CW (screen y-down)
        pts = [(cx, cy-ry, 0 if cw else math.pi),
               (cx+rx, cy, math.pi/2),
               (cx, cy+ry, math.pi if cw else 0),
               (cx-rx, cy, -math.pi/2)]
        for (ax, ay, _) in [pts[0], pts[2]]:
            pass
        # simple: 4 arrows tangent
        for k,(ax,ay) in enumerate([(cx,cy-ry),(cx+rx,cy),(cx,cy+ry),(cx-rx,cy)]):
            ang = [0, math.pi/2, math.pi, -math.pi/2][k]
            if not cw: ang += math.pi
            arrowhead(ax, ay, ang, c)

def arc(p, cmx, cmy, rm, a0, a1, c=WALL, w=8):
    cx, cy = P(p, cmx, cmy); r = rm*S
    d.arc([cx-r, cy-r, cx+r, cy+r], a0, a1, fill=c, width=w)

def landmark(p, mx, my, lbl="랜드마크"):
    cx, cy = P(p, mx, my)
    d.polygon([(cx,cy-12),(cx+11,cy+8),(cx-11,cy+8)], fill=LMARK, outline=(40,32,24), width=2)

def lz(p, mx, my):
    cx, cy = P(p, mx, my); r = 16
    d.ellipse([cx-r,cy-r,cx+r,cy+r], fill=LZC, outline=(10,12,10), width=2)
    d.text((cx,cy), "퇴", font=F_L, fill=(10,30,14), anchor="mm")
    d.text((cx, cy-26), "추출 LZ", font=F_L, fill=LZC, anchor="mm")

def title(p, t, sub, note):
    x0 = p*PW + 50
    d.text((x0,30), t, font=F_T, fill=TXT)
    d.text((x0,66), sub, font=F_S, fill=DIM)
    d.text((x0,92), note, font=font(13), fill=(112,118,126))

# ===== A 광장 (Ring) =====
p=0
heat(p, 1.0); walls(p)
for (x,y) in [(24,24),(70,24),(24,70),(70,70)]:
    block(p, x,y, x+6,y+6)
lane(p, 22,22, 78,78); lane(p, 22,78, 78,22)   # 대각 카빙
loop_ellipse(p, 50,50, 40,40, cw=True)          # 외곽 링 카이팅
landmark(p, 50,50)
lz(p, 50,92)
title(p, "시드 A — 광장", "링 카이팅 · 가장 열림 · 기준선",
      "넓은 중앙 광장(고밀도) + 외곽 링 순환. 대각 대시 카빙. \"눌러앉아 돌린다\"")

# ===== B 블록 (Figure-8) =====
p=1
heat(p, 1.0); walls(p)
for (x1,y1,x2,y2) in [(18,18,40,40),(60,18,82,40),(18,60,40,82),(60,60,82,82)]:
    block(p, x1,y1,x2,y2)
lane(p, 50,8, 50,92); lane(p, 8,50, 92,50)      # 중앙 십자 카빙 거리
loop_ellipse(p, 29,50, 20,38, cw=False)         # 좌 루프
loop_ellipse(p, 71,50, 20,38, cw=True)          # 우 루프 = 8자
landmark(p, 50,50)
lz(p, 90,50)
title(p, "시드 B — 블록", "8자 순환 · 테크니컬 카이팅",
      "건물 블록 사이 거리 = 맞물린 루프. 코너로 호드 떨치고 재집결. 거리=카빙 레인")

# ===== C 나선 (Funnel) =====
p=2
heat(p, 1.7); walls(p)   # 더 가파른 = 중앙 좁고 치명
arc(p, 50,50, 38, 35, 325, WALL, 9)             # 외곽 링벽(상단 개구)
arc(p, 50,50, 24, 200, 150, WALL, 9)            # 중간 링벽(하단 개구, 오프셋)
loop_ellipse(p, 50,50, 38,38, cw=True, arrows=True)
loop_ellipse(p, 50,50, 24,24, cw=True, arrows=False)
# 수렴 화살표 (압축)
for ang in [60, 150, 240, 330]:
    a = math.radians(ang); cx,cy = P(p,50,50)
    px,py = cx+44*math.cos(a), cy+44*math.sin(a)
    arrowhead(px,py, a+math.pi, c=(232,120,90), L=13)
landmark(p, 50,50)
lz(p, 50,8)
title(p, "시드 C — 나선", "압축 카빙 · 공격적 · 최고 페이오프",
      "호드를 나선 안으로 유인=압축. 링벽 개구=탈출컷(데스트랩 방지). 눈(중앙)=치명/대박")

# ===== 범례 =====
ly = PH + 12; lx = 50
def sw(x,y,c,t,w=26,h=18):
    d.rectangle([x,y,x+w,y+h], fill=c if isinstance(c,tuple) and len(c)==3 else c, outline=(90,90,96))
    d.text((x+w+9, y+h/2), t, font=F_LG, fill=TXT, anchor="lm")
    return x+w+9+d.textlength(t,font=F_LG)+30

# heat gradient swatch
gx, gy, gw, gh = lx, ly, 150, 18
for i in range(gw):
    t=i/(gw-1); col = lerp(COOL,MID,t/0.5) if t<0.5 else lerp(MID,HOT,(t-0.5)/0.5)
    d.line([(gx+i,gy),(gx+i,gy+gh)], fill=col)
d.rectangle([gx,gy,gx+gw,gy+gh], outline=(90,90,96))
d.text((gx+gw+9, gy+gh/2), "밀도/위험: 외곽 안전·느림 → 중앙 치명·빠름 (어디서 사냥할지)", font=F_LG, fill=TXT, anchor="lm")

y2 = ly + 34
x = lx
# loop
cx=x+13; cy=y2+9; d.ellipse([cx-11,cy-7,cx+11,cy+7], outline=LOOP, width=3)
d.text((x+30,cy),"카이팅 루프(순환 동선)",font=F_LG,fill=TXT,anchor="lm"); x += 30+d.textlength("카이팅 루프(순환 동선)",font=F_LG)+34
x = sw(x, y2, LANE, "카빙 레인(대시 관통)")
x = sw(x, y2, BLOCK, "건물/엄폐(카이팅 장애물)")
# landmark tri
d.polygon([(x+9,y2),(x+18,y2+18),(x,y2+18)],fill=LMARK,outline=(40,32,24),width=1)
d.text((x+26,y2+9),"랜드마크(방향 기준)",font=F_LG,fill=TXT,anchor="lm"); x += 26+d.textlength("랜드마크(방향 기준)",font=F_LG)+30
# LZ
cx=x+10; cy=y2+9; d.ellipse([cx-10,cy-10,cx+10,cy+10],fill=LZC,outline=(10,12,10),width=2)
d.text((x+28,y2+9),"단일 추출 LZ — 할당량 채우면 점등(끝맺음, 매순간 결정 아님)",font=F_LG,fill=TXT,anchor="lm")

d.text((IMG_W//2, 6),
       "ZombieCrush — 수동 시드 3장 v2 (서바이버즈 농성: 할당량+무한스폰+15분+) · 100×100m · 공통=밀도 그라디언트+카이팅 루프+데스트랩0+단일 LZ",
       font=font(15,True), fill=TXT, anchor="ma")

out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "2026-06-14-seed-layouts-v2.png")
img.save(out); print("SAVED:", out, img.size)
