using Dalamud.Game.Command;
using Dalamud.Game.DutyState;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using RouletteRecorder.Dalamud.DAO;
using RouletteRecorder.Dalamud.Utils;
using RouletteRecorder.Dalamud.Windows;
using System;
using System.Linq;
using ClientAchievement = FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;

namespace RouletteRecorder.Dalamud;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IDutyState DutyState { get; private set; } = null!;
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;

    private const string CommandName = "/prr";
    // Achievement 1604 is the 2,000-clear mentor roulette achievement ("I Hope Mentor Will Notice Me VI").
    private const uint MentorRouletteAchievementId = 1604;
    private const uint MentorRouletteAchievementMaxCount = 2000;

    public static Configuration Configuration { get; private set; } = null!;
    public static Localization Localization { get; private set; } = null!;

    public readonly WindowSystem WindowSystem = new("日随伴侣");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private static DateTime lastMentorRouletteAchievementRequest = DateTime.MinValue;
    private static uint? mentorRouletteAchievementCurrent;
    private static uint? mentorRouletteAchievementMax;
    private static string mentorRouletteAchievementStatus = "Achievement progress not requested";

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Language = "zh_CN";
        Localization = new Localization(Configuration.Language);
        Database.Load();
        EnsureDefaultSubscriptions();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "打开日随伴侣界面"
        });

        ClientState.CfPop += OnCfPop;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.Logout += OnLogout;

        DutyState.DutyCompleted += OnDutyCompleted;

        PluginInterface.UiBuilder.Draw += DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        ClientState.CfPop -= OnCfPop;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        ClientState.Logout -= OnLogout;

        DutyState.DutyCompleted -= OnDutyCompleted;

        PluginInterface.UiBuilder.Draw -= DrawUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
    }

    private void OnLogin()
    {
        // TODO reconnect roulette recover ui
    }

    private static void OnLogout(int type, int code)
    {
        if (Roulette.Instance != null && Roulette.Instance.RouletteType != null && !Roulette.Instance.IsCompleted)
        {
            Database.SavePendingRoulette();
        }
    }

    private static void OnTerritoryChanged(uint territoryId)
    {
        var currentContent = DataManager.GetExcelSheet<TerritoryType>().GetRow(territoryId).ContentFinderCondition.ValueNullable;
        PluginLog.Debug($"[OnTerritoryChanged] currentContent: {currentContent?.Name}");

        if (Roulette.Instance == null)
            Roulette.Init();

        // entered the duty territory
        if (Roulette.Instance!.ContentName == null)
        {
            Roulette.Instance.ContentName = currentContent?.Name.ToString();
        }
        else
        {
            PluginLog.Debug("[OnTerritoryChanged] detected exited roulette, force to finish");

            if (Roulette.Instance.RouletteType != null) Roulette.Instance.Finish();
        }
    }

    private static unsafe void OnCfPop(ContentFinderCondition condition)
    {
        string? rouletteType = null;

        var queueInfo = ContentsFinder.Instance()->QueueInfo;
        var poppedContentType = queueInfo.PoppedQueueEntry.ContentType;
        var poppedContentId = queueInfo.PoppedQueueEntry.Id;

        if (poppedContentType == ContentsType.Roulette)
        {
            var currentRoulette = DataManager.GetExcelSheet<Lumina.Excel.Sheets.ContentRoulette>().GetRow(poppedContentId);
            rouletteType = currentRoulette.Name.ToString();
            PluginLog.Debug($"[OnCfPop] Detected roulette pop: {rouletteType}");
        }

        Roulette.Init(null, rouletteType);

        PluginLog.Debug(
            $"[OnCfPop] PoppedContentType: {poppedContentType}, PoppedContentId: {poppedContentId}, rouletteType: {rouletteType}"
        );
    }

    private static void OnDutyCompleted(IDutyStateEventArgs args)
    {
        PluginLog.Debug($"[OnDutyCompleted] {args.TerritoryType.ValueNullable?.RowId}");
        if (Roulette.Instance == null) return;

        Roulette.Instance.IsCompleted = true;
        Roulette.Instance.EndedAt ??= DateTime.Now.ToString("T");
        if (Roulette.Instance.RouletteType != null)
        {
            Roulette.Instance.Finish();
        }
    }

    private void OnCommand(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUi();
    }

    private void DrawUi() => WindowSystem.Draw();

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void OpenConfigUi()
    {
        ConfigWindow.IsOpen = true;
        ConfigWindow.RequestFocus = true;
    }

    private static void EnsureDefaultSubscriptions()
    {
        if (Configuration.DefaultSubscriptionsInitialized)
        {
            return;
        }

        if (Configuration.SubscribedRouletteIds.Count == 0)
        {
            foreach (var roulette in Database.CfRoulettes.Where(Database.IsMentorRoulette))
            {
                Configuration.SubscribedRouletteIds.Add(roulette.RowId);
            }
        }

        Configuration.DefaultSubscriptionsInitialized = true;
        Configuration.Save();
    }

    public static string? GetJobName() => PlayerState.ClassJob.ValueNullable?.Name.ToString();
    public static uint? GetJobId() => PlayerState.ClassJob.RowId;

    public static unsafe string GetMentorRouletteAchievementProgressText()
    {
        UpdateMentorRouletteAchievementProgress();

        if (mentorRouletteAchievementCurrent != null)
        {
            return $"{mentorRouletteAchievementCurrent} / {mentorRouletteAchievementMax ?? MentorRouletteAchievementMaxCount}";
        }

        return Localization.Localize(mentorRouletteAchievementStatus);
    }

    public static unsafe void RefreshMentorRouletteAchievementProgress()
    {
        UpdateMentorRouletteAchievementProgress(true);
    }

    private static unsafe void UpdateMentorRouletteAchievementProgress(bool force = false)
    {
        if (!ClientState.IsLoggedIn)
        {
            mentorRouletteAchievementStatus = "Not logged in";
            return;
        }

        var achievement = ClientAchievement.Instance();
        if (achievement == null)
        {
            mentorRouletteAchievementStatus = "Achievement API unavailable";
            return;
        }

        if (!force && achievement->IsLoaded() && achievement->IsComplete((int)MentorRouletteAchievementId))
        {
            mentorRouletteAchievementCurrent = MentorRouletteAchievementMaxCount;
            mentorRouletteAchievementMax = MentorRouletteAchievementMaxCount;
            mentorRouletteAchievementStatus = "Achievement progress loaded";
            return;
        }

        if (!force &&
            achievement->ProgressAchievementId == MentorRouletteAchievementId &&
            achievement->ProgressRequestState == ClientAchievement.AchievementState.Loaded)
        {
            mentorRouletteAchievementCurrent = achievement->ProgressCurrent;
            mentorRouletteAchievementMax = achievement->ProgressMax == 0 ? MentorRouletteAchievementMaxCount : achievement->ProgressMax;
            mentorRouletteAchievementStatus = "Achievement progress loaded";
            return;
        }

        if (!force && DateTime.Now - lastMentorRouletteAchievementRequest < TimeSpan.FromSeconds(30))
        {
            mentorRouletteAchievementStatus = "Reading achievement progress...";
            return;
        }

        achievement->RequestAchievementProgress(MentorRouletteAchievementId);
        lastMentorRouletteAchievementRequest = DateTime.Now;
        mentorRouletteAchievementStatus = "Reading achievement progress...";
    }

}
