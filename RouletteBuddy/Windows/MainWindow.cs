using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
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

    private void DrawMinimalStyle()
    {
        DrawMinimalCurrentTaskTab();
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

    private static void DrawCurrentTaskTab()
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
        }

        if (showTodayCount)
        {
            DrawProperty("Today Mentor Roulette Count", Database.GetTodayMentorRouletteCount().ToString(CultureInfo.InvariantCulture));
        }

        if (showTotalCount)
        {
            DrawProperty("Mentor Roulette Total Count", Plugin.GetMentorRouletteAchievementProgressText());
            ImGui.SameLine();
            if (ImGui.SmallButton(Plugin.Localization.Localize("Refresh Achievement Progress")))
            {
                Plugin.RefreshMentorRouletteAchievementProgress();
            }
        }
    }

    private static void DrawMinimalCurrentTaskTab()
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

    public string PrintProperty(string messageTemplate, string? value)
    {
        return string.Format(Plugin.Localization.Localize(messageTemplate), value ?? "null");
    }
}
