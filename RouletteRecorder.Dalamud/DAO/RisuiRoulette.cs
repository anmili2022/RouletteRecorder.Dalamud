using Dalamud.Utility;
using Newtonsoft.Json;
using RouletteRecorder.Dalamud.Utils;
using System;
using System.Globalization;

namespace RouletteRecorder.Dalamud.DAO;

public class RisuiRoulette(string? contentName, string? rouletteType, bool isCompleted = false)
{
    [JsonProperty(Order = 0)]
    public string? RouletteType { get; set; } = rouletteType;

    [JsonProperty(Order = 1)]
    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    [JsonProperty(Order = 2)]
    public string StartedAt { get; set; } = DateTime.Now.ToString("T");

    [JsonProperty(Order = 3)]
    public string? EndedAt { get; set; }

    [JsonProperty(Order = 4)]
    public string? ContentName { get; set; } = contentName;

    [JsonProperty(Order = 5)]
    public string? JobName { get; set; }

    [JsonProperty(Order = 6)]
    public bool IsCompleted { get; set; } = isCompleted;

    [JsonProperty("playername", Order = 7)]
    public string? PlayerName { get; set; }

    [JsonProperty("world", Order = 8)]
    public string? World { get; set; }

    [JsonIgnore]
    public static RisuiRoulette? Instance { get; private set; }

    public static void Init(string? contentName = null, string? rouletteType = null, bool isCompleted = false)
    {
        Instance = new RisuiRoulette(contentName, rouletteType, isCompleted);
    }

    public static void Clear()
    {
        Instance = null;
    }

    public DateTime? GetStartedDateTime()
    {
        return TryParseDateTime(Date, StartedAt);
    }

    public DateTime? GetEndedDateTime()
    {
        if (EndedAt.IsNullOrEmpty()) return null;

        var startedAt = GetStartedDateTime();
        var endedAt = TryParseDateTime(Date, EndedAt);

        if (startedAt != null && endedAt != null && endedAt < startedAt)
        {
            endedAt = endedAt.Value.AddDays(1);
        }

        return endedAt;
    }

    public void Finish()
    {
        try
        {
            if (Instance == null || Instance.RouletteType.IsNullOrWhitespace())
            {
                return;
            }

            Instance.ContentName ??= Instance.RouletteType;
            Instance.JobName = Plugin.GetJobName() ?? "未知职业";
            Instance.PlayerName = Plugin.GetPlayerName() ?? "未知角色";
            Instance.World = Plugin.GetPlayerWorldName() ?? "未知服务器";
            Instance.EndedAt ??= DateTime.Now.ToString("T");

            Database.InsertRisuiRoulette(Instance);
            Instance = null;
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Failed to finish risui roulette");
        }
    }

    private static DateTime? TryParseDateTime(string? date, string? time)
    {
        if (date.IsNullOrEmpty() || time.IsNullOrEmpty()) return null;

        var text = $"{date} {time}";
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var currentCultureValue))
        {
            return currentCultureValue;
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var invariantCultureValue))
        {
            return invariantCultureValue;
        }

        return null;
    }
}
