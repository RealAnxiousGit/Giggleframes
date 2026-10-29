using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace GigglelandPerf
{
    /// Frame-rate readout in the top-right corner (the game's objective text owns the top-left), in the game's own bold menu font with a dark outline.
    /// Green at 60 FPS and up, yellow from 30 to 59 (starting to stutter), red under 30 (unplayable).
    static class FpsCounter
    {
        static readonly Color Green = new(0.30f, 0.95f, 0.35f), Yellow = new(1f, 0.85f, 0.15f), Red = new(1f, 0.25f, 0.2f);

        static GameObject root;
        static TextMeshProUGUI text;
        static bool fontMissing;
        static int frames;
        static float time;
        static float fps, ms;

        public static bool On => Settings.FpsCounter.I == 1;

        public static Color ColorFor(float f) => f >= 60f ? Green : f >= 30f ? Yellow : Red;

        public static void Tick()
        {
            if (!On)
            {
                if (root != null && root.activeSelf) root.SetActive(false);
                frames = 0; time = 0f; fps = 0f;
                return;
            }
            frames++;
            time += Time.unscaledDeltaTime;
            if (time >= 0.5f)
            {
                fps = frames / time;
                ms = 1000f * time / frames;
                frames = 0;
                time = 0f;
                if (text != null || Build()) Show();
            }
        }

        static void Show()
        {
            if (!root.activeSelf) root.SetActive(true);
            text.color = ColorFor(fps);
            text.text = $"{fps:0} FPS  <size=70%>{ms:0.0} ms</size>";
        }

        static bool Build()
        {
            if (fontMissing) return false;
            TMP_FontAsset font = null;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_FontAsset>()))
            {
                var f = o.TryCast<TMP_FontAsset>();
                if (f != null && f.name.StartsWith("Baloo2-ExtraBold", StringComparison.OrdinalIgnoreCase)) { font = f; break; }
            }
            if (font == null)
            {
                // The menu font loads with the main menu; until then OnGUI draws a fallback.
                if (Time.unscaledTime > 120f) fontMissing = true;
                return false;
            }

            root = new GameObject("GiggleframesFps");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = root.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;

            var go = new GameObject("Text");
            go.transform.SetParent(root.transform, false);
            text = go.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = 34;
            text.fontStyle = FontStyles.Bold;
            text.richText = true;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.TopRight;
            // An instance material, so the outline doesn't spread to the game's own text.
            var mat = text.fontMaterial;
            mat.EnableKeyword("OUTLINE_ON");
            text.outlineWidth = 0.28f;
            text.outlineColor = new Color32(0, 0, 0, 230);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-18f, -10f);
            rt.sizeDelta = new Vector2(600f, 60f);
            return true;
        }

        // Before the menu font has loaded (the first seconds of a launch), draw with IMGUI instead.
        static GUIStyle style, shadow;

        public static void OnGUI()
        {
            if (!On || text != null || fps <= 0f) return;
            style ??= new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold };
            shadow ??= new GUIStyle(style) { normal = { textColor = Color.black } };
            style.normal.textColor = ColorFor(fps);
            string s = $"{fps:0} FPS  {ms:0.0} ms";
            style.alignment = shadow.alignment = TextAnchor.UpperRight;
            GUI.Label(new Rect(Screen.width - 412, 10, 400, 30), s, shadow);
            GUI.Label(new Rect(Screen.width - 414, 8, 400, 30), s, style);
        }
    }
}
