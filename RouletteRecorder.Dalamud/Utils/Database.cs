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

namespace RouletteRecorder.Dalamud.Utils;

public class Database
{
    public static readonly string DbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data.json");
    public static readonly string PendingDbPath = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data_pending.json");
    public static readonly ContentRoulette[] CfRoulettes = Plugin.DataManager.GetExcelSheet<ContentRoulette>().Where(roulette => roulette is { IsInDutyFinder: true, IsGoldSaucer: false }).ToArray();
    public static bool IsPendingDbExists() => File.Exists(PendingDbPath);

    public static List<Roulette> Roulettes { get; private set; } = [];

    public static void Load()
    {
        if (!File.Exists(DbPath)) Save();

        var content = File.ReadAllText(DbPath);

        var deserialized = JsonConvert.DeserializeObject<List<Roulette>>(content);
        if (deserialized != null)
        {
            Roulettes = deserialized;
        }
    }

    public static void InsertRoulette(Roulette roulette)
    {
        Roulettes.Add(roulette);
        Save();
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

    public static void Save()
    {
        File.WriteAllText(DbPath, JsonConvert.SerializeObject(Roulettes));
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
