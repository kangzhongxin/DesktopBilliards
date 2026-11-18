"""Generate clean geometry illustrations for the blog article."""

import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ASSETS = Path(r"E:\OtherProject\DesktopBilliards\blog\assets")
S = 2  # draw at 2x, then downsample for anti-aliasing

BG = (9, 15, 28)
PANEL = (13, 26, 43)
MINT = (140, 232, 206)
ICE = (166, 202, 255)
WHITE = (240, 247, 255)
MUTED = (148, 167, 193)
GOLD = (255, 198, 105)
CORAL = (255, 112, 92)

FONT_BOLD = "C:/Windows/Fonts/msyhbd.ttc"
FONT_REG = "C:/Windows/Fonts/msyh.ttc"

NUMBER_COLORS = {
    1: (245, 197, 66), 2: (72, 128, 228), 3: (224, 78, 78),
    4: (150, 98, 220), 5: (240, 145, 62), 6: (62, 176, 112),
    7: (158, 68, 68), 8: (28, 34, 46), 9: (238, 204, 96),
    10: (80, 144, 235), 11: (232, 92, 92), 12: (164, 116, 232),
    13: (246, 164, 76), 14: (88, 190, 128), 15: (184, 82, 82),
}

RACK = [
    [1],
    [2, 9],
    [3, 8, 10],
    [4, 11, 5, 12],
    [6, 13, 7, 14, 15],
]


def font(size: int, bold: bool = True) -> ImageFont.FreeTypeFont:
    return ImageFont.truetype(FONT_BOLD if bold else FONT_REG, int(size * S))


def scaled_box(box):
    return [int(v * S) for v in box]


def text_center(draw, xy, text, f, fill, stroke=0, stroke_fill=None):
    x, y = xy
    bbox = draw.textbbox((0, 0), text, font=f, stroke_width=stroke)
    w = bbox[2] - bbox[0]
    h = bbox[3] - bbox[1]
    draw.text(
        (x * S - w / 2, y * S - h / 2 - bbox[1]),
        text,
        font=f,
        fill=fill,
        stroke_width=stroke,
        stroke_fill=stroke_fill,
    )


def arrow(draw, start, end, fill, width=2, head=12):
    x1, y1 = start
    x2, y2 = end
    draw.line([(x1 * S, y1 * S), (x2 * S, y2 * S)], fill=fill, width=int(width * S))

    angle = math.atan2(y2 - y1, x2 - x1)
    for delta in (2.6, -2.6):
        hx = x2 - math.cos(angle + delta) * head
        hy = y2 - math.sin(angle + delta) * head
        draw.line(
            [(x2 * S, y2 * S), (hx * S, hy * S)],
            fill=fill,
            width=int(width * S),
        )


def dashed_line(draw, start, end, fill, width=2, dash=14, gap=12):
    x1, y1 = start
    x2, y2 = end
    length = math.hypot(x2 - x1, y2 - y1)
    if length <= 0:
        return
    dx = (x2 - x1) / length
    dy = (y2 - y1) / length
    pos = 0.0
    while pos < length:
        a = pos
        b = min(length, pos + dash)
        draw.line(
            [(x1 + dx * a) * S, (y1 + dy * a) * S, (x1 + dx * b) * S, (y1 + dy * b) * S],
            fill=fill,
            width=int(width * S),
        )
        pos += dash + gap


def base_canvas(width, height):
    image = Image.new("RGB", (width * S, height * S), BG)
    draw = ImageDraw.Draw(image)

    # Subtle top glow.
    for i in range(90):
        alpha = int(14 * (1 - i / 90))
        if alpha <= 0:
            continue
        draw.ellipse(
            scaled_box((width * 0.5 - 420 + i * 2, -250 + i * 2, width * 0.5 + 420 - i * 2, 420 - i * 2)),
            fill=(10 + alpha // 3, 24 + alpha // 2, 42 + alpha),
        )

    return image, draw


def make_geometry():
    width, height = 1600, 1000
    image, draw = base_canvas(width, height)

    text_center(draw, (800, 62), "真实台球摆位：白球在头点，15 球在脚点", font(40), WHITE)
    text_center(draw, (800, 112), "Head spot · Foot spot · Equilateral rack", font(20, False), MUTED)

    table = (150, 215, 1450, 760)
    draw.rounded_rectangle(
        scaled_box(table), radius=30 * S, fill=PANEL, outline=(70, 130, 130), width=int(2 * S)
    )
    inner = (175, 240, 1425, 735)
    draw.rounded_rectangle(
        scaled_box(inner), radius=20 * S, outline=(45, 94, 112), width=int(2 * S)
    )

    center_y = (table[1] + table[3]) / 2
    head_x = table[0] + (table[2] - table[0]) * 0.25
    foot_x = table[0] + (table[2] - table[0]) * 0.75
    cue_r = 25
    ball_r = 24

    # Head string.
    dashed_line(draw, (head_x, table[1] + 18), (head_x, table[3] - 18), (90, 118, 148), 2, 10, 12)
    draw.text((head_x * S + 10 * S, table[1] * S + 26 * S), "head string", font=font(16, False), fill=MUTED)

    # Cue ball.
    draw.ellipse(
        scaled_box((head_x - cue_r, center_y - cue_r, head_x + cue_r, center_y + cue_r)),
        fill=(245, 248, 250),
        outline=MINT,
        width=int(3 * S),
    )
    draw.ellipse(
        scaled_box((head_x - cue_r * 0.45, center_y - cue_r * 0.65, head_x, center_y - cue_r * 0.2)),
        fill=(255, 255, 255),
    )
    text_center(draw, (head_x, center_y + 58), "白球 / Cue ball", font(18), ICE)

    # Rack.
    spacing = ball_r * 2 + 3
    row_spacing = spacing * math.sqrt(3) / 2
    for row, numbers in enumerate(RACK):
        for col, number in enumerate(numbers):
            x = foot_x + row * row_spacing
            y = center_y + (col - row / 2) * spacing
            color = NUMBER_COLORS[number]
            draw.ellipse(
                scaled_box((x - ball_r, y - ball_r, x + ball_r, y + ball_r)),
                fill=color,
                outline=(235, 242, 255),
                width=int(2 * S),
            )
            text_center(draw, (x, y), str(number), font(18), WHITE if number != 8 else WHITE)

    # Foot spot / head spot markers.
    draw.ellipse(
        scaled_box((foot_x - 4, center_y - 4, foot_x + 4, center_y + 4)),
        fill=GOLD,
    )
    text_center(draw, (foot_x + 10, center_y - 154), "foot spot", font(18), GOLD)

    # 25% dimension guides.
    arrow(draw, (table[0] + 25, table[1] + 52), (head_x, table[1] + 52), MUTED, 1.5, 10)
    text_center(draw, ((table[0] + head_x) / 2, table[1] + 36), "25%", font(18), MUTED)
    arrow(draw, (foot_x, table[1] + 52), (table[2] - 25, table[1] + 52), MUTED, 1.5, 10)
    text_center(draw, ((foot_x + table[2]) / 2, table[1] + 36), "25%", font(18), MUTED)

    # Break direction.
    dashed_line(draw, (head_x + cue_r + 14, center_y), (foot_x - 36, center_y), MINT, 2, 14, 12)
    arrow(draw, (foot_x - 88, center_y), (foot_x - 40, center_y), MINT, 2, 12)

    # Formula callouts.
    panel = (180, 810, 1420, 940)
    draw.rounded_rectangle(
        scaled_box(panel), radius=20 * S, fill=(10, 19, 33), outline=(55, 90, 116), width=int(1.5 * S)
    )
    text_center(draw, (800, 846), "相邻球心距  d = 2R + gap", font(24), WHITE)
    text_center(draw, (800, 886), "行间距 = (√3 / 2) · d    ·    顶点朝白球，行向脚库延伸", font(21, False), MUTED)
    text_center(draw, (800, 920), "1 号在顶点，8 号在中心，6 / 15 在底角", font(18, False), MINT)

    image = image.resize((width, height), Image.Resampling.LANCZOS)
    image.save(ASSETS / "geometry.png")
    print("Saved", ASSETS / "geometry.png")


def make_aim():
    width, height = 1400, 900
    image, draw = base_canvas(width, height)

    text_center(draw, (700, 62), "瞄准辅助线：射线 + 幽灵球 + 法线 / 切线", font(38), WHITE)
    text_center(draw, (700, 108), "Ray → ghost ball → object direction & cue deflection", font(19, False), MUTED)

    cue = (300, 585)
    cue_r = 48
    ghost = (784, 470)
    target = (880, 470)
    target_r = 48
    contact = (832, 470)

    # Aim ray.
    dashed_line(draw, cue, ghost, MINT, 2.5, 16, 12)
    text_center(draw, (490, 590), "瞄准线", font(21), MINT)

    # Cue ball.
    draw.ellipse(
        scaled_box((cue[0] - cue_r, cue[1] - cue_r, cue[0] + cue_r, cue[1] + cue_r)),
        fill=(245, 248, 250),
        outline=MINT,
        width=int(3 * S),
    )
    draw.ellipse(
        scaled_box((cue[0] - cue_r * 0.5, cue[1] - cue_r * 0.7, cue[0], cue[1] - cue_r * 0.2)),
        fill=(255, 255, 255),
    )
    text_center(draw, (cue[0], cue[1] + 82), "白球", font(22), ICE)

    # Ghost ball.
    ghost_fill = Image.new("RGBA", (width * S, height * S), (0, 0, 0, 0))
    gd = ImageDraw.Draw(ghost_fill)
    gd.ellipse(
        scaled_box((ghost[0] - cue_r, ghost[1] - cue_r, ghost[0] + cue_r, ghost[1] + cue_r)),
        fill=(140, 232, 206, 28),
        outline=(180, 244, 224, 210),
        width=int(2.5 * S),
    )
    image = Image.alpha_composite(image.convert("RGBA"), ghost_fill).convert("RGB")
    draw = ImageDraw.Draw(image)
    text_center(draw, (ghost[0], ghost[1] - 82), "幽灵球", font(22), MINT)

    # Target ball.
    draw.ellipse(
        scaled_box((target[0] - target_r, target[1] - target_r, target[0] + target_r, target[1] + target_r)),
        fill=(224, 78, 78),
        outline=(248, 244, 238),
        width=int(3 * S),
    )
    text_center(draw, (target[0], target[1]), "8", font(25), WHITE)
    text_center(draw, (target[0], target[1] + 82), "目标球", font(22), ICE)

    # Contact point.
    draw.ellipse(
        scaled_box((contact[0] - 5, contact[1] - 5, contact[0] + 5, contact[1] + 5)),
        fill=GOLD,
    )
    text_center(draw, (contact[0] + 12, contact[1] - 34), "碰撞点", font(17, False), GOLD)

    # Normal / object direction.
    draw.line(
        [(ghost[0] * S, ghost[1] * S), (contact[0] * S, contact[1] * S)],
        fill=GOLD,
        width=int(2.5 * S),
    )
    arrow(
        draw,
        (target[0] + target_r + 8, target[1]),
        (target[0] + target_r + 170, target[1]),
        GOLD,
        2.5,
        14,
    )
    text_center(draw, (1090, 438), "法线 / 目标球方向", font(21), GOLD)

    # Tangent / cue deflection.
    dashed_line(draw, (ghost[0], ghost[1] - 135), (ghost[0], ghost[1] + 135), ICE, 2.5, 12, 10)
    arrow(draw, (ghost[0], ghost[1]), (ghost[0], ghost[1] - 148), ICE, 2, 12)
    text_center(draw, (ghost[0] - 106, ghost[1] + 180), "切线 / 白球分离方向", font(21), ICE)

    # Right-angle marker.
    draw.line(
        [(ghost[0] * S, (ghost[1] - 26) * S), ((ghost[0] + 26) * S, (ghost[1] - 26) * S),
         ((ghost[0] + 26) * S, ghost[1] * S)],
        fill=MUTED,
        width=int(1.5 * S),
    )

    # Formula card.
    panel = (150, 720, 1250, 846)
    draw.rounded_rectangle(
        scaled_box(panel), radius=20 * S, fill=(10, 19, 33), outline=(55, 90, 116), width=int(1.5 * S)
    )
    text_center(draw, (700, 756), "射线-圆相交：取最小正根 t 作为第一次碰球", font(22), WHITE)
    text_center(draw, (700, 796), "t = -b - √(b² - c)   ·   b = dot(oc, dir)   ·   c = |oc|² - (Rc + Rt)²", font(19, False), MUTED)
    text_center(draw, (700, 828), "目标球方向 = normalize(target - ghost)    ·    白球分离方向 = 切线", font(17, False), MINT)

    image = image.resize((width, height), Image.Resampling.LANCZOS)
    image.save(ASSETS / "aim.png")
    print("Saved", ASSETS / "aim.png")


if __name__ == "__main__":
    ASSETS.mkdir(parents=True, exist_ok=True)
    make_geometry()
    make_aim()
