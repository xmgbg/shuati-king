<div align="center">
  <img src="assets/logo-animated.gif" width="112" alt="刷题KING Logo" />
  <h1>刷题KING</h1>
  <p><strong>从学习资料到日常练习，打造自己的桌面题库。</strong></p>
  <p>题库管理 · 四种题型 · 错题复习 · 模拟考试</p>
  <p>
    <img src="https://img.shields.io/badge/C%23-512BD4?style=flat-square" alt="C#" />
    <img src="https://img.shields.io/badge/.NET-10-512BD4?style=flat-square" alt=".NET 10" />
    <img src="https://img.shields.io/badge/WPF-075AF5?style=flat-square" alt="WPF" />
    <a href="LICENSE"><img src="https://img.shields.io/badge/License-Apache--2.0-19704F?style=flat-square" alt="Apache License 2.0" /></a>
  </p>
  <p><a href="#界面一览">界面一览</a> · <a href="#系统架构">系统架构</a> · <a href="#技术栈">技术栈</a> · <a href="#快速启动">快速启动</a></p>
</div>

## 项目介绍

刷题KING是一款基于 C#、.NET 10 和 WPF 开发的 Windows 桌面刷题软件，围绕资料建库、题目审核、日常练习、模拟考试与错题复习组织学习流程。无需注册账号，个人设置与学习记录保存在本机。

项目面向 PDF、Word 和文字资料建库场景。目前可基于示例题库体验练习、考试和历史回看；文件内容提取、AI 出题、AI 连接检查及题库包导入导出仍为流程演示，尚未接入真实文档解析或模型服务。

## 核心功能

| 功能 | 内容 |
| --- | --- |
| 题库管理 | 查看资料来源、题型数量与审核状态，切换题库并继续上次练习 |
| 题目审核 | 编辑题干、选项、答案与解析，支持填空可接受答案、批量确认和缺答案校验 |
| 日常练习 | 顺序练习、随机练习、错题重练，支持单选、多选、判断与填空 |
| 错题复习 | 练习和考试答错后加入错题本，之后答对一次自动移出 |
| 模拟考试 | 勾选题型、拖动排序，设置题量、每题分值和时长，支持暂停恢复与到时交卷 |
| 考试记录 | 查看成绩、用时和逐题解析，保留考试当时的题目、答案与评分 |
| 个人设置 | 昵称与头像、三套 AI 配置、浅色／深色／跟随系统主题和题目字号 |

## 界面一览

### 题库首页

![刷题KING题库首页，展示题库列表、题型详情和继续练习入口](assets/preview-home.png)

## 系统架构

![刷题KING系统架构：WPF 页面、题库模型、学习规则与本地存储](assets/architecture.png)

主窗口负责导航和页面切换。题库与审核页面操作题库模型，练习、考试及历史页面共享学习状态与评分规则；学习记录由 `LearningStore` 保存，个人资料和 AI 配置由 `SettingsStore` 加密保存。

架构图源文件：[docs/render_architecture.py](docs/render_architecture.py)。

## 技术栈

<table>
  <tr>
    <td align="center" width="33%">
      <img src="https://img.shields.io/badge/C%23-512BD4?style=for-the-badge" alt="C#" /><br />
      <strong>业务逻辑</strong><br />题目校验、练习状态与考试评分
    </td>
    <td align="center" width="33%">
      <img src="https://img.shields.io/badge/.NET-10-512BD4?style=for-the-badge" alt=".NET 10" /><br />
      <strong>运行平台</strong><br />Windows 桌面运行与单文件发布
    </td>
    <td align="center" width="33%">
      <img src="https://img.shields.io/badge/WPF_/_XAML-075AF5?style=for-the-badge" alt="WPF / XAML" /><br />
      <strong>桌面界面</strong><br />页面布局、控件交互与主题资源
    </td>
  </tr>
</table>

本地数据使用 `System.Text.Json` 序列化；个人资料与 AI 配置通过 Windows DPAPI 加密。界面和交互校验使用 WPF 校验项目运行。

## 目录结构

```text
shuati-king/
├─ src/                         # 桌面应用源码
│  ├─ App.xaml                  # 应用入口与全局资源
│  ├─ MainWindow.xaml            # 主窗口与顶部导航
│  ├─ Models/                    # 题库、题目、设置与学习状态
│  ├─ Services/                  # 评分规则、存储、主题与流程演示
│  ├─ Styles/                    # 控件样式和主题资源
│  ├─ Views/                     # 题库、审核、练习、考试与个人页面
│  ├─ Properties/PublishProfiles/
│  │  └─ Portable.pubxml         # 自带运行时的单文件发布配置
│  └─ ShuatiKing.App.csproj
├─ tests/                       # 模型、页面交互与学习流程校验
├─ assets/                      # LOGO、界面截图与架构图
├─ docs/                        # 架构图绘图源文件
├─ design-previews/             # 界面设计参考
├─ Directory.Build.props        # 统一构建产物路径
├─ README.md
└─ LICENSE
```

构建产物集中保存在 `.artifacts/`；临时草稿、截图和辅助脚本集中保存在 `.scratch/`，按用途分二级目录。这两个目录均不进入 Git。

## 快速启动

### 从源码运行

开发环境：Windows、.NET 10 SDK，支持 Windows 桌面开发。

在终端执行：

```powershell
git clone https://github.com/xmgbg/shuati-king.git
cd shuati-king
dotnet run --project src
```

### 发布 EXE

在项目根目录执行：

```powershell
dotnet publish src -p:PublishProfile=Portable
```

输出位置：

```text
.artifacts/app/ShuatiKing.exe
```

发布包面向 Windows x64，自带 .NET 运行时，可直接双击运行，无需另装 .NET Desktop Runtime。后续发布会更新同一位置的 EXE，更新前请先关闭正在运行的程序。

### 构建与校验

```powershell
# Release 构建
dotnet build src --configuration Release

# 运行模型与界面交互校验
dotnet run --project tests
```

校验使用独立临时数据，结束后自动清理。默认不生成截图；需要检查界面时执行：

```powershell
dotnet run --project tests -- --screenshots
```

截图统一输出至 `.scratch/screenshots/`。编译文件与中间文件分别位于 `.artifacts/bin/` 和 `.artifacts/obj/`。

## 本地数据

默认保存位置为 `%LOCALAPPDATA%\ShuatiKing\`：

| 文件 | 保存内容 |
| --- | --- |
| `settings.dat` | 昵称、头像、主题、题目字号和 AI 配置；JSON 序列化后使用 DPAPI 加密 |
| `settings.learning.json` | 练习进度、错题、当前考试与历史考试记录；以 JSON 保存 |

`settings.dat` 绑定当前 Windows 用户，直接复制到其他电脑或用户下不能解密。学习记录文件不采用同样的加密方式。

## 开源协议

项目采用 [Apache License 2.0](LICENSE)。
