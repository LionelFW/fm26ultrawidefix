using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FM26UltrawideFix;

/// <summary>
/// Persistent MonoBehaviour that:
///   1. Forces the display resolution to the actual physical resolution on startup.
///   2. Re-applies panel scaling every frame so FM26 cannot revert it between scenes.
///   3. Optionally dumps scene hierarchy on load for diagnostics.
/// </summary>
public class PanelScaler : MonoBehaviour
{
    // IL2CPP requires this constructor
    public PanelScaler(nint ptr) : base(ptr) { }

    private bool _resolutionApplied;
    private int  _cameraPollFrame;
    private int  _uiExpandFrame;

    void Start()
    {
        ForceResolution();

        if (Plugin.DiagnosticDump.Value)
            SceneManager.sceneLoaded += new Action<Scene, LoadSceneMode>(OnSceneLoaded);
    }

    void Update()
    {
        // Re-apply until confirmed; FM26 may revert resolution on scene transitions.
        if (!_resolutionApplied)
            ForceResolution();

        // Camera.OnEnable is not patchable in IL2Cpp — poll instead.
        if (Plugin.PatchMatchCamera.Value)
            FixCameraAspects();

        // Expand UIDocument roots to fill the extra horizontal logical space.
        // Runs periodically because FM26 may reset styles on scene transitions.
        if (++_uiExpandFrame >= 30)
        {
            _uiExpandFrame = 0;
            ExpandUIDocumentRoots();
        }

        if (Plugin.DiagnosticDump.Value)
            RunDiagnostics();
    }

    // ------------------------------------------------------------------

    private void ForceResolution()
    {
        // Only override resolution when the user has explicitly configured target dimensions.
        // When both are 0 (default), respect whatever resolution the game is running at.
        int targetW = Plugin.TargetWidth.Value;
        int targetH = Plugin.TargetHeight.Value;

        if (targetW <= 0 || targetH <= 0)
        {
            _resolutionApplied = true;
            return;
        }

        if (Screen.width == targetW && Screen.height == targetH)
        {
            _resolutionApplied = true;
            return;
        }

        Plugin.Log.LogInfo($"Setting resolution {targetW}x{targetH} (was {Screen.width}x{Screen.height})");
        Screen.SetResolution(targetW, targetH, FullScreenMode.FullScreenWindow);
        _resolutionApplied = true;
    }

    // ------------------------------------------------------------------
    // Camera aspect — polled because Camera.OnEnable is not patchable in IL2Cpp.
    // Runs every 30 frames (~0.5 s at 60 fps) to catch cameras added after scene load.
    // ------------------------------------------------------------------

    private void FixCameraAspects()
    {
        if (++_cameraPollFrame < 30) return;
        _cameraPollFrame = 0;

        float targetAspect = (float)Screen.width / Screen.height;

        foreach (var cam in Camera.allCameras)
        {
            if (cam == null) continue;
            if (Math.Abs(cam.aspect - targetAspect) < 0.005f) continue;

            // Preserve vertical FOV for perspective cameras only
            if (!cam.orthographic && cam.aspect > 0f)
            {
                float origFovRad = cam.fieldOfView * Mathf.Deg2Rad;
                float newFovRad  = 2f * (float)Math.Atan(Math.Tan(origFovRad / 2f) * (cam.aspect / targetAspect));
                cam.fieldOfView  = newFovRad * Mathf.Rad2Deg;
            }

            cam.aspect = targetAspect;
            Plugin.Log.LogDebug($"Camera '{cam.name}' aspect → {targetAspect:F4}");
        }
    }

    // ------------------------------------------------------------------
    // Scaling logic — called from UltrawidePatches.PanelSettings postfix.
    // Public/static so the Harmony patch can invoke it without a MonoBehaviour ref.
    // ------------------------------------------------------------------

    // Tracks the last height ratio applied so ExpandUIDocumentRoots can compute thresholds.
    private static float _lastHeightRatio = 1f;

    internal static void ApplyScaling(PanelSettings settings)
    {
        if (settings == null) return;

        float screenW = Screen.width;
        float screenH = Screen.height;
        var   refRes  = settings.referenceResolution;

        if (refRes.x <= 0f || refRes.y <= 0f) return;

        float screenAspect = screenW / screenH;
        float refAspect    = (float)refRes.x / refRes.y;

        if (screenAspect <= refAspect + 0.01f) return;

        float heightRatio = screenH / refRes.y;
        _lastHeightRatio  = heightRatio;

        // C: ScaleWithScreenSize + height-only match lets Unity compute scale automatically
        // (scale = screenH / referenceResolution.y = heightRatio). The panel canvas is
        // screenW/heightRatio × refRes.y logical pixels — correct for any ultrawide ratio.
        settings.scaleMode       = PanelScaleMode.ScaleWithScreenSize;
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match           = 1f;
    }

    // Named PanelManager slots that are full-canvas-width layout containers.
    // ModalDialog, Tooltip, and Card are intentionally absent — they are floating
    // elements that must keep their intrinsic sizes.
    private static readonly string[] s_fullWidthSlots =
    {
        "Background", "Menu", "Report", "Ribbon",
        "Overlay", "LoadingScreen", "Watermark", "Version"
    };

    // A: Expand only the known full-width named slots in each UIDocument.
    // Runs every ~0.5 s so styles survive scene transitions.
    private static void ExpandUIDocumentRoots()
    {
        if ((float)Screen.width / Screen.height < 1.9f) return;

        try
        {
            var docs = GameObject.FindObjectsOfType<UIDocument>();
            foreach (var doc in docs)
            {
                if (doc == null) continue;
                var root = doc.rootVisualElement;
                if (root == null) continue;

                ForceFullWidth(root);
                root.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));

                foreach (var slotName in s_fullWidthSlots)
                {
                    var slot = root.Q(slotName, (string)null);
                    if (slot == null) continue;
                    ForceFullWidth(slot);
                    slot.style.height = new StyleLength(new Length(100f, LengthUnit.Percent));
                    for (int i = 0; i < slot.childCount; i++)
                        ExpandWithinSlot(slot[i], slot, 0);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"[PanelScaler] ExpandUIDocumentRoots: {ex.Message}");
        }
    }

    // Recursively expands layout containers within a known full-width slot.
    // Only expands elements spanning ≥ 80 % of their parent — page/section wrappers.
    // Narrow flex children (tabs, cards, buttons) are left at their natural size.
    private static void ExpandWithinSlot(VisualElement ve, VisualElement parent, int depth)
    {
        if (ve == null || depth > 20) return;

        ve.style.maxWidth = StyleKeyword.None;

        float myW     = TryGetLayoutWidth(ve);
        float parentW = TryGetLayoutWidth(parent);

        if (myW > 0f && parentW > 0f && myW >= parentW * 0.8f)
        {
            ve.style.width       = new StyleLength(new Length(100f, LengthUnit.Percent));
            ve.style.marginLeft  = new StyleLength(new Length(0f, LengthUnit.Pixel));
            ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
        }

        for (int i = 0; i < ve.childCount; i++)
            ExpandWithinSlot(ve[i], ve, depth + 1);
    }

    // Returns the element's layout width in logical pixels, using multiple fallback
    // strategies because IL2CPP can fail on interface-dispatch properties.
    private static float TryGetLayoutWidth(VisualElement ve)
    {
        // ve.layout is a plain Rect struct — most reliable path in IL2CPP.
        try
        {
            float lw = ve.layout.width;
            if (!float.IsNaN(lw) && lw > 0f) return lw;
        }
        catch { }

        // resolvedStyle is an interface — may throw in IL2CPP, but try as fallback.
        try
        {
            float rw = ve.resolvedStyle.width;
            if (!float.IsNaN(rw) && rw > 0f) return rw;
        }
        catch { }

        // Last resort: inline style only (misses USS-defined widths).
        try
        {
            var ws = ve.style.width;
            if (ws.keyword == StyleKeyword.Undefined
                && ws.value.unit == LengthUnit.Pixel
                && ws.value.value > 0f)
                return ws.value.value;
        }
        catch { }

        return -1f;
    }

    // Expands a VisualElement to fill available width and removes centering margins.
    private static void ForceFullWidth(VisualElement ve)
    {
        ve.style.width       = new StyleLength(new Length(100f, LengthUnit.Percent));
        ve.style.maxWidth    = StyleKeyword.None;
        ve.style.marginLeft  = new StyleLength(new Length(0f, LengthUnit.Pixel));
        ve.style.marginRight = new StyleLength(new Length(0f, LengthUnit.Pixel));
    }


    // ------------------------------------------------------------------
    // Diagnostics — enable via config [Debug] DiagnosticDump=true
    // ------------------------------------------------------------------

    private int _veDumpFrame;

    private void RunDiagnostics()
    {
        // Dump VE hierarchy once every ~5 s (300 frames) so the log stays readable.
        if (++_veDumpFrame < 300) return;
        _veDumpFrame = 0;

        Plugin.Log.LogInfo("=== VE hierarchy dump (UIDocuments) ===");
        try
        {
            var docs = GameObject.FindObjectsOfType<UIDocument>();
            Plugin.Log.LogInfo($"  UIDocument count: {docs.Length}");
            foreach (var doc in docs)
            {
                if (doc == null) continue;
                Plugin.Log.LogInfo($"  [UIDocument] GO={doc.gameObject.name}");
                var root = doc.rootVisualElement;
                if (root != null)
                    DumpVE(root, 0, maxDepth: 15);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogInfo($"  VE dump error: {ex.Message}");
        }
        Plugin.Log.LogInfo("=== end VE dump ===");
    }

    private static void DumpVE(VisualElement ve, int depth, int maxDepth)
    {
        if (ve == null || depth > maxDepth) return;

        string indent = new string(' ', depth * 2);
        float rw = -1f, lw = -1f;
        try { rw = ve.resolvedStyle.width; }  catch { }
        try { lw = ve.layout.width; }         catch { }

        var inlineW = "(none)";
        try
        {
            var ws = ve.style.width;
            inlineW = ws.keyword == StyleKeyword.Undefined
                ? $"{ws.value.value}{ws.value.unit}"
                : ws.keyword.ToString();
        }
        catch { }

        Plugin.Log.LogInfo(
            $"{indent}[{depth}] name={ve.name ?? "(null)"} " +
            $"children={ve.childCount} " +
            $"resolved={rw:F0} layout={lw:F0} inline={inlineW}");

        for (int i = 0; i < ve.childCount; i++)
            DumpVE(ve[i], depth + 1, maxDepth);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Plugin.Log.LogInfo($"=== Scene loaded: '{scene.name}' (mode={mode}) ===");
        foreach (var go in scene.GetRootGameObjects())
        {
            var components = go.GetComponents<Component>();
            var sb = new System.Text.StringBuilder();
            foreach (var c in components)
            {
                if (sb.Length > 0) sb.Append(", ");
                try { sb.Append(c.GetType().Name); }
                catch { sb.Append("?"); }
            }
            Plugin.Log.LogInfo($"  GO: {go.name}  |  {sb}");
        }
    }
}
