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
        : base($"设置窗口 v{typeof(Plugin).Assembly.GetName().Version}###rouletteRecorderConfigWindow", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 425),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawSaveFolderSection();
        DrawFloatingWindowStyleSection();

        if (ImGui.CollapsingHeader(Plugin.Localization.Localize("Subscribed Roulette Types")))
        {
            ImGui.Indent();
            foreach (var roulette in Database.CfRoulettes)
            {
                var selected = Plugin.Configuration.SubscribedRouletteIds.Contains(roulette.RowId);
                if (ImGui.Checkbox(roulette.Name.ToString(), ref selected))
                {
                    Plugin.Configuration.SetSubscribedRouletteId(roulette, selected);
                }
            }
            ImGui.Unindent();
        }

        if (ImGui.CollapsingHeader(Plugin.Localization.Localize("DungeonLogger Account Config")))
        {
            ImGui.Indent();
            if (ImGui.Checkbox(Plugin.Localization.Localize("Enable DungeonLogger Report"), ref Plugin.Configuration.DungeonLoggerConfig.Enabled))
            {
                Plugin.Configuration.Save();
            }
            ;

            if (Plugin.Configuration.DungeonLoggerConfig.Enabled)
            {
                ImGui.Text(Plugin.Localization.Localize("User Name"));
                ImGui.SameLine();
                if (ImGui.InputText("##username", ref Plugin.Configuration.DungeonLoggerConfig.Username, 100))
                {
                    Plugin.Configuration.Save();
                }

                ImGui.Text(Plugin.Localization.Localize("Password"));
                ImGui.SameLine();
                if (ImGui.InputText("##password", ref Plugin.Configuration.DungeonLoggerConfig.Password, 100, ImGuiInputTextFlags.Password))
                {
                    Plugin.Configuration.Save();
                }
                ;
            }

            if (ImGui.Button(Plugin.Localization.Localize("Test Login")))
            {
                Task.Run(TestDungeonLoggerLogin);
            }

            var loginStatusColor = loginStatus switch
            {
                LoginStatus.Pending => ImGuiColors.DalamudYellow,
                LoginStatus.Success => ImGuiColors.ParsedGreen,
                LoginStatus.Failed => ImGuiColors.DalamudRed,
                _ => ImGuiColors.DalamudWhite
            };

            const string pendingMessage = "Sending request to Dungeon Logger Server";
            ImGui.TextColored(loginStatusColor, Plugin.Localization.Localize(loginStatus == LoginStatus.Pending ? pendingMessage : loginResponseMessage));

            ImGui.Unindent();
        }
    }

    private static void DrawSaveFolderSection()
    {
        if (ImGui.Button(Plugin.Localization.Localize("Open Save Folder")))
        {
            OpenSaveFolder();
        }

        ImGui.SameLine();
        ImGui.TextDisabled(Plugin.PluginInterface.ConfigDirectory.FullName);
        ImGui.Separator();
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

    private static void DrawFloatingWindowStyleSection()
    {
        if (!ImGui.CollapsingHeader(Plugin.Localization.Localize("Floating Window Style"), ImGuiTreeNodeFlags.DefaultOpen))
        {
            return;
        }

        ImGui.Indent();

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

        var lockFloatingWindow = Plugin.Configuration.LockFloatingWindow;
        if (ImGui.Checkbox(Plugin.Localization.Localize("Lock Floating Window"), ref lockFloatingWindow))
        {
            Plugin.Configuration.LockFloatingWindow = lockFloatingWindow;
            Plugin.Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextDisabled(Plugin.Localization.Localize("Floating Window Display Items"));
        DrawDisplayItemCheckbox("Show Current Task", Plugin.Configuration.MinimalShowCurrentTask, value => Plugin.Configuration.MinimalShowCurrentTask = value);
        DrawDisplayItemCheckbox("Show Task Time", Plugin.Configuration.MinimalShowTaskTime, value => Plugin.Configuration.MinimalShowTaskTime = value);
        DrawDisplayItemCheckbox("Show Today Mentor Roulette Count", Plugin.Configuration.MinimalShowTodayMentorRouletteCount, value => Plugin.Configuration.MinimalShowTodayMentorRouletteCount = value);
        DrawDisplayItemCheckbox("Show Mentor Roulette Total Count", Plugin.Configuration.MinimalShowMentorRouletteTotalCount, value => Plugin.Configuration.MinimalShowMentorRouletteTotalCount = value);

        ImGui.Unindent();
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
