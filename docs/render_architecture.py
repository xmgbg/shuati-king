"""生成 README 使用的架构 PNG。依赖 Pillow，字体使用 Windows 微软雅黑。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
SCALE = 2
image = Image.new('RGB', (1800 * SCALE, 1120 * SCALE), '#F5F8FE')
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


text(60, 44, '刷题KING · 系统架构', 43, bold=True)
text(60, 112, 'WPF 桌面应用 / 题库、练习与考试 / 本地数据', 24, MUTED)

for x in (320, 900, 1480):
    arrow([(900, 292), (900, 322), (x, 322), (x, 356)])
    arrow([(x, 506), (x, 566)])
arrow([(900, 716), (900, 776)])
arrow([(1480, 716), (1480, 776)])

box(560, 180, 680, 112, 'App.xaml / MainWindow', ['应用启动 · 顶部导航 · 页面切换'], primary=True)
box(60, 356, 520, 150, '题库与审核页面', ['LibraryView / ImportView', 'ReviewView / PackageDialog'])
box(640, 356, 520, 150, '练习、考试与历史页面', ['PracticeView / WrongBookView', 'ExamView / ExamResultView / ExamHistoryView'])
box(1220, 356, 520, 150, '个人资料与设置页面', ['ProfileView / AiSettingsView', 'ThemeManager：主题资源'])

box(60, 566, 520, 150, '题库与题目模型', ['SampleLibrary / QuestionDraft', '审核校验 · 选项、答案与解析'])
box(640, 566, 520, 150, '学习状态与规则', ['LearningState / LearningRules', '作答 · 判分 · 错题 · 考试快照'])
box(1220, 566, 520, 150, '个人资料与 AI 配置', ['LocalSettings / AiConfiguration', '昵称 · 头像 · 主题 · 模型配置'])

box(640, 776, 520, 168, 'LearningStore', ['JSON 本地读写', '练习进度、错题与考试历史', 'settings.learning.json'])
box(1220, 776, 520, 168, 'SettingsStore', ['JSON 序列化 + Windows DPAPI', '当前 Windows 用户范围加密', 'settings.dat'])
text(320, 804, '流程演示', 27, bold=True, center=True)
text(320, 853, 'WorkflowPreview / ConnectionPreview', 21, MUTED, center=True)
text(320, 895, '尚未接入真实文档解析与 AI 服务', 22, MUTED, center=True)
text(60, 1010, r'本地数据目录：%LOCALAPPDATA%\ShuatiKing', 25, bold=True)
text(60, 1062, '题库使用示例数据；学习记录与个人设置分别保存', 22, MUTED)

image.resize((1800, 1120), Image.Resampling.LANCZOS).save(ROOT / 'assets' / 'architecture.png', optimize=True)
