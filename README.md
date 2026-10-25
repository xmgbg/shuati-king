# 刷题KING

<img src="assets/logo.png" alt="刷题KING LOGO" width="160" />

刷题KING是一款使用 C# 和 WPF 开发的 Windows 桌面刷题软件，目标是让用户从 PDF、Word 或文本资料建立题库，进行日常练习和模拟考试。

## 当前进度

目前为前端原型，已完成前两步：

- 题库首页：白底顶部导航、继续练习区域、示例题库列表和可切换的题库详情。
- “我的”：昵称、头像、最多三套 AI 配置、模型变更后的重启提示、主题和题目字号。
- 连接状态预览：可演示检查中、成功、HTTP 错误和本地网络错误。**不发送真实请求，演示成功不代表服务已经连通。**

题库使用示例数据。资料解析、真实 AI 调用、题库导入导出、练习和考试将在后续步骤实现。

## 启动前端预览

在 Windows 下双击项目根目录的 `启动预览.cmd`。脚本优先使用本项目 `.tools/dotnet` 中的本地 SDK；没有本地 SDK 时，需要安装 .NET 10 SDK。

也可在项目根目录执行：

```powershell
dotnet run --project src/ShuatiKing.App
```

个人设置保存在 `%LOCALAPPDATA%\ShuatiKing\settings.dat`，使用 Windows 当前用户加密。昵称和头像点击保存后生效；主题与字号选择后自动保存。模型变更后可关闭软件再打开，无需立即重启电脑。

## 构建与界面校验

```powershell
dotnet build src/ShuatiKing.App
dotnet run --project tests/ShuatiKing.Checks
```

校验程序使用独立的临时设置文件，检查题库切换、个人设置恢复、三套配置上限、重启提示和连接预览状态，并将实际 XAML 渲染图输出到被 Git 忽略的 `.artifacts` 目录。

## 设计基准

界面遵循 [C2 白底导航工作台](design-previews/C2-白底导航工作台.png)：蓝白配色、顶部导航、左侧题库列表和右侧详情。
