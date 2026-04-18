#!/usr/bin/env python3
"""
WW2 Commander - 程序化美术素材生成器
用纯 Python 生成所有游戏需要的 2D 素材
"""
import os
from PIL import Image, ImageDraw, ImageFont
import math
import random

OUTPUT_DIR = os.path.join(os.path.dirname(os.path.dirname(__file__)), "Assets", "Art")
random.seed(42)  # 固定种子保证一致性

# ==================== 颜色方案 ====================
COLORS = {
    # 联军
    "us_infantry":   {"body": (58, 80, 64), "helmet": (62, 77, 68), "outline": (30, 40, 32)},
    "us_tank":       {"body": (55, 65, 50), "turret": (65, 75, 58), "track": (40, 45, 35), "outline": (25, 30, 20)},
    "brit_infantry": {"body": (90, 80, 55), "helmet": (85, 75, 50), "outline": (50, 45, 30)},
    "brit_tank":     {"body": (75, 85, 55), "turret": (80, 90, 60), "track": (50, 55, 40), "outline": (35, 40, 25)},
    # 轴心国
    "de_infantry":   {"body": (85, 78, 60), "helmet": (70, 65, 50), "outline": (45, 40, 30)},
    "de_tank":       {"body": (80, 75, 60), "turret": (70, 65, 52), "track": (50, 48, 38), "outline": (35, 32, 22)},
    # 环境
    "grass":         (76, 105, 66),
    "dirt":          (139, 119, 90),
    "water":         (55, 90, 130),
    "road":          (110, 100, 85),
    "building":      (140, 130, 115),
    "forest":        (45, 75, 40),
    # UI
    "ui_bg":         (30, 35, 30),
    "ui_text":       (200, 200, 180),
    "ui_accent":     (180, 160, 100),
    "ui_alert":      (200, 60, 60),
    "ui_ok":         (80, 160, 80),
    "paper_bg":      (225, 215, 190),
    "ink":           (40, 35, 30),
}

# ==================== 辅助函数 ====================
def make_dir(path):
    os.makedirs(path, exist_ok=True)

def new_img(w, h, color=None):
    img = Image.new("RGBA", (w, h), color or (0, 0, 0, 0))
    return img, ImageDraw.Draw(img)

def draw_circle(draw, cx, cy, r, fill, outline=None):
    draw.ellipse([cx-r, cy-r, cx+r, cy+r], fill=fill, outline=outline)

def draw_rect(draw, x, y, w, h, fill, outline=None):
    draw.rectangle([x, y, x+w, y+h], fill=fill, outline=outline)

def draw_triangle(draw, points, fill, outline=None):
    draw.polygon(points, fill=fill, outline=outline)

# ==================== 单位精灵 ====================
def generate_unit_sprite(size, unit_type, nation, name):
    """生成单个单位精灵"""
    img, draw = new_img(size, size)
    cx, cy = size // 2, size // 2
    
    palette = COLORS.get(f"{nation}_{unit_type}", COLORS["us_infantry"])
    
    if unit_type == "infantry":
        # 俯视角士兵：圆形头盔 + 椭圆身体 + 武器
        r = size // 4
        # 身体椭圆
        draw.ellipse([cx-r, cy-r//2, cx+r, cy+r*1.2], fill=palette["body"], outline=palette["outline"])
        # 头盔
        draw_circle(draw, cx, cy - r//2, r//2 + 2, palette["helmet"], palette["outline"])
        # 武器（斜线）
        weapon_len = r * 1.3
        angle = random.uniform(-0.3, 0.3)
        wx = cx + int(weapon_len * math.sin(angle))
        wy = cy - r//2 + int(weapon_len * math.cos(angle))
        draw.line([cx + r//3, cy - r//2, wx, wy], fill=(30, 30, 30), width=max(1, size//40))
        
    elif unit_type == "tank":
        # 俯视角坦克：椭圆底盘 + 炮塔 + 炮管
        rw, rh = size // 3, size // 4
        # 底盘
        draw.rounded_rectangle([cx-rw, cy-rh, cx+rw, cy+rh], radius=rh//2, 
                               fill=palette["body"], outline=palette["outline"])
        # 履带
        draw.rectangle([cx-rw-2, cy-rh+2, cx-rw+6, cy+rh-2], fill=palette["track"])
        draw.rectangle([cx+rw-6, cy-rh+2, cx+rw+2, cy+rh-2], fill=palette["track"])
        # 炮塔
        draw_circle(draw, cx, cy, rw//2, palette["turret"], palette["outline"])
        # 炮管
        draw.line([cx, cy, cx, cy - rh - 4], fill=palette["outline"], width=max(2, size//30))
    
    return img

def generate_all_units():
    """生成所有单位精灵"""
    units_dir = os.path.join(OUTPUT_DIR, "Units")
    make_dir(units_dir)
    
    configs = [
        (128, "infantry", "us", "us_infantry"),
        (128, "tank", "us", "us_tank"),
        (128, "infantry", "brit", "brit_infantry"),
        (128, "tank", "brit", "brit_tank"),
        (128, "infantry", "de", "de_infantry"),
        (128, "tank", "de", "de_tank"),
        # 小号（沙盘用）
        (48, "infantry", "us", "us_infantry_small"),
        (48, "tank", "us", "us_tank_small"),
        (48, "infantry", "de", "de_infantry_small"),
        (48, "tank", "de", "de_tank_small"),
        (48, "infantry", "brit", "brit_infantry_small"),
        (48, "tank", "brit", "brit_tank_small"),
    ]
    
    for size, utype, nation, name in configs:
        img = generate_unit_sprite(size, utype, nation, name)
        img.save(os.path.join(units_dir, f"{name}.png"))
    
    print(f"✅ 生成 {len(configs)} 个单位精灵")

# ==================== 沙盘地图 ====================
def generate_sand_table(size=1024):
    """生成沙盘底图"""
    img, draw = new_img(size, size)
    
    # 底色（草绿）
    draw.rectangle([0, 0, size, size], fill=COLORS["grass"])
    
    # 添加噪点纹理
    for _ in range(3000):
        x, y = random.randint(0, size-1), random.randint(0, size-1)
        base = COLORS["grass"]
        noise = random.randint(-15, 15)
        c = tuple(max(0, min(255, v + noise)) for v in base)
        draw.point((x, y), fill=c)
    
    # 道路
    roads = [
        # 主干道（横向）
        [(0, 300), (200, 310), (400, 320), (600, 300), (800, 290), (1024, 300)],
        # 纵向支路
        [(500, 0), (510, 200), (520, 400), (500, 600), (490, 800), (500, 1024)],
    ]
    for road in roads:
        for i in range(len(road)-1):
            draw.line([road[i], road[i+1]], fill=COLORS["road"], width=18)
            draw.line([road[i], road[i+1]], fill=(120, 110, 95), width=12)
    
    # 河流
    river_pts = [(0, 700), (150, 650), (300, 720), (500, 680), (650, 750), (800, 700), (1024, 730)]
    for i in range(len(river_pts)-1):
        draw.line([river_pts[i], river_pts[i+1]], fill=COLORS["water"], width=20)
        draw.line([river_pts[i], river_pts[i+1]], fill=(65, 100, 140), width=12)
    
    # 桥
    draw.rectangle([495, 670, 540, 700], fill=COLORS["building"], outline=(80, 70, 60))
    draw.line([(500, 672), (535, 672)], fill=(160, 150, 130), width=2)
    draw.line([(500, 698), (535, 698)], fill=(160, 150, 130), width=2)
    
    # 森林（绿色圆形区域）
    forests = [
        (150, 150, 80), (850, 200, 100), (200, 900, 70), (750, 850, 90),
        (350, 500, 60), (650, 450, 75),
    ]
    for fx, fy, fr in forests:
        for i in range(fr, 0, -5):
            alpha = int(180 * (i / fr))
            green = max(30, COLORS["forest"][1] + int(20 * (i / fr)))
            color = (COLORS["forest"][0], green, COLORS["forest"][2])
            draw.ellipse([fx-i, fy-i, fx+i, fy+i], fill=color)
    
    # 建筑群（小矩形块）
    villages = [
        (300, 200, 6, 4), (700, 350, 5, 3), (150, 550, 4, 3), (850, 600, 7, 4),
    ]
    for vx, vy, vw, vh in villages:
        for i in range(vw):
            for j in range(vh):
                bx = vx + i * 16 + random.randint(-3, 3)
                by = vy + j * 16 + random.randint(-3, 3)
                bw = random.randint(10, 14)
                bh = random.randint(10, 14)
                shade = random.randint(-10, 10)
                c = tuple(max(0, min(255, v + shade)) for v in COLORS["building"])
                draw.rectangle([bx, by, bx+bw, by+bh], fill=c, outline=(100, 90, 80))
    
    # 网格线（浅色）
    grid_color = (255, 255, 255, 30)
    for i in range(0, size, 128):
        draw.line([(i, 0), (i, size)], fill=grid_color, width=1)
        draw.line([(0, i), (size, i)], fill=grid_color, width=1)
    
    path = os.path.join(OUTPUT_DIR, "Maps", "sand_table.png")
    make_dir(os.path.dirname(path))
    img.save(path)
    print("✅ 生成沙盘地图 (1024x1024)")

# ==================== 指挥所室内 ====================
def generate_command_post(size=1024):
    """生成指挥所桌面俯视角"""
    img, draw = new_img(size, size)
    
    # 桌面（深色木纹）
    draw.rectangle([0, 0, size, size], fill=(45, 38, 30))
    # 木纹
    for y in range(0, size, 8):
        shade = random.randint(-8, 8)
        c = tuple(max(0, min(255, v + shade)) for v in (45, 38, 30))
        draw.rectangle([0, y, size, y+3], fill=c)
    
    # 沙盘区域（中心）
    pad = 60
    draw.rectangle([pad, pad, size-pad, size-pad], fill=(85, 110, 75), outline=(60, 50, 35), width=4)
    # 沙盘边框装饰
    for offset in [6, 10]:
        draw.rectangle([pad-offset, pad-offset, size-pad+offset, size-pad+offset], 
                       outline=(100, 85, 55), width=1)
    
    # 左侧文件区
    for i in range(4):
        py = 100 + i * 80
        draw.rectangle([15, py, 45, py+55], fill=COLORS["paper_bg"], outline=COLORS["ink"])
        # 模拟文字行
        for j in range(4):
            lw = random.randint(15, 28)
            draw.rectangle([18, py+8+j*10, 18+lw, py+10+j*10], fill=COLORS["ink"])
    
    # 右侧无线电
    draw.rounded_rectangle([size-80, 80, size-15, 200], radius=5, 
                           fill=(50, 50, 50), outline=(70, 70, 70))
    # 无线电指示灯
    draw_circle(draw, size-48, 120, 4, (200, 50, 50))  # 红灯
    draw_circle(draw, size-35, 120, 4, (50, 200, 50))  # 绿灯
    # 无线电旋钮
    draw_circle(draw, size-48, 160, 8, (80, 80, 80), outline=(100, 100, 100))
    # 天线
    draw.line([size-48, 80, size-30, 30], fill=(100, 100, 100), width=2)
    
    # 底部状态栏
    draw.rectangle([0, size-60, size, size], fill=(35, 30, 25))
    draw.rectangle([0, size-60, size, size-58], outline=(100, 85, 55))
    
    path = os.path.join(OUTPUT_DIR, "UI", "command_post_desk.png")
    make_dir(os.path.dirname(path))
    img.save(path)
    print("✅ 生成指挥所桌面 (1024x1024)")

# ==================== UI 元素 ====================
def generate_ui_elements():
    """生成 UI 素材"""
    ui_dir = os.path.join(OUTPUT_DIR, "UI")
    make_dir(ui_dir)
    
    # 无线电文本面板背景
    img, draw = new_img(600, 300)
    draw.rounded_rectangle([0, 0, 599, 299], radius=8, fill=COLORS["ui_bg"], outline=(60, 65, 55), width=2)
    # 屏幕效果（扫描线）
    for y in range(0, 300, 4):
        draw.line([(10, y), (590, y)], fill=(255, 255, 255, 3), width=1)
    # 标题栏
    draw.rectangle([0, 0, 599, 30], fill=(45, 50, 42))
    img.save(os.path.join(ui_dir, "radio_panel_bg.png"))
    
    # 指令卡片
    img, draw = new_img(300, 200)
    draw.rounded_rectangle([0, 0, 299, 199], radius=5, fill=COLORS["paper_bg"], outline=(180, 170, 145))
    # 标题区
    draw.rectangle([5, 5, 295, 35], fill=(160, 150, 125))
    # 模拟文字
    for i in range(6):
        draw.rectangle([15, 50+i*20, random.randint(100, 250), 52+i*20], fill=COLORS["ink"])
    img.save(os.path.join(ui_dir, "order_card.png"))
    
    # 状态便签
    img, draw = new_img(250, 180)
    draw.rounded_rectangle([0, 0, 249, 179], radius=3, fill=(255, 250, 200), outline=(200, 190, 140))
    # 弯曲效果
    draw.polygon([(230, 179), (249, 179), (249, 160)], fill=(220, 215, 170))
    img.save(os.path.join(ui_dir, "status_note.png"))
    
    # 按钮素材
    for label, color in [("btn_green", COLORS["ui_ok"]), ("btn_red", COLORS["ui_alert"]), ("btn_accent", COLORS["ui_accent"])]:
        img, draw = new_img(160, 50)
        outline_color = tuple(max(0, c-30) for c in color)
        draw.rounded_rectangle([0, 0, 159, 49], radius=6, fill=color, outline=outline_color)
        draw.rounded_rectangle([2, 2, 157, 24], radius=4, fill=(255, 255, 255, 30))
        img.save(os.path.join(ui_dir, f"{label}.png"))
    
    # 兵棋标记（沙盘上放的小棋子）
    for nation, color in [("us", (60, 90, 60)), ("de", (80, 70, 55)), ("brit", (85, 75, 50))]:
        # 步兵标记（三角形）
        img, draw = new_img(32, 32)
        outline_color = tuple(max(0, c-25) for c in color)
        draw.polygon([(16, 4), (4, 28), (28, 28)], fill=color, outline=outline_color)
        img.save(os.path.join(ui_dir, f"marker_{nation}_inf.png"))
        
        # 坦克标记（方形）
        img, draw = new_img(32, 32)
        outline_color = tuple(max(0, c-25) for c in color)
        draw.rounded_rectangle([4, 6, 28, 26], radius=3, fill=color, outline=outline_color)
        # 炮管
        draw.line([16, 6, 16, 0], fill=(40, 40, 40), width=2)
        img.save(os.path.join(ui_dir, f"marker_{nation}_tank.png"))
    
    print("✅ 生成 UI 素材")

# ==================== 效果/动画帧 ====================
def generate_effects():
    """生成简单效果"""
    eff_dir = os.path.join(OUTPUT_DIR, "Effects")
    make_dir(eff_dir)
    
    # 爆炸效果帧
    for frame in range(6):
        size = 64
        img, draw = new_img(size, size)
        cx, cy = size//2, size//2
        r = int((frame + 1) * size / 14)
        
        # 外圈（橙红色）
        alpha = max(50, 255 - frame * 40)
        draw_circle(draw, cx, cy, r, (255, 120+frame*20, 30, alpha))
        # 内圈（亮黄）
        draw_circle(draw, cx, cy, max(2, r//2), (255, 255, 150, alpha))
        # 碎片
        if frame > 1:
            for _ in range(frame * 3):
                angle = random.uniform(0, 2*math.pi)
                dist = random.uniform(r * 0.8, r * 1.5)
                px = cx + int(dist * math.cos(angle))
                py = cy + int(dist * math.sin(angle))
                if 0 <= px < size and 0 <= py < size:
                    draw.rectangle([px, py, px+2, py+2], fill=(200, 80, 20))
        
        img.save(os.path.join(eff_dir, f"explosion_{frame}.png"))
    
    # 无线电波纹
    for frame in range(4):
        size = 64
        img, draw = new_img(size, size)
        cx, cy = size//2, size//2
        for ring in range(3):
            r = 8 + ring * 8 + frame * 4
            alpha = max(20, 150 - ring * 40 - frame * 20)
            draw.arc([cx-r, cy-r, cx+r, cy+r], 0, 360, fill=(100, 200, 100, alpha), width=2)
        img.save(os.path.join(eff_dir, f"radio_wave_{frame}.png"))
    
    print("✅ 生成效果素材")

# ==================== 地图瓦片 ====================
def generate_map_tiles():
    """生成地形瓦片（256x256）"""
    tile_dir = os.path.join(OUTPUT_DIR, "Tiles")
    make_dir(tile_dir)
    size = 256
    
    tiles = {
        "grass": COLORS["grass"],
        "dirt": COLORS["dirt"],
        "water": COLORS["water"],
        "road": COLORS["road"],
    }
    
    for name, base_color in tiles.items():
        img, draw = new_img(size, size)
        draw.rectangle([0, 0, size, size], fill=base_color)
        # 噪点纹理
        for _ in range(2000):
            x, y = random.randint(0, size-1), random.randint(0, size-1)
            noise = random.randint(-12, 12)
            c = tuple(max(0, min(255, v + noise)) for v in base_color)
            draw.point((x, y), fill=c)
        img.save(os.path.join(tile_dir, f"tile_{name}.png"))
    
    print(f"✅ 生成 {len(tiles)} 个地形瓦片")

# ==================== 主入口 ====================
if __name__ == "__main__":
    print("🎨 WW2 Commander 美术素材生成器")
    print("=" * 40)
    
    generate_all_units()
    generate_sand_table()
    generate_command_post()
    generate_ui_elements()
    generate_effects()
    generate_map_tiles()
    
    print("=" * 40)
    print("✅ 全部素材生成完成！")
    print(f"📁 输出目录: {OUTPUT_DIR}")
    
    # 统计
    total = 0
    for root, dirs, files in os.walk(OUTPUT_DIR):
        total += len(files)
    print(f"📊 共生成 {total} 个文件")
