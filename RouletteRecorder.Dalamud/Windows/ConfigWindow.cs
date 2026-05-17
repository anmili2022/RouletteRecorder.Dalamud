using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using Dalamud.Utility;
using RouletteRecorder.Dalamud.Network.DungeonLogger;
using RouletteRecorder.Dalamud.Utils;
using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;

namespace RouletteRecorder.Dalamud.Windows;

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

    public ConfigWindow(Plugin plugin)
        : base($"设置窗口 v{typeof(Plugin).Assembly.GetName().Version}###rouletteRecorderConfigWindow", ImGuiWindowFlags.None)
    {
        this.plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(520, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawHeader();
        DrawSaveFolderSection();
        DrawFloatingWindowStyleSection();
        DrawDailyTaskMonitorSection();
        DrawSubscribedRouletteTypesSection();
        DrawDungeonLoggerSection();
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
            Plugin.Configuration.Save();
        }

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

    private static void DrawDailyTaskMonitorSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Daily Task Monitor"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.Indent();
        ImGui.TextDisabled(Plugin.Localization.Localize("Daily Task Monitor Hint"));
        ImGui.Spacing();

        if (ImGui.BeginTable("DailyTaskMonitorTable", 2, ImGuiTableFlags.SizingStretchProp))
        {
            var index = 0;
            foreach (var option in Database.GetDailyTaskMonitorOptions())
            {
                ImGui.TableNextColumn();

                var selected = Plugin.Configuration.MonitoredDailyTaskKeys.Contains(option.Key);
                if (ImGui.Checkbox($"{option.Name}##dailyTaskMonitor{option.Key}", ref selected))
                {
                    Plugin.Configuration.SetMonitoredDailyTaskKey(option.Key, selected);
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
}
