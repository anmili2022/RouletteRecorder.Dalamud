using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Windowing;
using RouletteBuddy.Helpers;
using System;
using System.Numerics;

namespace RouletteBuddy.Windows;

public sealed class NoteWindow : Window, IDisposable
{
    private const int NoteMaxLength = 20000;
    private int pushedPreDrawStyleColorCount;
    private readonly CleanBackgroundManager? backgroundManager;

    public NoteWindow()
        : base("公共便签###rouletteRecorderNoteWindow", ImGuiWindowFlags.None)
    {
        Size = new Vector2(420, 300);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = Vector2.Zero,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };
        ShowCloseButton = true;
        IsOpen = Plugin.Configuration.EnableNoteWindow;

        try
        {
            backgroundManager = new CleanBackgroundManager(Plugin.PluginLog);
            backgroundManager.Initialize();
        }
        catch (Exception e)
        {
            Plugin.PluginLog.Error(e, "Failed to initialize note frosted background manager");
            backgroundManager = null;
        }
    }

    public void Dispose()
    {
        backgroundManager?.Dispose();
    }

    public override void PreDraw()
    {
        var title = GetCurrentTitle();
        var useFrostedBackground = Plugin.Configuration.NoteBackgroundStyleMode == NoteBackgroundStyle.Frosted;
        WindowName = $"{title}###rouletteRecorderNoteWindow";
        Flags = useFrostedBackground
            ? ImGuiWindowFlags.NoBackground
            : ImGuiWindowFlags.None;
        ShowCloseButton = true;
        AllowBackgroundBlur = useFrostedBackground;
        BgAlpha = GetCurrentNoteWindowOpacity();

        pushedPreDrawStyleColorCount = 0;
        if (useFrostedBackground)
        {
            var frostedStrength = GetFrostedStrength();
            var opacity = GetCurrentNoteWindowOpacity();
            var titleTint = new Vector3(0.12f, 0.14f, 0.19f);
            var titleAlpha = Math.Clamp((0.24f + 0.42f * frostedStrength) * (0.70f + 0.30f * opacity), 0.18f, 0.78f);

            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, Math.Clamp(0.20f + 0.35f * frostedStrength, 0.20f, 0.62f)));
            pushedPreDrawStyleColorCount++;
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(titleTint, titleAlpha));
            pushedPreDrawStyleColorCount++;
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(titleTint, Math.Clamp(titleAlpha + 0.10f, 0.20f, 0.82f)));
            pushedPreDrawStyleColorCount++;
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(titleTint, titleAlpha));
            pushedPreDrawStyleColorCount++;
        }
        else if (Plugin.Configuration.NoteBackgroundStyleMode == NoteBackgroundStyle.Transparent)
        {
            var opacity = GetCurrentNoteWindowOpacity();
            var titleTint = new Vector3(0.08f, 0.09f, 0.12f);
            var titleAlpha = Math.Clamp(opacity * 1.25f, 0.02f, 1.0f);

            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(titleTint, titleAlpha));
            pushedPreDrawStyleColorCount++;
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(titleTint, Math.Clamp(titleAlpha + 0.08f, 0.02f, 1.0f)));
            pushedPreDrawStyleColorCount++;
            ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(titleTint, titleAlpha));
            pushedPreDrawStyleColorCount++;
        }
    }

    public override void PostDraw()
    {
        if (pushedPreDrawStyleColorCount <= 0)
        {
            return;
        }

        ImGui.PopStyleColor(pushedPreDrawStyleColorCount);
        pushedPreDrawStyleColorCount = 0;
    }

    public override void Draw()
    {
        var useFrostedInputBackground = Plugin.Configuration.NoteBackgroundStyleMode == NoteBackgroundStyle.Frosted;
        if (useFrostedInputBackground)
        {
            try
            {
                if (backgroundManager != null)
                {
                    backgroundManager.BlurIterations = 1 + (int)Math.Round(GetFrostedStrength() * 5f);
                }

                backgroundManager?.DrawBackground(GetCurrentNoteWindowOpacity());
            }
            catch (Exception e)
            {
                Plugin.PluginLog.Error(e, "Failed to draw note frosted background");
            }
        }

        if (Plugin.Configuration.NoteScopeMode == NoteScope.Character &&
            GetCurrentCharacterNoteKey() == null)
        {
            ImGui.TextColored(ImGuiColors.DalamudYellow, Plugin.Localization.Localize("Character Note Login Required"));
            ImGui.TextWrapped(Plugin.Localization.Localize("Character Note Login Required Hint"));
            return;
        }

        var content = GetCurrentNoteContent();
        var inputSize = ImGui.GetContentRegionAvail();
        if (inputSize.Y < ImGui.GetTextLineHeightWithSpacing())
        {
            inputSize.Y = ImGui.GetTextLineHeightWithSpacing();
        }

        var pushedStyleColors = 1;
        var borderAlpha = useFrostedInputBackground
            ? Math.Clamp(0.25f + 0.40f * GetFrostedStrength(), 0.25f, 0.75f)
            : 0.35f;
        ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 1f, borderAlpha));
        if (useFrostedInputBackground)
        {
            var frostedStrength = GetFrostedStrength();
            var inputBgAlpha = Math.Clamp(0.03f + 0.10f * frostedStrength, 0.03f, 0.16f);
            var inputTint = new Vector3(0.18f, 0.22f, 0.30f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(inputTint, inputBgAlpha));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(inputTint, Math.Clamp(inputBgAlpha + 0.05f, 0.06f, 0.24f)));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(inputTint, Math.Clamp(inputBgAlpha + 0.08f, 0.08f, 0.28f)));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(inputTint, inputBgAlpha * 0.75f));
            pushedStyleColors += 4;
        }
        else if (Plugin.Configuration.NoteBackgroundStyleMode == NoteBackgroundStyle.Transparent)
        {
            var opacity = GetCurrentNoteWindowOpacity();
            var inputTint = new Vector3(0.06f, 0.07f, 0.10f);
            var inputBgAlpha = Math.Clamp(opacity * 0.90f, 0.02f, 1.0f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(inputTint, inputBgAlpha));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(inputTint, Math.Clamp(inputBgAlpha + 0.06f, 0.02f, 1.0f)));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(inputTint, Math.Clamp(inputBgAlpha + 0.10f, 0.02f, 1.0f)));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(inputTint, inputBgAlpha * 0.85f));
            pushedStyleColors += 4;
        }

        var changed = ImGui.InputTextMultiline(
            "##rouletteRecorderNoteInput",
            ref content,
            NoteMaxLength,
            inputSize,
            ImGuiInputTextFlags.AllowTabInput);

        ImGui.PopStyleColor(pushedStyleColors);
        ImGui.PopStyleVar();

        if (changed)
        {
            SetCurrentNoteContent(content);
        }
    }

    private static string GetCurrentTitle()
    {
        return Plugin.Localization.Localize(
            Plugin.Configuration.NoteScopeMode == NoteScope.Character
                ? "Character Note"
                : "Public Note");
    }

    private static float GetCurrentNoteWindowOpacity()
    {
        var opacity = Plugin.Configuration.NoteBackgroundStyleMode == NoteBackgroundStyle.Transparent
            ? Plugin.Configuration.NoteTransparentWindowOpacity
            : Plugin.Configuration.NoteFrostedWindowOpacity;

        return Math.Clamp(opacity, 0.05f, 1.0f);
    }

    private static float GetFrostedStrength()
    {
        return Math.Clamp(Plugin.Configuration.NoteFrostedStrength, 0f, 1f);
    }

    private static string GetCurrentNoteContent()
    {
        if (Plugin.Configuration.NoteScopeMode == NoteScope.Public)
        {
            return Plugin.Configuration.PublicNoteContent ?? string.Empty;
        }

        var key = GetCurrentCharacterNoteKey();
        if (key == null)
        {
            return string.Empty;
        }

        Plugin.Configuration.CharacterNoteContents ??= [];
        return Plugin.Configuration.CharacterNoteContents.TryGetValue(key, out var content)
            ? content
            : string.Empty;
    }

    private static void SetCurrentNoteContent(string content)
    {
        if (Plugin.Configuration.NoteScopeMode == NoteScope.Public)
        {
            if (Plugin.Configuration.PublicNoteContent == content)
            {
                return;
            }

            Plugin.Configuration.PublicNoteContent = content;
            Plugin.Configuration.Save();
            return;
        }

        var key = GetCurrentCharacterNoteKey();
        if (key == null)
        {
            return;
        }

        Plugin.Configuration.CharacterNoteContents ??= [];
        if (string.IsNullOrEmpty(content))
        {
            if (Plugin.Configuration.CharacterNoteContents.Remove(key))
            {
                Plugin.Configuration.Save();
            }

            return;
        }

        if (Plugin.Configuration.CharacterNoteContents.TryGetValue(key, out var oldContent) &&
            oldContent == content)
        {
            return;
        }

        Plugin.Configuration.CharacterNoteContents[key] = content;
        Plugin.Configuration.Save();
    }

    private static string? GetCurrentCharacterNoteKey()
    {
        var playerName = Plugin.GetPlayerName();
        var worldName = Plugin.GetPlayerWorldName();
        if (string.IsNullOrWhiteSpace(playerName) || string.IsNullOrWhiteSpace(worldName))
        {
            return null;
        }

        return $"{worldName}/{playerName}";
    }
}
