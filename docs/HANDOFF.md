# 日随伴侣交接文档

> 最后更新：2026-05-15  
> 项目路径：`E:\git\RouletteRecorder.Dalamud`  
> 插件名称：`日随伴侣`  
> 内部名：`日随伴侣卫月版`  
> 当前版本：`1.0.0.0`

## 1. 项目概述

日随伴侣是 [RouletteRecorder](https://github.com/StarHeartHunt/RouletteRecorder) 的 Dalamud 插件版本，用于自动记录《FINAL FANTASY XIV》的每日随机任务，当前主要围绕指导者随机任务进行优化。

当前版本提供：

- 自动识别随机任务弹出。
- 进入副本后记录任务名称。
- 任务完成时记录完成状态和结束时间。
- 任务结束后任务时长停止增长。
- 历史任务记录。
- 当前任务悬浮窗。
- 经典样式和极简样式。
- 悬浮窗透明度设置。
- 锁定悬浮窗。
- 锁定后鼠标穿透。
- 默认订阅指导者随机任务。
- 今日导随任务次数统计。
- 当前角色导随任务总次数读取。
- CSV 导出。
- 打开存档文件夹。

## 2. 当前技术环境

### 2.1 Dalamud / API

当前项目按国服 Dalamud API 15 构建：

```xml
<PackageReference Include="Dalamud.NET.Sdk" Version="15.0.0" />
```

本地 Dalamud 开发 DLL 目录：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\addon\Hooks\dev\
```

Dalamud API 文档：

```text
https://dalamud.dev/api/
```

重要约束：

- 不再通过反射调用 Dalamud API。
- 与 Dalamud API 相关的调用使用强类型接口。
- `Localization.cs` 中使用 `System.Reflection` 读取嵌入资源，这不是调用 Dalamud API。

### 2.2 构建目标

当前项目本地构建目标为 `.NET 10`，输出目录已改为仓库根目录下的：

```text
output/
```

构建命令：

```powershell
dotnet build
```

构建输出：

```text
output/RouletteRecorder.Dalamud.dll
output/RouletteRecorder.Dalamud.json
```

Release 构建：

```powershell
dotnet build -c Release
```

## 3. 插件清单和发布信息

主要清单文件：

```text
RouletteRecorder.Dalamud/RouletteRecorder.Dalamud.json
repo.json
```

构建后输出清单：

```text
output/RouletteRecorder.Dalamud.json
```

当前关键字段应保持为：

```json
{
  "Name": "日随伴侣",
  "InternalName": "日随伴侣卫月版",
  "AssemblyVersion": "1.0.0.0",
  "DalamudApiLevel": 15
}
```

说明：

- `Name`：插件安装器和界面中展示的名称。
- `InternalName`：Dalamud 用于配置目录和插件标识的内部名。
- `AssemblyVersion`：当前版本号。
- `Description` 和 `Punchline` 已改为中英双语，中文在上，英文在下。

## 4. 当前默认设置

默认设置来自开发者当前本机配置，并已写入：

```text
RouletteRecorder.Dalamud/Configuration.cs
```

当前默认值：

```csharp
public string Language = "zh_CN";
public HashSet<uint> SubscribedRouletteIds { get; set; } = [9];
public FloatingWindowStyle FloatingWindowStyleMode { get; set; } = FloatingWindowStyle.Minimal;
public float FloatingWindowOpacity { get; set; } = 0.54f;
public bool LockFloatingWindow { get; set; } = false;
public bool DefaultSubscriptionsInitialized { get; set; } = true;
public bool MinimalShowCurrentTask { get; set; } = true;
public bool MinimalShowTaskTime { get; set; } = false;
public bool MinimalShowTodayMentorRouletteCount { get; set; } = true;
public bool MinimalShowMentorRouletteTotalCount { get; set; } = false;
```

含义：

| 设置项 | 默认值 | 说明 |
| --- | --- | --- |
| `Language` | `zh_CN` | 默认中文 |
| `SubscribedRouletteIds` | `[9]` | 默认订阅指导者随机任务 |
| `FloatingWindowStyleMode` | `Minimal` | 默认极简样式 |
| `FloatingWindowOpacity` | `0.54` | 默认悬浮窗透明度 54% |
| `LockFloatingWindow` | `false` | 默认不锁定悬浮窗 |
| `DefaultSubscriptionsInitialized` | `true` | 默认订阅初始化完成 |
| `MinimalShowCurrentTask` | `true` | 显示当前任务 |
| `MinimalShowTaskTime` | `false` | 不显示任务时间 |
| `MinimalShowTodayMentorRouletteCount` | `true` | 显示今日导随任务次数 |
| `MinimalShowMentorRouletteTotalCount` | `false` | 不显示导随任务总次数 |

注意：

- 虽然部分配置项仍以 `MinimalShow...` 命名，但这些显示项现在同时影响经典样式和极简样式。
- 如果后续重命名为 `ShowCurrentTask` 等更通用名称，需要考虑旧配置迁移。

## 5. 悬浮窗行为

主窗口文件：

```text
RouletteRecorder.Dalamud/Windows/MainWindow.cs
```

设置窗口文件：

```text
RouletteRecorder.Dalamud/Windows/ConfigWindow.cs
```

### 5.1 经典样式

经典样式包含两个页签：

- 当前任务
- 历史任务

当前任务页签显示项受配置控制：

| 显示项 | 控制内容 |
| --- | --- |
| 当前任务 | 任务类型、任务名称 |
| 任务时间 | 任务时长、开始时间、是否完成 |
| 今日导随任务次数 | 今日导随任务次数 |
| 导随任务总次数 | 当前角色导随任务总次数、刷新成就进度按钮 |

历史任务表格显示：

```text
任务名称 | 任务类型 | 时长 | 开始时间 | 结束时间
```

历史任务表格保留纵向滚动条。

### 5.2 极简样式

极简样式要求：

- 不显示窗口标题。
- 不显示页签。
- 不显示小队成员。
- 不显示设置按钮。
- 不显示导出 CSV 按钮。
- 右键点击悬浮窗打开设置窗口。
- 悬浮窗本体不显示横向或纵向滚动条。
- 今日导随任务次数使用黄色显示。
- 显示项受同一组配置控制。

### 5.3 锁定悬浮窗

配置项：

```csharp
public bool LockFloatingWindow { get; set; }
```

行为：

- 未锁定时：可以移动、可以缩放、鼠标不穿透。
- 锁定时：按当前悬浮窗大小锁定，不可移动，不可缩放，鼠标穿透。
- 只有选择“锁定悬浮窗”时才禁止放大缩小。

实现重点：

- 锁定时添加：

```csharp
ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
```

- 锁定时设置：

```csharp
IsClickthrough = true;
```

## 6. 当前任务和历史任务记录

任务实体：

```text
RouletteRecorder.Dalamud/DAO/Roulette.cs
```

数据库工具：

```text
RouletteRecorder.Dalamud/Utils/Database.cs
```

当前行为：

- `OnCfPop` 中识别随机任务弹出，并初始化当前任务。
- `OnTerritoryChanged` 中记录任务名称。
- `OnDutyCompleted` 中标记任务完成，并设置结束时间。
- 离开副本或登出时保存未完成任务。
- 任务完成后时长停止增长。

历史任务不再记录或显示小队成员。

CSV 导出也已取消成员列。

## 7. 导随次数逻辑

### 7.1 今日导随任务次数

统计来源：

```text
Database.Roulettes
```

统计规则：

- 今天的记录。
- 已完成。
- 任务类型是指导者随机任务。

相关方法：

```csharp
Database.GetTodayMentorRouletteCount()
```

### 7.2 当前角色导随任务总次数

来源：游戏成就进度。

当前使用成就：

```csharp
private const uint MentorRouletteAchievementId = 1604;
private const uint MentorRouletteAchievementMaxCount = 2000;
```

显示格式：

```text
当前进度 / 2000
```

设置或当前任务页中有“刷新成就进度”按钮。

## 8. 关键代码文件

| 文件 | 说明 |
| --- | --- |
| `RouletteRecorder.Dalamud/Plugin.cs` | 插件入口、Dalamud 服务注入、事件注册、命令注册 |
| `RouletteRecorder.Dalamud/Configuration.cs` | 配置结构和默认值 |
| `RouletteRecorder.Dalamud/Windows/MainWindow.cs` | 悬浮窗、经典/极简样式、当前任务和历史任务展示 |
| `RouletteRecorder.Dalamud/Windows/ConfigWindow.cs` | 设置窗口 |
| `RouletteRecorder.Dalamud/DAO/Roulette.cs` | 单条任务记录模型 |
| `RouletteRecorder.Dalamud/Utils/Database.cs` | 数据加载、保存、CSV 导出、统计 |
| `RouletteRecorder.Dalamud/Models/RouletteCSVMap.cs` | CSV 字段映射 |
| `RouletteRecorder.Dalamud/Utils/Localization.cs` | 本地化加载 |
| `RouletteRecorder.Dalamud/Resources/zh_CN.json` | 中文文本 |
| `RouletteRecorder.Dalamud/RouletteRecorder.Dalamud.csproj` | 项目配置、输出目录、构建后处理 |
| `RouletteRecorder.Dalamud/RouletteRecorder.Dalamud.json` | 插件 manifest 模板 |
| `repo.json` | 插件仓库清单 |
| `README.md` | 用户说明文档 |

## 9. 本地配置和数据位置

国服 XIVLauncherCN 下的配置目录通常为：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\
```

当前插件配置文件示例：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\日随伴侣卫月版.json
```

当前插件数据目录示例：

```text
C:\Users\Administrator\AppData\Roaming\XIVLauncherCN\pluginConfigs\日随伴侣卫月版\
```

历史数据文件：

```text
data.json
```

CSV 默认导出文件：

```text
data.csv
```

注意：

- 不要把本机绝对路径硬编码到默认配置中。
- `CsvExportPath` 应继续使用 `Plugin.PluginInterface.ConfigDirectory.FullName` 拼接。

## 10. 常用命令

构建：

```powershell
dotnet build
```

Release 构建：

```powershell
dotnet build -c Release
```

检查输出清单：

```powershell
Get-Content output\RouletteRecorder.Dalamud.json
```

搜索成员相关残留：

```powershell
rg "Members|Current Party Members|GetPartyMemberSummaries|CapturePartyMembers|PartyList|IObjectTable|No party members|成员|member" README.md RouletteRecorder.Dalamud -n
```

插件聊天命令：

```text
/prr
```

## 11. 验收检查清单

每次修改后建议检查：

- `dotnet build` 是否 0 警告、0 错误。
- 输出目录是否为 `output/`。
- `output/RouletteRecorder.Dalamud.json` 中：
  - `Name` 为 `日随伴侣`。
  - `InternalName` 为 `日随伴侣卫月版`。
  - `AssemblyVersion` 为 `1.0.0.0`。
  - `DalamudApiLevel` 为 `15`。
- 插件可以在 Dalamud 中加载。
- `/prr` 可以打开或关闭悬浮窗。
- 极简样式无标题、无页签、无按钮。
- 极简样式右键可打开设置窗口。
- 极简样式今日导随任务次数为黄色。
- 经典样式显示项开关生效。
- “任务时间”同时控制任务时长、开始时间、是否完成。
- 锁定悬浮窗后不可移动、不可缩放、鼠标穿透。
- 取消锁定后可移动、可缩放、鼠标不穿透。
- 历史任务表格有滚动条。
- 悬浮窗本体没有横向或纵向滚动条。
- 历史任务不显示成员列。
- 当前任务不显示小队成员。

## 12. 已知注意事项

1. 配置项命名仍有历史遗留：

   ```csharp
   MinimalShowCurrentTask
   MinimalShowTaskTime
   MinimalShowTodayMentorRouletteCount
   MinimalShowMentorRouletteTotalCount
   ```

   这些配置现在并不只影响极简样式，也影响经典样式。后续如果重命名，需要做配置迁移，避免用户升级后设置丢失。

2. README 在 PowerShell 默认控制台中使用 `Get-Content` 可能显示乱码，这是控制台编码显示问题，文件本身为 UTF-8。

3. 当前默认配置中的 `SubscribedRouletteIds = [9]` 是指导者随机任务。若后续游戏数据或地区版本变化，需要重新确认该 RowId。

4. 如果 Dalamud API 版本升级，需要重新核对以下接口：

   - `IClientState.CfPop`
   - `IClientState.TerritoryChanged`
   - `IDutyState.DutyCompleted`
   - `IPlayerState`
   - `WindowSystem.AddWindow`

5. 避免重新引入旧 API：

   - 不要使用旧签名的 `IClientState.LocalPlayer`。
   - 不要使用旧签名的 `IClientState.TerritoryChanged Action<ushort>`。
   - 不要使用旧签名的 `WindowSystem.AddWindow(Window)`。

## 13. 后续建议

- 将 `MinimalShow...` 配置项迁移为更通用的 `Show...` 命名。
- 在设置窗口中把 `Subscribed Roulette Types` 也汉化为“订阅随机任务类型”。
- 增加一键恢复默认设置按钮。
- 增加配置迁移版本号。
- 为历史记录增加清空或备份功能。
- 为成就进度读取失败时增加更明确的状态提示。
