using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using RouletteRecorder.Dalamud.DAO;
using RouletteRecorder.Dalamud.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace RouletteRecorder.Dalamud.Windows;

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

        var tableFlags = ImGuiTableFlags.Borders |
                         ImGuiTableFlags.RowBg |
                         ImGuiTableFlags.Resizable |
                         ImGuiTableFlags.ScrollY |
                         ImGuiTableFlags.SizingStretchProp;

        if (!ImGui.BeginTable("RouletteRecorderHistoryTable", 5, tableFlags, new Vector2(0, GetHistoryTableHeight())))
        {
            return;
        }

        ImGui.TableSetupColumn(Plugin.Localization.Localize("Content Name"));
        ImGui.TableSetupColumn(Plugin.Localization.Localize("Task Type"));
        ImGui.TableSetupColumn(Plugin.Localization.Localize("Duration"));
        ImGui.TableSetupColumn(Plugin.Localization.Localize("Start Time"));
        ImGui.TableSetupColumn(Plugin.Localization.Localize("End Time"));
        ImGui.TableHeadersRow();

        foreach (var roulette in Database.Roulettes.AsEnumerable().Reverse())
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextWrapped(roulette.ContentName ?? "-");

            ImGui.TableNextColumn();
            ImGui.TextWrapped(roulette.RouletteType ?? "-");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(roulette.GetDurationText());

            ImGui.TableNextColumn();
            ImGui.TextWrapped(roulette.GetStartTimeText());

            ImGui.TableNextColumn();
            ImGui.TextWrapped(roulette.GetEndTimeText());
        }

        ImGui.EndTable();
    }

    private static float GetHistoryTableHeight()
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

        if (!ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows))
        {
            return;
        }

        ImGui.BeginTooltip();
        ImGui.TextColored(ImGuiColors.DalamudYellow, "日随伴侣");
        ImGui.Separator();

        var hasMonitoredTasks = false;
        var shownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in Database.GetDailyTaskMonitorOptions())
        {
            if (!Plugin.Configuration.MonitoredDailyTaskKeys.Contains(option.Key) ||
                !shownNames.Add(option.Name))
            {
                continue;
            }

            DrawRouletteCompletionTipLine(option.Key, option.Name);
            hasMonitoredTasks = true;
        }

        if (!hasMonitoredTasks)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No Monitored Daily Tasks"));
        }

        ImGui.EndTooltip();
    }

    private static void DrawRouletteCompletionTipLine(string taskKey, string rouletteName)
    {
        var isCompleted = Database.IsDailyTaskCompletedInCurrentResetCycle(taskKey, rouletteName);
        var statusColor = isCompleted ? ImGuiColors.DalamudWhite : ImGuiColors.DalamudRed;
        ImGui.TextUnformatted($"{rouletteName}: ");
        ImGui.SameLine(0, 0);
        ImGui.TextColored(statusColor, Plugin.Localization.Localize(isCompleted ? "Roulette Completed" : "Roulette Not Completed"));
    }

    private void OpenConfigOnRightClick()
    {
        if (ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            plugin.OpenConfigUi();
        }
    }

    public string PrintProperty(string messageTemplate, string? value)
    {
        return string.Format(Plugin.Localization.Localize(messageTemplate), value ?? "null");
    }
}
