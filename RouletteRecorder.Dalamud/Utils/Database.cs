using CsvHelper;
using Dalamud.Utility;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using RouletteRecorder.Dalamud.DAO;
using RouletteRecorder.Dalamud.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using ClientInstanceContent = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent;

namespace RouletteRecorder.Dalamud.Utils;

public class Database
{
    public const string DailyTaskRouletteKeyPrefix = "roulette:";
    public const string DailyTaskContentFinderConditionKeyPrefix = "contentFinderCondition:";
    public const string DailyTaskCrystallineConflictCasualKey = "crystallineConflict:casual";
    public const string DailyTaskCrystallineConflictRankedKey = "crystallineConflict:ranked";
    public const string CrystallineConflictCasualName = "水晶冲突练习赛";
    public const string CrystallineConflictRankedName = "水晶冲突段位赛";

    public static readonly string DbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data.json");
    public static readonly string RisuiDbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "risui.json");
    public static readonly string PendingDbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data_pending.json");
    public static readonly ContentRoulette[] CfRoulettes = Plugin.DataManager.GetExcelSheet<ContentRoulette>()
        .Where(roulette => roulette is { IsInDutyFinder: true, IsGoldSaucer: false })
        .OrderBy(roulette => roulette.SortKey)
        .ThenBy(roulette => roulette.RowId)
        .ToArray();
    public static bool IsPendingDbExists() => File.Exists(PendingDbPath);

    public static List<Roulette> Roulettes { get; private set; } = [];
    public static List<RisuiRoulette> RisuiRoulettes { get; private set; } = [];

    public static void Load()
    {
        if (!File.Exists(DbPath)) Save();
        if (!File.Exists(RisuiDbPath)) SaveRisui();

        var content = File.ReadAllText(DbPath);

        var deserialized = JsonConvert.DeserializeObject<List<Roulette>>(content);
        if (deserialized != null)
        {
            Roulettes = deserialized;
        }

        var risuiContent = File.ReadAllText(RisuiDbPath);
        var deserializedRisui = JsonConvert.DeserializeObject<List<RisuiRoulette>>(risuiContent);
        if (deserializedRisui != null)
        {
            RisuiRoulettes = deserializedRisui;
        }
    }

    public static void InsertRoulette(Roulette roulette)
    {
        Roulettes.Add(roulette);
        Save();
    }

    public static void InsertRisuiRoulette(RisuiRoulette roulette)
    {
        RisuiRoulettes.Add(roulette);
        SaveRisui();
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
        if (TryGetClientRouletteCompletion(taskKey, out var isCompleted))
        {
            return isCompleted;
        }

        return IsRisuiRouletteCompletedInCurrentResetCycle(rouletteName);
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

    public static bool IsRisuiRouletteCompletedInCurrentResetCycle(string rouletteName)
    {
        var resetAt = GetCurrentRouletteResetCycleStart();
        var playerName = Plugin.GetPlayerName();
        var worldName = Plugin.GetPlayerWorldName();

        if (playerName.IsNullOrWhitespace() || worldName.IsNullOrWhitespace())
        {
            return false;
        }

        return RisuiRoulettes.Any(roulette =>
            roulette.IsCompleted &&
            AreDailyTaskNamesEquivalent(roulette.RouletteType, rouletteName) &&
            string.Equals(roulette.PlayerName, playerName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(roulette.World, worldName, StringComparison.OrdinalIgnoreCase) &&
            IsRisuiRouletteInCurrentResetCycle(roulette, resetAt));
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

        return name
            .Replace("（", string.Empty, StringComparison.Ordinal)
            .Replace("）", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim();
    }

    private static bool IsRisuiRouletteInCurrentResetCycle(RisuiRoulette roulette, DateTime resetAt)
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

    public static void Save()
    {
        File.WriteAllText(DbPath, JsonConvert.SerializeObject(Roulettes));
    }

    public static void SaveRisui()
    {
        File.WriteAllText(RisuiDbPath, JsonConvert.SerializeObject(RisuiRoulettes));
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
