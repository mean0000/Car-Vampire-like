# -*- coding: utf-8 -*-
# 수동 시드 3장 톱다운 평면도 비교 렌더 (그레이박스 의도 전달용 다이어그램)
import os
from PIL import Image, ImageDraw, ImageFont

SCALE = 6              # px per meter
MAP = 90              # map is 90x90 m
MAP_PX = MAP * SCALE  # 540
TITLE_H = 130
LEGEND_H = 150
PANEL_W = MAP_PX + 80          # 620
PANEL_H = TITLE_H + MAP_PX + 30
IMG_W = PANEL_W * 3
IMG_H = PANEL_H + LEGEND_H

# palette (dark graybox)
BG       = (16, 18, 22)
C_BAND   = (36, 41, 48)   # 외곽: 안전/저밀도 (어둡게)
B_BAND   = (52, 60, 70)   # 중간
A_CORE   = (120, 96, 64)  # 코어: 고노출/고보상 (밝고 따뜻)
WALL     = (74, 68, 60)   # 봉쇄벽 W
STREET   = (84, 92, 102)  # 거리
SPINE    = (96, 104, 116) # 스파인(관통축, 더 넓음)
LMARK    = (150, 118, 78) # 고가 랜드마크
POCKET   = (150, 70, 175) # 매복 포켓 (자주)
X1C      = (70, 205, 115) # 개방 추출 (초록)
X2C      = (235, 200, 70) # 키 추출 (노랑)
X3C      = (232, 78, 68)  # 플레어 추출 (빨강)
TXT      = (228, 230, 234)
TXT_DIM  = (150, 156, 164)

def font(sz, bold=False):
    path = "C:/Windows/Fonts/malgunbd.ttf" if bold else "C:/Windows/Fonts/malgun.ttf"
    try:
        return ImageFont.truetype(path, sz)
    except Exception:
        return ImageFont.load_default()

F_TITLE = font(26, True)
F_SUB   = font(17)
F_LBL   = font(15, True)
F_LEG   = font(15)
F_LEGB  = font(15, True)

img = Image.new("RGB", (IMG_W, IMG_H), BG)
d = ImageDraw.Draw(img, "RGBA")

def P(panel, mx, my):
    """map coords (0..90) -> pixel, origin top-left of map area"""
    x0 = panel * PANEL_W + 40
    y0 = TITLE_H
    return (x0 + mx * SCALE, y0 + my * SCALE)

def rect(panel, x1, y1, x2, y2, color):
    d.rectangle([P(panel, x1, y1), P(panel, x2, y2)], fill=color)

def square_outline(panel, x1, y1, x2, y2, color, w):
    d.rectangle([P(panel, x1, y1), P(panel, x2, y2)], outline=color, width=w)

def circle(panel, mx, my, r_m, color, outline=None):
    cx, cy = P(panel, mx, my)
    r = r_m * SCALE
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color,
              outline=outline if outline else color, width=2)

def poly(panel, pts, color):
    d.polygon([P(panel, x, y) for (x, y) in pts], fill=color)

def text(px, py, s, fnt, color, anchor="la"):
    d.text((px, py), s, font=fnt, fill=color, anchor=anchor)

def draw_bands(panel):
    rect(panel, 4, 4, 86, 86, C_BAND)      # C 외곽
    rect(panel, 16, 16, 74, 74, B_BAND)    # B 중간
    rect(panel, 30, 30, 60, 60, A_CORE)    # A 코어광장
    square_outline(panel, 4, 4, 86, 86, WALL, 8)  # 봉쇄벽 W

def extract(panel, mx, my, color, lbl):
    circle(panel, mx, my, 2.6, color, outline=(12, 12, 14))
    # label offset toward inside
    lx, ly = P(panel, mx, my)
    text(lx, ly - 26, lbl, F_LBL, color, anchor="mm")

def title(panel, t, sub):
    x0 = panel * PANEL_W + 40
    text(x0, 30, t, F_TITLE, TXT)
    text(x0, 68, sub, F_SUB, TXT_DIM)
    # core depth arrow hint
    text(x0, 94, "← 외곽=안전  ·  중앙 코어=고노출/고보상 →", font(13), (110,116,124))

# ---------- SEED A : 사거리 (Cross) ----------
pa = 0
draw_bands(pa)
rect(pa, 42, 4, 48, 86, STREET)   # 세로 스포크
rect(pa, 4, 42, 86, 48, STREET)   # 가로 스포크
rect(pa, 40, 26, 50, 64, LMARK)   # 고가 랜드마크 N-S
title(pa, "시드 A — 사거리", "가독성·기준선  |  동사: 전방 학살·처단")
extract(pa, 45, 6,  X3C, "X3")    # 북단 최심
extract(pa, 45, 84, X1C, "X1")    # 남단 개방(스폰 근처)
extract(pa, 84, 45, X2C, "X2")    # 동측 키

# ---------- SEED B : 미로 (Weave) ----------
pb = 1
draw_bands(pb)
# 직선 스포크 없음 — 지그재그 회랑
rect(pb, 22, 58, 28, 86, STREET)
rect(pb, 22, 58, 48, 64, STREET)
rect(pb, 42, 30, 48, 64, STREET)
rect(pb, 60, 66, 82, 72, STREET)
rect(pb, 74, 40, 80, 72, STREET)
rect(pb, 56, 40, 80, 46, STREET)
# 대각 랜드마크 SW-NE
poly(pb, [(22.5,60.5),(29.5,67.5),(67.5,29.5),(60.5,22.5)], LMARK)
# 매복 포켓 (콘 차단) 산포
for (x,y) in [(10,30),(64,12),(78,80),(12,68),(50,78)]:
    rect(pb, x, y, x+6, y+8, POCKET)
title(pb, "시드 B — 미로", "누비기·탐색 긴장  |  동사: 매복·대시 돌파")
# 추출구 西측 몰림(방향 커밋 강제)
extract(pb, 6, 24, X1C, "X1")
extract(pb, 6, 50, X2C, "X2")
extract(pb, 10, 80, X3C, "X3")

# ---------- SEED C : 관통 (Gauntlet / Spine) ----------
pc = 2
draw_bands(pc)
rect(pc, 4, 38, 86, 52, SPINE)     # 지배 축(붕괴 고가/봉쇄 대로) 전폭
rect(pc, 10, 43, 80, 49, LMARK)    # 고가 랜드마크 = 스파인 강조
for x in range(16, 82, 10):        # 지지 기둥
    circle(pc, x, 46, 1.1, (60,54,48))
# 측면 매복지 (플랭크)
for (x,y) in [(24,20),(40,16),(58,22),(68,68),(46,72),(30,70)]:
    rect(pc, x, y, x+6, y+8, POCKET)
title(pc, "시드 C — 관통", "돌파·무리 압축  |  동사: 무리 끊기·돌파")
extract(pc, 84, 46, X3C, "X3")     # 축 최심단(풀 건틀릿 커밋)
extract(pc, 45, 84, X1C, "X1")     # 남측 플랭크(조기 탈출)
extract(pc, 20, 6,  X2C, "X2")     # 북측 키

# ---------- 공유 범례 ----------
ly0 = PANEL_H + 14
lx = 40
def swatch(x, y, color, label, w=26, h=18):
    d.rectangle([x, y, x+w, y+h], fill=color, outline=(90,90,96))
    text(x+w+10, y+h/2, label, F_LEG, TXT, anchor="lm")
    return x + w + 14 + d.textlength(label, font=F_LEG) + 34

x = lx
x = swatch(x, ly0, A_CORE, "코어 A (고노출·고보상)")
x = swatch(x, ly0, B_BAND, "중간 B")
x = swatch(x, ly0, C_BAND, "외곽 C (안전·빈약)")
x = swatch(x, ly0, WALL, "봉쇄벽 W")
x = swatch(x, ly0, LMARK, "고가 랜드마크 / 스파인")
x = swatch(x, ly0, STREET, "거리")
x = swatch(x, ly0, POCKET, "매복 포켓")

y2 = ly0 + 36
def dot_leg(x, y, color, label):
    d.ellipse([x, y, x+18, y+18], fill=color, outline=(12,12,14), width=2)
    text(x+26, y+9, label, F_LEG, TXT, anchor="lm")
    return x + 26 + d.textlength(label, font=F_LEG) + 40

x = lx
x = dot_leg(x, y2, X1C, "X1 개방 추출 (겁쟁이 출구)")
x = dot_leg(x, y2, X2C, "X2 키 추출 (코어 드롭)")
x = dot_leg(x, y2, X3C, "X3 플레어 추출 (전구역 노출·최심·호드 수렴)")

# 헤더
text(IMG_W//2, 6, "ZombieCrush — 첫 스테이지 수동 시드 3장 (그레이박스 평면도 · 90×90m · 동심원 3띠 + 비대칭 추출 3종 불변)",
     font(16, True), TXT, anchor="ma")

out_dir = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(out_dir, "2026-06-14-seed-layouts.png")
img.save(out)
print("SAVED:", out, img.size)
