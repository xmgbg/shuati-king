"""生成 README 使用的架构 PNG。依赖 Pillow，字体使用 Windows 微软雅黑。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SCALE = 2
image = Image.new('RGB', (1800 * SCALE, 1210 * SCALE), '#F5F8FE')
draw = ImageDraw.Draw(image)
BLUE, INK, MUTED, LINE = '#075AF5', '#182844', '#52647D', '#D8E3F2'


def font(size, bold=False):
    return ImageFont.truetype('C:/Windows/Fonts/' + ('msyhbd.ttc' if bold else 'msyh.ttc'), size * SCALE)


def text(x, y, value, size=24, color=INK, bold=False, center=False):
    draw.text((x * SCALE, y * SCALE), value, fill=color, font=font(size, bold), anchor='mt' if center else 'lt')


def box(x, y, width, height, title, lines, primary=False):
    rect = tuple(v * SCALE for v in (x, y, x + width, y + height))
    draw.rounded_rectangle(rect, radius=14 * SCALE, fill=BLUE if primary else 'white', outline=BLUE if primary else LINE, width=2 * SCALE)
    text(x + width / 2, y + 22, title, 29, 'white' if primary else INK, True, True)
    for i, line in enumerate(lines):
        text(x + width / 2, y + 68 + i * 32, line, 22, '#EAF2FF' if primary else MUTED, center=True)


def arrow(points):
    points = [(x * SCALE, y * SCALE) for x, y in points]
    draw.line(points, fill='#8FAAD3', width=3 * SCALE, joint='curve')
    (x0, y0), (x1, y1) = points[-2:]
    if y1 > y0:
        head = [(x1, y1), (x1 - 7 * SCALE, y1 - 12 * SCALE), (x1 + 7 * SCALE, y1 - 12 * SCALE)]
    elif x1 > x0:
        head = [(x1, y1), (x1 - 12 * SCALE, y1 - 7 * SCALE), (x1 - 12 * SCALE, y1 + 7 * SCALE)]
    else:
        raise ValueError('箭头仅使用向下或向右方向')
    draw.polygon(head, fill='#8FAAD3')


text(60, 44, '刷题KING · 应用架构', 43, bold=True)
text(60, 112, 'Windows 桌面应用 · 页面、状态与服务的主要关系', 24, MUTED)

arrow([(900, 292), (900, 326), (320, 326), (320, 360)])
arrow([(900, 292), (900, 360)])
arrow([(1160, 432), (1220, 432)])
arrow([(320, 506), (320, 590)])
arrow([(900, 506), (900, 590)])
arrow([(1480, 506), (1480, 590)])
arrow([(1300, 506), (1300, 548), (1060, 548), (1060, 590)])
arrow([(900, 722), (900, 770), (620, 770), (620, 822)])
arrow([(900, 722), (900, 770), (1210, 770), (1210, 822)])
arrow([(1210, 956), (1210, 1028)])

box(560, 180, 680, 112, 'App.xaml  /  MainWindow', ['应用启动 · 顶部导航 · 页面切换'], primary=True)
box(60, 360, 520, 146, '题库首页 · LibraryView', ['示例题库列表 / 所选题库详情', '继续练习区域'])
box(640, 360, 520, 146, '我的 · ProfileView', ['昵称 / 头像 / 个性化设置', '承载 AI 配置页面'])
box(1220, 360, 520, 146, 'AI 配置 · AiSettingsView', ['三套配置 / 当前选择', '模型重启提示 / 连接状态'])
box(60, 590, 520, 132, 'SampleLibrary', ['示例题库模型', '题型数量 / 来源 / 审核状态'])
box(640, 590, 520, 132, 'LocalSettings / AiConfiguration', ['个人资料 / 主题 / 题目字号', '服务地址 / 密钥 / 模型'])
box(1220, 590, 520, 132, 'ConnectionPreview', ['连接反馈与错误码演示', '不发起网络请求'])
box(380, 822, 480, 134, 'ThemeManager', ['主题资源切换', '浅色 / 深色 / 跟随系统'])
box(940, 822, 540, 134, 'SettingsStore', ['JSON 序列化与读写', 'Windows DPAPI 当前用户加密'])
box(760, 1028, 900, 132, '本机设置文件 · settings.dat', [r'%LOCALAPPDATA%\ShuatiKing', '保存个人资料、偏好与 AI 配置'])
text(60, 1136, '架构对应仓库现有实现', 22, MUTED)

image.resize((1800, 1210), Image.Resampling.LANCZOS).save(ROOT / 'assets' / 'architecture.png', optimize=True)
