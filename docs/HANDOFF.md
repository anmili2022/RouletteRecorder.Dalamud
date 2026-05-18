# 日随伴侣交接文档

> 最后更新：2026-05-18
> 项目路径：`E:\git\RouletteRecorder.Dalamud`
> 当前分支：`master`
> 插件名称：`日随伴侣`
> 内部名：`日随伴侣卫月版`
> 当前版本：`1.0.2.0`
> 当前发布页：`https://github.com/anmili2022/RouletteRecorder.Dalamud/releases/tag/v1.0.2.0`

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
- 支持周常任务：天书奇谈、第三巡行、幻巧战、零式重量级 1-4。
- 支持标准随机任务完成状态读取。
- 支持水晶冲突练习赛、段位赛完成状态记录与判断。
- 支持角色名和服务器名区分。
- 支持经典样式和极简样式悬浮窗。
- 支持悬浮窗锁定、穿透、透明度、显示项开关。
- 支持当前时间显示。
- 支持今日导随次数和导随总次数显示。
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

### 3.2 常用构建命令

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
output/RouletteRecorder.Dalamud.dll
output/RouletteRecorder.Dalamud.json
output/RouletteRecorder.Dalamud/latest.zip
```

注意：发布时使用的是：

```text
output/RouletteRecorder.Dalamud/latest.zip
```

不要误用旧的：

```text
output/latest.zip
```

## 4. 插件清单和发布信息

项目版本在：

```text
RouletteRecorder.Dalamud/RouletteRecorder.Dalamud.csproj
```

当前版本字段：

```xml
<Version>1.0.2.0</Version>
<AssemblyVersion>1.0.2.0</AssemblyVersion>
<FileVersion>1.0.2.0</FileVersion>
<InformationalVersion>1.0.2.0</InformationalVersion>
```

仓库清单：

```text
repo.json
```

当前关键 manifest 字段应为：

```json
{
  "Name": "日随伴侣",
  "InternalName": "日随伴侣卫月版",
  "AssemblyVersion": "1.0.2.0",
  "DalamudApiLevel": 15
}
```

构建后处理脚本：

```text
RouletteRecorder.Dalamud/Build/LocalizeOutputManifest.ps1
```

它负责：

- 把输出 manifest 的 `Name` 固定为 `日随伴侣`。
- 把输出 manifest 的 `InternalName` 固定为 `日随伴侣卫月版`。
- 写入中英双语 `Description` / `Punchline` / `Tags`。
- 重新打包 `output/RouletteRecorder.Dalamud/latest.zip`，保证 zip 内 manifest 也是中文内部名。
- 不再硬编码 `AssemblyVersion`，版本以 `.csproj` 为准。

## 5. 数据文件

插件配置目录由 Dalamud 提供：

```csharp
Plugin.PluginInterface.ConfigDirectory.FullName
```

常见本地路径：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\日随伴侣卫月版\
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
RouletteRecorder.Dalamud/DAO/Roulette.cs
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
RouletteRecorder.Dalamud/DAO/TaskHistoryRoulette.cs
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
RouletteRecorder.Dalamud/Utils/Database.cs
RouletteRecorder.Dalamud/Windows/MainWindow.cs
RouletteRecorder.Dalamud/Windows/ConfigWindow.cs
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
| `weekly:currentAllianceRaid` | 团本（第三巡行） | 只看 `task_history.json` 本周过本记录 |
| `weekly:unrealTrial` | 幻巧战（神龙幻巧战） | 优先任务搜索器文本 / `PlayerState` 幻巧状态；无法判断时显示未知 |
| `weekly:savageRaid:1` | 零式重量级1 | 优先任务搜索器奖励/锁定状态；回退 `task_history.json` |
| `weekly:savageRaid:2` | 零式重量级2 | 同上 |
| `weekly:savageRaid:3` | 零式重量级3 | 同上 |
| `weekly:savageRaid:4` | 零式重量级4 | 同上 |

重要：用户已明确要求“第三巡行取消 UI 读取，只以历史记录为准”。当前实现：

```csharp
private static MonitorTaskStatus GetCurrentAllianceRaidStatus(string taskKey, string taskName)
{
    return GetRecordedWeeklyTaskStatus(taskKey, taskName);
}
```

因此第三巡行 Tips 不再读任务搜索器 UI / Agent / `NumCollectedRewards`。

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

第三巡行和幻巧战在 Tips 里使用短名：

| 设置/记录完整名 | Tips 显示名 |
| --- | --- |
| 团本（第三巡行） | 团本 |
| 幻巧战（神龙幻巧战） | 幻巧战 |

第三巡行命中历史记录时当前文案保持为：

```text
团本: 本周有过本记录
```

未命中时：

```text
团本: 本周无过本记录
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

第三巡行：

- 只调用 `GetRecordedWeeklyTaskStatus()`。
- 只看 `task_history.json` 内当前角色 / 当前服务器 / 当前周常周期内的完成记录。
- 匹配优先使用 `monitorTaskKey == "weekly:currentAllianceRaid"`。
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
RouletteRecorder.Dalamud/Windows/MainWindow.cs
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

## 9. 当前默认设置

默认值来自开发者当前配置，写在：

```text
RouletteRecorder.Dalamud/Configuration.cs
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
public HashSet<string> MonitoredWeeklyTaskKeys { get; set; } = ["weekly:wondrousTails", "weekly:currentAllianceRaid", "weekly:unrealTrial", "weekly:savageRaid:1"];
public int TribalQuestCompletionCount { get; set; } = 3;
public bool MinimalShowCurrentTask { get; set; } = false;
public bool MinimalShowTaskTime { get; set; } = false;
public bool MinimalShowTodayMentorRouletteCount { get; set; } = true;
public bool MinimalShowMentorRouletteTotalCount { get; set; } = false;
public bool ShowCurrentTime { get; set; } = true;
```

注意：

- 默认订阅 `SubscribedRouletteIds = [9]` 是指导者随机任务。
- 默认每日/每周任务监控项来自 2026-05-18 读取到的当前用户配置 `日随伴侣卫月版.json`。
- 默认蛮族任务完成次数阈值为 3。
- 如果将来重命名 `MinimalShow...`，要做配置迁移，避免用户升级后设置丢失。

## 10. 设置窗口

核心文件：

```text
RouletteRecorder.Dalamud/Windows/ConfigWindow.cs
```

当前结构：

- 日随伴侣设置概览。
- 存档数据。
- 悬浮窗样式。
  - 外观。
  - 窗口行为。
  - 显示内容。
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

当前没有单独的 `/prr on` 或 `/prr off`。如果后续用户需要，可以在 `Plugin.OnCommand` 中加。

## 12. 关键代码文件

| 文件 | 说明 |
| --- | --- |
| `RouletteRecorder.Dalamud/Plugin.cs` | 插件入口、服务注入、事件注册、命令、任务弹出和完成处理 |
| `RouletteRecorder.Dalamud/Configuration.cs` | 配置结构和默认值 |
| `RouletteRecorder.Dalamud/DAO/Roulette.cs` | `data.json` 单条任务记录模型 |
| `RouletteRecorder.Dalamud/DAO/TaskHistoryRoulette.cs` | `task_history.json` 单条任务记录模型 |
| `RouletteRecorder.Dalamud/Utils/Database.cs` | 数据加载保存、统计、每日/每周任务监控、完成状态判断、历史任务文件自动重载 |
| `RouletteRecorder.Dalamud/Windows/MainWindow.cs` | 主悬浮窗、Tips、当前时间、历史任务 |
| `RouletteRecorder.Dalamud/Windows/ConfigWindow.cs` | 设置窗口 |
| `RouletteRecorder.Dalamud/Resources/zh_CN.json` | 中文本地化 |
| `RouletteRecorder.Dalamud/Build/LocalizeOutputManifest.ps1` | manifest 本地化和发布包重打包 |
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
Get-Content -Encoding UTF8 output\RouletteRecorder.Dalamud.json
```

确认：

- `Name` 是 `日随伴侣`。
- `InternalName` 是 `日随伴侣卫月版`。
- `AssemblyVersion` 是目标版本。
- `DalamudApiLevel` 是 `15`。

检查 zip：

```powershell
tar -tf output\RouletteRecorder.Dalamud\latest.zip
```

应该只有类似：

```text
CsvHelper.dll
RouletteRecorder.Dalamud.deps.json
RouletteRecorder.Dalamud.dll
RouletteRecorder.Dalamud.json
```

不应该包含嵌套的旧 `latest.zip`。

检查 zip 内 manifest：

```powershell
tar -xOf output\RouletteRecorder.Dalamud\latest.zip RouletteRecorder.Dalamud.json
```

## 14. 已知注意事项

1. `MinimalShow...` 命名是历史遗留，实际已同时影响经典和极简样式。
2. `ClickthroughFloatingWindow = true` 时无法通过鼠标悬停显示 Tips，这是穿透窗口的正常结果。
3. 水晶冲突地图不是每日任务监控项，不要重新加入。
4. `task_history.json` 判断必须匹配角色名和服务器名。
5. 标准随机任务完成状态优先用客户端数据，不要只靠 `task_history.json`。
6. 第三巡行当前只看 `task_history.json` 本周过本记录，不读任务搜索器 UI；不要把它改回奖励领取判断。
7. 幻巧战、零式当前仍有任务搜索器 / 客户端状态读取逻辑，和第三巡行不同。
8. 第三巡行 Tips 文案保持“本周有过本记录 / 本周无过本记录”，不要显示“不能确认奖励已领取”括号说明。
9. `task_history.json` 支持外部修改后自动重载，但已经运行的旧 DLL 不具备该能力，需要重载插件。
10. 发布包必须用 `output/RouletteRecorder.Dalamud/latest.zip`。
11. `LocalizeOutputManifest.ps1` 不要再硬编码版本号。
12. 如果 Dalamud API 升级，需要重新核对：
   - `IClientState.CfPop`
   - `IClientState.TerritoryChanged`
   - `IDutyState.DutyCompleted`
   - `IObjectTable.LocalPlayer`
   - `IPlayerState`
   - `InstanceContent.Instance()->IsRouletteComplete`

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
  - `pluginConfigs\日随伴侣卫月版`
  - `pluginConfigs\RouletteRecorder.Dalamud`
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

## 16. 下次建议

- 如用户需要，增加 `/prr on` 和 `/prr off`。
- 后续可把 `MinimalShow...` 迁移为通用 `Show...`，并做配置迁移。
- 可增加“一键恢复默认设置”。
- 可增加历史记录清空、备份或导入功能。
- 可增加 Tips 中的刷新时间开关，而不是固定显示。

