using Dalamud.Configuration;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace RouletteRecorder.Dalamud;

public enum FloatingWindowStyle
{
    Classic = 0,
    Minimal = 1,
}

public enum NoteScope
{
    Public = 0,
    Character = 1,
}

public enum NoteBackgroundStyle
{
    Frosted = 0,
    Transparent = 1,
}

[Serializable]
public class DungeonLoggerConfig
{
    public bool Enabled = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Username = string.Empty;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
    public string Password = string.Empty;
}


[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public string Language = "zh_CN";
    public HashSet<uint> SubscribedRouletteIds { get; set; } = [9];
    public string CsvExportPath { get; set; } = Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, "data.csv");
    public DungeonLoggerConfig DungeonLoggerConfig { get; set; } = new();
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
    public HashSet<string> MonitoredWeeklyTaskKeys { get; set; } = ["weekly:wondrousTails", "weekly:currentAllianceRaid:1", "weekly:currentAllianceRaid:2", "weekly:currentAllianceRaid:3", "weekly:unrealTrial", "weekly:savageRaid:1"];
    public int TribalQuestCompletionCount { get; set; } = 3;
    public bool MinimalShowCurrentTask { get; set; } = false;
    public bool MinimalShowTaskTime { get; set; } = false;
    public bool MinimalShowTodayMentorRouletteCount { get; set; } = true;
    public bool MinimalShowMentorRouletteTotalCount { get; set; } = false;
    public bool ShowCurrentTime { get; set; } = true;
    public bool EnableNoteWindow { get; set; } = false;
    public NoteScope NoteScopeMode { get; set; } = NoteScope.Public;
    public NoteBackgroundStyle NoteBackgroundStyleMode { get; set; } = NoteBackgroundStyle.Frosted;
    public float NoteFrostedStrength { get; set; } = 1.0f;
    public float NoteFrostedWindowOpacity { get; set; } = 0.45f;
    public float NoteTransparentWindowOpacity { get; set; } = 0.12f;
    public string PublicNoteContent { get; set; } = string.Empty;
    public Dictionary<string, string> CharacterNoteContents { get; set; } = [];

    public bool SetSubscribedRouletteId(ContentRoulette roulette, bool selected)
    {
        var ret = selected ? SubscribedRouletteIds.Add(roulette.RowId) : SubscribedRouletteIds.Remove(roulette.RowId);
        Save();

        return ret;
    }

    public bool SetMonitoredDailyTaskKey(string key, bool selected)
    {
        var ret = selected ? MonitoredDailyTaskKeys.Add(key) : MonitoredDailyTaskKeys.Remove(key);
        Save();

        return ret;
    }

    public bool SetMonitoredWeeklyTaskKey(string key, bool selected)
    {
        var ret = selected ? MonitoredWeeklyTaskKeys.Add(key) : MonitoredWeeklyTaskKeys.Remove(key);
        Save();

        return ret;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
