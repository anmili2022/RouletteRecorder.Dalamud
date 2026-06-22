using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using RouletteBuddy.DAO;
using RouletteBuddy.Network.DungeonLogger;
using RouletteBuddy.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Numerics;
using System.Threading.Tasks;

namespace RouletteBuddy.Windows;

public sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private enum LoginStatus
    {
        Initial,
        Pending,
        Success,
        Failed
    }

    private string loginResponseMessage = string.Empty;
    private LoginStatus loginStatus = LoginStatus.Initial;
    private static bool showClearConfirm;
    private static string subscribedRecordsSearch = string.Empty;
    private static string taskHistoryPlayerSearch = string.Empty;
    private static string taskHistoryTaskSearch = string.Empty;

    public ConfigWindow(Plugin plugin)
        : base($"设置窗口 v{typeof(Plugin).Assembly.GetName().Version}###rouletteRecorderConfigWindow", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawHeader();
        DrawSaveFolderSection();
        DrawFloatingWindowStyleSection();
        DrawPersonalNoteSection();
        DrawDailyTaskMonitorSection();
        DrawSubscribedRouletteTypesSection();
        DrawDungeonLoggerSection();
        DrawStoredRecordsSection();
        DrawLanguageSection();
    }

    private static void DrawHeader()
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, Plugin.Localization.Localize("Settings Overview"));
        ImGui.TextWrapped(Plugin.Localization.Localize("Settings Overview Description"));
        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void DrawSaveFolderSection()
    {
        DrawSectionTitle("Save Data");
        ImGui.TextDisabled(Plugin.Localization.Localize("Save Folder Hint"));
        ImGui.TextWrapped(Plugin.PluginInterface.ConfigDirectory.FullName);

        if (ImGui.Button(Plugin.Localization.Localize("Open Save Folder")))
        {
            OpenSaveFolder();
        }

        ImGui.SameLine();
        var recordAll = Plugin.Configuration.RecordAllDungeons;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Record All Dungeons"), ref recordAll))
        {
            Plugin.Configuration.RecordAllDungeons = recordAll;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button(Plugin.Localization.Localize("Clear Records")))
        {
            ImGui.OpenPopup("##clearRecordsConfirm");
        }

        if (ImGui.BeginPopupModal("##clearRecordsConfirm", ref showClearConfirm, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted(Plugin.Localization.Localize("Clear Records Confirm"));
            if (ImGui.Button(Plugin.Localization.Localize("Yes")))
            {
                Database.ClearTaskHistory();
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button(Plugin.Localization.Localize("No")))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }

        ImGui.Separator();
        ImGui.Spacing();
    }

    private static void OpenSaveFolder()
    {
        try
        {
            var saveFolder = Plugin.PluginInterface.ConfigDirectory.FullName;
            Directory.CreateDirectory(saveFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = saveFolder,
                UseShellExecute = true
            });
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Failed to open save folder");
        }
    }

    private void DrawFloatingWindowStyleSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Floating Window Style"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.Indent();

        DrawSubTitle("Appearance");

        var currentStyle = Plugin.Configuration.FloatingWindowStyleMode;
        var currentLabel = GetFloatingWindowStyleLabel(currentStyle);

        ImGui.SetNextItemWidth(-1f);
        if (ImGui.BeginCombo($"{Plugin.Localization.Localize("Display Style")}##floatingWindowStyle", currentLabel))
        {
            foreach (FloatingWindowStyle style in Enum.GetValues(typeof(FloatingWindowStyle)))
            {
                var isSelected = currentStyle == style;
                if (ImGui.Selectable(GetFloatingWindowStyleLabel(style), isSelected))
                {
                    Plugin.Configuration.FloatingWindowStyleMode = style;
                    Plugin.Configuration.Save();
                    currentStyle = style;
                }

                if (isSelected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        ImGui.TextDisabled(Plugin.Localization.Localize("Current Style Description"));
        ImGui.TextWrapped(GetFloatingWindowStyleDescription(currentStyle));
        ImGui.Spacing();

        var opacityPercent = (int)Math.Round(Math.Clamp(Plugin.Configuration.FloatingWindowOpacity, 0.1f, 1.0f) * 100f);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderInt($"{Plugin.Localization.Localize("Floating Window Opacity")}##floatingWindowOpacity", ref opacityPercent, 10, 100, "%d%%"))
        {
            Plugin.Configuration.FloatingWindowOpacity = Math.Clamp(opacityPercent / 100f, 0.1f, 1.0f);
            Plugin.Configuration.Save();
        }
        ImGui.TextDisabled(Plugin.Localization.Localize("Floating Window Opacity Hint"));

        ImGui.Spacing();
        DrawSubTitle("Window Behavior");

        var lockFloatingWindow = Plugin.Configuration.LockFloatingWindow;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Lock Floating Window"), ref lockFloatingWindow))
        {
            Plugin.Configuration.LockFloatingWindow = lockFloatingWindow;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var clickthroughFloatingWindow = Plugin.Configuration.ClickthroughFloatingWindow;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Clickthrough Floating Window"), ref clickthroughFloatingWindow))
        {
            Plugin.Configuration.ClickthroughFloatingWindow = clickthroughFloatingWindow;
            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        var enableFloatingWindow = plugin.IsMainUiOpen;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Enable Floating Window"), ref enableFloatingWindow))
        {
            plugin.SetMainUiOpen(enableFloatingWindow);
        }

        ImGui.TextDisabled(Plugin.Localization.Localize("Clickthrough Floating Window Hint"));

        var showRouletteCompletionTips = Plugin.Configuration.ShowRouletteCompletionTips;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Show Roulette Completion Tips"), ref showRouletteCompletionTips))
        {
            Plugin.Configuration.ShowRouletteCompletionTips = showRouletteCompletionTips;
            if (!showRouletteCompletionTips)
            {
                Plugin.Configuration.PinRouletteCompletionTips = false;
            }

            Plugin.Configuration.Save();
        }

        ImGui.SameLine();
        DrawPinnedCompletionTipsButton();

        var hideCompletedMonitorTasks = Plugin.Configuration.HideCompletedMonitorTasks;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Hide Completed Tasks"), ref hideCompletedMonitorTasks))
        {
            Plugin.Configuration.HideCompletedMonitorTasks = hideCompletedMonitorTasks;
            Plugin.Configuration.Save();
        }

        ImGui.TextDisabled(Plugin.Localization.Localize("Hide Completed Tasks Hint"));

        ImGui.Spacing();
        DrawSubTitle("Display Content");
        ImGui.TextDisabled(Plugin.Localization.Localize("Floating Window Display Items"));
        if (ImGui.BeginTable("FloatingWindowDisplayItemsTable", 2, ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableNextColumn();
            DrawDisplayItemCheckbox("Show Current Task", Plugin.Configuration.MinimalShowCurrentTask, value => Plugin.Configuration.MinimalShowCurrentTask = value);

            ImGui.TableNextColumn();
            DrawDisplayItemCheckbox("Show Task Time", Plugin.Configuration.MinimalShowTaskTime, value => Plugin.Configuration.MinimalShowTaskTime = value);

            ImGui.TableNextColumn();
            DrawDisplayItemCheckbox("Show Today Mentor Roulette Count", Plugin.Configuration.MinimalShowTodayMentorRouletteCount, value => Plugin.Configuration.MinimalShowTodayMentorRouletteCount = value);

            ImGui.TableNextColumn();
            DrawDisplayItemCheckbox("Show Mentor Roulette Total Count", Plugin.Configuration.MinimalShowMentorRouletteTotalCount, value => Plugin.Configuration.MinimalShowMentorRouletteTotalCount = value);

            ImGui.TableNextColumn();
            DrawDisplayItemCheckbox("Show Current Time", Plugin.Configuration.ShowCurrentTime, value => Plugin.Configuration.ShowCurrentTime = value);

            ImGui.EndTable();
        }

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private void DrawPersonalNoteSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Personal Note"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.Indent();
        ImGui.TextDisabled(Plugin.Localization.Localize("Personal Note Hint"));

        var enableNoteWindow = plugin.IsNoteUiOpen;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Enable Note Window"), ref enableNoteWindow))
        {
            plugin.SetNoteUiOpen(enableNoteWindow);
        }

        ImGui.Spacing();

        ImGui.TextUnformatted(Plugin.Localization.Localize("Note Type"));
        var currentScope = Plugin.Configuration.NoteScopeMode;
        DrawNoteScopeRadioButton(NoteScope.Public, ref currentScope);
        ImGui.SameLine();
        DrawNoteScopeRadioButton(NoteScope.Character, ref currentScope);

        ImGui.TextWrapped(GetNoteScopeDescription(currentScope));
        ImGui.Spacing();

        ImGui.TextUnformatted(Plugin.Localization.Localize("Note Background Style"));
        var currentBackgroundStyle = Plugin.Configuration.NoteBackgroundStyleMode;
        DrawNoteBackgroundStyleRadioButton(NoteBackgroundStyle.Frosted, ref currentBackgroundStyle);
        ImGui.SameLine();
        DrawNoteBackgroundStyleRadioButton(NoteBackgroundStyle.Transparent, ref currentBackgroundStyle);

        ImGui.TextWrapped(GetNoteBackgroundStyleDescription(currentBackgroundStyle));
        DrawNoteAppearanceSliders(currentBackgroundStyle);

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawNoteScopeRadioButton(NoteScope scope, ref NoteScope currentScope)
    {
        var isSelected = currentScope == scope;
        if (!ImGui.RadioButton($"{GetNoteScopeLabel(scope)}##noteScope{scope}", isSelected))
        {
            return;
        }

        Plugin.Configuration.NoteScopeMode = scope;
        Plugin.Configuration.Save();
        currentScope = scope;
    }

    private static void DrawNoteBackgroundStyleRadioButton(NoteBackgroundStyle style, ref NoteBackgroundStyle currentBackgroundStyle)
    {
        var isSelected = currentBackgroundStyle == style;
        if (!ImGui.RadioButton($"{GetNoteBackgroundStyleLabel(style)}##noteBackgroundStyle{style}", isSelected))
        {
            return;
        }

        Plugin.Configuration.NoteBackgroundStyleMode = style;
        Plugin.Configuration.Save();
        currentBackgroundStyle = style;
    }

    private static void DrawNoteAppearanceSliders(NoteBackgroundStyle currentBackgroundStyle)
    {
        ImGui.Spacing();

        if (currentBackgroundStyle == NoteBackgroundStyle.Frosted)
        {
            var frostedStrengthPercent = (int)Math.Round(Math.Clamp(Plugin.Configuration.NoteFrostedStrength, 0f, 1f) * 100f);
            ImGui.SetNextItemWidth(-1f);
            if (ImGui.SliderInt(
                    $"{Plugin.Localization.Localize("Note Frosted Strength")}##noteFrostedStrength",
                    ref frostedStrengthPercent,
                    0,
                    100,
                    "%d%%"))
            {
                Plugin.Configuration.NoteFrostedStrength = Math.Clamp(frostedStrengthPercent / 100f, 0f, 1f);
                Plugin.Configuration.Save();
            }

            ImGui.TextDisabled(Plugin.Localization.Localize("Note Frosted Strength Hint"));
            ImGui.Spacing();
        }

        var opacity = currentBackgroundStyle == NoteBackgroundStyle.Transparent
            ? Plugin.Configuration.NoteTransparentWindowOpacity
            : Plugin.Configuration.NoteFrostedWindowOpacity;
        var opacityPercent = (int)Math.Round(Math.Clamp(opacity, 0.05f, 1.0f) * 100f);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderInt(
                $"{Plugin.Localization.Localize("Note Window Opacity")}##noteWindowOpacity",
                ref opacityPercent,
                5,
                100,
                "%d%%"))
        {
            var newOpacity = Math.Clamp(opacityPercent / 100f, 0.05f, 1.0f);
            if (currentBackgroundStyle == NoteBackgroundStyle.Transparent)
            {
                Plugin.Configuration.NoteTransparentWindowOpacity = newOpacity;
            }
            else
            {
                Plugin.Configuration.NoteFrostedWindowOpacity = newOpacity;
            }

            Plugin.Configuration.Save();
        }

        ImGui.TextDisabled(Plugin.Localization.Localize("Note Window Opacity Hint"));
    }

    private void DrawDailyTaskMonitorSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Daily And Weekly Task Monitor"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.Indent();
        ImGui.TextDisabled(Plugin.Localization.Localize("Daily And Weekly Task Monitor Hint"));
        ImGui.Spacing();

        DrawSubTitle("Daily Tasks");
        DrawTaskMonitorOptionTable(
            "DailyTaskMonitorTable",
            Database.GetDailyTaskMonitorOptions(),
            optionKey => Plugin.Configuration.MonitoredDailyTaskKeys.Contains(optionKey),
            (optionKey, selected) => Plugin.Configuration.SetMonitoredDailyTaskKey(optionKey, selected),
            "dailyTaskMonitor");

        DrawTribalQuestCompletionCountSlider();

        ImGui.Spacing();
        DrawSubTitle("Weekly Task Module");
        DrawTaskMonitorOptionTable(
            "WeeklyTaskMonitorTable",
            Database.GetWeeklyNonSavageTaskMonitorOptions(),
            optionKey => Plugin.Configuration.MonitoredWeeklyTaskKeys.Contains(optionKey),
            (optionKey, selected) => Plugin.Configuration.SetMonitoredWeeklyTaskKey(optionKey, selected),
            "weeklyTaskMonitor");

        ImGui.Spacing();
        DrawSubTitle("Alliance Raid Task Module");
        DrawWeeklyAllianceRaidTaskModule();

        ImGui.Spacing();
        DrawSubTitle("Savage Raid Task Module");
        DrawWeeklySavageTaskModule();

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawTribalQuestCompletionCountSlider()
    {
        ImGui.Spacing();

        var completionCount = Math.Clamp(Plugin.Configuration.TribalQuestCompletionCount, 1, Database.MaxTribalQuestAllowance);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.SliderInt(
                $"{Plugin.Localization.Localize("Tribal Quest Completion Count")}##tribalQuestCompletionCount",
                ref completionCount,
                1,
                Database.MaxTribalQuestAllowance,
                "%d"))
        {
            Plugin.Configuration.TribalQuestCompletionCount = completionCount;
            Plugin.Configuration.Save();
        }

        ImGui.TextDisabled(Plugin.Localization.Localize("Tribal Quest Completion Count Hint"));
    }

    private static void DrawWeeklySavageTaskModule()
    {
        var savageOptions = Database.GetWeeklySavageTaskMonitorOptions().ToArray();
        DrawTaskMonitorOptionTable(
            "WeeklySavageTaskMonitorTable",
            savageOptions,
            optionKey => Plugin.Configuration.MonitoredWeeklyTaskKeys.Contains(optionKey),
            (optionKey, selected) => Plugin.Configuration.SetMonitoredWeeklyTaskKey(optionKey, selected),
            "weeklySavageTaskMonitor");

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        ImGui.TextWrapped(Plugin.Localization.Localize("Savage Raid Tasks Hint"));
        ImGui.PopStyleColor();
    }

    private static void DrawWeeklyAllianceRaidTaskModule()
    {
        var allianceRaidOptions = Database.GetWeeklyAllianceRaidTaskMonitorOptions().ToArray();
        DrawTaskMonitorOptionTable(
            "WeeklyAllianceRaidTaskMonitorTable",
            allianceRaidOptions,
            optionKey => Plugin.Configuration.MonitoredWeeklyTaskKeys.Contains(optionKey),
            (optionKey, selected) => Plugin.Configuration.SetMonitoredWeeklyTaskKey(optionKey, selected),
            "weeklyAllianceRaidTaskMonitor");

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        ImGui.TextWrapped(Plugin.Localization.Localize("Alliance Raid Tasks Hint"));
        ImGui.PopStyleColor();
    }

    private static void DrawTaskMonitorOptionTable(
        string tableId,
        IEnumerable<(string Key, string Name)> options,
        Func<string, bool> isSelected,
        Action<string, bool> setSelected,
        string checkboxIdPrefix)
    {
        if (ImGui.BeginTable(tableId, 2, ImGuiTableFlags.SizingStretchProp))
        {
            var index = 0;
            foreach (var option in options)
            {
                ImGui.TableNextColumn();

                var selected = isSelected(option.Key);
                if (ImGui.Checkbox($"{option.Name}##{checkboxIdPrefix}{option.Key}", ref selected))
                {
                    setSelected(option.Key, selected);
                }

                index++;
                if (index % 2 == 0)
                {
                    ImGui.TableNextRow();
                }
            }

            ImGui.EndTable();
        }
    }

    private static void DrawSubscribedRouletteTypesSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Subscribed Roulette Types")))
        {
            return;
        }

        ImGui.Indent();
        ImGui.TextDisabled(string.Format(
            Plugin.Localization.Localize("Subscribed Roulette Summary"),
            Plugin.Configuration.SubscribedRouletteIds.Count,
            Database.CfRoulettes.Length));
        ImGui.Spacing();

        if (ImGui.BeginTable("SubscribedRouletteTypesTable", 2, ImGuiTableFlags.SizingStretchProp))
        {
            var index = 0;
            foreach (var roulette in Database.CfRoulettes)
            {
                ImGui.TableNextColumn();

                var selected = Plugin.Configuration.SubscribedRouletteIds.Contains(roulette.RowId);
                if (ImGui.Checkbox($"{roulette.Name}##subscribedRoulette{roulette.RowId}", ref selected))
                {
                    Plugin.Configuration.SetSubscribedRouletteId(roulette, selected);
                }

                index++;
                if (index % 2 == 0)
                {
                    ImGui.TableNextRow();
                }
            }

            ImGui.EndTable();
        }

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private void DrawDungeonLoggerSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("DungeonLogger Account Config")))
        {
            return;
        }

        ImGui.Indent();
        ImGui.TextDisabled(Plugin.Localization.Localize("DungeonLogger Hint"));

        if (ImGui.Checkbox(Plugin.Localization.Localize("Enable DungeonLogger Report"), ref Plugin.Configuration.DungeonLoggerConfig.Enabled))
        {
            Plugin.Configuration.Save();
        }

        if (Plugin.Configuration.DungeonLoggerConfig.Enabled)
        {
            ImGui.Spacing();
            DrawLoginInput("User Name", "##username", ref Plugin.Configuration.DungeonLoggerConfig.Username, ImGuiInputTextFlags.None);
            DrawLoginInput("Password", "##password", ref Plugin.Configuration.DungeonLoggerConfig.Password, ImGuiInputTextFlags.Password);
        }

        ImGui.Spacing();
        if (ImGui.Button(Plugin.Localization.Localize("Test Login")))
        {
            Task.Run(TestDungeonLoggerLogin);
        }

        DrawLoginStatus();
        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawPinnedCompletionTipsButton()
    {
        var isPinned = Plugin.Configuration.PinRouletteCompletionTips;
        var label = Plugin.Localization.Localize(isPinned ? "Unpin Completion Tips" : "Pin Completion Tips");

        if (!Plugin.Configuration.ShowRouletteCompletionTips)
        {
            ImGui.BeginDisabled();
        }

        if (ImGui.Button(label))
        {
            Plugin.Configuration.PinRouletteCompletionTips = !isPinned;
            Plugin.Configuration.Save();
        }

        if (!Plugin.Configuration.ShowRouletteCompletionTips)
        {
            ImGui.EndDisabled();
        }
    }

    private static void DrawStoredRecordsSection()
    {
        DrawSubscribedTaskRecordsSection();
        DrawTaskHistoryRecordsSection();
    }

    private static void DrawSubscribedTaskRecordsSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Subscribed Task Records")))
        {
            return;
        }

        ImGui.Indent();
        DrawRecordsSearchBar("##subscribedRecordsSearch", "Search task name", ref subscribedRecordsSearch);

        var filteredRecords = Database.Roulettes
            .Where(record => MatchesSubscribedRecord(record, subscribedRecordsSearch))
            .ToList();

        ImGui.TextDisabled(string.Format(
            Plugin.Localization.Localize("Record Count Format"),
            filteredRecords.Count));

        if (filteredRecords.Count == 0)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No history records"));
            ImGui.Unindent();
            ImGui.Spacing();
            return;
        }

        var sb = new StringBuilder();
        foreach (var record in filteredRecords.AsEnumerable().Reverse().Take(100))
        {
            sb.AppendLine(Plugin.Localization.Localize("Content Name") + "：" + (record.ContentName ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Task Type") + "：" + Database.GetRouletteTypeDisplayName(record.RouletteType));
            sb.AppendLine(Plugin.Localization.Localize("Duration") + "：" + record.GetDurationText(record.IsCompleted ? null : DateTime.Now));
            sb.AppendLine(Plugin.Localization.Localize("Start Time") + "：" + record.GetStartTimeText());
            sb.AppendLine(Plugin.Localization.Localize("End Time") + "：" + record.GetEndTimeText());
            sb.AppendLine(Plugin.Localization.Localize("Job Name") + "：" + (record.JobName ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Completed") + "：" + Plugin.Localization.Localize(record.IsCompleted ? "Yes" : "No"));
            sb.AppendLine();
        }

        var recordsText = sb.ToString().TrimEnd();
        var recordsCopy = recordsText;
        ImGui.InputTextMultiline("##subscribedRecordsText", ref recordsCopy, recordsCopy.Length + 1, new Vector2(-1, Math.Min(ImGui.CalcTextSize(recordsText).Y + 10, 400f)), ImGuiInputTextFlags.None);

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawTaskHistoryRecordsSection()
    {
        Database.ReloadTaskHistoryIfChanged();

        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Task History Records")))
        {
            return;
        }

        ImGui.Indent();
        DrawRecordsSearchBar("##taskHistoryPlayerSearch", "Search player", ref taskHistoryPlayerSearch);
        DrawRecordsSearchBar("##taskHistoryTaskSearch", "Search task name", ref taskHistoryTaskSearch);

        var filteredRecords = Database.TaskHistoryRoulettes
            .Where(record => MatchesTaskHistoryRecord(record, taskHistoryPlayerSearch, taskHistoryTaskSearch))
            .ToList();

        ImGui.TextDisabled(string.Format(
            Plugin.Localization.Localize("Record Count Format"),
            filteredRecords.Count));
        ImGui.TextDisabled(Database.TaskHistoryDbPath);

        if (filteredRecords.Count == 0)
        {
            ImGui.TextDisabled(Plugin.Localization.Localize("No history records"));
            ImGui.Unindent();
            ImGui.Spacing();
            return;
        }

        var sb = new StringBuilder();
        foreach (var record in filteredRecords.AsEnumerable().Reverse().Take(100))
        {
            sb.AppendLine(Plugin.Localization.Localize("Content Name") + "：" + (record.ContentName ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Task Type") + "：" + Database.GetRouletteTypeDisplayName(record.RouletteType, record.MonitorTaskKey));
            sb.AppendLine(Plugin.Localization.Localize("Start Time") + "：" + GetTaskHistoryStartTimeText(record));
            sb.AppendLine(Plugin.Localization.Localize("End Time") + "：" + GetTaskHistoryEndTimeText(record));
            sb.AppendLine(Plugin.Localization.Localize("Job Name") + "：" + (record.JobName ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Completed") + "：" + Plugin.Localization.Localize(record.IsCompleted ? "Yes" : "No"));
            sb.AppendLine(Plugin.Localization.Localize("Player Name") + "：" + (record.PlayerName ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("World") + "：" + (record.World ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Monitor Task Key") + "：" + (record.MonitorTaskKey ?? "-"));
            sb.AppendLine(Plugin.Localization.Localize("Record Source") + "：" + (record.MonitorTaskKey.IsNullOrWhitespace()
                ? (string.Equals(record.RouletteType, record.ContentName, StringComparison.OrdinalIgnoreCase)
                    ? Plugin.Localization.Localize("Direct Queue Record")
                    : Plugin.Localization.Localize("Daily Roulette Record"))
                : Plugin.Localization.Localize("Monitor Task Record")));
            sb.AppendLine();
        }

        var recordsText = sb.ToString().TrimEnd();
        var recordsCopy = recordsText;
        ImGui.InputTextMultiline("##taskHistoryRecordsText", ref recordsCopy, recordsCopy.Length + 1, new Vector2(-1, Math.Min(ImGui.CalcTextSize(recordsText).Y + 10, 400f)), ImGuiInputTextFlags.None);

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private static void DrawRecordsSearchBar(string idSuffix, string hintKey, ref string value)
    {
        ImGui.SetNextItemWidth(-120f);
        ImGui.InputTextWithHint($"{idSuffix}##search", Plugin.Localization.Localize(hintKey), ref value, 200);
        ImGui.SameLine();
        if (ImGui.SmallButton($"清除{idSuffix}"))
        {
            value = string.Empty;
        }
    }

    private static bool MatchesSubscribedRecord(Roulette record, string searchText)
    {
        if (searchText.IsNullOrWhitespace())
        {
            return true;
        }

        return ContainsText(record.ContentName, searchText) ||
               ContainsText(Database.GetRouletteTypeDisplayName(record.RouletteType), searchText);
    }

    private static bool MatchesTaskHistoryRecord(TaskHistoryRoulette record, string playerSearchText, string taskSearchText)
    {
        var playerMatches = playerSearchText.IsNullOrWhitespace() ||
                            ContainsText(record.PlayerName, playerSearchText) ||
                            ContainsText(record.World, playerSearchText);
        var taskMatches = taskSearchText.IsNullOrWhitespace() ||
                          ContainsText(record.ContentName, taskSearchText) ||
                          ContainsText(Database.GetRouletteTypeDisplayName(record.RouletteType, record.MonitorTaskKey), taskSearchText) ||
                          ContainsText(record.MonitorTaskKey, taskSearchText);

        return playerMatches && taskMatches;
    }

    private static bool ContainsText(string? value, string searchText)
    {
        return !value.IsNullOrWhitespace() &&
               value.Contains(searchText, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTaskHistoryStartTimeText(TaskHistoryRoulette record)
    {
        return record.GetStartedDateTime()?.ToString("yyyy-MM-dd HH:mm:ss") ?? $"{record.Date} {record.StartedAt}";
    }

    private static string GetTaskHistoryEndTimeText(TaskHistoryRoulette record)
    {
        if (record.EndedAt.IsNullOrWhitespace())
        {
            return "-";
        }

        return record.GetEndedDateTime()?.ToString("yyyy-MM-dd HH:mm:ss") ?? $"{record.Date} {record.EndedAt}";
    }

    private static void DrawLoginInput(string labelKey, string id, ref string value, ImGuiInputTextFlags flags)
    {
        ImGui.TextUnformatted(Plugin.Localization.Localize(labelKey));
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.InputText(id, ref value, 100, flags))
        {
            Plugin.Configuration.Save();
        }
    }

    private void DrawLoginStatus()
    {
        var loginStatusColor = loginStatus switch
        {
            LoginStatus.Pending => ImGuiColors.DalamudYellow,
            LoginStatus.Success => ImGuiColors.ParsedGreen,
            LoginStatus.Failed => ImGuiColors.DalamudRed,
            _ => ImGuiColors.DalamudWhite
        };

        const string pendingMessage = "Sending request to Dungeon Logger Server";
        var message = loginStatus == LoginStatus.Pending ? pendingMessage : loginResponseMessage;
        if (!message.IsNullOrWhitespace())
        {
            ImGui.TextColored(loginStatusColor, Plugin.Localization.Localize(message));
        }
    }

    private static void DrawSectionTitle(string labelKey)
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, Plugin.Localization.Localize(labelKey));
    }

    private static void DrawSubTitle(string labelKey)
    {
        ImGui.TextColored(ImGuiColors.DalamudYellow, Plugin.Localization.Localize(labelKey));
        ImGui.Separator();
    }

    private static void DrawDisplayItemCheckbox(string labelKey, bool currentValue, Action<bool> setValue)
    {
        var value = currentValue;
        if (ImGui.Checkbox(Plugin.Localization.Localize(labelKey), ref value))
        {
            setValue(value);
            Plugin.Configuration.Save();
        }
    }

    private static string GetFloatingWindowStyleLabel(FloatingWindowStyle style)
    {
        return style switch
        {
            FloatingWindowStyle.Classic => Plugin.Localization.Localize("Classic Style"),
            FloatingWindowStyle.Minimal => Plugin.Localization.Localize("Minimal Style"),
            _ => style.ToString()
        };
    }

    private static string GetFloatingWindowStyleDescription(FloatingWindowStyle style)
    {
        return Plugin.Localization.Localize(style switch
        {
            FloatingWindowStyle.Classic => "Classic Style Description",
            FloatingWindowStyle.Minimal => "Minimal Style Description",
            _ => "Unknown Style Description"
        });
    }

    private static string GetNoteScopeLabel(NoteScope scope)
    {
        return scope switch
        {
            NoteScope.Public => Plugin.Localization.Localize("Public Note"),
            NoteScope.Character => Plugin.Localization.Localize("Character Note"),
            _ => scope.ToString()
        };
    }

    private static string GetNoteScopeDescription(NoteScope scope)
    {
        return Plugin.Localization.Localize(scope switch
        {
            NoteScope.Public => "Public Note Description",
            NoteScope.Character => "Character Note Description",
            _ => "Unknown Note Type Description"
        });
    }

    private static string GetNoteBackgroundStyleLabel(NoteBackgroundStyle style)
    {
        return style switch
        {
            NoteBackgroundStyle.Frosted => Plugin.Localization.Localize("Frosted Background"),
            NoteBackgroundStyle.Transparent => Plugin.Localization.Localize("Transparent Background"),
            _ => style.ToString()
        };
    }

    private static string GetNoteBackgroundStyleDescription(NoteBackgroundStyle style)
    {
        return Plugin.Localization.Localize(style switch
        {
            NoteBackgroundStyle.Frosted => "Frosted Background Description",
            NoteBackgroundStyle.Transparent => "Transparent Background Description",
            _ => "Unknown Note Background Description"
        });
    }

    public async Task TestDungeonLoggerLogin()
    {
        loginStatus = LoginStatus.Pending;
        var username = Plugin.Configuration.DungeonLoggerConfig.Username;
        var password = Plugin.Configuration.DungeonLoggerConfig.Password;
        if (username.IsNullOrEmpty() || password.IsNullOrEmpty())
        {
            loginStatus = LoginStatus.Failed;
            loginResponseMessage = "Username or Password is empty";
            return;
        }

        try
        {
            using var client = new DungeonLoggerClient();
            var response = await client.PostLogin(password, username);
            loginStatus = response?.Code != 0 ? LoginStatus.Failed : LoginStatus.Success;
            loginResponseMessage = response?.Msg ?? string.Empty;
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Request failed when logging into DungeonLogger Server");

            loginStatus = LoginStatus.Failed;
            loginResponseMessage = e.ToString();
        }
    }

    private static void DrawLanguageSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Language Settings")))
        {
            return;
        }

        ImGui.Indent();
        ImGui.TextDisabled(Plugin.Localization.Localize("Language Settings Hint"));

        var currentLang = Plugin.Configuration.Language;
        var selectedZh = string.Equals(currentLang, "zh_CN", StringComparison.OrdinalIgnoreCase);
        if (ImGui.RadioButton(Plugin.Localization.Localize("Language Chinese"), selectedZh))
        {
            if (!selectedZh)
            {
                Plugin.Configuration.Language = "zh_CN";
                Plugin.Localization.SwitchLanguage("zh_CN");
                Plugin.Configuration.Save();
            }
        }

        var selectedEn = string.Equals(currentLang, "en", StringComparison.OrdinalIgnoreCase);
        if (ImGui.RadioButton(Plugin.Localization.Localize("Language English"), selectedEn))
        {
            if (!selectedEn)
            {
                Plugin.Configuration.Language = "en";
                Plugin.Localization.SwitchLanguage("en");
                Plugin.Configuration.Save();
            }
        }

        ImGui.Spacing();
        ImGui.TextColored(ImGuiColors.DalamudGrey, Plugin.Localization.Localize("Localization Contribute Hint"));
        ImGui.Unindent();
        ImGui.Spacing();
    }
}
