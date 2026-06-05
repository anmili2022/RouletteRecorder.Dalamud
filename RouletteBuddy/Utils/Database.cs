using CsvHelper;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using RouletteBuddy.DAO;
using RouletteBuddy.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ClientAddonContentsFinder = FFXIVClientStructs.FFXIV.Client.UI.AddonContentsFinder;
using ClientAgentContentsFinder = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentContentsFinder;
using ClientAgentContentsType = FFXIVClientStructs.FFXIV.Client.UI.Agent.ContentsType;
using ClientInstanceContent = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent;
using ClientPlayerState = FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using ClientQuestManager = FFXIVClientStructs.FFXIV.Client.Game.QuestManager;

namespace RouletteBuddy.Utils;

public enum MonitorTaskCompletionState
{
    NotCompleted,
    Completed,
    Unknown
}

public readonly record struct MonitorTaskStatus(
    MonitorTaskCompletionState State,
    string StatusText,
    string? DetailText = null);

public class Database
{
    private static string? lastCompletedCheckPlayerIdentity;

    public const string DailyTaskRouletteKeyPrefix = "roulette:";
    public const string DailyTaskContentFinderConditionKeyPrefix = "contentFinderCondition:";
    public const string DailyTaskCrystallineConflictCasualKey = "crystallineConflict:casual";
    public const string DailyTaskCrystallineConflictRankedKey = "crystallineConflict:ranked";
    public const string DailyTaskTribalQuestsAllowanceKey = "daily:tribalQuestsAllowance";
    public const string WeeklyTaskWondrousTailsKey = "weekly:wondrousTails";
    public const string WeeklyTaskCurrentAllianceRaidKey = "weekly:currentAllianceRaid";
    public const string WeeklyTaskAllianceRaid1Key = "weekly:currentAllianceRaid:1";
    public const string WeeklyTaskAllianceRaid2Key = "weekly:currentAllianceRaid:2";
    public const string WeeklyTaskAllianceRaid3Key = "weekly:currentAllianceRaid:3";
    public const string WeeklyTaskUnrealTrialKey = "weekly:unrealTrial";
    public const string WeeklyTaskSavageRaid1Key = "weekly:savageRaid:1";
    public const string WeeklyTaskSavageRaid2Key = "weekly:savageRaid:2";
    public const string WeeklyTaskSavageRaid3Key = "weekly:savageRaid:3";
    public const string WeeklyTaskSavageRaid4Key = "weekly:savageRaid:4";
    public const string CrystallineConflictCasualName = "水晶冲突练习赛";
    public const string CrystallineConflictRankedName = "水晶冲突段位赛";
    public const int MaxTribalQuestAllowance = 12;
    private const int WondrousTailsMaxStickers = 9;
    private const int WeeklyResetDay = (int)DayOfWeek.Tuesday;
    private const int WeeklyResetHour = 16;
    private const string CurrentAllianceRaidNameKeyword = "团本";
    private const string CurrentAllianceRaidCompletionNameKeyword = "第三巡行";
    private const string CurrentUnrealTrialNameKeyword = "神龙幻巧战";
    private static readonly string[] CurrentAllianceRaidNameKeywords =
    [
        "第一巡行",
        "第二巡行",
        "第三巡行"
    ];
    private static readonly string[] AllianceRaidTaskKeys =
    [
        WeeklyTaskAllianceRaid1Key,
        WeeklyTaskAllianceRaid2Key,
        WeeklyTaskAllianceRaid3Key
    ];
    private static readonly string[] CurrentSavageRaidNameKeywords =
    [
        "重量级1",
        "重量级2",
        "重量级3",
        "重量级4"
    ];
    private static readonly string[] SavageRaidTaskKeys =
    [
        WeeklyTaskSavageRaid1Key,
        WeeklyTaskSavageRaid2Key,
        WeeklyTaskSavageRaid3Key,
        WeeklyTaskSavageRaid4Key
    ];
    private static readonly Regex RewardClaimedCountRegex = new(
        @"(?:奖励)?已领取\s*(?<claimed>\d+)\s*/\s*(?<total>\d+)|(?<claimed2>\d+)\s*/\s*(?<total2>\d+)\s*(?:奖励)?已领取",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const float ContentsFinderRowMergeTolerance = 8f;

    private readonly record struct ContentsFinderTextSnapshot(int Index, float X, float Y, string Text);

    private readonly record struct ContentsFinderSnapshot(
        uint? SelectedRegularContentId,
        string[] SelectedDutyTexts,
        string[] Texts,
        string[] Rows,
        string CombinedText,
        byte? CollectedRewardsCount,
        bool? JoinButtonEnabled);

    public static readonly string DbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data.json");
    public static readonly string TaskHistoryDbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "task_history.json");
    private static readonly string LegacyTaskHistoryDbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "risui.json");
    public static readonly string PendingDbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data_pending.json");
    public static readonly ContentRoulette[] CfRoulettes = Plugin.DataManager.GetExcelSheet<ContentRoulette>()
        .Where(roulette => roulette is { IsInDutyFinder: true, IsGoldSaucer: false })
        .OrderBy(roulette => roulette.SortKey)
        .ThenBy(roulette => roulette.RowId)
        .ToArray();
    public static bool IsPendingDbExists() => File.Exists(PendingDbPath);

    public static List<Roulette> Roulettes { get; private set; } = [];
    public static List<TaskHistoryRoulette> TaskHistoryRoulettes { get; private set; } = [];
    private static DateTime? taskHistoryDbLastWriteTimeUtc;

    public static void Load()
    {
        if (!File.Exists(DbPath)) Save();
        EnsureTaskHistoryDbExists();

        var content = File.ReadAllText(DbPath);

        var deserialized = JsonConvert.DeserializeObject<List<Roulette>>(content);
        if (deserialized != null)
        {
            Roulettes = deserialized;
        }

        LoadTaskHistoryRecords();
    }

    public static void ReloadTaskHistoryIfChanged()
    {
        EnsureTaskHistoryDbExists();

        var lastWriteTimeUtc = GetTaskHistoryDbLastWriteTimeUtc();
        if (lastWriteTimeUtc == null || lastWriteTimeUtc == taskHistoryDbLastWriteTimeUtc)
        {
            return;
        }

        try
        {
            LoadTaskHistoryRecords();
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Failed to reload task history records");
        }
    }

    private static void EnsureTaskHistoryDbExists()
    {
        if (File.Exists(TaskHistoryDbPath))
        {
            return;
        }

        if (File.Exists(LegacyTaskHistoryDbPath))
        {
            File.Copy(LegacyTaskHistoryDbPath, TaskHistoryDbPath);
            return;
        }

        SaveTaskHistory();
    }

    private static void LoadTaskHistoryRecords()
    {
        var taskHistoryContent = File.ReadAllText(TaskHistoryDbPath);
        var deserializedTaskHistory = JsonConvert.DeserializeObject<List<TaskHistoryRoulette>>(taskHistoryContent);
        TaskHistoryRoulettes = deserializedTaskHistory ?? [];
        taskHistoryDbLastWriteTimeUtc = GetTaskHistoryDbLastWriteTimeUtc();
    }

    private static DateTime? GetTaskHistoryDbLastWriteTimeUtc()
    {
        return File.Exists(TaskHistoryDbPath)
            ? File.GetLastWriteTimeUtc(TaskHistoryDbPath)
            : null;
    }

    public static void InsertRoulette(Roulette roulette)
    {
        Roulettes.Add(roulette);
        Save();
    }

    public static void InsertTaskHistoryRoulette(TaskHistoryRoulette roulette)
    {
        TaskHistoryRoulettes.Add(roulette);
        SaveTaskHistory();
    }

    public static int GetTodayMentorRouletteCount()
    {
        var mentorRouletteNames = CfRoulettes
            .Where(IsMentorRoulette)
            .Select(roulette => roulette.Name.ToString())
            .Where(name => !name.IsNullOrWhitespace())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return Roulettes.Count(roulette =>
            roulette.IsCompleted &&
            IsToday(roulette) &&
            IsMentorRouletteName(roulette.RouletteType, mentorRouletteNames));
    }

    private static bool IsToday(Roulette roulette)
    {
        var startedAt = roulette.GetStartedDateTime();
        if (startedAt != null)
        {
            return startedAt.Value.Date == DateTime.Today;
        }

        return DateTime.TryParseExact(
            roulette.Date,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out var date) && date.Date == DateTime.Today;
    }

    public static bool IsMentorRoulette(ContentRoulette roulette)
    {
        return IsMentorRouletteName(roulette.Name.ToString(), null);
    }

    public static bool IsMentorRouletteName(string? rouletteType, HashSet<string>? mentorRouletteNames = null)
    {
        if (rouletteType.IsNullOrWhitespace())
        {
            return false;
        }

        if (mentorRouletteNames?.Contains(rouletteType) == true)
        {
            return true;
        }

        return rouletteType.Contains("Mentor", StringComparison.OrdinalIgnoreCase) ||
               rouletteType.Contains("指导", StringComparison.Ordinal) ||
               rouletteType.Contains("指导者", StringComparison.Ordinal) ||
               rouletteType.Contains("导随", StringComparison.Ordinal) ||
               rouletteType.Contains("導師", StringComparison.Ordinal) ||
               rouletteType.Contains("導隨", StringComparison.Ordinal);
    }

    public static IEnumerable<(string Key, string Name)> GetDailyTaskMonitorOptions()
    {
        var shownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var shownNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var roulette in CfRoulettes)
        {
            var rouletteName = roulette.Name.ToString();
            var key = $"{DailyTaskRouletteKeyPrefix}{roulette.RowId}";
            var name = rouletteName;

            if (IsCrystallineConflictCasualName(rouletteName))
            {
                key = DailyTaskCrystallineConflictCasualKey;
                name = CrystallineConflictCasualName;
            }
            else if (IsCrystallineConflictRankedName(rouletteName))
            {
                key = DailyTaskCrystallineConflictRankedKey;
                name = CrystallineConflictRankedName;
            }

            if (name.IsNullOrWhitespace() ||
                !shownKeys.Add(key) ||
                !shownNames.Add(NormalizeDailyTaskName(name)))
            {
                continue;
            }

            yield return (key, name);
        }

        if (shownKeys.Add(DailyTaskCrystallineConflictCasualKey))
        {
            yield return (DailyTaskCrystallineConflictCasualKey, CrystallineConflictCasualName);
        }

        if (shownKeys.Add(DailyTaskCrystallineConflictRankedKey))
        {
            yield return (DailyTaskCrystallineConflictRankedKey, CrystallineConflictRankedName);
        }

        foreach (var option in GetDailyUtilityTaskMonitorOptions())
        {
            if (shownKeys.Add(option.Key))
            {
                yield return option;
            }
        }
    }

    public static IEnumerable<(string Key, string Name)> GetDailyUtilityTaskMonitorOptions()
    {
        yield return (DailyTaskTribalQuestsAllowanceKey, Plugin.Localization.Localize("Tribal Quests Allowance"));
    }

    public static IEnumerable<(string Key, string Name)> GetWeeklyTaskMonitorOptions()
    {
        yield return (WeeklyTaskWondrousTailsKey, Plugin.Localization.Localize("Wondrous Tails"));
        foreach (var option in GetWeeklyAllianceRaidTaskMonitorOptions())
        {
            yield return option;
        }
        yield return (WeeklyTaskUnrealTrialKey, Plugin.Localization.Localize("Unreal Trial"));
        yield return (WeeklyTaskSavageRaid1Key, Plugin.Localization.Localize("Savage Raid 1"));
        yield return (WeeklyTaskSavageRaid2Key, Plugin.Localization.Localize("Savage Raid 2"));
        yield return (WeeklyTaskSavageRaid3Key, Plugin.Localization.Localize("Savage Raid 3"));
        yield return (WeeklyTaskSavageRaid4Key, Plugin.Localization.Localize("Savage Raid 4"));
    }

    public static IEnumerable<(string Key, string Name)> GetWeeklyAllianceRaidTaskMonitorOptions()
    {
        yield return (WeeklyTaskAllianceRaid1Key, Plugin.Localization.Localize("Alliance Raid 1"));
        yield return (WeeklyTaskAllianceRaid2Key, Plugin.Localization.Localize("Alliance Raid 2"));
        yield return (WeeklyTaskAllianceRaid3Key, Plugin.Localization.Localize("Alliance Raid 3"));
    }

    public static IEnumerable<(string Key, string Name)> GetWeeklyNonSavageTaskMonitorOptions()
    {
        return GetWeeklyTaskMonitorOptions().Where(option =>
            !IsWeeklyAllianceRaidTaskKey(option.Key) &&
            !IsWeeklySavageTaskKey(option.Key));
    }

    public static IEnumerable<(string Key, string Name)> GetWeeklySavageTaskMonitorOptions()
    {
        return GetWeeklyTaskMonitorOptions().Where(option => IsWeeklySavageTaskKey(option.Key));
    }

    public static string? GetCrystallineConflictMonitorKeyForRouletteKey(string taskKey)
    {
        if (!TryParseDailyTaskRouletteKey(taskKey, out var rouletteId))
        {
            return null;
        }

        foreach (var roulette in CfRoulettes)
        {
            if (roulette.RowId != rouletteId)
            {
                continue;
            }

            var rouletteName = roulette.Name.ToString();
            if (IsCrystallineConflictCasualName(rouletteName))
            {
                return DailyTaskCrystallineConflictCasualKey;
            }

            if (IsCrystallineConflictRankedName(rouletteName))
            {
                return DailyTaskCrystallineConflictRankedKey;
            }

            return null;
        }

        return null;
    }

    public static string? GetCrystallineConflictRouletteName(ContentFinderCondition condition)
    {
        if (condition.CrystallineConflictRankedRoulette && !condition.CrystallineConflictCasualRoulette)
        {
            return CrystallineConflictRankedName;
        }

        if (condition.CrystallineConflictCasualRoulette && !condition.CrystallineConflictRankedRoulette)
        {
            return CrystallineConflictCasualName;
        }

        if (condition.Rated || condition.RatedMatch)
        {
            return CrystallineConflictRankedName;
        }

        if (condition.CrystallineConflictCasualRoulette)
        {
            return CrystallineConflictCasualName;
        }

        if (condition.CrystallineConflictRankedRoulette)
        {
            return CrystallineConflictRankedName;
        }

        return null;
    }

    public static bool IsDailyTaskCompletedInCurrentResetCycle(string taskKey, string rouletteName)
    {
        var currentIdentity = $"{Plugin.GetPlayerName()}@{Plugin.GetPlayerWorldName()}";

        // Client data shortcut: only trust if verified for current character
        if (string.Equals(currentIdentity, lastCompletedCheckPlayerIdentity, StringComparison.OrdinalIgnoreCase) &&
            TryGetClientRouletteCompletion(taskKey, out var isCompleted))
        {
            return isCompleted;
        }

        // Always verify with task history (filtered by current player name/world)
        var result = IsTaskHistoryRouletteCompletedInCurrentResetCycle(rouletteName);

        // Cache identity only when task history confirms completion
        if (result)
        {
            lastCompletedCheckPlayerIdentity = currentIdentity;
        }

        return result;
    }

    public static MonitorTaskStatus GetDailyMonitorTaskStatus(string taskKey, string taskName)
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return UnknownStatus("Not logged in");
        }

        return taskKey switch
        {
            DailyTaskTribalQuestsAllowanceKey => GetTribalQuestsAllowanceStatus(),
            _ => ToCompletedStatus(IsDailyTaskCompletedInCurrentResetCycle(taskKey, taskName))
        };
    }

    public static MonitorTaskStatus GetWeeklyMonitorTaskStatus(string taskKey, string taskName)
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return UnknownStatus("Not logged in");
        }

        return taskKey switch
        {
            WeeklyTaskWondrousTailsKey => GetWondrousTailsStatus(),
            WeeklyTaskCurrentAllianceRaidKey => GetCurrentAllianceRaidStatus(taskKey, taskName),
            _ when IsWeeklyAllianceRaidTaskKey(taskKey) => GetRecordedWeeklyTaskStatus(taskKey, taskName),
            WeeklyTaskUnrealTrialKey => GetUnrealTrialStatus(taskKey, taskName),
            _ when IsWeeklySavageTaskKey(taskKey) => GetSavageRaidStatus(taskKey, taskName),
            _ => GetRecordedWeeklyTaskStatus(taskKey, taskName)
        };
    }

    public static bool IsDailyUtilityTaskKey(string taskKey)
    {
        return string.Equals(taskKey, DailyTaskTribalQuestsAllowanceKey, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsWeeklyTaskKey(string taskKey)
    {
        return string.Equals(taskKey, WeeklyTaskWondrousTailsKey, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(taskKey, WeeklyTaskCurrentAllianceRaidKey, StringComparison.OrdinalIgnoreCase) ||
               IsWeeklyAllianceRaidTaskKey(taskKey) ||
               string.Equals(taskKey, WeeklyTaskUnrealTrialKey, StringComparison.OrdinalIgnoreCase) ||
               SavageRaidTaskKeys.Contains(taskKey, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsWeeklyAllianceRaidTaskKey(string taskKey)
    {
        return AllianceRaidTaskKeys.Contains(taskKey, StringComparer.OrdinalIgnoreCase);
    }

    public static bool IsWeeklySavageTaskKey(string taskKey)
    {
        return SavageRaidTaskKeys.Contains(taskKey, StringComparer.OrdinalIgnoreCase);
    }

    public static bool TryGetWeeklyMonitorTaskForContent(ContentFinderCondition condition, out string taskKey, out string taskName)
    {
        taskKey = string.Empty;
        taskName = string.Empty;

        if (IsCurrentUnrealTrial(condition))
        {
            taskKey = WeeklyTaskUnrealTrialKey;
            taskName = Plugin.Localization.Localize("Unreal Trial");
            return true;
        }

        for (var i = 0; i < CurrentAllianceRaidNameKeywords.Length && i < AllianceRaidTaskKeys.Length; i++)
        {
            if (!IsCurrentAllianceRaidCondition(condition, CurrentAllianceRaidNameKeywords[i]))
            {
                continue;
            }

            taskKey = AllianceRaidTaskKeys[i];
            taskName = Plugin.Localization.Localize($"Alliance Raid {i + 1}");
            return true;
        }

        var currentAllianceRaids = GetCurrentAllianceRaidConditions();
        for (var i = 0; i < currentAllianceRaids.Length && i < AllianceRaidTaskKeys.Length; i++)
        {
            if (currentAllianceRaids[i].RowId != condition.RowId)
            {
                continue;
            }

            taskKey = AllianceRaidTaskKeys[i];
            taskName = Plugin.Localization.Localize($"Alliance Raid {i + 1}");
            return true;
        }

        for (var i = 0; i < CurrentSavageRaidNameKeywords.Length && i < SavageRaidTaskKeys.Length; i++)
        {
            if (!IsCurrentSavageRaidCondition(condition, CurrentSavageRaidNameKeywords[i]))
            {
                continue;
            }

            taskKey = SavageRaidTaskKeys[i];
            taskName = Plugin.Localization.Localize($"Savage Raid {i + 1}");
            return true;
        }

        var currentSavageRaids = GetCurrentSavageRaidConditions();
        for (var i = 0; i < currentSavageRaids.Length && i < SavageRaidTaskKeys.Length; i++)
        {
            if (currentSavageRaids[i].RowId != condition.RowId)
            {
                continue;
            }

            taskKey = SavageRaidTaskKeys[i];
            taskName = Plugin.Localization.Localize($"Savage Raid {i + 1}");
            return true;
        }

        if (GetCurrentAllianceRaidConditions().Any(currentContent => currentContent.RowId == condition.RowId))
        {
            taskKey = WeeklyTaskCurrentAllianceRaidKey;
            taskName = Plugin.Localization.Localize("Current Alliance Raid");
            return true;
        }

        return false;
    }

    private static bool TryGetClientRouletteCompletion(string taskKey, out bool isCompleted)
    {
        isCompleted = false;

        if (TryParseDailyTaskRouletteKey(taskKey, out var rouletteId))
        {
            return TryGetClientRouletteCompletion(rouletteId, out isCompleted);
        }

        if (TryGetCrystallineConflictRouletteId(taskKey, out rouletteId))
        {
            return TryGetClientRouletteCompletion(rouletteId, out isCompleted);
        }

        return false;
    }

    private static bool TryParseDailyTaskRouletteKey(string taskKey, out uint rouletteId)
    {
        rouletteId = 0;

        return taskKey.StartsWith(DailyTaskRouletteKeyPrefix, StringComparison.OrdinalIgnoreCase) &&
               uint.TryParse(taskKey[DailyTaskRouletteKeyPrefix.Length..], out rouletteId);
    }

    private static bool TryGetCrystallineConflictRouletteId(string taskKey, out uint rouletteId)
    {
        rouletteId = 0;

        foreach (var roulette in CfRoulettes)
        {
            var rouletteName = roulette.Name.ToString();
            var isTargetRoulette = taskKey switch
            {
                DailyTaskCrystallineConflictCasualKey => IsCrystallineConflictCasualName(rouletteName),
                DailyTaskCrystallineConflictRankedKey => IsCrystallineConflictRankedName(rouletteName),
                _ => false
            };

            if (!isTargetRoulette)
            {
                continue;
            }

            rouletteId = roulette.RowId;
            return true;
        }

        return false;
    }

    private static unsafe bool TryGetClientRouletteCompletion(uint rouletteId, out bool isCompleted)
    {
        isCompleted = false;

        if (rouletteId > byte.MaxValue)
        {
            return false;
        }

        var instanceContent = ClientInstanceContent.Instance();
        if (instanceContent == null)
        {
            return false;
        }

        isCompleted = instanceContent->IsRouletteComplete((byte)rouletteId);
        return true;
    }

    private static unsafe MonitorTaskStatus GetTribalQuestsAllowanceStatus()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return UnknownStatus("Not logged in");
        }

        var questManager = ClientQuestManager.Instance();
        if (questManager == null)
        {
            return UnknownStatus("Task Status Unknown");
        }

        var remainingAllowance = (int)questManager->GetBeastTribeAllowance();
        var completedQuestCount = Math.Clamp(MaxTribalQuestAllowance - remainingAllowance, 0, MaxTribalQuestAllowance);
        var targetCount = Math.Clamp(Plugin.Configuration.TribalQuestCompletionCount, 1, MaxTribalQuestAllowance);
        var completed = completedQuestCount >= targetCount;

        return new MonitorTaskStatus(
            completed ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
            completed ? "Roulette Completed" : "Roulette Not Completed");
    }

    private static unsafe MonitorTaskStatus GetWondrousTailsStatus()
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            return UnknownStatus("Not logged in");
        }

        var playerState = ClientPlayerState.Instance();
        if (playerState == null || !playerState->IsLoaded)
        {
            return UnknownStatus("Task Status Unknown");
        }

        if (!playerState->HasWeeklyBingoJournal)
        {
            return UnknownStatus("No Wondrous Tails Journal");
        }

        var stickers = playerState->WeeklyBingoNumPlacedStickers;
        var detail = string.Format(
            CultureInfo.InvariantCulture,
            Plugin.Localization.Localize("Stickers Count Format"),
            stickers,
            WondrousTailsMaxStickers);

        return new MonitorTaskStatus(
            stickers >= WondrousTailsMaxStickers ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
            stickers >= WondrousTailsMaxStickers ? "Roulette Completed" : "Roulette Not Completed",
            detail);
    }

    private static MonitorTaskStatus GetCurrentAllianceRaidStatus(string taskKey, string taskName)
    {
        return ToRecordedThisWeekStatus(IsCurrentAllianceRaidCompletedInCurrentResetCycle());
    }

    private static MonitorTaskStatus GetUnrealTrialStatus(string taskKey, string taskName)
    {
        if (TryGetContentsFinderSnapshotForWeeklyTask(taskKey, out var snapshot) &&
            TryParseFauxHollowsStatus(snapshot.CombinedText, out var contentsFinderStatus))
        {
            return contentsFinderStatus;
        }

        if (TryGetFauxHollowsPlayerStateStatus(out var playerStateStatus))
        {
            return playerStateStatus;
        }

        return GetRewardStatusUnknownWithoutUsingLocalClearRecord(taskKey, taskName);
    }

    private static MonitorTaskStatus GetSavageRaidStatus(string taskKey, string taskName)
    {
        if (TryGetContentsFinderSnapshotForWeeklyTask(taskKey, out var snapshot) &&
            TryParseSavageContentsFinderStatus(snapshot, out var contentsFinderStatus))
        {
            return contentsFinderStatus;
        }

        return GetRecordedWeeklyTaskStatus(taskKey, taskName);
    }

    private static MonitorTaskStatus GetRecordedWeeklyTaskStatus(string taskKey, string taskName)
    {
        return ToRecordedThisWeekStatus(IsTaskHistoryMonitorTaskCompletedInCurrentResetCycle(taskKey, taskName, GetCurrentWeeklyResetCycleStart()));
    }

    private static bool IsCurrentAllianceRaidCompletedInCurrentResetCycle()
    {
        ReloadTaskHistoryIfChanged();

        var playerName = Plugin.GetPlayerName();
        var worldName = Plugin.GetPlayerWorldName();
        if (playerName.IsNullOrWhitespace() || worldName.IsNullOrWhitespace())
        {
            return false;
        }

        var completionContentNames = GetCurrentAllianceRaidCompletionContentNames();
        if (completionContentNames.Length == 0)
        {
            return false;
        }

        var resetAt = GetCurrentWeeklyResetCycleStart();
        return TaskHistoryRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            IsCurrentAllianceRaidCompletionMatched(roulette, completionContentNames) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, worldName, StringComparison.OrdinalIgnoreCase) &&
            IsTaskHistoryRouletteInCurrentResetCycle(roulette, resetAt));
    }

    private static bool IsCurrentAllianceRaidCompletionMatched(TaskHistoryRoulette roulette, IReadOnlyCollection<string> completionContentNames)
    {
        if (completionContentNames.Count == 0)
        {
            return false;
        }

        if (!roulette.ContentName.IsNullOrWhitespace() &&
            completionContentNames.Any(contentName => AreDailyTaskNamesEquivalent(roulette.ContentName, contentName)))
        {
            return true;
        }

        var displayType = GetRouletteTypeDisplayName(roulette.RouletteType, roulette.MonitorTaskKey);
        return !displayType.IsNullOrWhitespace() &&
               completionContentNames.Any(contentName => AreDailyTaskNamesEquivalent(displayType, contentName));
    }

    private static string[] GetCurrentAllianceRaidCompletionContentNames()
    {
        var currentAllianceRaidCompletionConditions = GetCurrentAllianceRaidConditions()
            .Where(condition => ContainsNormalizedName(condition.Name.ToString(), CurrentAllianceRaidCompletionNameKeyword))
            .ToArray();

        if (currentAllianceRaidCompletionConditions.Length > 0)
        {
            return currentAllianceRaidCompletionConditions
                .Select(condition => condition.Name.ToString())
                .Where(name => !name.IsNullOrWhitespace())
                .ToArray();
        }

        var currentAllianceRaids = GetCurrentAllianceRaidConditions();
        if (currentAllianceRaids.Length == 0)
        {
            return [];
        }

        var lastAllianceRaidName = currentAllianceRaids[^1].Name.ToString();
        return lastAllianceRaidName.IsNullOrWhitespace() ? [] : [lastAllianceRaidName];
    }

    private static MonitorTaskStatus GetRewardStatusUnknownWithoutUsingLocalClearRecord(string taskKey, string taskName)
    {
        var recordedThisWeek = IsTaskHistoryMonitorTaskCompletedInCurrentResetCycle(taskKey, taskName, GetCurrentWeeklyResetCycleStart());
        return UnknownStatus(
            "Task Status Unknown",
            Plugin.Localization.Localize(recordedThisWeek
                ? "Recorded Clear Does Not Confirm Reward"
                : "Open Duty Finder To Check Reward Status"));
    }

    private static bool TryParseSavageContentsFinderStatus(ContentsFinderSnapshot snapshot, out MonitorTaskStatus status)
    {
        var hasRewardStatus = TryParseRewardStatus(snapshot.CombinedText, out var rewardStatus);
        var isLockedOrUnavailable = IsContentsFinderDutyLockedOrUnavailable(snapshot);

        if (isLockedOrUnavailable)
        {
            var detailParts = new List<string>();
            if (hasRewardStatus)
            {
                detailParts.Add(FormatStatusAsDetail(rewardStatus));
            }

            if (snapshot.JoinButtonEnabled == false)
            {
                detailParts.Add(Plugin.Localization.Localize("Join Button Unavailable"));
            }

            status = new MonitorTaskStatus(
                MonitorTaskCompletionState.Unknown,
                "Duty Locked Or Requirements Not Met",
                JoinDistinctDetails(detailParts));
            return true;
        }

        if (hasRewardStatus)
        {
            status = rewardStatus;
            return true;
        }

        status = default;
        return false;
    }

    private static bool TryGetCollectedRewardsCountStatus(
        ContentsFinderSnapshot snapshot,
        int expectedTotal,
        out MonitorTaskStatus status)
    {
        if (snapshot.CollectedRewardsCount is not { } collectedRewardsCount)
        {
            status = default;
            return false;
        }

        var total = Math.Max(expectedTotal, 1);
        var claimed = Math.Clamp(collectedRewardsCount, 0, total);
        status = new MonitorTaskStatus(
            claimed >= total ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
            claimed >= total ? "Reward Claimed" : "Reward Not Claimed",
            string.Format(CultureInfo.InvariantCulture, "{0} / {1}", claimed, total));
        return true;
    }

    private static bool TryParseRewardStatus(string text, out MonitorTaskStatus status)
    {
        var normalizedText = NormalizeStatusText(text);
        var claimedCountMatch = RewardClaimedCountRegex.Match(normalizedText);
        if (claimedCountMatch.Success)
        {
            var claimedGroup = claimedCountMatch.Groups["claimed"].Success
                ? claimedCountMatch.Groups["claimed"]
                : claimedCountMatch.Groups["claimed2"];
            var totalGroup = claimedCountMatch.Groups["total"].Success
                ? claimedCountMatch.Groups["total"]
                : claimedCountMatch.Groups["total2"];

            if (int.TryParse(claimedGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var claimed) &&
                int.TryParse(totalGroup.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var total))
            {
                var completed = total > 0 && claimed >= total;
                status = new MonitorTaskStatus(
                    completed ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
                    "Reward Claimed",
                    string.Format(CultureInfo.InvariantCulture, "{0} / {1}", claimed, total));
                return true;
            }
        }

        if (ContainsAny(normalizedText, "奖励未领取", "Reward Not Claimed", "Reward Unclaimed", "Reward Available"))
        {
            status = new MonitorTaskStatus(MonitorTaskCompletionState.NotCompleted, "Reward Not Claimed");
            return true;
        }

        if (ContainsAny(normalizedText, "奖励已领取", "Reward Claimed"))
        {
            status = new MonitorTaskStatus(MonitorTaskCompletionState.Completed, "Reward Claimed");
            return true;
        }

        status = default;
        return false;
    }

    private static bool TryParseFauxHollowsStatus(string text, out MonitorTaskStatus status)
    {
        var normalizedText = NormalizeStatusText(text);
        var hasFauxHollowsText = ContainsAny(normalizedText, "挑战幻巧拼图", "幻巧拼图", "Faux Hollows");
        if (!hasFauxHollowsText)
        {
            status = default;
            return false;
        }

        if (ContainsAny(normalizedText, "未达成", "尚未达成", "Not Achieved", "Incomplete"))
        {
            status = new MonitorTaskStatus(MonitorTaskCompletionState.NotCompleted, "Faux Hollows Puzzle Not Achieved");
            return true;
        }

        if (ContainsAny(normalizedText, "已达成", "Achieved", "Complete"))
        {
            status = new MonitorTaskStatus(MonitorTaskCompletionState.Completed, "Faux Hollows Puzzle Achieved");
            return true;
        }

        status = default;
        return false;
    }

    private static unsafe bool TryGetFauxHollowsPlayerStateStatus(out MonitorTaskStatus status)
    {
        if (!Plugin.ClientState.IsLoggedIn)
        {
            status = UnknownStatus("Not logged in");
            return true;
        }

        var playerState = ClientPlayerState.Instance();
        if (playerState == null || !playerState->IsLoaded)
        {
            status = default;
            return false;
        }

        var weeklyResetAt = GetCurrentWeeklyResetCycleStart();
        var fauxHollowsTimestamp = playerState->FauxHollowsTimestamp;
        if (fauxHollowsTimestamp > 0)
        {
            try
            {
                var achievedAt = DateTimeOffset.FromUnixTimeSeconds(fauxHollowsTimestamp).LocalDateTime;
                var completed = achievedAt >= weeklyResetAt;
                status = new MonitorTaskStatus(
                    completed ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
                    completed ? "Faux Hollows Puzzle Achieved" : "Faux Hollows Puzzle Not Achieved");
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Ignore malformed client state and fall back to local records.
            }
        }

        if (playerState->FauxHollowsState > 0)
        {
            status = new MonitorTaskStatus(MonitorTaskCompletionState.Completed, "Faux Hollows Puzzle Achieved");
            return true;
        }

        status = new MonitorTaskStatus(MonitorTaskCompletionState.NotCompleted, "Faux Hollows Puzzle Not Achieved");
        return true;
    }

    private static MonitorTaskStatus ToCompletedStatus(bool completed)
    {
        return new MonitorTaskStatus(
            completed ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
            completed ? "Roulette Completed" : "Roulette Not Completed");
    }

    private static MonitorTaskStatus ToRecordedThisWeekStatus(bool completed)
    {
        return new MonitorTaskStatus(
            completed ? MonitorTaskCompletionState.Completed : MonitorTaskCompletionState.NotCompleted,
            completed ? "Recorded This Week" : "Not Recorded This Week");
    }

    private static MonitorTaskStatus UnknownStatus(string statusText, string? detailText = null)
    {
        return new MonitorTaskStatus(MonitorTaskCompletionState.Unknown, statusText, detailText);
    }

    private static unsafe bool TryGetContentsFinderSnapshotForWeeklyTask(string taskKey, out ContentsFinderSnapshot snapshot)
    {
        snapshot = default;

        if (!TryGetContentsFinderSnapshot(out var contentsFinderSnapshot))
        {
            return false;
        }

        var taskConditions = GetWeeklyTaskConditions(taskKey)
            .ToArray();
        if (taskConditions.Length == 0)
        {
            return false;
        }

        var expectedNames = taskConditions
            .Select(condition => condition.Name.ToString())
            .Where(name => !name.IsNullOrWhitespace())
            .Concat(GetWeeklyTaskFallbackKeywords(taskKey))
            .ToArray();

        var conditionIds = taskConditions
            .SelectMany(condition => new[]
            {
                condition.RowId,
                condition.Content.RowId,
                condition.TerritoryType.RowId
            })
            .Where(rowId => rowId > 0)
            .ToHashSet();
        if (contentsFinderSnapshot.SelectedRegularContentId is { } selectedContentId)
        {
            if (conditionIds.Contains(selectedContentId))
            {
                snapshot = contentsFinderSnapshot;
                return true;
            }

            // Some ContentsFinder agent fields point to the selected registration entry rather than
            // the duty currently shown in the right-side details panel. Do not fail early here; use
            // visible text matching below as a fallback.
        }

        if (expectedNames.Length == 0)
        {
            return false;
        }

        var selectedDutyText = string.Join("\n", contentsFinderSnapshot.SelectedDutyTexts);
        if (expectedNames.Any(expectedName => ContainsNormalizedName(selectedDutyText, expectedName)))
        {
            snapshot = contentsFinderSnapshot;
            return true;
        }

        if (expectedNames.Any(expectedName => ContainsNormalizedName(contentsFinderSnapshot.CombinedText, expectedName)))
        {
            snapshot = contentsFinderSnapshot;
            return true;
        }

        return false;
    }

    private static unsafe bool TryGetContentsFinderSnapshot(out ContentsFinderSnapshot snapshot)
    {
        snapshot = default;

        var addonPtr = Plugin.GameGui.GetAddonByName("ContentsFinder", 1);
        if (addonPtr.IsNull || !addonPtr.IsReady || !addonPtr.IsVisible || addonPtr.Address == nint.Zero)
        {
            return false;
        }

        var addon = (AtkUnitBase*)addonPtr.Address;
        uint? selectedRegularContentId = null;
        byte? collectedRewardsCount = null;
        string[] agentTexts = [];
        var agent = ClientAgentContentsFinder.Instance();
        if (agent != null)
        {
            collectedRewardsCount = agent->NumCollectedRewards;
            agentTexts = CollectAgentContentsFinderTexts(agent);

            // InterfaceSub.SelectedDutyId is the duty currently shown in the right-side details panel.
            // SelectedDuty can instead point at the queue/register selection, which is not always the
            // same thing when a roulette and a special duty are selected at the same time.
            if (agent->InterfaceSub.SelectedDutyId > 0)
            {
                selectedRegularContentId = (uint)agent->InterfaceSub.SelectedDutyId;
            }
            else if (agent->SelectedDuty.ContentType == ClientAgentContentsType.Regular)
            {
                selectedRegularContentId = agent->SelectedDuty.Id;
            }
        }

        var contentsFinderAddon = (ClientAddonContentsFinder*)addon;
        var selectedDutyTexts = CollectSelectedDutyTexts(contentsFinderAddon);
        var textSnapshots = CollectContentsFinderTextSnapshots(addon);
        var atkValueTexts = CollectAddonAtkValueTexts(addon);
        if (textSnapshots.Count == 0 &&
            selectedDutyTexts.Length == 0 &&
            atkValueTexts.Length == 0 &&
            agentTexts.Length == 0)
        {
            return false;
        }

        var rows = BuildContentsFinderTextRows(textSnapshots);
        var texts = textSnapshots
            .Select(textSnapshot => textSnapshot.Text)
            .Where(text => !text.IsNullOrWhitespace())
            .Concat(selectedDutyTexts)
            .Concat(atkValueTexts)
            .Concat(agentTexts)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var combinedTextSource = rows.Length > 0
            ? rows.Concat(atkValueTexts).Concat(agentTexts).Distinct(StringComparer.Ordinal).ToArray()
            : texts;
        var combinedText = string.Join("\n", combinedTextSource);
        bool? joinButtonEnabled = contentsFinderAddon->JoinButton == null
            ? null
            : contentsFinderAddon->JoinButton->IsEnabled;

        snapshot = new ContentsFinderSnapshot(
            selectedRegularContentId,
            selectedDutyTexts,
            texts,
            rows,
            combinedText,
            collectedRewardsCount,
            joinButtonEnabled);
        return true;
    }

    private static unsafe string[] CollectAgentContentsFinderTexts(ClientAgentContentsFinder* agent)
    {
        if (agent == null)
        {
            return [];
        }

        var texts = new List<string>(16);
        foreach (var utf8String in agent->Strings)
        {
            var text = NormalizeStatusText(utf8String.ToString());
            if (!text.IsNullOrWhitespace())
            {
                texts.Add(text);
            }
        }

        var description = NormalizeStatusText(agent->InterfaceSub.Description.ToString());
        if (!description.IsNullOrWhitespace())
        {
            texts.Add(description);
        }

        foreach (var contentsPointer in agent->ContentList)
        {
            var contents = contentsPointer.Value;
            if (contents == null)
            {
                continue;
            }

            var name = NormalizeStatusText(contents->Name.ToString());
            if (!name.IsNullOrWhitespace())
            {
                texts.Add(name);
            }
        }

        return texts
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static unsafe string[] CollectAddonAtkValueTexts(AtkUnitBase* addon)
    {
        if (addon == null || addon->AtkValues == null || addon->AtkValuesCount == 0)
        {
            return [];
        }

        var texts = new List<string>((int)addon->AtkValuesCount);
        for (var i = 0; i < addon->AtkValuesCount; i++)
        {
            CollectAtkValueTexts(&addon->AtkValues[i], texts, 0);
        }

        return texts
            .Where(text => !text.IsNullOrWhitespace())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static unsafe void CollectAtkValueTexts(AtkValue* value, List<string> texts, int depth)
    {
        if (value == null || depth > 3)
        {
            return;
        }

        if (value->Type is AtkValueType.String or AtkValueType.String8 or AtkValueType.ManagedString)
        {
            var text = NormalizeStatusText(value->GetValueAsString());
            if (!text.IsNullOrWhitespace())
            {
                texts.Add(text);
            }

            return;
        }

        if (value->Type is AtkValueType.Vector or AtkValueType.ManagedVector)
        {
            var vectorSize = value->GetVectorSize();
            for (var i = 0u; i < vectorSize; i++)
            {
                CollectAtkValueTexts(value->GetVectorValue(i), texts, depth + 1);
            }
        }
    }

    private static unsafe string[] CollectSelectedDutyTexts(ClientAddonContentsFinder* addon)
    {
        if (addon == null)
        {
            return [];
        }

        var selectedTexts = new List<string>(addon->SelectedDutyTextNode.Length);
        foreach (var textNodePointer in addon->SelectedDutyTextNode)
        {
            var textNode = textNodePointer.Value;
            if (textNode == null)
            {
                continue;
            }

            var text = GetTextNodeText(textNode);
            if (text.IsNullOrWhitespace())
            {
                continue;
            }

            selectedTexts.Add(text);
        }

        return selectedTexts
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static unsafe List<ContentsFinderTextSnapshot> CollectContentsFinderTextSnapshots(AtkUnitBase* addon)
    {
        var result = new List<ContentsFinderTextSnapshot>(96);
        if (addon == null)
        {
            return result;
        }

        var visited = new HashSet<nint>();
        var index = 0;
        CollectTextSnapshotsFromUldManager(&addon->UldManager, result, visited, ref index);
        CollectTextSnapshotsFromNode(addon->RootNode, result, visited, ref index);

        return result
            .GroupBy(snapshot => snapshot.Text, StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(snapshot => snapshot.Y)
                .ThenBy(snapshot => snapshot.X)
                .First())
            .ToList();
    }

    private static unsafe void CollectTextSnapshotsFromUldManager(
        AtkUldManager* uldManager,
        List<ContentsFinderTextSnapshot> result,
        HashSet<nint> visited,
        ref int index)
    {
        if (uldManager == null)
        {
            return;
        }

        CollectTextSnapshotsFromNode(uldManager->RootNode, result, visited, ref index);

        if (uldManager->NodeList == null || uldManager->NodeListCount == 0)
        {
            return;
        }

        var nodeListCount = (int)uldManager->NodeListCount;
        for (var i = 0; i < nodeListCount; i++)
        {
            CollectTextSnapshotsFromNode(uldManager->NodeList[i], result, visited, ref index);
        }
    }

    private static unsafe void CollectTextSnapshotsFromNode(
        AtkResNode* node,
        List<ContentsFinderTextSnapshot> result,
        HashSet<nint> visited,
        ref int index)
    {
        if (node == null || !visited.Add((nint)node))
        {
            return;
        }

        if (node->Type == NodeType.Text)
        {
            var textNode = (AtkTextNode*)node;
            var text = GetTextNodeText(textNode);
            if (!text.IsNullOrWhitespace())
            {
                result.Add(new ContentsFinderTextSnapshot(index++, textNode->AtkResNode.X, textNode->AtkResNode.Y, text));
            }
        }

        if (node->Type == NodeType.Component)
        {
            var component = node->GetComponent();
            if (component != null)
            {
                CollectTextSnapshotsFromUldManager(&component->UldManager, result, visited, ref index);
            }
        }

        var child = node->ChildNode;
        while (child != null)
        {
            var next = child->NextSiblingNode;
            CollectTextSnapshotsFromNode(child, result, visited, ref index);
            child = next;
        }
    }

    private static string[] BuildContentsFinderTextRows(List<ContentsFinderTextSnapshot> snapshots)
    {
        if (snapshots.Count == 0)
        {
            return [];
        }

        snapshots.Sort(static (left, right) =>
        {
            var byY = left.Y.CompareTo(right.Y);
            return byY != 0 ? byY : left.X.CompareTo(right.X);
        });

        var rows = new List<(float Y, List<ContentsFinderTextSnapshot> Nodes)>(32);
        foreach (var snapshot in snapshots)
        {
            if (rows.Count == 0)
            {
                rows.Add((snapshot.Y, [snapshot]));
                continue;
            }

            var lastRow = rows[^1];
            if (Math.Abs(snapshot.Y - lastRow.Y) <= ContentsFinderRowMergeTolerance)
            {
                lastRow.Nodes.Add(snapshot);
                rows[^1] = lastRow;
                continue;
            }

            rows.Add((snapshot.Y, [snapshot]));
        }

        var result = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            row.Nodes.Sort(static (left, right) => left.X.CompareTo(right.X));

            var parts = new List<string>(row.Nodes.Count);
            foreach (var node in row.Nodes)
            {
                if (node.Text.IsNullOrWhitespace())
                {
                    continue;
                }

                if (parts.Count == 0 || !string.Equals(parts[^1], node.Text, StringComparison.Ordinal))
                {
                    parts.Add(node.Text);
                }
            }

            var text = NormalizeStatusText(string.Join(" ", parts));
            if (!text.IsNullOrWhitespace())
            {
                result.Add(text);
            }
        }

        return result
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static unsafe string GetTextNodeText(AtkTextNode* textNode)
    {
        if (textNode == null)
        {
            return string.Empty;
        }

        try
        {
            return NormalizeStatusText(SeString.Parse(textNode->NodeText.AsSpan()).TextValue);
        }
        catch
        {
            return NormalizeStatusText(textNode->NodeText.ToString());
        }
    }

    private static bool IsContentsFinderDutyLockedOrUnavailable(ContentsFinderSnapshot snapshot)
    {
        var text = snapshot.CombinedText;
        if (ContainsAny(
                text,
                "无法参加",
                "没有满足下列条件",
                "未满足下列条件",
                "条件未满足",
                "未满足条件",
                "未解锁",
                "尚未开放",
                "不可参加",
                "Unable to register",
                "Unable to join",
                "Requirements not met",
                "Requirement not met",
                "Not yet unlocked",
                "Locked"))
        {
            return true;
        }

        return snapshot.JoinButtonEnabled == false;
    }

    private static string FormatStatusAsDetail(MonitorTaskStatus status)
    {
        var text = Plugin.Localization.Localize(status.StatusText);
        if (!status.DetailText.IsNullOrWhitespace())
        {
            text = $"{text} {status.DetailText}";
        }

        return text;
    }

    private static string? JoinDistinctDetails(IEnumerable<string> details)
    {
        var distinctDetails = details
            .Where(detail => !detail.IsNullOrWhitespace())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return distinctDetails.Length == 0
            ? null
            : string.Join("，", distinctDetails);
    }

    private static bool ContainsAny(string text, params string[] keywords)
    {
        if (text.IsNullOrWhitespace())
        {
            return false;
        }

        return keywords.Any(keyword => !keyword.IsNullOrWhitespace() &&
                                       text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeStatusText(string? text)
    {
        if (text.IsNullOrWhitespace())
        {
            return string.Empty;
        }

        var normalized = NormalizeDigits(text)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Replace("　", " ", StringComparison.Ordinal)
            .Trim();
        normalized = Regex.Replace(normalized, @"[ \t]+", " ");
        normalized = Regex.Replace(normalized, @"\n{2,}", "\n");

        return normalized;
    }

    private static IEnumerable<string> GetWeeklyTaskFallbackKeywords(string taskKey)
    {
        if (string.Equals(taskKey, WeeklyTaskCurrentAllianceRaidKey, StringComparison.OrdinalIgnoreCase))
        {
            yield return CurrentAllianceRaidNameKeyword;
            yield return "团队任务";
            yield return "第一巡行";
            yield return "第二巡行";
            yield return "第三巡行";
            yield break;
        }

        var allianceIndex = Array.FindIndex(AllianceRaidTaskKeys, key => string.Equals(key, taskKey, StringComparison.OrdinalIgnoreCase));
        if (allianceIndex >= 0 && allianceIndex < CurrentAllianceRaidNameKeywords.Length)
        {
            yield return CurrentAllianceRaidNameKeyword;
            yield return "团队任务";
            yield return CurrentAllianceRaidNameKeywords[allianceIndex];
            yield return $"团本{CurrentAllianceRaidNameKeywords[allianceIndex]}";
            yield break;
        }

        if (string.Equals(taskKey, WeeklyTaskUnrealTrialKey, StringComparison.OrdinalIgnoreCase))
        {
            yield return CurrentUnrealTrialNameKeyword;
            yield return "幻巧战";
            yield break;
        }

        var savageIndex = Array.FindIndex(SavageRaidTaskKeys, key => string.Equals(key, taskKey, StringComparison.OrdinalIgnoreCase));
        if (savageIndex >= 0 && savageIndex < CurrentSavageRaidNameKeywords.Length)
        {
            yield return CurrentSavageRaidNameKeywords[savageIndex];
            yield return $"零式{CurrentSavageRaidNameKeywords[savageIndex]}";
        }
    }

    public static string GetRouletteTypeDisplayName(string? rouletteType, string? monitorTaskKey = null)
    {
        if (TryGetAllianceRaidHistoryDisplayName(rouletteType, monitorTaskKey, out var allianceRaidDisplayName))
        {
            return allianceRaidDisplayName;
        }

        return rouletteType ?? "-";
    }

    private static bool TryGetAllianceRaidHistoryDisplayName(string? rouletteType, string? monitorTaskKey, out string displayName)
    {
        if (!rouletteType.IsNullOrWhitespace())
        {
            if (ContainsNormalizedName(rouletteType, CurrentAllianceRaidNameKeywords[0]))
            {
                displayName = Plugin.Localization.Localize("Alliance Raid 1");
                return true;
            }

            if (ContainsNormalizedName(rouletteType, CurrentAllianceRaidNameKeywords[1]))
            {
                displayName = Plugin.Localization.Localize("Alliance Raid 2");
                return true;
            }

            if (ContainsNormalizedName(rouletteType, CurrentAllianceRaidNameKeywords[2]))
            {
                displayName = Plugin.Localization.Localize("Alliance Raid 3");
                return true;
            }

            if (rouletteType.Contains("团本", StringComparison.OrdinalIgnoreCase) ||
                rouletteType.Contains("团队任务", StringComparison.OrdinalIgnoreCase))
            {
                displayName = Plugin.Localization.Localize("Current Alliance Raid History Display");
                return true;
            }
        }

        if (monitorTaskKey.IsNullOrWhitespace())
        {
            displayName = string.Empty;
            return false;
        }

        if (string.Equals(monitorTaskKey, WeeklyTaskAllianceRaid1Key, StringComparison.OrdinalIgnoreCase))
        {
            displayName = Plugin.Localization.Localize("Alliance Raid 1");
            return true;
        }

        if (string.Equals(monitorTaskKey, WeeklyTaskAllianceRaid2Key, StringComparison.OrdinalIgnoreCase))
        {
            displayName = Plugin.Localization.Localize("Alliance Raid 2");
            return true;
        }

        if (string.Equals(monitorTaskKey, WeeklyTaskAllianceRaid3Key, StringComparison.OrdinalIgnoreCase))
        {
            displayName = Plugin.Localization.Localize("Alliance Raid 3");
            return true;
        }

        if (string.Equals(monitorTaskKey, WeeklyTaskCurrentAllianceRaidKey, StringComparison.OrdinalIgnoreCase))
        {
            displayName = Plugin.Localization.Localize("Current Alliance Raid History Display");
            return true;
        }

        displayName = string.Empty;
        return false;
    }

    public static List<(string PlayerName, string World)> GetCharacterIdentities()
    {
        ReloadTaskHistoryIfChanged();
        return TaskHistoryRoulettes
            .Where(r => !r.PlayerName.IsNullOrWhitespace() && !r.World.IsNullOrWhitespace())
            .Select(r => (r.PlayerName!, r.World!))
            .Distinct()
            .ToList();
    }

    public static bool IsTaskHistoryRouletteCompletedForPlayer(string rouletteName, string playerName, string world)
    {
        ReloadTaskHistoryIfChanged();
        var resetAt = GetCurrentRouletteResetCycleStart();
        if (playerName.IsNullOrWhitespace() || world.IsNullOrWhitespace()) return false;
        return TaskHistoryRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            AreDailyTaskNamesEquivalent(roulette.RouletteType, rouletteName) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, world, StringComparison.OrdinalIgnoreCase) &&
            IsTaskHistoryRouletteInCurrentResetCycle(roulette, resetAt));
    }

    public static bool IsTaskHistoryMonitorTaskCompletedForPlayer(string taskKey, string taskName, string playerName, string world, DateTime resetAt)
    {
        ReloadTaskHistoryIfChanged();
        var weeklyTaskContentNames = IsWeeklyTaskKey(taskKey)
            ? GetWeeklyTaskConditions(taskKey).Select(c => c.Name.ToString()).Where(n => !n.IsNullOrWhitespace()).ToArray()
            : [];
        if (playerName.IsNullOrWhitespace() || world.IsNullOrWhitespace()) return false;
        return TaskHistoryRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            IsTaskHistoryRecordMatched(roulette, taskKey, taskName, weeklyTaskContentNames) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, world, StringComparison.OrdinalIgnoreCase) &&
            IsTaskHistoryRouletteInCurrentResetCycle(roulette, resetAt));
    }

    public static bool IsCurrentAllianceRaidCompletedForPlayer(string playerName, string world)
    {
        ReloadTaskHistoryIfChanged();
        if (playerName.IsNullOrWhitespace() || world.IsNullOrWhitespace()) return false;
        var completionContentNames = GetCurrentAllianceRaidCompletionContentNames();
        if (completionContentNames.Length == 0) return false;
        var resetAt = GetCurrentWeeklyResetCycleStart();
        return TaskHistoryRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            IsCurrentAllianceRaidCompletionMatched(roulette, completionContentNames) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, world, StringComparison.OrdinalIgnoreCase) &&
            IsTaskHistoryRouletteInCurrentResetCycle(roulette, resetAt));
    }

    public static bool IsTaskHistoryRouletteCompletedInCurrentResetCycle(string rouletteName)
    {
        ReloadTaskHistoryIfChanged();

        var resetAt = GetCurrentRouletteResetCycleStart();
        var playerName = Plugin.GetPlayerName();
        var worldName = Plugin.GetPlayerWorldName();

        if (playerName.IsNullOrWhitespace() || worldName.IsNullOrWhitespace())
        {
            return false;
        }

        return TaskHistoryRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            AreDailyTaskNamesEquivalent(roulette.RouletteType, rouletteName) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, worldName, StringComparison.OrdinalIgnoreCase) &&
            IsTaskHistoryRouletteInCurrentResetCycle(roulette, resetAt));
    }

    public static bool IsTaskHistoryMonitorTaskCompletedInCurrentResetCycle(string taskKey, string taskName, DateTime resetAt)
    {
        ReloadTaskHistoryIfChanged();

        var playerName = Plugin.GetPlayerName();
        var worldName = Plugin.GetPlayerWorldName();
        var weeklyTaskContentNames = IsWeeklyTaskKey(taskKey)
            ? GetWeeklyTaskConditions(taskKey)
                .Select(condition => condition.Name.ToString())
                .Where(name => !name.IsNullOrWhitespace())
                .ToArray()
            : [];

        if (playerName.IsNullOrWhitespace() || worldName.IsNullOrWhitespace())
        {
            return false;
        }

        return TaskHistoryRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            IsTaskHistoryRecordMatched(roulette, taskKey, taskName, weeklyTaskContentNames) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, worldName, StringComparison.OrdinalIgnoreCase) &&
            IsTaskHistoryRouletteInCurrentResetCycle(roulette, resetAt));
    }

    private static bool IsTaskHistoryRecordMatched(
        TaskHistoryRoulette roulette,
        string taskKey,
        string taskName,
        IReadOnlyCollection<string> weeklyTaskContentNames)
    {
        if (!roulette.MonitorTaskKey.IsNullOrWhitespace() &&
            string.Equals(roulette.MonitorTaskKey, taskKey, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsContentNameMatchedToWeeklyTask(roulette.ContentName, weeklyTaskContentNames))
        {
            return true;
        }

        if (string.Equals(taskKey, WeeklyTaskUnrealTrialKey, StringComparison.OrdinalIgnoreCase) &&
            IsUnrealTrialHistoryRecordMatched(roulette))
        {
            return true;
        }

        return AreDailyTaskNamesEquivalent(GetRouletteTypeDisplayName(roulette.RouletteType, roulette.MonitorTaskKey), taskName);
    }

    private static bool IsUnrealTrialHistoryRecordMatched(TaskHistoryRoulette roulette)
    {
        return IsUnrealTrialHistoryName(roulette.ContentName) ||
               IsUnrealTrialHistoryName(roulette.RouletteType) ||
               string.Equals(roulette.MonitorTaskKey, WeeklyTaskUnrealTrialKey, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUnrealTrialHistoryName(string? name)
    {
        if (name.IsNullOrWhitespace())
        {
            return false;
        }

        return ContainsNormalizedName(name, CurrentUnrealTrialNameKeyword) ||
               name.Contains("幻巧战", StringComparison.Ordinal) ||
               name.Contains("幻巧", StringComparison.Ordinal) ||
               name.Contains("Unreal", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsContentNameMatchedToWeeklyTask(string? contentName, IReadOnlyCollection<string> weeklyTaskContentNames)
    {
        if (contentName.IsNullOrWhitespace() || weeklyTaskContentNames.Count == 0)
        {
            return false;
        }

        return weeklyTaskContentNames.Any(taskContentName => AreDailyTaskNamesEquivalent(taskContentName, contentName));
    }

    private static IEnumerable<ContentFinderCondition> GetWeeklyTaskConditions(string taskKey)
    {
        if (string.Equals(taskKey, WeeklyTaskCurrentAllianceRaidKey, StringComparison.OrdinalIgnoreCase))
        {
            return GetCurrentAllianceRaidConditions();
        }

        var allianceIndex = Array.FindIndex(AllianceRaidTaskKeys, key => string.Equals(key, taskKey, StringComparison.OrdinalIgnoreCase));
        if (allianceIndex >= 0)
        {
            var explicitAllianceMatches = GetContentFinderConditionsByNameKeyword(CurrentAllianceRaidNameKeywords[allianceIndex])
                .Where(IsAllianceRaidCondition)
                .ToArray();
            if (explicitAllianceMatches.Length > 0)
            {
                return explicitAllianceMatches;
            }

            var allianceRaids = GetCurrentAllianceRaidConditions();
            return allianceIndex < allianceRaids.Length
                ? [allianceRaids[allianceIndex]]
                : [];
        }

        if (string.Equals(taskKey, WeeklyTaskUnrealTrialKey, StringComparison.OrdinalIgnoreCase))
        {
            var exactMatches = GetContentFinderConditionsByNameKeyword(CurrentUnrealTrialNameKeyword)
                .Where(IsCurrentUnrealTrial)
                .ToArray();

            return exactMatches.Length > 0
                ? exactMatches
                : Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()
                    .Where(IsCurrentUnrealTrial)
                    .ToArray();
        }

        var savageIndex = Array.FindIndex(SavageRaidTaskKeys, key => string.Equals(key, taskKey, StringComparison.OrdinalIgnoreCase));
        if (savageIndex < 0)
        {
            return [];
        }

        var explicitSavageMatches = GetContentFinderConditionsByNameKeyword(CurrentSavageRaidNameKeywords[savageIndex])
            .Where(IsSavageRaidCondition)
            .ToArray();
        if (explicitSavageMatches.Length > 0)
        {
            return explicitSavageMatches;
        }

        var savageRaids = GetCurrentSavageRaidConditions();
        return savageIndex < savageRaids.Length
            ? [savageRaids[savageIndex]]
            : [];
    }

    private static bool AreDailyTaskNamesEquivalent(string? left, string? right)
    {
        if (left.IsNullOrWhitespace() || right.IsNullOrWhitespace())
        {
            return false;
        }

        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var normalizedLeft = NormalizeDailyTaskName(left);
        var normalizedRight = NormalizeDailyTaskName(right);
        if (string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (IsCrystallineConflictCasualName(left) && IsCrystallineConflictCasualName(right)) ||
               (IsCrystallineConflictRankedName(left) && IsCrystallineConflictRankedName(right));
    }

    private static bool IsCrystallineConflictName(string? name)
    {
        return IsCrystallineConflictCasualName(name) || IsCrystallineConflictRankedName(name);
    }

    private static bool IsCrystallineConflictCasualName(string? name)
    {
        var normalizedName = NormalizeDailyTaskName(name);
        return string.Equals(normalizedName, NormalizeDailyTaskName(CrystallineConflictCasualName), StringComparison.OrdinalIgnoreCase) ||
               normalizedName.Contains("crystallineconflictcasual", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCrystallineConflictRankedName(string? name)
    {
        var normalizedName = NormalizeDailyTaskName(name);
        return string.Equals(normalizedName, NormalizeDailyTaskName(CrystallineConflictRankedName), StringComparison.OrdinalIgnoreCase) ||
               normalizedName.Contains("crystallineconflictranked", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDailyTaskName(string? name)
    {
        if (name.IsNullOrWhitespace())
        {
            return string.Empty;
        }

        return NormalizeDigits(name)
            .Replace("（", string.Empty, StringComparison.Ordinal)
            .Replace("）", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace("：", string.Empty, StringComparison.Ordinal)
            .Replace(":", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("　", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static string NormalizeDigits(string text)
    {
        return text
            .Replace('０', '0')
            .Replace('１', '1')
            .Replace('２', '2')
            .Replace('３', '3')
            .Replace('４', '4')
            .Replace('５', '5')
            .Replace('６', '6')
            .Replace('７', '7')
            .Replace('８', '8')
            .Replace('９', '9');
    }

    private static bool IsTaskHistoryRouletteInCurrentResetCycle(TaskHistoryRoulette roulette, DateTime resetAt)
    {
        var recordTime = roulette.GetEndedDateTime() ?? roulette.GetStartedDateTime();
        return recordTime != null && recordTime >= resetAt;
    }

    private static DateTime GetCurrentRouletteResetCycleStart()
    {
        var resetAtToday = DateTime.Today.AddHours(23);
        return DateTime.Now >= resetAtToday
            ? resetAtToday
            : resetAtToday.AddDays(-1);
    }

    internal static DateTime GetCurrentWeeklyResetCycleStart()
    {
        var today = DateTime.Today;
        var daysSinceResetDay = ((int)today.DayOfWeek - WeeklyResetDay + 7) % 7;
        var resetAt = today.AddDays(-daysSinceResetDay).AddHours(WeeklyResetHour);

        return DateTime.Now >= resetAt
            ? resetAt
            : resetAt.AddDays(-7);
    }

    private static ContentFinderCondition[] GetCurrentAllianceRaidConditions()
    {
        var candidates = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(IsAllianceRaidCondition)
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        var maxExpansion = candidates.Max(condition => condition.RequiredExVersion.RowId);
        var maxLevel = candidates
            .Where(condition => condition.RequiredExVersion.RowId == maxExpansion)
            .Max(condition => condition.ClassJobLevelRequired);
        var currentExpansionCandidates = candidates
            .Where(condition => condition.RequiredExVersion.RowId == maxExpansion &&
                                condition.ClassJobLevelRequired == maxLevel)
            .ToArray();

        if (currentExpansionCandidates.Length == 0)
        {
            return [];
        }

        return currentExpansionCandidates
            .OrderBy(condition => condition.SortKey)
            .ThenBy(condition => condition.RowId)
            .ToArray();
    }

    private static ContentFinderCondition[] GetCurrentSavageRaidConditions()
    {
        var explicitSavageRaids = GetExplicitCurrentSavageRaidConditions();
        if (explicitSavageRaids.Length == CurrentSavageRaidNameKeywords.Length)
        {
            return explicitSavageRaids;
        }

        var candidates = Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(IsSavageRaidCondition)
            .ToArray();

        if (candidates.Length == 0)
        {
            return [];
        }

        var maxExpansion = candidates.Max(condition => condition.RequiredExVersion.RowId);
        var maxLevel = candidates
            .Where(condition => condition.RequiredExVersion.RowId == maxExpansion)
            .Max(condition => condition.ClassJobLevelRequired);

        return candidates
            .Where(condition => condition.RequiredExVersion.RowId == maxExpansion &&
                                condition.ClassJobLevelRequired == maxLevel)
            .OrderBy(condition => condition.SortKey)
            .ThenBy(condition => condition.RowId)
            .TakeLast(4)
            .OrderBy(condition => condition.SortKey)
            .ThenBy(condition => condition.RowId)
            .ToArray();
    }

    private static ContentFinderCondition[] GetExplicitCurrentSavageRaidConditions()
    {
        var result = new List<ContentFinderCondition>();
        foreach (var keyword in CurrentSavageRaidNameKeywords)
        {
            var matches = GetContentFinderConditionsByNameKeyword(keyword)
                .Where(IsSavageRaidCondition)
                .OrderBy(condition => condition.SortKey)
                .ThenBy(condition => condition.RowId)
                .ToArray();

            if (matches.Length == 0)
            {
                continue;
            }

            result.Add(matches[0]);
        }

        return result.ToArray();
    }

    private static bool IsAllianceRaidCondition(ContentFinderCondition condition)
    {
        return condition is { IsInDutyFinder: true, PvP: false } &&
               condition.QueueMaxPlayers >= 24 &&
               condition.ClassJobLevelRequired > 0 &&
               !condition.Name.ToString().IsNullOrWhitespace();
    }

    private static bool IsSavageRaidCondition(ContentFinderCondition condition)
    {
        var name = condition.Name.ToString();
        if (name.IsNullOrWhitespace())
        {
            return false;
        }

        return condition is { IsInDutyFinder: true, PvP: false, QueueMaxPlayers: 8, HighEndDuty: true } &&
               !IsCurrentUnrealTrial(condition) &&
               !name.Contains("绝", StringComparison.Ordinal) &&
               (name.Contains("零式", StringComparison.Ordinal) ||
                name.Contains("Savage", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCurrentSavageRaidCondition(ContentFinderCondition condition, string nameKeyword)
    {
        return IsSavageRaidCondition(condition) && ContainsNormalizedName(condition.Name.ToString(), nameKeyword);
    }

    private static bool IsCurrentAllianceRaidCondition(ContentFinderCondition condition, string nameKeyword)
    {
        return IsAllianceRaidCondition(condition) && ContainsNormalizedName(condition.Name.ToString(), nameKeyword);
    }

    private static bool IsCurrentUnrealTrial(ContentFinderCondition condition)
    {
        var name = condition.Name.ToString();
        if (name.IsNullOrWhitespace())
        {
            return false;
        }

        return condition is { IsInDutyFinder: true, PvP: false, HighEndDuty: true } &&
               (ContainsNormalizedName(name, CurrentUnrealTrialNameKeyword) ||
                name.Contains("幻巧战", StringComparison.Ordinal) ||
                name.Contains("Unreal", StringComparison.OrdinalIgnoreCase));
    }

    private static ContentFinderCondition[] GetContentFinderConditionsByNameKeyword(string nameKeyword)
    {
        return Plugin.DataManager.GetExcelSheet<ContentFinderCondition>()
            .Where(condition => ContainsNormalizedName(condition.Name.ToString(), nameKeyword))
            .OrderBy(condition => condition.SortKey)
            .ThenBy(condition => condition.RowId)
            .ToArray();
    }

    private static bool ContainsNormalizedName(string? name, string keyword)
    {
        if (name.IsNullOrWhitespace() || keyword.IsNullOrWhitespace())
        {
            return false;
        }

        return NormalizeDailyTaskName(name).Contains(NormalizeDailyTaskName(keyword), StringComparison.OrdinalIgnoreCase);
    }

    public static void Save()
    {
        File.WriteAllText(DbPath, JsonConvert.SerializeObject(Roulettes));
    }

    public static void SaveTaskHistory()
    {
        File.WriteAllText(TaskHistoryDbPath, JsonConvert.SerializeObject(TaskHistoryRoulettes));
        taskHistoryDbLastWriteTimeUtc = GetTaskHistoryDbLastWriteTimeUtc();
    }

    public static void ClearTaskHistory()
    {
        TaskHistoryRoulettes.Clear();
        SaveTaskHistory();
    }

    public static Roulette? LoadFromPendingData()
    {
        if (!File.Exists(PendingDbPath)) return null;

        var content = File.ReadAllText(DbPath);
        return content.IsNullOrEmpty() ? null : JsonConvert.DeserializeObject<Roulette>(content);
    }

    public static void SavePendingRoulette()
    {
        if (Roulette.Instance != null)
        {
            File.WriteAllText(PendingDbPath, JsonConvert.SerializeObject(Roulette.Instance));
        }
    }

    public static void ExportAsCsv(string destPath)
    {
        // make excel recognize the encoding
        using var writer = new StreamWriter(destPath, false, new UTF8Encoding(true));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        csv.Context.RegisterClassMap<RouletteCsvMap>();
        csv.WriteRecords(Roulettes);

        // open the file explorer to the export location
        var argument = "/select, \"" + destPath + "\"";
        Process.Start("explorer.exe", argument);
    }
}
