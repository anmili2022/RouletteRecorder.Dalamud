# 日随伴侣

## 中文

### 简介

日随伴侣是 [RouletteRecorder](https://github.com/StarHeartHunt/RouletteRecorder) 的 Dalamud 插件版本，用于自动记录《FINAL FANTASY XIV》的每日随机任务，包括指导者随机任务。

### 交接与维护文档

- [日随伴侣交接文档](docs/HANDOFF.md)
- [日随伴侣发布流程](docs/RELEASE.md)

### 安装

1. 打开 Dalamud 插件安装器。
2. 将以下地址添加到插件仓库：

   ```text
   https://raw.githubusercontent.com/anmili2022/RouletteRecorder.Dalamud/refs/heads/master/repo.json
   ```

3. 在插件安装器中搜索 `日随伴侣` 并安装。
4. 可通过插件安装器打开主界面，也可以在游戏聊天框中输入：

   ```text
   /prr
   ```

### 使用

1. 输入 `/prr` 打开悬浮窗。
2. 经典样式的悬浮窗包含两个页签：
   - `当前任务`：显示当前任务类型、任务名称、任务进行时长、今日导随任务次数以及当前角色导随任务次数。
   - `历史任务`：显示已记录任务的任务名称、任务类型、时长、开始时间和结束时间。
3. 右键点击悬浮窗可打开设置窗口。
4. 点击 `设置`。
5. 如需调整悬浮窗外观，展开 `悬浮窗样式`：
   - `经典样式`：当前完整样式，保留两个页签和历史任务表格。
   - `极简样式`：更紧凑的样式，不显示窗口标题、页签、设置按钮和导出按钮，仅保留当前任务核心信息；右键点击悬浮窗可打开设置窗口。
   - 可通过 `悬浮窗透明度` 调整悬浮窗背景透明度。
   - 可通过 `锁定悬浮窗` 旁边的 `开启悬浮窗` 选项显示或隐藏主悬浮窗。
   - `悬浮窗显示项` 同时影响经典样式和极简样式：`当前任务` 控制任务类型和任务名称；`任务时间` 控制任务时长、开始时间和是否完成；`今日导随任务次数` 与 `导随任务总次数` 分别控制对应计数显示。极简样式中的今日导随任务次数会以黄色强调显示。
6. 如需使用个人便签，展开 `个人便签`：
   - 可通过 `/prr bq` 或 `开启便签窗口` 打开/关闭便签悬浮窗。
   - `公共便签` 对所有角色可见。
   - `角色便签` 按当前角色名和服务器分别保存，只有对应角色可见。
   - 便签内容会在多行输入框内容变化时自动保存。
   - 便签背景可选择 `磨砂背景` 或 `透明背景`。
   - `磨砂背景` 可调整磨砂强度和窗口透明度；`透明背景` 可单独调整窗口透明度。
7. 展开 `Subscribed Roulette Types`。
8. 选择需要订阅并记录的随机任务类型。
9. 之后正常排本即可，插件会自动记录符合条件的随机任务。

> 当前角色导随任务次数会从成就进度中读取，使用的是指导者随机任务 2000 次成就进度。首次打开或手动刷新时可能需要等待游戏返回成就数据。
> 今日导随任务次数会从本插件历史记录中统计，统计范围为今天已记录且已完成的指导者随机任务。

### 开发

#### 前置条件

开发和构建本插件前，请确保已满足以下条件：

- 已安装 XIVLauncher、FINAL FANTASY XIV 和 Dalamud。
- 已至少通过 XIVLauncher 启动过一次游戏，并成功加载 Dalamud。
- XIVLauncher 使用默认安装路径和默认配置。
  - 如果 Dalamud 开发目录不在默认路径，请设置 `DALAMUD_HOME` 环境变量。
- 已安装与当前 Dalamud 开发环境兼容的 .NET SDK。
  - 当前项目使用 `Dalamud.NET.Sdk/15.0.0`，本地构建目标为 `.NET 10`。

#### 构建

可以使用 Visual Studio 2022、JetBrains Rider 或命令行构建项目。

命令行构建：

```powershell
dotnet build
```

构建输出目录已改为仓库根目录下的 `output`：

```text
output/RouletteRecorder.Dalamud.dll
```

如果使用 `Release` 配置：

```powershell
dotnet build -c Release
```

生成的插件文件同样会输出到：

```text
output/RouletteRecorder.Dalamud.dll
```

#### 在游戏中加载开发插件

1. 启动游戏。
2. 在聊天框输入 `/xlsettings`，或在 Dalamud Console 中输入 `xlsettings`，打开 Dalamud 设置。
3. 进入 `Experimental`。
4. 将以下插件 DLL 的完整路径添加到 Dev Plugin Locations：

   ```text
   <仓库路径>/output/RouletteRecorder.Dalamud.dll
   ```

   例如：

   ```text
   E:/git/RouletteRecorder.Dalamud/output/RouletteRecorder.Dalamud.dll
   ```

5. 在聊天框输入 `/xlplugins`，或在 Dalamud Console 中输入 `xlplugins`，打开插件安装器。
6. 进入 `Dev Tools > Installed Dev Plugins`。
7. 找到并启用 `日随伴侣`。
8. 启用后可通过 `/prr` 打开插件界面。

> Dev Plugin Locations 通常只需要添加一次，Dalamud 会保存该设置。之后可以在插件安装器中启用、禁用或设置是否随游戏启动加载。

### 常用命令

```text
/prr
```

打开或关闭日随伴侣悬浮窗。

```text
/prr cfg
```

打开日随伴侣设置面板。

```text
/prr bq
```

打开或关闭个人便签悬浮窗。

---

## English

### Introduction

Daily Roulette Companion is the Dalamud plugin version of [RouletteRecorder](https://github.com/StarHeartHunt/RouletteRecorder). It automatically records your daily roulettes in FINAL FANTASY XIV, including mentor roulettes.

### Handoff and maintenance

- [日随伴侣 Handoff Document](docs/HANDOFF.md)
- [日随伴侣 Release Guide](docs/RELEASE.md)

### Installation

1. Open the Dalamud Plugin Installer.
2. Add the following URL to your custom plugin repositories:

   ```text
   https://raw.githubusercontent.com/anmili2022/RouletteRecorder.Dalamud/refs/heads/master/repo.json
   ```

3. Search for `日随伴侣` in the Plugin Installer and install it.
4. You can open the main UI from the Plugin Installer or by typing the following command in chat:

   ```text
   /prr
   ```

### Usage

1. Type `/prr` to open the floating window.
2. In Classic Style, the floating window contains two tabs:
   - `Current Task`: shows the current task type, duty name, elapsed task duration, today's mentor roulette count, and the current character's mentor roulette count.
   - `History Tasks`: shows recorded tasks with duty name, task type, duration, start time, and end time.
3. Right-click the floating window to open settings.
4. Click `设置` (`Settings`).
5. To adjust the floating window appearance, expand `Floating Window Style`:
   - `Classic Style`: the current full layout with both tabs and the history task table.
   - `Minimal Style`: a more compact layout without the window title, tabs, settings button, or CSV export button; it keeps only the core current-task information. Right-click the floating window to open settings.
   - Use `悬浮窗透明度` (`Floating Window Opacity`) to adjust the floating window background opacity.
   - Use `开启悬浮窗` (`Enable Floating Window`) next to `锁定悬浮窗` (`Lock Floating Window`) to show or hide the main floating window.
   - `悬浮窗显示项` (`Floating Window Display Items`) affects both Classic Style and Minimal Style: `当前任务` (`Current Task`) controls the task type and duty name; `任务时间` (`Task Time`) controls task duration, start time, and completed status; `今日导随任务次数` (`Today Mentor Roulette Count`) and `导随任务总次数` (`Mentor Roulette Total Count`) control their corresponding counters. In Minimal Style, today's mentor roulette count is highlighted in yellow.
6. To use personal notes, expand `个人便签` (`Personal Note`):
   - Use `/prr bq` or `开启便签窗口` (`Enable Note Window`) to open or close the note floating window.
   - `公共便签` (`Public Note`) is visible to all characters.
   - `角色便签` (`Character Note`) is saved by current character name and world, and is only visible to that character.
   - Note content is saved automatically when the multiline input content changes.
   - The note background can be set to `磨砂背景` (`Frosted Background`) or `透明背景` (`Transparent Background`).
   - `磨砂背景` (`Frosted Background`) supports frosted strength and window opacity adjustment; `透明背景` (`Transparent Background`) has its own opacity adjustment.
7. Expand `Subscribed Roulette Types`.
8. Select the roulette types you want to subscribe to and record.
9. Queue normally; the plugin will automatically record matching roulettes.

> The current character's mentor roulette count is read from achievement progress, using the 2,000 mentor roulettes achievement. When opening the window for the first time or refreshing manually, it may take a moment for the game to return achievement data.
> Today's mentor roulette count is calculated from this plugin's history records, counting completed mentor roulettes recorded today.

### Development

#### Prerequisites

Before developing or building this plugin, make sure the following requirements are met:

- XIVLauncher, FINAL FANTASY XIV, and Dalamud are installed.
- The game has been launched through XIVLauncher at least once, and Dalamud has been loaded successfully.
- XIVLauncher is installed with its default paths and configuration.
  - If your Dalamud development directory is in a custom location, set the `DALAMUD_HOME` environment variable.
- A .NET SDK compatible with the current Dalamud development environment is installed.
  - This project uses `Dalamud.NET.Sdk/15.0.0`, and the local build target is `.NET 10`.

#### Building

You can build the project with Visual Studio 2022, JetBrains Rider, or the command line.

Command-line build:

```powershell
dotnet build
```

The build output directory has been changed to `output` in the repository root:

```text
output/RouletteRecorder.Dalamud.dll
```

For a `Release` build:

```powershell
dotnet build -c Release
```

The generated plugin file will also be written to:

```text
output/RouletteRecorder.Dalamud.dll
```

#### Loading the development plugin in-game

1. Launch the game.
2. Type `/xlsettings` in chat, or `xlsettings` in the Dalamud Console, to open Dalamud settings.
3. Go to `Experimental`.
4. Add the full path of the following plugin DLL to Dev Plugin Locations:

   ```text
   <repository path>/output/RouletteRecorder.Dalamud.dll
   ```

   Example:

   ```text
   E:/git/RouletteRecorder.Dalamud/output/RouletteRecorder.Dalamud.dll
   ```

5. Type `/xlplugins` in chat, or `xlplugins` in the Dalamud Console, to open the Plugin Installer.
6. Go to `Dev Tools > Installed Dev Plugins`.
7. Find and enable `日随伴侣`.
8. After enabling it, use `/prr` to open the plugin UI.

> Dev Plugin Locations usually only need to be added once. Dalamud will keep the setting. After that, you can enable, disable, or configure startup loading from the Plugin Installer.

### Common command

```text
/prr
```

Open or close the 日随伴侣 floating window.

```text
/prr cfg
```

Open the 日随伴侣 settings panel.

```text
/prr bq
```

Open or close the personal note floating window.
