using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;

namespace FM26UltrawideFix;

[HarmonyPatch]
internal static class UltrawidePatches
{
    // ---------------------------------------------------------------------------
    // PanelSettings — UI Toolkit panel scaling
    //
    // VERIFY before shipping: open BepInEx/interop/Il2CppUnityEngine.UIElementsModule.dll
    // in dnSpy and confirm PanelSettings has an "ApplyPanelSettings" method with this
    // exact name. IL2CPP mangling may rename it — update nameof() if needed.
    // ---------------------------------------------------------------------------

    [HarmonyPatch(typeof(PanelSettings), nameof(PanelSettings.ApplyPanelSettings))]
    [HarmonyPostfix]
    static void PanelSettings_ApplyPanelSettings_Postfix(PanelSettings __instance)
    {
        PanelScaler.ApplyScaling(__instance);
    }

    // ---------------------------------------------------------------------------
    // Screen resolution — force ultrawide on every SetResolution call so the game
    // can't silently revert to 16:9 after startup.
    // ---------------------------------------------------------------------------

    [HarmonyPatch(typeof(Screen), nameof(Screen.SetResolution), typeof(int), typeof(int), typeof(FullScreenMode))]
    [HarmonyPrefix]
    static void Screen_SetResolution_Prefix(ref int width, ref int height, ref FullScreenMode fullscreenMode)
    {
        // Only intercept when the user has explicitly set target dimensions in config.
        // When both are 0 (default), pass through and let the game use its own resolution setting.
        int targetW = Plugin.TargetWidth.Value;
        int targetH = Plugin.TargetHeight.Value;

        if (targetW <= 0 || targetH <= 0) return;

        if (width != targetW || height != targetH)
        {
            Plugin.Log.LogDebug($"Intercepted SetResolution({width}x{height}) → forcing {targetW}x{targetH}");
            width          = targetW;
            height         = targetH;
            fullscreenMode = FullScreenMode.FullScreenWindow;
        }
    }

}
