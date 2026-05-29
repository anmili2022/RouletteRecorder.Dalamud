# 日随伴侣交接文档

> 最后更新：2026-05-24
> 项目路径：`E:\git\RouletteBuddy`
> 当前分支：`master`
> 插件名称：`日随伴侣`
> 内部名：`RouletteBuddy`
> 当前版本：`1.0.6.0`
> 当前发布页：\`https://github.com/anmili2022/RouletteRecorder.Dalamud/releases/tag/v1.0.6.0\`

## 1. 接手先看

进入项目后先读：

```powershell
Get-Content -Encoding UTF8 README.md
```

然后按任务需要看：

```text
docs/HANDOFF.md    当前交接文档
docs/RELEASE.md    下次快速发布流程
```

本项目中文文件较多，用 PowerShell 读取时优先带上：

```powershell
Get-Content -Encoding UTF8 <file>
```

卫月 / Dalamud API 文档：

```text
https://dalamud.dev/api/
```

## 2. 项目概述

日随伴侣是 [RouletteRecorder](https://github.com/StarHeartHunt/RouletteRecorder) 的 Dalamud 插件版本，用于自动记录《FINAL FANTASY XIV》的每日随机任务，包括指导者随机任务。

当前版本重点能力：

- 自动识别随机任务弹出。
- 自动记录任务类型、任务名称、开始时间、结束时间、是否完成。
- `data.json` 保存订阅任务记录。
- `task_history.json` 保存历史任务记录；首次升级时会从旧 `risui.json` 自动复制迁移。
- 支持每日、每周任务监控 Tips。
- 支持周常任务：天书奇谈、团本（第一 / 第二 / 第三巡行）、幻巧战、零式重量级 1-4。
- 支持标准随机任务完成状态读取。
- 支持水晶冲突练习赛、段位赛完成状态记录与判断。
- 支持角色名和服务器名区分。
- 支持经典样式和极简样式悬浮窗。
- 支持悬浮窗锁定、穿透、透明度、显示项开关。
- 支持当前时间显示。
- 支持今日导随次数和导随总次数显示。
- 支持个人便签悬浮窗，可在公共便签和角色便签之间切换，内容变化自动保存，并支持 D3D11 真实磨砂背景 / 透明背景；磨砂强度和窗口透明度可调。
- 设置窗口底部可查看“订阅任务记录”和“历史任务记录”。
- 支持 CSV 导出。
- 支持 DungeonLogger 上报配置。

## 3. 当前技术环境

### 3.1 Dalamud / API

当前项目按 Dalamud API 15 构建：

```xml
<Project Sdk="Dalamud.NET.Sdk/15.0.0">
```

本地国服 Dalamud 开发 DLL 目录通常为：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\addon\Hooks\dev\
```

当前本地构建目标为 `.NET 10`。

### 3.2 个人便签 D3D11 磨砂背景依赖

本轮新增个人便签真实磨砂背景，方案参考自：

```text
E:\git\ARH
```

当前实现已从最初移植的 TerraFX 版本改为 Vortice 版本，目的为保留 D3D11 真实磨砂效果，同时显著降低发布包体积。

核心实现依赖：

```xml
<AllowUnsafeBlocks>true</AllowUnsafeBlocks>
<PackageReference Include="Vortice.Direct3D11" Version="3.8.3" />
<PackageReference Include="Vortice.DXGI" Version="3.8.3" />
```

实际发布包还会带上 Vortice 的传递依赖：

```text
Vortice.DirectX.dll
Vortice.Mathematics.dll
SharpGen.Runtime.dll
SharpGen.Runtime.COM.dll
```

以及嵌入式计算着色器：

```text
RouletteBuddy/Shaders/AlphaFix.cso
RouletteBuddy/Shaders/HBlur.cso
RouletteBuddy/Shaders/VBlur.cso
```

注意：

- 这些 `.cso` 文件是嵌入资源，不会以独立文件出现在发布 zip 中。
- 不再依赖 `TerraFX.Interop.Windows`；如果输出目录残留旧的 `TerraFX.Interop.Windows.dll`，它会被 packager 误打进 zip，需要手动删除后重新构建。
- 如果后续移除真实磨砂背景，需要同步移除 `AllowUnsafeBlocks`、Vortice 相关依赖、`Helpers/CleanBackgroundManager.cs` 和 `Shaders/*.cso`。

### 3.3 常用构建命令

Debug / 默认构建：

```powershell
dotnet build
```

Release 构建：

```powershell
dotnet build -c Release
```

当前输出目录：

```text
output/
```

关键输出：

```text
output/RouletteBuddy.dll
output/RouletteBuddy.json
output/RouletteBuddy/latest.zip
output/Vortice.Direct3D11.dll
output/Vortice.DXGI.dll
output/Vortice.DirectX.dll
output/Vortice.Mathematics.dll
output/SharpGen.Runtime.dll
output/SharpGen.Runtime.COM.dll
```

注意：发布时使用的是：

```text
output/RouletteBuddy/latest.zip
```

不要误用旧的：

```text
output/latest.zip
```

## 4. 插件清单和发布信息

项目版本在：

```text
RouletteBuddy/RouletteBuddy.csproj
```

当前版本字段：

```xml
<Version>1.0.6.0</Version>
<AssemblyVersion>1.0.6.0</AssemblyVersion>
<FileVersion>1.0.6.0</FileVersion>
<InformationalVersion>1.0.6.0</InformationalVersion>
```

仓库清单：

```text
repo.json
```

当前关键 manifest 字段应为：

```json
{
  "Name": "日随伴侣",
  "InternalName": "RouletteBuddy",
  "AssemblyVersion": "1.0.6.0",
  "DalamudApiLevel": 15
}
```

构建后处理脚本：

```text
RouletteBuddy/Build/LocalizeOutputManifest.ps1
```

它负责：

- 把输出 manifest 的 `Name` 固定为 `日随伴侣`。
- 把输出 manifest 的 `InternalName` 固定为 `RouletteBuddy`。
- 写入中英双语 `Description` / `Punchline` / `Tags`。
- 重新打包 `output/RouletteBuddy/latest.zip`，保证 zip 内 manifest 也是中文内部名。
- 不再硬编码 `AssemblyVersion`，版本以 `.csproj` 为准。

## 5. 数据文件

插件配置目录由 Dalamud 提供：

```csharp
Plugin.PluginInterface.ConfigDirectory.FullName
```

常见本地路径：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\RouletteBuddy\
```

### 5.1 data.json

路径：

```text
data.json
```

用途：

- 保存历史记录。
- 只保存用户订阅的随机任务。
- 主要用于历史任务、今日导随次数、CSV 导出。
- 设置窗口底部折叠菜单显示名为“订阅任务记录”。

对应模型：

```text
RouletteBuddy/DAO/Roulette.cs
```

### 5.2 task_history.json

路径：

```text
task_history.json
```

用途：

- 保存另一份历史任务记录。
- 无需订阅即可保存所有标准随机任务。
- 额外保存水晶冲突练习赛、水晶冲突段位赛、每周监控任务记录。
- 用于 Tips 的完成状态回退判断。
- 记录角色名和服务器名，避免同账号多角色串数据。
- 设置窗口底部折叠菜单显示名为“历史任务记录”。
- 运行中会记录文件最后修改时间；判断完成状态前会调用 `ReloadTaskHistoryIfChanged()`，发现 `task_history.json` 被外部修改后会重新读入内存。
- 旧文件 `risui.json` 仅作为兼容迁移来源保留：如果 `task_history.json` 不存在但旧 `risui.json` 存在，会复制一份到 `task_history.json`；不会删除旧文件。

对应模型：

```text
RouletteBuddy/DAO/TaskHistoryRoulette.cs
```

`TaskHistoryRoulette` 相比 `Roulette` 多出：

```csharp
public string? PlayerName { get; set; }
public string? World { get; set; }
public string? MonitorTaskKey { get; set; }
```

保存时从：

```csharp
Plugin.GetPlayerName()
Plugin.GetPlayerWorldName()
```

读取。

## 6. 每日 / 每周任务监控和 Tips

核心文件：

```text
RouletteBuddy/Utils/Database.cs
RouletteBuddy/Windows/MainWindow.cs
RouletteBuddy/Windows/ConfigWindow.cs
```

### 6.1 每日任务监控选项

每日任务监控配置项：

```csharp
public bool DefaultDailyTaskMonitorInitialized { get; set; }
public bool DefaultDailyUtilityTaskMonitorInitialized { get; set; }
public HashSet<string> MonitoredDailyTaskKeys { get; set; }
public int TribalQuestCompletionCount { get; set; }
```

当前每日任务选项来自：

```csharp
Database.GetDailyTaskMonitorOptions()
```

排序规则：

- 标准随机任务按游戏内 `ContentRoulette.SortKey` 排序。
- 相同排序再按 `RowId` 排序。
- 水晶冲突练习赛 / 段位赛按游戏内随机任务顺序合并显示。

注意：

- 不要把 `ContentFinderCondition` 里的水晶冲突地图条目直接放进每日任务监控。
- 用户只需要：

```text
水晶冲突练习赛
水晶冲突段位赛
```

不需要：

```text
火山之心
角力学校
九霄云上
机关大殿
赤土红沙
```

每日实用任务：

```csharp
Database.GetDailyUtilityTaskMonitorOptions()
```

当前包括：

```text
蛮族任务次数
```

完成阈值来自：

```csharp
Plugin.Configuration.TribalQuestCompletionCount
```

### 6.2 每周任务监控选项

每周任务监控配置项：

```csharp
public bool DefaultWeeklyTaskMonitorInitialized { get; set; }
public HashSet<string> MonitoredWeeklyTaskKeys { get; set; }
```

每周任务来自：

```csharp
Database.GetWeeklyTaskMonitorOptions()
```

当前包括：

| Key | 显示名 | 状态来源 |
| --- | --- | --- |
| `weekly:wondrousTails` | 天书奇谈 | `PlayerState` 的天书贴纸数 |
| `weekly:currentAllianceRaid:1` | 团本第一巡行 | 只看 `task_history.json` 本周过本记录 |
| `weekly:currentAllianceRaid:2` | 团本第二巡行 | 只看 `task_history.json` 本周过本记录 |
| `weekly:currentAllianceRaid:3` | 团本第三巡行 | 只看 `task_history.json` 本周过本记录 |
| `weekly:unrealTrial` | 幻巧战（神龙幻巧战） | 优先任务搜索器文本 / `PlayerState` 幻巧状态；无法判断时显示未知 |
| `weekly:savageRaid:1` | 零式重量级1 | 优先任务搜索器奖励/锁定状态；回退 `task_history.json` |
| `weekly:savageRaid:2` | 零式重量级2 | 同上 |
| `weekly:savageRaid:3` | 零式重量级3 | 同上 |
| `weekly:savageRaid:4` | 零式重量级4 | 同上 |

兼容说明：旧的 `weekly:currentAllianceRaid` 仅保留给历史兼容和旧配置迁移，UI 不再显示。

重要：用户已明确要求“团本取消 UI 读取，只以历史记录为准”。当前实现：

```csharp
private static MonitorTaskStatus GetCurrentAllianceRaidStatus(string taskKey, string taskName)
{
    return ToRecordedThisWeekStatus(IsCurrentAllianceRaidCompletedInCurrentResetCycle());
}
```

因此旧的团本总项 Tips 不再读任务搜索器 UI / Agent / `NumCollectedRewards`。
第一巡行、第二巡行、第三巡行都会进入历史记录，并分别统计完成状态；旧的团本总项仍然保留第三巡行完成判定作为兼容逻辑。

周常刷新周期：

```text
每周二 16:00
```

对应代码：

```csharp
private const int WeeklyResetDay = (int)DayOfWeek.Tuesday;
private const int WeeklyResetHour = 16;
```

### 6.3 Tips 显示内容

Tips 顶部显示黄色文字：

```text
日随伴侣
```

任务状态颜色：

| 状态 | 颜色 |
| --- | --- |
| 已完成 | 白色 |
| 未完成 | 红色 |
| 未知 | 灰色 |

Tips 会按分组显示已勾选任务：

- 每日任务
- 每周任务
- 零式任务

当前 Tips 不显示长说明文字。刷新时间说明已从顶部长文案移除。

团本任务和幻巧战在 Tips 里使用短名：

| 设置/记录完整名 | Tips 显示名 |
| --- | --- |
| 团本第一巡行 / 团本第二巡行 / 团本第三巡行 | 团本1 / 团本2 / 团本3 |
| 幻巧战（神龙幻巧战） | 幻巧战 |

团本任务命中历史记录时当前文案保持为：

```text
团本1: 本周有过本记录
团本2: 本周有过本记录
团本3: 本周有过本记录
```

未命中时：

```text
团本1: 本周无过本记录
团本2: 本周无过本记录
团本3: 本周无过本记录
```

不要再显示“不能确认奖励已领取”之类的括号说明；用户已经要求去掉。

### 6.4 Tips 显示条件

当前逻辑：

- `ShowRouletteCompletionTips == true` 时允许显示。
- 鼠标悬停在悬浮窗任意位置即可显示。
- `PinRouletteCompletionTips == true` 时显示独立固定 Tips 窗口。
- `HideCompletedMonitorTasks == true` 时隐藏已完成监控项。
- 锁定悬浮窗以后仍然可以显示 Tips。
- 如果开启“穿透悬浮窗”，窗口不会响应鼠标悬停，因此 Tips 也无法靠悬停显示；这是穿透行为本身导致的。

设置窗口“窗口行为”里有两个相关控件：

- “启用完成情况 Tips”：总开关。
- “固定显示 Tips / 取消固定 Tips”：控制 `PinRouletteCompletionTips`，固定窗口不再放在 Tips 浮窗标题栏里。

用户明确要求：

- 锁定悬浮窗不等于穿透。
- 穿透只跟“穿透悬浮窗”选项绑定。

当前实现：

```csharp
IsClickthrough = Plugin.Configuration.ClickthroughFloatingWindow;
```

## 7. 完成状态判断

### 7.1 每日任务完成状态

入口：

```csharp
Database.IsDailyTaskCompletedInCurrentResetCycle(string taskKey, string rouletteName)
```

判断策略：

1. 优先读取客户端随机任务完成状态：

   ```csharp
   InstanceContent.Instance()->IsRouletteComplete((byte)rouletteId)
   ```

2. 读取不到时回退到 `task_history.json`。
3. `task_history.json` 回退判断时必须同时匹配：
   - `RouletteType`
   - `PlayerName`
   - `World`
   - `IsCompleted == true`
   - 当前 23:00 刷新周期内

刷新周期：

```text
每天 23:00
```

当前周期起点逻辑：

```csharp
DateTime.Today.AddHours(23)
```

如果当前时间早于今天 23:00，则周期起点为昨天 23:00。

### 7.2 每周任务完成状态

入口：

```csharp
Database.GetWeeklyMonitorTaskStatus(string taskKey, string taskName)
```

当前分派：

```csharp
WeeklyTaskWondrousTailsKey => GetWondrousTailsStatus(),
WeeklyTaskCurrentAllianceRaidKey => GetCurrentAllianceRaidStatus(taskKey, taskName),
WeeklyTaskUnrealTrialKey => GetUnrealTrialStatus(taskKey, taskName),
_ when IsWeeklySavageTaskKey(taskKey) => GetSavageRaidStatus(taskKey, taskName),
_ => GetRecordedWeeklyTaskStatus(taskKey, taskName)
```

团本任务：

- 第一巡行、第二巡行、第三巡行都会被记录进历史，并各自统计完成状态。
- 新的团本第一 / 第二 / 第三巡行都只看 `task_history.json` 内当前角色 / 当前服务器 / 当前周常周期内的完成记录。
- 旧的团本总项保留兼容逻辑，仍然只在第三巡行的完成记录出现时判定完成。
- 不读取任务搜索器 UI，不再通过奖励领取数量判断。

历史记录匹配函数：

```csharp
Database.IsTaskHistoryMonitorTaskCompletedInCurrentResetCycle(...)
```

匹配条件：

- `IsCompleted == true`
- 当前角色名一致
- 当前服务器名一致
- `monitorTaskKey` 命中，或任务名称 / 副本名称命中
- 记录时间在当前周常周期内

记录时间使用：

```csharp
roulette.GetEndedDateTime() ?? roulette.GetStartedDateTime()
```

周常周期起点：

```csharp
GetCurrentWeeklyResetCycleStart()
```

当前周常刷新时间：

```text
每周二 16:00
```

幻巧战：

- 当前并未改成“只看历史记录”。
- 仍会优先读取任务搜索器文本里的幻巧状态。
- 再尝试读取 `PlayerState` 幻巧状态。
- 无法确认时返回未知状态。

零式：

- 当前仍会优先读取任务搜索器奖励/锁定状态。
- 回退到 `task_history.json` 记录。

### 7.3 历史任务文件自动重载

为了方便手动编辑 / 测试 `task_history.json`，当前实现增加了：

```csharp
Database.ReloadTaskHistoryIfChanged()
```

调用位置：

- 每日任务回退判断前。
- 每周任务历史记录判断前。
- 设置窗口绘制“历史任务记录”折叠菜单前。

逻辑：

1. 检查 `task_history.json` 是否存在。
2. 比较文件最后修改时间。
3. 如果文件被外部改过，重新读取到 `TaskHistoryRoulettes`。

注意：如果游戏里加载的是旧 DLL，仍然需要禁用再启用插件或重启游戏才能获得这项能力。

## 8. 悬浮窗行为

核心文件：

```text
RouletteBuddy/Windows/MainWindow.cs
```

### 8.1 样式

支持两种样式：

| 样式 | 说明 |
| --- | --- |
| 经典样式 | 有标题、页签、当前任务、历史任务、按钮 |
| 极简样式 | 无标题、无页签、无按钮，更适合常驻悬浮 |

### 8.2 显示项

配置项虽然仍以 `MinimalShow...` 命名，但现在同时影响经典样式和极简样式：

```csharp
MinimalShowCurrentTask
MinimalShowTaskTime
MinimalShowTodayMentorRouletteCount
MinimalShowMentorRouletteTotalCount
ShowCurrentTime
```

显示内容：

| 配置项 | 内容 |
| --- | --- |
| `MinimalShowCurrentTask` | 任务类型、任务名称 |
| `MinimalShowTaskTime` | 任务时长、开始时间、是否完成 |
| `MinimalShowTodayMentorRouletteCount` | 今日导随次数 |
| `MinimalShowMentorRouletteTotalCount` | 导随总次数 |
| `ShowCurrentTime` | 当前时间 |

当前时间格式：

```text
HH:mm:ss
```

当前时间显示要求：

- 不显示“当前时间:”前缀。
- 不显示年份日期。
- 白色文字。

### 8.3 锁定和穿透

配置项：

```csharp
LockFloatingWindow
ClickthroughFloatingWindow
```

当前行为：

| 锁定悬浮窗 | 穿透悬浮窗 | 结果 |
| --- | --- | --- |
| 关 | 关 | 可移动、可缩放、不穿透 |
| 开 | 关 | 不可移动、不可缩放、不穿透，可悬停看 Tips |
| 关 | 开 | 可移动、可缩放、穿透 |
| 开 | 开 | 不可移动、不可缩放、穿透 |

### 8.4 个人便签悬浮窗

核心文件：

```text
RouletteBuddy/Windows/NoteWindow.cs
RouletteBuddy/Helpers/CleanBackgroundManager.cs
RouletteBuddy/Shaders/*.cso
```

入口：

- 设置窗口里的“个人便签”区域。
- 命令 `/prr bq`，作用是开关便签悬浮窗；开着时关闭，关着时打开。

便签类型：

| 类型 | 保存位置 | 可见范围 |
| --- | --- | --- |
| 公共便签 | `Configuration.PublicNoteContent` | 所有角色可见 |
| 角色便签 | `Configuration.CharacterNoteContents` | 仅当前角色 / 当前服务器可见 |

角色便签 key：

```csharp
$"{worldName}/{playerName}"
```

注意：

- 未登录时无法确定角色名和服务器名，因此选择“角色便签”会显示提示，不允许编辑。
- 便签内容使用 `ImGui.InputTextMultiline`，内容变化时立即写入配置并 `Save()`。
- 多行输入框有边框线，最小高度为 1 行文本高度。
- 便签窗口本身不设置最小窗口尺寸：

  ```csharp
  MinimumSize = Vector2.Zero
  ```

- 便签窗口保留原生标题栏、关闭按钮和折叠按钮。标题栏显示：

  ```text
  公共便签
  角色便签
  ```

设置窗口 UI：

- “公共便签 / 角色便签”必须使用单选框，不使用下拉菜单。
- “磨砂背景 / 透明背景”必须使用单选框，不使用下拉菜单。
- “磨砂背景”显示：
  - 便签磨砂强度。
  - 便签窗口透明度。
- “透明背景”显示：
  - 便签窗口透明度。

背景样式：

| 样式 | 实现 |
| --- | --- |
| 磨砂背景 | 使用 `CleanBackgroundManager` 抓取 D3D11 back buffer，经 `AlphaFix/HBlur/VBlur` 计算着色器处理后，通过 `ImGui.GetBackgroundDrawList()` 绘制到便签窗口背后 |
| 透明背景 | 不跑 D3D11 模糊，只使用普通窗口透明度和透明标题栏 / 输入框背景 |

磨砂背景实现细节：

- 便签窗口设置：

  ```csharp
  Flags = ImGuiWindowFlags.NoBackground;
  AllowBackgroundBlur = true;
  ```

- 绘制时调用：

  ```csharp
  backgroundManager?.DrawBackground(GetCurrentNoteWindowOpacity());
  ```

- 磨砂强度会影响 shader 模糊迭代次数：

  ```csharp
  backgroundManager.BlurIterations = 1 + (int)Math.Round(GetFrostedStrength() * 5f);
  ```

- `CleanBackgroundManager` 内部会 clamp 到 `1 - 8` 次。
- `CleanBackgroundManager` 参考自 `E:\git\ARH\AutoRaidHelper\Helpers\CleanBackgroundManager.cs`，但命名空间、资源名、绘制层级已适配本项目。
- 本项目版本使用 `ImGui.GetBackgroundDrawList()`，不是 ARH 原本的 `ImGui.GetWindowDrawList()`，避免模糊贴图盖住便签原生标题栏、关闭按钮和折叠按钮。

透明背景实现细节：

- `NoteTransparentWindowOpacity` 不只控制窗口 `BgAlpha`，还同步控制：
  - 标题栏背景透明度。
  - 多行输入框背景透明度。
  - 多行输入框 child 背景透明度。
- 这是用户明确要求：“透明背景的滑块同时可以控制标题栏和多行的背景透明度”。

## 9. 当前默认设置

默认值来自开发者当前配置，写在：

```text
RouletteBuddy/Configuration.cs
```

当前默认值：

```csharp
public string Language = "zh_CN";
public HashSet<uint> SubscribedRouletteIds { get; set; } = [9];
public FloatingWindowStyle FloatingWindowStyleMode { get; set; } = FloatingWindowStyle.Minimal;
public float FloatingWindowOpacity { get; set; } = 0.5f;
public bool EnableFloatingWindow { get; set; } = true;
public bool LockFloatingWindow { get; set; } = true;
public bool ClickthroughFloatingWindow { get; set; } = false;
public bool ShowRouletteCompletionTips { get; set; } = true;
public bool PinRouletteCompletionTips { get; set; } = false;
public bool HideCompletedMonitorTasks { get; set; } = false;
public bool DefaultSubscriptionsInitialized { get; set; } = true;
public bool DefaultDailyTaskMonitorInitialized { get; set; } = true;
public bool DefaultDailyUtilityTaskMonitorInitialized { get; set; } = true;
public bool DefaultWeeklyTaskMonitorInitialized { get; set; } = true;
public HashSet<string> MonitoredDailyTaskKeys { get; set; } = ["roulette:3", "roulette:5", "roulette:6", "roulette:7", "roulette:8", "roulette:17", "daily:tribalQuestsAllowance"];
public HashSet<string> MonitoredWeeklyTaskKeys { get; set; } = ["weekly:wondrousTails", "weekly:currentAllianceRaid:1", "weekly:currentAllianceRaid:2", "weekly:currentAllianceRaid:3", "weekly:unrealTrial", "weekly:savageRaid:1"];
public int TribalQuestCompletionCount { get; set; } = 3;
public bool MinimalShowCurrentTask { get; set; } = false;
public bool MinimalShowTaskTime { get; set; } = false;
public bool MinimalShowTodayMentorRouletteCount { get; set; } = true;
public bool MinimalShowMentorRouletteTotalCount { get; set; } = false;
public bool ShowCurrentTime { get; set; } = true;
public bool EnableNoteWindow { get; set; } = false;
public NoteScope NoteScopeMode { get; set; } = NoteScope.Public;
public NoteBackgroundStyle NoteBackgroundStyleMode { get; set; } = NoteBackgroundStyle.Frosted;
public float NoteFrostedStrength { get; set; } = 1.0f;
public float NoteFrostedWindowOpacity { get; set; } = 0.45f;
public float NoteTransparentWindowOpacity { get; set; } = 0.12f;
public string PublicNoteContent { get; set; } = string.Empty;
public Dictionary<string, string> CharacterNoteContents { get; set; } = [];
```

注意：

- 默认订阅 `SubscribedRouletteIds = [9]` 是指导者随机任务。
- 默认每日/每周任务监控项来自 2026-05-18 读取到的当前用户配置 `RouletteBuddy.json`。
- 默认蛮族任务完成次数阈值为 3。
- 如果将来重命名 `MinimalShow...`，要做配置迁移，避免用户升级后设置丢失。

## 10. 设置窗口

核心文件：

```text
RouletteBuddy/Windows/ConfigWindow.cs
```

当前结构：

- 日随伴侣设置概览。
- 存档数据。
- 悬浮窗样式。
  - 外观。
  - 窗口行为。
  - 显示内容。
- 个人便签。
  - 便签窗口开关。
  - 公共便签 / 角色便签。
  - 磨砂背景 / 透明背景。
  - 磨砂强度。
  - 窗口透明度。
- 每日、每周任务监控。
  - 每日任务。
  - 蛮族任务完成次数阈值。
  - 每周任务模块。
  - 零式任务模块。
- 订阅随机任务类型。
- DungeonLogger 账号设置。
- 底部记录查看：
  - 订阅任务记录：读取 `data.json`。
  - 历史任务记录：读取 `task_history.json`。

窗口允许折叠，最小尺寸：

```csharp
MinimumSize = Vector2.Zero
```

记录表格开启了横向 / 纵向滚动：

```csharp
ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY
```

“历史任务记录”表格显示字段包括：

```text
任务名称、任务类型、开始时间、结束时间、职业、是否完成、角色、服务器、监控任务 Key、记录来源
```

## 11. 命令

插件命令：

```text
/prr
```

作用：打开或关闭主悬浮窗。

设置窗口命令：

```text
/prr cfg
```

作用：打开设置窗口。

```text
/prr bq
```

作用：打开或关闭个人便签悬浮窗。

当前没有单独的 `/prr on` 或 `/prr off`。如果后续用户需要，可以在 `Plugin.OnCommand` 中加。

## 12. 关键代码文件

| 文件 | 说明 |
| --- | --- |
| `RouletteBuddy/Plugin.cs` | 插件入口、服务注入、事件注册、命令、任务弹出和完成处理 |
| `RouletteBuddy/Configuration.cs` | 配置结构和默认值 |
| `RouletteBuddy/DAO/Roulette.cs` | `data.json` 单条任务记录模型 |
| `RouletteBuddy/DAO/TaskHistoryRoulette.cs` | `task_history.json` 单条任务记录模型 |
| `RouletteBuddy/Utils/Database.cs` | 数据加载保存、统计、每日/每周任务监控、完成状态判断、历史任务文件自动重载 |
| `RouletteBuddy/Windows/MainWindow.cs` | 主悬浮窗、Tips、当前时间、历史任务 |
| `RouletteBuddy/Windows/NoteWindow.cs` | 个人便签悬浮窗 |
| `RouletteBuddy/Windows/ConfigWindow.cs` | 设置窗口 |
| `RouletteBuddy/Helpers/CleanBackgroundManager.cs` | 个人便签 D3D11 真实磨砂背景，移植自 `E:\git\ARH` |
| `RouletteBuddy/Shaders/*.cso` | D3D11 背景模糊计算着色器资源 |
| `RouletteBuddy/Resources/zh_CN.json` | 中文本地化 |
| `RouletteBuddy/Build/LocalizeOutputManifest.ps1` | manifest 本地化和发布包重打包 |
| `.github/workflows/build.yml` | GitHub Actions 构建/发布流程 |
| `.github/scripts/Make-Repo.ps1` | GitHub Actions 生成 `repo.json` |
| `repo.json` | Dalamud 插件仓库清单 |
| `docs/RELEASE.md` | 快速发布流程 |

## 13. 验收检查清单

修改代码后建议至少检查：

```powershell
dotnet build
```

发布前建议检查：

```powershell
dotnet build -c Release
```

以及：

```powershell
Get-Content -Encoding UTF8 output\RouletteBuddy.json
```

确认：

- `Name` 是 `日随伴侣`。
- `InternalName` 是 `RouletteBuddy`。
- `AssemblyVersion` 是目标版本。
- `DalamudApiLevel` 是 `15`。

检查 zip：

```powershell
tar -tf output\RouletteBuddy\latest.zip
```

应该只有类似：

```text
CsvHelper.dll
RouletteBuddy.deps.json
RouletteBuddy.dll
RouletteBuddy.json
SharpGen.Runtime.COM.dll
SharpGen.Runtime.dll
Vortice.Direct3D11.dll
Vortice.DirectX.dll
Vortice.DXGI.dll
Vortice.Mathematics.dll
```

不应该包含嵌套的旧 `latest.zip`。
不应该包含残留的 `TerraFX.Interop.Windows.dll`。

检查 zip 内 manifest：

```powershell
tar -xOf output\RouletteBuddy\latest.zip RouletteBuddy.json
```

## 14. 已知注意事项

1. `MinimalShow...` 命名是历史遗留，实际已同时影响经典和极简样式。
2. `ClickthroughFloatingWindow = true` 时无法通过鼠标悬停显示 Tips，这是穿透窗口的正常结果。
3. 水晶冲突地图不是每日任务监控项，不要重新加入。
4. `task_history.json` 判断必须匹配角色名和服务器名。
5. 标准随机任务完成状态优先用客户端数据，不要只靠 `task_history.json`。
6. 团本任务已拆成第一、第二、第三巡行三个独立监控项；它们都只看 `task_history.json` 本周过本记录，不读任务搜索器 UI。
7. 幻巧战、零式当前仍有任务搜索器 / 客户端状态读取逻辑，和团本任务不同。
8. 团本任务 Tips 文案保持“本周有过本记录 / 本周无过本记录”，不要显示“不能确认奖励已领取”括号说明。
9. `task_history.json` 支持外部修改后自动重载，但已经运行的旧 DLL 不具备该能力，需要重载插件。
10. 发布包必须用 `output/RouletteBuddy/latest.zip`。
11. `LocalizeOutputManifest.ps1` 不要再硬编码版本号。
12. 如果 Dalamud API 升级，需要重新核对：
   - `IClientState.CfPop`
   - `IClientState.TerritoryChanged`
   - `IDutyState.DutyCompleted`
   - `IObjectTable.LocalPlayer`
   - `IPlayerState`
   - `InstanceContent.Instance()->IsRouletteComplete`
13. 个人便签“磨砂背景”不是单纯 ImGui 半透明样式，而是 D3D11 真实模糊背景：
   - 依赖 `Vortice.Direct3D11` / `Vortice.DXGI` 以及其传递依赖 `SharpGen.Runtime*`、`Vortice.DirectX`、`Vortice.Mathematics`。
   - 依赖 `unsafe`。
   - 依赖嵌入式 `.cso` shader 资源。
   - 如果用户反馈磨砂无效，优先查看 Dalamud 日志里 `CleanBackgroundManager` 是否获取到 DirectX11 设备、shader 是否加载成功、SRV/UAV 是否创建成功。
   - 磨砂背景使用 `GetBackgroundDrawList()` 绘制在便签窗口背后，便签窗口自身必须保持 `ImGuiWindowFlags.NoBackground`，否则会被普通窗口背景盖住。
   - 当前发布包不应包含 `TerraFX.Interop.Windows.dll`；如出现，通常是 `output` 目录残留旧文件，需要删除后重新构建。
14. 透明背景滑块必须同时影响窗口主体、标题栏和多行输入框背景透明度，不要只改 `BgAlpha`。

## 15. 2026-05-18 收工记录

本轮主要变更：

- 第三巡行取消任务搜索器 UI 读取，只以 `task_history.json` 当前周常周期内的历史任务记录判断。
- `risui.json` 改名为 `task_history.json`，内部类从 `RisuiRoulette` 重构为 `TaskHistoryRoulette`。
- 保留旧 `risui.json` 自动复制迁移逻辑。
- 设置窗口底部新增两个折叠菜单：
  - 订阅任务记录：`data.json`
  - 历史任务记录：`task_history.json`
- Tips 里“团本（第三巡行）”显示为“团本”，“幻巧战（神龙幻巧战）”显示为“幻巧战”。
- Tips 第三巡行命中历史记录时显示“本周有过本记录”，不再显示“不能确认奖励已领取”说明。
- 增加 `task_history.json` 外部修改自动重载。
- 为测试第三巡行，在本机多个可能配置目录里手动写入过模拟记录：
  - `pluginConfigs\RouletteBuddy`
  - `pluginConfigs\RouletteBuddy`
  - `pluginConfigs\随机任务记录卫月版`

本机模拟记录注意：

- 曾添加 `丹凤吟 / 白银乡` 的第三巡行记录。
- 后续又添加 `四宮輝夜 / 银泪湖` 的第三巡行记录。
- 这些是本机测试数据，不属于仓库文件。
- 第三巡行判断要求角色名和服务器完全一致；例如 `四宮輝夜` 里的 `宮`、`輝` 与简体字不同，不能混用。

构建验证：

```powershell
dotnet build
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

## 16. 2026-05-20 收工记录

本轮主要变更：

- 新增个人便签悬浮窗：
  - 新文件 `RouletteBuddy/Windows/NoteWindow.cs`。
  - 命令 `/prr bq` 用于打开或关闭便签窗口。
  - 支持公共便签和角色便签。
  - 便签内容变化时自动保存。
  - 便签窗口保留原生标题栏、关闭按钮和折叠按钮。
  - 便签窗口不限制最小窗口尺寸。
  - 多行输入框最小高度为 1 行，带边框线。
- 设置窗口新增“个人便签”区域：
  - “公共便签 / 角色便签”使用单选框。
  - “磨砂背景 / 透明背景”使用单选框。
  - 支持便签磨砂强度滑块。
  - 支持便签窗口透明度滑块。
- 公共便签 / 角色便签保存策略：
  - 公共便签保存到 `Configuration.PublicNoteContent`。
  - 角色便签保存到 `Configuration.CharacterNoteContents`。
  - 角色便签 key 为 `服务器名/角色名`。
- 透明背景行为：
  - 透明度滑块同时影响窗口主体、标题栏、多行输入框背景。
  - 这是用户明确要求，不要回退成只影响 `BgAlpha`。
- 磨砂背景行为：
  - 先尝试过 `AllowBackgroundBlur` 和 ImGui 半透明模拟，但用户反馈“没有磨砂感觉”。
  - 后参考 `E:\git\ARH`，移植 D3D11 真实磨砂背景方案。
  - 新增 `RouletteBuddy/Helpers/CleanBackgroundManager.cs`。
  - 新增 `RouletteBuddy/Shaders/AlphaFix.cso`、`HBlur.cso`、`VBlur.cso`。
  - 最初使用 `TerraFX.Interop.Windows` 版本，构建包约 8.64 MB，主要体积来自 `TerraFX.Interop.Windows.dll`。
  - 后续改为 `Vortice.Direct3D11` / `Vortice.DXGI` 版本，保留真实 D3D11 磨砂，发布包降到约 0.595 MB。
  - `.csproj` 开启 `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`。
  - 磨砂强度会影响 blur shader 迭代次数。
  - 如 `output` 目录残留旧的 `TerraFX.Interop.Windows.dll`，packager 可能会误打包；已手动删除残留后重新构建。
- 文档更新：
  - `README.md` 增加个人便签和 `/prr bq` 说明。
  - `docs/HANDOFF.md` 更新本轮交接内容。

本轮修改 / 新增文件：

```text
README.md
docs/HANDOFF.md
RouletteBuddy/Configuration.cs
RouletteBuddy/Plugin.cs
RouletteBuddy/Resources/zh_CN.json
RouletteBuddy/RouletteBuddy.csproj
RouletteBuddy/Windows/ConfigWindow.cs
RouletteBuddy/Windows/NoteWindow.cs
RouletteBuddy/Helpers/CleanBackgroundManager.cs
RouletteBuddy/Shaders/AlphaFix.cso
RouletteBuddy/Shaders/HBlur.cso
RouletteBuddy/Shaders/VBlur.cso
RouletteBuddy/packages.lock.json
```

构建验证：

```powershell
dotnet build
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

当前构建包内容检查：

```powershell
tar -tf output\RouletteBuddy\latest.zip
```

结果包含：

```text
CsvHelper.dll
RouletteBuddy.deps.json
RouletteBuddy.dll
RouletteBuddy.json
SharpGen.Runtime.COM.dll
SharpGen.Runtime.dll
Vortice.Direct3D11.dll
Vortice.DirectX.dll
Vortice.DXGI.dll
Vortice.Mathematics.dll
```

注意：

- `.cso` shader 是嵌入资源，不会单独出现在 zip 中。
- 当前 `latest.zip` 大小约 `623,459` 字节（约 `0.595 MB`）。
- 当前发布包不应再出现 `TerraFX.Interop.Windows.dll`。
- 本轮作为 `v1.0.6.0` 发布。

## 17. 2026-05-21 发布记录

本轮发布版本：

```text
1.0.6.0
```

主要发布内容：

- 发布个人便签悬浮窗功能。
- 支持 `/prr bq` 打开或关闭便签窗口。
- 支持公共便签 / 角色便签，两者通过设置窗口单选框切换。
- 支持便签内容变化时自动保存。
- 支持磨砂背景 / 透明背景，两者通过设置窗口单选框切换。
- 磨砂背景使用 D3D11 back buffer + 计算着色器实现真实模糊效果。
- 透明背景滑块同时影响窗口主体、标题栏和多行输入框背景透明度。
- 多行输入框带边框，最小高度为 1 行。
- 便签窗口保留原生标题栏、关闭按钮和折叠按钮，不限制最小窗口尺寸。
- 真磨砂实现已从 TerraFX 版本切换到 Vortice 版本，避免发布包被 `TerraFX.Interop.Windows.dll` 放大到约 9 MB。

发布前验证：

```powershell
dotnet build -c Release
```

结果要求：

```text
已成功生成。
0 个警告
0 个错误
```

发布包：

```text
output/RouletteBuddy/latest.zip
```

发布包预期内容：

```text
CsvHelper.dll
RouletteBuddy.deps.json
RouletteBuddy.dll
RouletteBuddy.json
SharpGen.Runtime.COM.dll
SharpGen.Runtime.dll
Vortice.Direct3D11.dll
Vortice.DirectX.dll
Vortice.DXGI.dll
Vortice.Mathematics.dll
```

发布包不应包含：

```text
TerraFX.Interop.Windows.dll
latest.zip
```

对应 Release：

```text
https://github.com/anmili2022/RouletteRecorder.Dalamud/releases/tag/v1.0.6.0
```

## 18. 2026-05-24 收工记录

本轮主要处理用户关于“团本任务”的需求：

- 用户明确要求：
  - 团本任务模块独立出来，做成跟零式一样。
  - 第一巡行、第二巡行、第三巡行分别统计是否完成。
  - Tips 文案压缩成更像零式的短风格。
  - 不要写旧称，直接写“团本”。

### 18.1 当前实现行为

团本任务现在已经拆成三个独立每周监控项：

| Key | 配置 / 历史显示名 | Tips 显示名 | 状态来源 |
| --- | --- | --- | --- |
| `weekly:currentAllianceRaid:1` | 团本第一巡行 | 团本1 | `task_history.json` 本周过本记录 |
| `weekly:currentAllianceRaid:2` | 团本第二巡行 | 团本2 | `task_history.json` 本周过本记录 |
| `weekly:currentAllianceRaid:3` | 团本第三巡行 | 团本3 | `task_history.json` 本周过本记录 |

关键行为：

- 第一巡行、第二巡行、第三巡行都会进入历史记录。
- 三个巡行在 Tips 中分别显示完成状态，不再共用一个“团本”完成状态。
- 团本任务只看 `task_history.json` 当前角色 / 当前服务器 / 当前周常周期内的完成记录。
- 团本任务不读任务搜索器 UI / Agent / 奖励领取数量。
- 配置窗口里有独立的“团本任务模块”，位置在每周任务模块和零式任务模块之间。
- 悬浮窗 Tips 里有独立的“团本任务”分组，显示 `团本1` / `团本2` / `团本3`。
- 设置页和历史记录仍用较完整的 `团本第一巡行` / `团本第二巡行` / `团本第三巡行`，方便区分。
- 旧的 `weekly:currentAllianceRaid` 只保留兼容逻辑，不再作为 UI 新选项显示。

### 18.2 兼容和迁移逻辑

旧配置中如果存在：

```text
weekly:currentAllianceRaid
```

启动时会在 `Plugin.EnsureDefaultWeeklyTaskMonitors()` 中迁移成：

```text
weekly:currentAllianceRaid:1
weekly:currentAllianceRaid:2
weekly:currentAllianceRaid:3
```

随后旧 key 会因为不在 `Database.GetWeeklyTaskMonitorOptions()` 的有效选项中而被清理。

注意：

- `Database.WeeklyTaskCurrentAllianceRaidKey` 仍然保留，用于历史兼容。
- 旧的 `GetCurrentAllianceRaidStatus(...)` / `IsCurrentAllianceRaidCompletedInCurrentResetCycle()` 也仍保留兼容逻辑：旧总项只认第三巡行完成。
- 新 UI 正常情况下不再展示旧总项；不要把旧总项重新加回 `GetWeeklyTaskMonitorOptions()`。

### 18.3 本轮关键代码改动

主要文件：

```text
RouletteBuddy/Configuration.cs
RouletteBuddy/Plugin.cs
RouletteBuddy/Resources/zh_CN.json
RouletteBuddy/Utils/Database.cs
RouletteBuddy/Windows/ConfigWindow.cs
RouletteBuddy/Windows/MainWindow.cs
docs/HANDOFF.md
```

关键实现点：

- `Database.cs`
  - 新增：
    - `WeeklyTaskAllianceRaid1Key`
    - `WeeklyTaskAllianceRaid2Key`
    - `WeeklyTaskAllianceRaid3Key`
    - `CurrentAllianceRaidNameKeywords`
    - `AllianceRaidTaskKeys`
  - 新增 `GetWeeklyAllianceRaidTaskMonitorOptions()`。
  - `GetWeeklyNonSavageTaskMonitorOptions()` 会排除团本任务和零式任务，避免团本重复出现在普通周常分组。
  - `TryGetWeeklyMonitorTaskForContent(...)` 会优先把当前团本匹配到第一 / 第二 / 第三巡行独立 key。
  - `GetWeeklyTaskConditions(...)` 支持按团本独立 key 返回对应巡行的 `ContentFinderCondition`。
  - `GetRouletteTypeDisplayName(...)` 会把历史记录里的巡行识别成 `团本第一巡行` / `团本第二巡行` / `团本第三巡行`。

- `Plugin.cs`
  - `EnsureDefaultWeeklyTaskMonitors()` 增加旧团本总项到新三项的迁移。

- `ConfigWindow.cs`
  - 新增“团本任务模块”。
  - 位置在“每周任务模块”和“零式任务模块”之间。

- `MainWindow.cs`
  - Tips 新增“团本任务”分组。
  - `GetMonitorTaskTipDisplayName(...)` 把三个团本项显示为 `团本1` / `团本2` / `团本3`。

- `zh_CN.json`
  - 新增团本模块和短名文案。
  - 已把用户可见文案里的旧称去掉。

### 18.4 文案约定

用户明确要求直接写“团本”，因此当前文案约定如下：

| 场景 | 文案 |
| --- | --- |
| 设置模块标题 | 团本任务模块 |
| Tips 分组标题 | 团本任务 |
| Tips 条目 | 团本1 / 团本2 / 团本3 |
| 设置项 / 历史类型 | 团本第一巡行 / 团本第二巡行 / 团本第三巡行 |
| 旧总项兼容显示 | 团本 |

如果后续继续改文案，注意不要重新写回旧称。

### 18.5 构建验证

本轮代码改动后已经执行：

```powershell
dotnet build
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

当前只完成本地构建验证，尚未执行发布流程。
如果需要发布，请继续按 `docs/RELEASE.md` 走 Release 构建、打包、更新 `repo.json` 和 GitHub Release。

### 18.6 当前待注意事项

- 团本完成状态依赖实际历史记录，判断时必须匹配当前角色名和服务器名。
- 如果用户反馈“打了但没显示完成”，优先检查：
  - `task_history.json` 是否有该巡行记录。
  - 记录的 `IsCompleted` 是否为 `true`。
  - `playername` 和 `world` 是否与当前角色完全一致。
  - 记录时间是否落在当前每周刷新周期内。
  - `monitorTaskKey` 是否为新 key，或 `ContentName` 是否能匹配对应巡行名称。
- 旧历史记录若没有新 `monitorTaskKey`，仍可通过 `ContentName` / `RouletteType` 的巡行名匹配。
- 不要把团本改回任务搜索器奖励领取判断；用户之前已经明确要求团本只以历史记录为准。

## 20. 2026-05-24 收工记录（直接排本不进历史记录修复）

本轮修复了一个 bug：直接指定排本（非随机排随）的周常监控任务（如24人团队本）不会出现在主历史记录（`data.json` / 「历史任务」标签页）中。

### 20.1 问题根因

直接排本时，`CfPop` 事件的 `poppedContentType` 为 `ContentsType.Regular`，`OnCfPop` 只设置了 `TaskHistoryRoulette` 的字段，没有设置主 `Roulette` 的 `rouletteType`。随后 `Roulette.Finish()` 因为 `Instance.RouletteType == null` 直接跳过保存，导致 `Database.InsertRoulette()` 不会被调用。

`TaskHistoryRoulette` 正确记录到了 `task_history.json`，但「历史任务」标签页只展示 `Database.Roulettes`（来自 `data.json`），所以用户看不到记录。

### 20.2 本轮修改

涉及文件：

```text
RouletteBuddy/Plugin.cs
RouletteBuddy/DAO/Roulette.cs
docs/HANDOFF.md
```

#### 20.2.1 Plugin.cs — OnCfPop

`RouletteBuddy/Plugin.cs:180`

在 `ContentsType.Regular` + `TryGetWeeklyMonitorTaskForContent` 命中时，增加：

```csharp
rouletteType = weeklyTaskName;
```

使主 `Roulette` 的 `RouletteType` 不再为 null，从而能进入 `Finish()` 的保存流程。

#### 20.2.2 Roulette.cs — Finish()

`RouletteBuddy/DAO/Roulette.cs:105-108`

原来的订阅检查逻辑：

```csharp
var currContentRoulette = Database.CfRoulettes.FirstOrDefault(x => x.Name.ToString().Equals(RouletteType));
var isSubscribedRouletteType = Plugin.Configuration.SubscribedRouletteIds.Contains(currContentRoulette.RowId);
if (Instance.RouletteType == null || Instance.ContentName == null || !isSubscribedRouletteType) return;
```

改为：

```csharp
var currContentRoulette = Database.CfRoulettes.FirstOrDefault(x => x.Name.ToString().Equals(RouletteType));
var isKnownRoulette = currContentRoulette.RowId != 0;
var isSubscribed = !isKnownRoulette || Plugin.Configuration.SubscribedRouletteIds.Contains(currContentRoulette.RowId);
if (Instance.RouletteType == null || Instance.ContentName == null || !isSubscribed) return;
```

逻辑说明：

- **已知轮盘（`ContentRoulette` 名称匹配成功，`RowId != 0`）**：走原来的 `SubscribedRouletteIds` 订阅检查，未订阅则跳过。
- **未知类型（周常任务名，不匹配任何 `CfRoulettes`，`RowId == 0`）**：跳过订阅检查，直接保存。

`ContentRoulette` 是 struct，`FirstOrDefault` 未命中时返回 `default(ContentRoulette)`，其 `RowId` 为 0。利用这一点区分已知轮盘和未知类型。

### 20.3 行为变化

改动前：

| 场景 | 主历史（data.json） | 任务历史（task_history.json） |
| --- | --- | --- |
| 随机排随命中订阅轮盘 | ✅ 记录 | ✅ 记录 |
| 随机排随未订阅轮盘 | ❌ 跳过 | ✅ 记录 |
| 直接排本周常任务（团本等） | ❌ 跳过 | ✅ 记录 |
| 直接排本非周常任务 | ❌ 跳过 | ❌ 跳过 |

改动后：

| 场景 | 主历史（data.json） | 任务历史（task_history.json） |
| --- | --- | --- |
| 随机排随命中订阅轮盘 | ✅ 记录 | ✅ 记录 |
| 随机排随未订阅轮盘 | ❌ 跳过 | ✅ 记录 |
| 直接排本周常任务（团本等） | ✅ 记录 | ✅ 记录 |
| 直接排本非周常任务 | ❌ 跳过 | ❌ 跳过 |

### 20.4 历史标签页显示

`RouletteType` 被设为周常任务名（如 `Plugin.Localization.Localize("Alliance Raid 1")` ≈ `"团本第一巡行"`），经 `GetRouletteTypeDisplayName` → `TryGetAllianceRaidHistoryDisplayName` 中的 `ContainsNormalizedName` 匹配关键词（`"第一巡行"` / `"第二巡行"` / `"第三巡行"`），会显示为 `"团本第一巡行"` / `"团本第二巡行"` / `"团本第三巡行"`。

### 20.5 构建验证

```powershell
dotnet build
```

结果：

```text
已成功生成。
0 个警告
0 个错误
```

## 21. 下次建议

- 如用户需要，增加 `/prr on` 和 `/prr off`。
- 后续可把 `MinimalShow...` 迁移为通用 `Show...`，并做配置迁移。
- 可增加“一键恢复默认设置”。
- 可增加历史记录清空、备份或导入功能。
- 可增加 Tips 中的刷新时间开关，而不是固定显示。
- 如用户确认不再需要旧总项兼容，可在未来版本中进一步清理 `WeeklyTaskCurrentAllianceRaidKey` 相关 UI 兼容路径，但要谨慎处理已有配置和历史记录。
- 进游戏实测个人便签真实磨砂背景：
  - 若不生效，优先看 Dalamud 日志中 `CleanBackgroundManager` 的 D3D11 设备、shader、SRV/UAV 初始化情况。
  - 若用户希望标题栏也完全参与自定义磨砂，可考虑像 ARH 一样改成 `NoTitleBar` 并手绘标题栏、关闭按钮和折叠按钮；当前实现保留原生标题栏以满足“有标题栏 / 有折叠按钮”的需求。

