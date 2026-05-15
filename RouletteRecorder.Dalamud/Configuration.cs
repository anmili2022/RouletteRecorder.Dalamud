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
    public float FloatingWindowOpacity { get; set; } = 0.54f;
    public bool LockFloatingWindow { get; set; } = false;
    public bool DefaultSubscriptionsInitialized { get; set; } = true;
    public bool MinimalShowCurrentTask { get; set; } = true;
    public bool MinimalShowTaskTime { get; set; } = false;
    public bool MinimalShowTodayMentorRouletteCount { get; set; } = true;
    public bool MinimalShowMentorRouletteTotalCount { get; set; } = false;

    public bool SetSubscribedRouletteId(ContentRoulette roulette, bool selected)
    {
        var ret = selected ? SubscribedRouletteIds.Add(roulette.RowId) : SubscribedRouletteIds.Remove(roulette.RowId);
        Save();

        return ret;
    }

    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
