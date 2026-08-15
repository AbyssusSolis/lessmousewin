# LessMouseWin

> Windows 托盘上的快捷键教练。这是 macOS [LessMouse](../lessmouse) 的 Windows 移植版：它观察你的键盘习惯，发现低效动作——五个退格改一个词、二十个方向键跨一段话——然后告诉你能救它的快捷键。当你开始用它，LessMouseWin 会发现，并为你庆祝。

## 直接使用

无需安装任何运行时或开发工具（已发布为自包含版本）：

```text
publish\win-x64\LessMouseWin.exe
```

或者解压 `dist\LessMouseWin-win-x64-v1.0.0.zip` 后运行其中的 `LessMouseWin.exe`。

> 首次运行若出现 SmartScreen 提示（未签名应用），选择“更多信息 → 仍要运行”即可；这是未签名 Windows 应用的标准流程，与 macOS Gatekeeper 的“右键打开”对应。

程序启动后驻留系统托盘：

- **左键托盘图标**：打开主面板（今日台账、建议收件箱）。
- **右键托盘图标**：打开面板、暂停/恢复追踪、统计、设置、查看原始数据、退出。
- 面板失焦会自动收起，行为与 macOS 菜单栏 popover 一致。

## 从源码构建

需要 Windows 10/11 + [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（x64）。

```powershell
cd lessmousewin
dotnet build LessMouseWin\LessMouseWin.csproj -c Release
# 运行冒烟测试（核心管线、隐私过滤、页面渲染）
dotnet run --project smoke\Smoke.csproj -c Release
# 发布自包含 win-x64
powershell -File scripts\publish.ps1
```

## 工作原理

1. LessMouseWin 用 `WH_KEYBOARD_LL` 低级键盘钩子统计按键——**只统计**快捷键和导航键的聚合计数（见隐私）。
2. 当某个低效模式一天内重复到阈值（例如三次“连按五个以上退格”），托盘图标对应的窗口会出现绿点/“新”卡片。
3. 点开卡片：展示它看到了什么、应该按什么（Windows 键帽渲染）——而且不止一条路。
4. 下一次你按下被教的快捷键，卡片翻转为**已采纳**，主面板为你亮起庆祝横幅。进度沉淀在统计页。

## 功能一览

- **今日台账**——按键事件数、检测到的低效模式、使用的不同组合，随打字实时刷新。
- **建议收件箱**——阈值按“确凿无疑的习惯”设定；未读卡片带绿点和“新”徽章。
- **采纳检测**——看过卡片后按下被教的快捷键，庆祝横幅亮一次（每张卡片一生一次）。
- **数据统计**——近 7 天最常用快捷键排行、采纳进度、观察天数。
- **暂停追踪**——彻底卸载键盘钩子：连观察都不观察。
- **排除应用**——密码管理器、网银、任何你指定的进程；被排除的应用连看都不看。
- **登录启动**——通过 `HKCU\...\Run` 注册，开机即驻留托盘。
- **中文 / English 双语**——默认跟随 Windows 显示语言，也可在设置中手动切换。
- **一键清数据**——两次点击确认；旧数据先归档到磁盘再清空。历史自动保留 60 天。

## Windows 教学卡片（v1）

| 你的习惯 | LessMouseWin 教你 |
|---|---|
| 连着按退格删字 | `Ctrl+Backspace` 按词删除 · `Ctrl+Delete` 删后一个词 |
| ← → 逐字爬行 | `Ctrl+←/→` 按词跳转 |
| 逐字划选 | `Ctrl+Shift+←/→` 按词选择 · `Shift+End` 选到行尾 |
| 长距离 ↑/↓ | `Ctrl+Home` / `Ctrl+End` 文档首尾 |
| 大量 ←/→ 光标移动 | `Home` / `End` 行首行尾 |
| 多应用工作数日而未用过虚拟桌面 | `Win+Ctrl+←/→` 切换虚拟桌面 |
| 用鼠标切换应用 | `Alt+Tab` / `Alt+Shift+Tab` |
| 浏览器用了数日而未用过 `Ctrl+Tab` | `Ctrl+Tab` / `Ctrl+Shift+Tab` 切换标签 |

已读未采纳的卡片会冷却（3–5 天）再提醒；“不再提醒”则永远消失；采纳只庆祝一次。

## macOS → Windows 迁移对照

| 维度 | macOS LessMouse | Windows LessMouseWin |
|---|---|---|
| 驻留形态 | `MenuBarExtra` 菜单栏 | `NotifyIcon` 系统托盘 + 无边框 popup |
| 键盘监听 | CGEventTap（输入监控权限） | `WH_KEYBOARD_LL`（无需权限） |
| 安全输入守卫 | `IsSecureEventInputEnabled()` | UIA `IsPassword` 焦点检测 + Win32 `ES_PASSWORD` |
| 前台应用标识 | Bundle ID | 进程名（`chrome.exe`、`msedge.exe`…） |
| 键码 | Carbon `kVK_*` | Win32 `VK_*` |
| 自动重复 | CGEvent 的 autorepeat 字段 | 按键按下集合去重（Windows 重复按键是无标志的 WM_KEYDOWN 流） |
| 应用切换追踪 | `NSWorkspace.didActivateApplicationNotification` | `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)` |
| 登录启动 | `SMAppService.mainApp` | `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` |
| 偏好设置 | `UserDefaults` | `%APPDATA%\LessMouse\settings.json` |
| UI | SwiftUI | WPF（保留原设计语言：墨色、纸面、品牌绿 #2CDB5C） |

**Windows 特有注意点：**

- Windows 没有“输入监控”权限页；低级钩子要么安装成功，要么给出 Win32 错误。若以普通权限运行，出于 Windows 安全边界，**无法观察管理员权限进程**（如某些安装程序、任务管理器）中的按键——这是平台行为，不是 bug。
- 系统对低级钩子回调有超时限制，因此回调只做读取键码、修饰键、前台 PID 和焦点 HWND 的工作；全部业务在 WPF UI 线程异步处理。
- macOS 的 `⌘` 对应 Windows `Ctrl`，`⌥` 对应 `Alt`；macOS 独有的卡片（“Home/End 的 Mac 方式”、全局 Emacs 键）已替换为 Windows 原生教学卡片。
- UWP/商店应用统一由 `ApplicationFrameHost.exe` 承载，因此这类应用在 Windows 上无法像 macOS bundle ID 那样逐个区分；排除 `ApplicationFrameHost` 会排除所有 UWP 应用。

## 隐私——全部政策

**LessMouseWin 绝不记录你输入的内容。** 事件过滤器只有约 20 行：

被统计的只有三类聚合计数：

- **快捷键**——按着 `Ctrl`/`Alt`/`Win` 的按键（`ctrl+c`、`alt+tab`）
- **导航键**——退格、方向键、Home/End、PageUp/Down、Esc、Tab、F 区
- **低效模式**——“今日退格连击 4 次”这样的模式计数

三重防线：

| 防线 | 作用 |
|---|---|
| 签名过滤器 | 裸字母和 `Shift`+字母是文字——在任何记录发生前就被丢弃；按住不放的重复键永远不计数 |
| 安全输入守卫 | 密码框聚焦期间，LessMouseWin 什么都不观察（UIA `IsPassword` + 传统 `ES_PASSWORD` 双重检测） |
| 应用排除 | 任何进程可整体排除——被排除的应用连观察都不观察 |

所有数据在人类可读的文件里——`%APPDATA%\LessMouse\stats.json`——统计页一键打开，设置页一键清除。代码库**零网络代码、零第三方 NuGet 依赖**；欢迎审计。

## 数据位置

- `%APPDATA%\LessMouse\stats.json`——按键与模式聚合计数
- `%APPDATA%\LessMouse\suggestions.json`——卡片状态（未读/已读/已采纳/不再提醒）
- `%APPDATA%\LessMouse\settings.json`——暂停、排除应用、语言、登录启动

“清除全部数据”会先把旧文件归档为 `stats.erased-<时间戳>.json` / `suggestions.erased-<时间戳>.json` 再清空；损坏文件会归档为 `*.corrupt-<时间戳>.json` 而不是崩溃。

## 目录结构

```text
lessmousewin/
├── LessMouseWin/          # WPF 应用与全部移植后的核心逻辑
├── smoke/                 # 冒烟测试（隐私过滤、检测器、页面渲染）
├── scripts/               # build / publish / run-smoke
├── publish/win-x64/       # 已发布的自包含 x64 版本
└── dist/                  # 压缩包
```

## 许可

本仓库以根目录的 `LICENSE` 文件为准。原 macOS 项目 LessMouse 使用 MIT 许可；本移植保留原始项目的隐私承诺与设计语言。
