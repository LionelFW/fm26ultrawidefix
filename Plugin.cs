using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace FM26UltrawideFix;

[BepInPlugin("com.fm26ultrawidefix", "FM26 Ultrawide Fix", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static new ManualLogSource Log = null!;

    public static ConfigEntry<bool> Enabled = null!;
    public static ConfigEntry<int> TargetWidth = null!;
    public static ConfigEntry<int> TargetHeight = null!;
    public static ConfigEntry<bool>   PatchMatchCamera = null!;
    public static ConfigEntry<bool>   DiagnosticDump = null!;
    public static ConfigEntry<bool>   LogExpansions = null!;
    public static ConfigEntry<bool>   LogSkipped    = null!;
    public static ConfigEntry<string> SkipExpansionElements = null!;

    public override void Load()
    {
        Log = base.Log;

        Enabled        = Config.Bind("General",    "Enabled",        true,  "Enable ultrawide fix");
        TargetWidth    = Config.Bind("Resolution",  "Width",          0,     "Override width (0 = auto-detect from primary display)");
        TargetHeight   = Config.Bind("Resolution",  "Height",         0,     "Override height (0 = auto-detect from primary display)");
        PatchMatchCamera       = Config.Bind("Patches", "PatchMatchCamera", true, "Correct aspect ratio on match-engine cameras");
        SkipExpansionElements  = Config.Bind("Patches", "SkipExpansionElements", "ModalDialog,GenericModalDialog,Card,ExternalNewsDynamicCard",
            "Comma-separated element names whose subtrees are excluded from width expansion. " +
            "Enable DiagnosticDump to find names, then add problem elements here.");
        DiagnosticDump = Config.Bind("Debug", "DiagnosticDump", false, "Log all root GameObjects and components on scene load");
        LogExpansions  = Config.Bind("Debug", "LogExpansions",  false, "Log every element our code expands each cycle — use to identify elements being incorrectly widened");
        LogSkipped     = Config.Bind("Debug", "LogSkipped",     false, "Log named elements that were NOT expanded and why (skip-list / unreadable / below-threshold / row-flex)");

        if (!Enabled.Value)
        {
            Log.LogInfo("FM26 Ultrawide Fix disabled via config.");
            return;
        }

        try
        {
            Harmony.CreateAndPatchAll(typeof(UltrawidePatches), "com.fm26ultrawidefix");
            Log.LogInfo("Harmony patches applied.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Harmony patching failed: {ex}");
        }

        ClassInjector.RegisterTypeInIl2Cpp<PanelScaler>();
        var go = new GameObject("FM26UltrawideFix_PanelScaler");
        GameObject.DontDestroyOnLoad(go);
        go.AddComponent<PanelScaler>();

        Log.LogInfo("FM26 Ultrawide Fix loaded.");
    }
}
