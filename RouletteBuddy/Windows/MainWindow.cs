using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;
using RouletteBuddy.DAO;
using RouletteBuddy.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading.Tasks;

namespace RouletteBuddy.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private static bool showCharacterOverview;
    private static bool characterOverviewCacheDirty = true;
    private static readonly List<CharacterOverviewRow> CharacterOverviewRows = [];

    public MainWindow(Plugin plugin)
        : base("日随伴侣###rouletteRecorderMainWindow", ImGuiWindowFlags.NoCollapse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        this.plugin = plugin;
        IsOpen = Plugin.Configuration.EnableFloatingWindow;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        ApplyWindowOptions();
    }

    public override void Draw()
    {
        if (Plugin.Configuration.FloatingWindowStyleMode == FloatingWindowStyle.Minimal)
        {
            DrawMinimalStyle();
        }
        else
        {
            DrawClassicStyle();
        }

        DrawRouletteCompletionTips();
        OpenConfigOnRightClick();
        DrawCharacterOverviewPopup();
    }

    private void ApplyWindowOptions()
    {
        var isMinimal = Plugin.Configuration.FloatingWindowStyleMode == FloatingWindowStyle.Minimal;
        const ImGuiWindowFlags noScrollFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        var isLocked = Plugin.Configuration.LockFloatingWindow;
        var lockFlags = isLocked
            ? ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize
            : ImGuiWindowFlags.None;

        Flags = isMinimal
            ? ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse | noScrollFlags | lockFlags
            : ImGuiWindowFlags.NoCollapse | noScrollFlags | lockFlags;
        ShowCloseButton = !isMinimal;
        IsClickthrough = Plugin.Configuration.ClickthroughFloatingWindow;
        BgAlpha = Math.Clamp(Plugin.Configuration.FloatingWindowOpacity, 0.1f, 1.0f);

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        SizeCondition = ImGuiCond.None;
    }

    private void DrawClassicStyle()
    {
        if (ImGui.BeginTabBar("RouletteRecorderTabs"))
        {
            if (ImGui.BeginTabItem(Plugin.Localization.Localize("Current Task")))
            {
                DrawCurrentTaskTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Plugin.Localization.Localize("History Tasks")))
            {
                DrawHistoryTasksTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.Separator();
        DrawFooterButtons(false);
    }

    private void DrawFooterButtons(bool compact)
    {
        var settingsLabel = Plugin.Localization.Localize("Show Settings");
        var exportLabel = Plugin.Localization.Localize("Export as CSV");

        var settingsClicked = compact
            ? ImGui.SmallButton(settingsLabel)
            : ImGui.Button(settingsLabel);

        if (settingsClicked)
        {
            plugin.ToggleConfigUi();
        }

        ImGui.SameLine();

        var exportClicked = compact
            ? ImGui.SmallButton(exportLabel)
            : ImGui.Button(exportLabel);

        if (exportClicked)
        {
            Task.Run(() => Database.ExportAsCsv(Plugin.Configuration.CsvExportPath));
        }
    }

    private void DrawCurrentTaskTab()
    {
        var roulette = Roulette.Instance;
        var hasCurrentTask = roulette is { RouletteType: not null } || roulette is { ContentName: not null };
        var showCurrentTask = Plugin.Configuration.MinimalShowCurrentTask;
        var showTaskTime = Plugin.Configuration.MinimalShowTaskTime;
        var showTodayCount = Plugin.Configuration.MinimalShowTodayMentorRouletteCount;
        var showTotalCount = Plugin.Configuration.MinimalShowMentorRouletteTotalCount;
        var showCurrentTime = Plugin.Configuration.ShowCurrentTime;

        if (showCurrentTask && !hasCurrentTask)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No active task"));
        }

        if (showCurrentTask)
        {
            DrawProperty("Task Type", roulette?.RouletteType ?? Plugin.Localization.Localize("Unknown"));
            DrawProperty("Content Name", roulette?.ContentName ?? Plugin.Localization.Localize("Unknown"));
        }

        if (showTaskTime)
        {
            DrawProperty("Task Duration", GetTaskDurationText(roulette));
            DrawProperty("Start Time", roulette?.GetStartTimeText() ?? "-");
            DrawProperty("Completed", FormatBoolean(roulette?.IsCompleted));
        }

        ImGui.Spacing();

        if (showCurrentTime)
        {
            ImGui.TextColored(ImGuiColors.DalamudWhite, GetCurrentTimeText());
            ImGui.SameLine();
            DrawCharacterOverviewButton();
        }

        if (showTodayCount)
        {
            DrawProperty("Today Mentor Roulette Count", Database.GetTodayMentorRouletteCount().ToString(CultureInfo.InvariantCulture));
        }

        if (showTotalCount)
        {
            DrawProperty("Mentor Roulette Total Count", Plugin.GetMentorRouletteAchievementProgressText());
        }

    }

    private void DrawMinimalStyle()
    {
        DrawMinimalCurrentTaskTab();
    }

    private void DrawMinimalCurrentTaskTab()
    {
        var roulette = Roulette.Instance;
        var taskType = roulette?.RouletteType ?? Plugin.Localization.Localize("Unknown");
        var contentName = roulette?.ContentName ?? Plugin.Localization.Localize("Unknown");
        var duration = GetTaskDurationText(roulette);
        var showCurrentTask = Plugin.Configuration.MinimalShowCurrentTask;
        var showTaskTime = Plugin.Configuration.MinimalShowTaskTime;
        var showTodayCount = Plugin.Configuration.MinimalShowTodayMentorRouletteCount;
        var showTotalCount = Plugin.Configuration.MinimalShowMentorRouletteTotalCount;
        var showCurrentTime = Plugin.Configuration.ShowCurrentTime;

        if (showCurrentTask && roulette is not { RouletteType: not null } && roulette is not { ContentName: not null })
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No active task"));
        }

        if (showCurrentTask && showTaskTime)
        {
            ImGui.TextUnformatted($"{taskType}  |  {duration}");
        }
        else if (showCurrentTask)
        {
            ImGui.TextUnformatted($"{Plugin.Localization.Localize("Task Type")}: {taskType}");
        }
        else if (showTaskTime)
        {
            ImGui.TextUnformatted($"{Plugin.Localization.Localize("Task Time")}: {duration}");
        }

        if (showCurrentTask)
        {
            ImGui.TextDisabled($"{Plugin.Localization.Localize("Content Name")}: {contentName}");
        }

        if (showCurrentTime)
        {
            ImGui.TextColored(ImGuiColors.DalamudWhite, GetCurrentTimeText());
            ImGui.SameLine();
            DrawCharacterOverviewButton();
        }

        if (showTodayCount)
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, $"{Plugin.Localization.Localize("Today Mentor Roulette Count")}: {Database.GetTodayMentorRouletteCount()}");
        }

        if (showTotalCount)
        {
            ImGui.TextDisabled($"{Plugin.Localization.Localize("Mentor Roulette Total Count")}: {Plugin.GetMentorRouletteAchievementProgressText()}");
        }
    }

    private static void DrawHistoryTasksTab()
    {
        if (Database.Roulettes.Count == 0)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No history records"));
            return;
        }

        var sb = new StringBuilder();
        foreach (var roulette in Database.Roulettes.AsEnumerable().Reverse().Take(50))
        {
            sb.AppendLine(Plugin.Localization.Localize("Content Name") + "：" + (roulette.ContentName ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Task Type") + "：" + Database.GetRouletteTypeDisplayName(roulette.RouletteType));
            sb.AppendLine(Plugin.Localization.Localize("Duration") + "：" + roulette.GetDurationText());
            sb.AppendLine(Plugin.Localization.Localize("Start Time") + "：" + roulette.GetStartTimeText());
            sb.AppendLine(Plugin.Localization.Localize("End Time") + "：" + roulette.GetEndTimeText());
            sb.AppendLine();
        }

        var recordsText = sb.ToString().TrimEnd();
        var childHeight = Math.Min(GetHistoryChildHeight(), 600f);
        var recordsCopy = recordsText;
        ImGui.InputTextMultiline("##mainHistoryText", ref recordsCopy, recordsCopy.Length + 1, new Vector2(0, childHeight), ImGuiInputTextFlags.None);
    }

    private static float GetHistoryChildHeight()
    {
        var availableHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() - 8f;
        return availableHeight > 0 ? availableHeight : 0;
    }

    private static void DrawProperty(string label, string value)
    {
        ImGui.TextUnformatted($"{Plugin.Localization.Localize(label)}: {value}");
    }

    private static string GetTaskDurationText(Roulette? roulette)
    {
        if (roulette == null)
        {
            return "-";
        }

        return roulette.GetDurationText(roulette.IsCompleted ? null : DateTime.Now);
    }

    private static string GetCurrentTimeText()
    {
        return DateTime.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private static string FormatBoolean(bool? value)
    {
        return value == null
            ? "-"
            : Plugin.Localization.Localize(value.Value ? "Yes" : "No");
    }

    private static void DrawRouletteCompletionTips()
    {
        if (!Plugin.Configuration.ShowRouletteCompletionTips)
        {
            return;
        }

        if (Plugin.Configuration.PinRouletteCompletionTips)
        {
            DrawPinnedRouletteCompletionTipsWindow();
            return;
        }

        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            return;
        }

        ImGui.BeginTooltip();
        DrawRouletteCompletionTipsContent();
        ImGui.EndTooltip();
    }

    private static void DrawPinnedRouletteCompletionTipsWindow()
    {
        var open = true;
        ImGui.SetNextWindowBgAlpha(Math.Clamp(Plugin.Configuration.FloatingWindowOpacity, 0.1f, 1.0f));
        if (ImGui.Begin(
                $"{Plugin.Localization.Localize("Pinned Completion Tips Title")}###rouletteRecorderPinnedCompletionTips",
                ref open,
                ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse))
        {
            DrawRouletteCompletionTipsContent();
        }
        ImGui.End();

        if (!open)
        {
            Plugin.Configuration.PinRouletteCompletionTips = false;
            Plugin.Configuration.Save();
        }
    }

    private static void DrawRouletteCompletionTipsContent()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, "日随伴侣");
        ImGui.Separator();

        var hasSelectedTasks = false;
        var hasVisibleTasks = false;

        DrawMonitorTaskTipSection(
            "Daily Tasks",
            Database.GetDailyTaskMonitorOptions(),
            Plugin.Configuration.MonitoredDailyTaskKeys,
            Database.GetDailyMonitorTaskStatus,
            ref hasSelectedTasks,
            ref hasVisibleTasks);

        DrawMonitorTaskTipSection(
            "Weekly Tasks",
            Database.GetWeeklyNonSavageTaskMonitorOptions(),
            Plugin.Configuration.MonitoredWeeklyTaskKeys,
            Database.GetWeeklyMonitorTaskStatus,
            ref hasSelectedTasks,
            ref hasVisibleTasks);

        DrawMonitorTaskTipSection(
            "Alliance Raid Tasks",
            Database.GetWeeklyAllianceRaidTaskMonitorOptions(),
            Plugin.Configuration.MonitoredWeeklyTaskKeys,
            Database.GetWeeklyMonitorTaskStatus,
            ref hasSelectedTasks,
            ref hasVisibleTasks);

        DrawMonitorTaskTipSection(
            "Savage Raid Tasks",
            Database.GetWeeklySavageTaskMonitorOptions(),
            Plugin.Configuration.MonitoredWeeklyTaskKeys,
            Database.GetWeeklyMonitorTaskStatus,
            ref hasSelectedTasks,
            ref hasVisibleTasks);

        if (!hasSelectedTasks)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No Monitored Tasks"));
        }
        else if (!hasVisibleTasks)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("All Selected Monitor Tasks Completed"));
        }
    }

    private static void DrawMonitorTaskTipSection(
        string sectionTitleKey,
        IEnumerable<(string Key, string Name)> options,
        HashSet<string> selectedTaskKeys,
        Func<string, string, MonitorTaskStatus> getStatus,
        ref bool hasSelectedTasks,
        ref bool hasVisibleTasks)
    {
        var sectionStarted = false;
        var shownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in options)
        {
            if (!selectedTaskKeys.Contains(option.Key) ||
                !shownNames.Add(option.Name))
            {
                continue;
            }

            hasSelectedTasks = true;
            var status = getStatus(option.Key, option.Name);
            if (Plugin.Configuration.HideCompletedMonitorTasks &&
                status.State == MonitorTaskCompletionState.Completed)
            {
                continue;
            }

            if (!sectionStarted)
            {
                if (hasVisibleTasks)
                {
                    ImGui.Separator();
                }

                ImGui.TextColored(ImGuiColors.DalamudYellow, Plugin.Localization.Localize(sectionTitleKey));
                sectionStarted = true;
            }

            DrawMonitorTaskTipLine(GetMonitorTaskTipDisplayName(option.Key, option.Name), status);
            hasVisibleTasks = true;
        }
    }

    private static string GetMonitorTaskTipDisplayName(string taskKey, string taskName)
    {
        if (string.Equals(taskKey, Database.WeeklyTaskCurrentAllianceRaidKey, StringComparison.OrdinalIgnoreCase))
        {
            return Plugin.Localization.Localize("Current Alliance Raid Tip Name");
        }

        if (string.Equals(taskKey, Database.WeeklyTaskAllianceRaid1Key, StringComparison.OrdinalIgnoreCase))
        {
            return Plugin.Localization.Localize("Alliance Raid 1 Tip Name");
        }

        if (string.Equals(taskKey, Database.WeeklyTaskAllianceRaid2Key, StringComparison.OrdinalIgnoreCase))
        {
            return Plugin.Localization.Localize("Alliance Raid 2 Tip Name");
        }

        if (string.Equals(taskKey, Database.WeeklyTaskAllianceRaid3Key, StringComparison.OrdinalIgnoreCase))
        {
            return Plugin.Localization.Localize("Alliance Raid 3 Tip Name");
        }

        if (string.Equals(taskKey, Database.WeeklyTaskUnrealTrialKey, StringComparison.OrdinalIgnoreCase))
        {
            return Plugin.Localization.Localize("Unreal Trial Tip Name");
        }

        return taskName;
    }

    private static void DrawMonitorTaskTipLine(string taskName, MonitorTaskStatus status)
    {
        var statusColor = status.State switch
        {
            MonitorTaskCompletionState.Completed => ImGuiColors.DalamudWhite,
            MonitorTaskCompletionState.NotCompleted => ImGuiColors.DalamudRed,
            _ => ImGuiColors.DalamudGrey
        };

        ImGui.TextUnformatted($"{taskName}: ");
        ImGui.SameLine(0, 0);
        ImGui.TextColored(statusColor, Plugin.Localization.Localize(status.StatusText));

        if (!string.IsNullOrWhiteSpace(status.DetailText))
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"({status.DetailText})");
        }
    }

    private void OpenConfigOnRightClick()
    {
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            plugin.ToggleConfigUi();
        }
    }

    private void DrawCharacterOverviewPopup()
    {
        if (!showCharacterOverview) return;

        if (characterOverviewCacheDirty)
        {
            RebuildCharacterOverviewCache();
        }

        var open = true;
        var visible = ImGui.Begin("多角色概览###CharacterOverview", ref open, ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoCollapse);
        if (!open)
        {
            showCharacterOverview = false;
            ImGui.End();
            return;
        }

        if (visible)
        {
            if (ImGui.SmallButton("刷新"))
            {
                RebuildCharacterOverviewCache();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton("显示全部"))
            {
                Plugin.Configuration.HiddenCharacterOverviewIdentities.Clear();
                Plugin.Configuration.Save();
                RebuildCharacterOverviewCache();
            }
            ImGui.SameLine();
            ImGui.TextDisabled("悬停查看详情");

            if (CharacterOverviewRows.Count == 0)
            {
                ImGui.TextDisabled("暂无角色记录");
            }
            else if (ImGui.BeginTable("characterOverviewTable", 9, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                string? identityToHide = null;
                ImGui.TableSetupColumn("角色");
                ImGui.TableSetupColumn("服务器");
                ImGui.TableSetupColumn("每日任务");
                ImGui.TableSetupColumn("每周任务");
                ImGui.TableSetupColumn("团本");
                ImGui.TableSetupColumn("记忆神典石");
                ImGui.TableSetupColumn("数理神典石");
                ImGui.TableSetupColumn("缓存日期");
                ImGui.TableSetupColumn("操作");
                ImGui.TableHeadersRow();

                foreach (var row in CharacterOverviewRows)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.Text(row.PlayerName);
                    ImGui.TableNextColumn();
                    ImGui.Text(row.World);
                    ImGui.TableNextColumn();
                    DrawCharacterOverviewStatus(row.DailySummary, row.DailyDetail, row.DailyCompleted);
                    ImGui.TableNextColumn();
                    DrawCharacterOverviewStatus(row.WeeklySummary, row.WeeklyDetail, row.WeeklyCompleted);
                    ImGui.TableNextColumn();
                    DrawCharacterOverviewStatus(row.AllianceSummary, row.AllianceDetail, row.AllianceCompleted);
                    ImGui.TableNextColumn();
                    DrawCharacterOverviewStatus(row.TomestoneSummary, row.TomestoneDetail, row.TomestoneCompleted);
                    ImGui.TableNextColumn();
                    DrawCharacterOverviewCacheDate(row.MathematicsTomestoneSummary, row.TomestoneDetail);
                    ImGui.TableNextColumn();
                    DrawCharacterOverviewCacheDate(row.TomestoneCacheDate, row.TomestoneDetail);
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton($"隐藏##hideCharacterOverview-{row.Identity}"))
                    {
                        identityToHide = row.Identity;
                    }
                }

                ImGui.EndTable();

                if (identityToHide is not null)
                {
                    Plugin.Configuration.HiddenCharacterOverviewIdentities.Add(identityToHide);
                    Plugin.Configuration.Save();
                    RebuildCharacterOverviewCache();
                }
            }

            if (ImGui.Button("关闭"))
            {
                showCharacterOverview = false;
            }
        }

        ImGui.End();
    }

    private void DrawCharacterOverviewButton()
    {
        if (ImGui.SmallButton("多角色"))
        {
            if (!showCharacterOverview)
            {
                characterOverviewCacheDirty = true;
            }

            showCharacterOverview = !showCharacterOverview;
        }

        ImGui.SameLine();
        if (ImGui.SmallButton("便签"))
        {
            plugin.ToggleNoteUi();
        }
    }

    private static void DrawCharacterOverviewStatus(string summary, string detail, bool completed)
    {
        if (summary == "-")
        {
            ImGui.TextDisabled(summary);
        }
        else if (!completed && TryParseCharacterOverviewProgress(summary, out var completedCount, out var totalCount) && completedCount > 0)
        {
            ImGui.TextColored(ImGuiColors.HealerGreen, completedCount.ToString(CultureInfo.InvariantCulture));
            ImGui.SameLine(0, 0);
            ImGui.TextUnformatted($"/{totalCount.ToString(CultureInfo.InvariantCulture)}");
        }
        else
        {
            ImGui.TextColored(completed ? ImGuiColors.HealerGreen : ImGuiColors.DalamudYellow, summary);
        }

        if (ImGui.IsItemHovered() && !detail.IsNullOrWhitespace())
        {
            DrawCharacterOverviewTooltip(detail);
        }
    }

    private static void DrawCharacterOverviewCacheDate(string cacheDate, string detail)
    {
        if (cacheDate == "-")
        {
            ImGui.TextDisabled(cacheDate);
        }
        else
        {
            ImGui.TextUnformatted(cacheDate);
        }

        if (ImGui.IsItemHovered() && !detail.IsNullOrWhitespace())
        {
            DrawCharacterOverviewTooltip(detail);
        }
    }

    private static bool TryParseCharacterOverviewProgress(string summary, out int completedCount, out int totalCount)
    {
        completedCount = 0;
        totalCount = 0;
        var parts = summary.Split('/', 2);
        return parts.Length == 2 &&
               int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out completedCount) &&
               int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out totalCount);
    }

    private static void DrawCharacterOverviewTooltip(string detail)
    {
        ImGui.BeginTooltip();
        foreach (var rawLine in detail.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.StartsWith("√ ", StringComparison.Ordinal))
            {
                ImGui.TextColored(ImGuiColors.HealerGreen, line);
            }
            else
            {
                ImGui.TextUnformatted(line);
            }
        }

        ImGui.EndTooltip();
    }

    private static void RebuildCharacterOverviewCache()
    {
        CharacterOverviewRows.Clear();

        var characters = Database.GetCharacterIdentities();
        var currentPlayer = Plugin.GetPlayerName();
        var currentWorld = Plugin.GetPlayerWorldName();

        UpdateCurrentCharacterTomestoneCache(currentPlayer, currentWorld);

        if (!string.IsNullOrWhiteSpace(currentPlayer) && !string.IsNullOrWhiteSpace(currentWorld) &&
            !characters.Any(c => string.Equals(c.PlayerName, currentPlayer, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.World, currentWorld, StringComparison.OrdinalIgnoreCase)))
        {
            characters.Insert(0, (currentPlayer!, currentWorld!));
        }

        characters = characters
            .Where(character => !Plugin.Configuration.HiddenCharacterOverviewIdentities.Contains(GetCharacterOverviewIdentity(character.PlayerName, character.World)))
            .ToList();

        var dailyTasks = Database.GetDailyTaskMonitorOptions().ToList();
        var weeklyTasks = Database.GetWeeklyTaskMonitorOptions()
            .Where(task => !Database.IsWeeklyAllianceRaidTaskKey(task.Key))
            .ToList();
        var allianceTasks = Database.GetWeeklyAllianceRaidTaskMonitorOptions()
            .Where(task => Plugin.Configuration.MonitoredWeeklyTaskKeys.Contains(task.Key))
            .ToList();
        var weeklyResetAt = Database.GetCurrentWeeklyResetCycleStart();

        foreach (var (playerName, world) in characters)
        {
            var dailyCompleted = 0;
            var dailyDetail = new StringBuilder();
            foreach (var (_, name) in dailyTasks)
            {
                var done = Database.IsTaskHistoryRouletteCompletedForPlayer(name, playerName, world);
                if (done) dailyCompleted++;
                dailyDetail.AppendLine(done ? $"√ {name}" : $"x {name}");
            }

            var weeklyCompleted = 0;
            var weeklyDetail = new StringBuilder();
            foreach (var (key, name) in weeklyTasks)
            {
                var done = Database.IsTaskHistoryMonitorTaskCompletedForPlayer(key, name, playerName, world, weeklyResetAt);
                if (done) weeklyCompleted++;
                weeklyDetail.AppendLine(done ? $"√ {name}" : $"x {name}");
            }

            var allianceCompleted = 0;
            var allianceDetail = new StringBuilder();
            foreach (var (key, name) in allianceTasks)
            {
                var done = Database.IsTaskHistoryMonitorTaskCompletedForPlayer(key, name, playerName, world, weeklyResetAt);
                if (done) allianceCompleted++;
                allianceDetail.AppendLine(done ? $"√ {name}" : $"x {name}");
            }

            var identity = GetCharacterOverviewIdentity(playerName, world);
            var tomestoneSummary = "-";
            var mathematicsTomestoneSummary = "-";
            var tomestoneDetail = "暂无神典石缓存";
            var tomestoneCacheDate = "-";
            var tomestoneCompleted = false;
            if (Plugin.Configuration.CharacterTomestoneCaches.TryGetValue(identity, out var tomestoneCache))
            {
                tomestoneSummary = $"{tomestoneCache.WeeklyAcquired}/{tomestoneCache.WeeklyLimit}-{tomestoneCache.MemoryCount}";
                mathematicsTomestoneSummary = $"{tomestoneCache.MathematicsCount}/2000";
                tomestoneCompleted = tomestoneCache.WeeklyLimit > 0 && tomestoneCache.WeeklyAcquired >= tomestoneCache.WeeklyLimit;
                if (DateTime.TryParse(tomestoneCache.CachedAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var cachedAt))
                {
                    tomestoneCacheDate = cachedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    tomestoneDetail = $"记忆神典石：{tomestoneSummary}\n数理神典石：{mathematicsTomestoneSummary}\n缓存时间：{cachedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}";
                }
                else
                {
                    tomestoneDetail = $"记忆神典石：{tomestoneSummary}\n数理神典石：{mathematicsTomestoneSummary}\n缓存时间：{tomestoneCache.CachedAt}";
                }
            }
            CharacterOverviewRows.Add(new CharacterOverviewRow(
                identity,
                playerName,
                world,
                dailyTasks.Count == 0 ? "-" : $"{dailyCompleted}/{dailyTasks.Count}",
                dailyTasks.Count == 0 ? "暂无每日任务" : dailyDetail.ToString(),
                dailyTasks.Count > 0 && dailyCompleted == dailyTasks.Count,
                weeklyTasks.Count == 0 ? "-" : $"{weeklyCompleted}/{weeklyTasks.Count}",
                weeklyTasks.Count == 0 ? "暂无每周任务" : weeklyDetail.ToString(),
                weeklyTasks.Count > 0 && weeklyCompleted == weeklyTasks.Count,
                allianceTasks.Count == 0 ? "-" : $"{allianceCompleted}/{allianceTasks.Count}",
                allianceTasks.Count == 0 ? "暂无团本任务" : allianceDetail.ToString(),
                allianceTasks.Count > 0 && allianceCompleted == allianceTasks.Count,
                tomestoneSummary,
                mathematicsTomestoneSummary,
                tomestoneDetail,
                tomestoneCacheDate,
                tomestoneCompleted));
        }

        characterOverviewCacheDirty = false;
    }

    private static unsafe void UpdateCurrentCharacterTomestoneCache(string? currentPlayer, string? currentWorld)
    {
        if (!Plugin.ClientState.IsLoggedIn || string.IsNullOrWhiteSpace(currentPlayer) || string.IsNullOrWhiteSpace(currentWorld))
        {
            return;
        }

        var inventoryManager = InventoryManager.Instance();
        if (inventoryManager == null)
        {
            return;
        }

        var identity = GetCharacterOverviewIdentity(currentPlayer, currentWorld);
        Plugin.Configuration.CharacterTomestoneCaches[identity] = new CharacterTomestoneCacheEntry
        {
            WeeklyAcquired = Convert.ToUInt32(inventoryManager->GetWeeklyAcquiredTomestoneCount(), CultureInfo.InvariantCulture),
            WeeklyLimit = Convert.ToUInt32(InventoryManager.GetLimitedTomestoneWeeklyLimit(), CultureInfo.InvariantCulture),
            MemoryCount = GetCurrentTomestoneCount(inventoryManager, 3),
            MathematicsCount = GetCurrentTomestoneCount(inventoryManager, 2),
            CachedAt = DateTime.Now.ToString("O", CultureInfo.InvariantCulture)
        };
        Plugin.Configuration.Save();
    }

    private static unsafe uint GetCurrentTomestoneCount(InventoryManager* inventoryManager, uint tomestoneCategory)
    {
        var tomestoneItemId = Plugin.DataManager.GetExcelSheet<TomestonesItem>()
            .FirstOrDefault(tomestone => tomestone.Tomestones.RowId == tomestoneCategory)
            .Item.RowId;
        if (tomestoneItemId == 0)
        {
            return 0;
        }

        return Convert.ToUInt32(inventoryManager->GetInventoryItemCount(tomestoneItemId), CultureInfo.InvariantCulture);
    }

    private static string GetCharacterOverviewIdentity(string playerName, string world)
    {
        return $"{playerName}@{world}";
    }

    private sealed record CharacterOverviewRow(
        string Identity,
        string PlayerName,
        string World,
        string DailySummary,
        string DailyDetail,
        bool DailyCompleted,
        string WeeklySummary,
        string WeeklyDetail,
        bool WeeklyCompleted,
        string AllianceSummary,
        string AllianceDetail,
        bool AllianceCompleted,
        string TomestoneSummary,
        string MathematicsTomestoneSummary,
        string TomestoneDetail,
        string TomestoneCacheDate,
        bool TomestoneCompleted);

    public string PrintProperty(string messageTemplate, string? value)
    {
        return string.Format(Plugin.Localization.Localize(messageTemplate), value ?? "null");
    }
}
