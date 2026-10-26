#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.Debugging
{
    /// <summary>
    /// Renders the running game at an iPhone portrait size offscreen (camera and UI into their own
    /// textures, with the layout told the screen is a phone) and saves composited screenshots. Lets
    /// tools check real phone layouts while the editor's Game view is any shape, or hidden.
    /// Driven from the Unity CLI: <see cref="Begin"/>, wait a frame or more, <see cref="Save"/>, <see cref="End"/>.
    /// </summary>
    public static class PortraitPreview
    {
        public const int Width = 1170;
        public const int Height = 2532;

        // iPhone 14-class insets at 3x: 47 pt status area on top, 34 pt home indicator at the bottom.
        static readonly Rect SafeArea = new Rect(0, 102, Width, Height - 141 - 102);

        static RenderTexture cameraTexture, uiTexture;
        static Camera camera;
        static readonly List<(PanelSettings settings, RenderTexture target, bool clear, Color clearColor)> panels =
            new List<(PanelSettings, RenderTexture, bool, Color)>();

        public static bool Active { get; private set; }

        public static void Begin()
        {
            if (Active) return;
            Active = true;
            ScreenInfo.Override(Width, Height, SafeArea);

            if (cameraTexture == null) cameraTexture = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            if (uiTexture == null) uiTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);

            camera = Camera.main;
            if (camera != null) camera.targetTexture = cameraTexture;

            panels.Clear();
            foreach (var ps in Resources.FindObjectsOfTypeAll<PanelSettings>())
            {
                panels.Add((ps, ps.targetTexture, ps.clearColor, ps.colorClearValue));
                ps.targetTexture = uiTexture;
                ps.clearColor = true;
                ps.colorClearValue = Color.clear;
            }
        }

        /// <summary>Composites the last rendered camera and UI frames into a PNG; returns the path written.</summary>
        public static string Save(string path)
        {
            if (!Active) return "preview not active";
            var scene = Read(cameraTexture);
            var ui = Read(uiTexture);
            var a = scene.GetPixels32();
            var b = ui.GetPixels32();
            for (int i = 0; i < a.Length; i++)
            {
                // The UI is drawn over a transparent clear, so its color is already alpha-weighted.
                float k = 1f - b[i].a / 255f;
                a[i].r = (byte)Mathf.Min(255, b[i].r + a[i].r * k);
                a[i].g = (byte)Mathf.Min(255, b[i].g + a[i].g * k);
                a[i].b = (byte)Mathf.Min(255, b[i].b + a[i].b * k);
                a[i].a = 255;
            }
            scene.SetPixels32(a);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, scene.EncodeToPNG());
            Object.Destroy(scene);
            Object.Destroy(ui);
            return Path.GetFullPath(path);
        }

        public static void End()
        {
            if (!Active) return;
            Active = false;
            ScreenInfo.ClearOverride();
            if (camera != null) camera.targetTexture = null;
            foreach (var (settings, target, clear, clearColor) in panels)
            {
                if (settings == null) continue;
                settings.targetTexture = target;
                settings.clearColor = clear;
                settings.colorClearValue = clearColor;
            }
            panels.Clear();
        }

        /// <summary>Visible candy buttons, as "label [classes]" lines, for tools to see what can be tapped.</summary>
        public static string Buttons()
        {
            var lines = new List<string>();
            foreach (var button in VisibleButtons())
                lines.Add($"{button.Text ?? "(icon)"} [{string.Join(" ", button.GetClasses())}]");
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Taps the last-drawn visible candy button whose label or class list contains <paramref name="query"/>
        /// (topmost popups are drawn last). Returns what was tapped, or "none".
        /// </summary>
        public static string Tap(string query)
        {
            UI.CandyButton hit = null;
            foreach (var button in VisibleButtons())
            {
                bool match = (button.Text != null && button.Text.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0);
                foreach (var c in button.GetClasses())
                    match |= c.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (match) hit = button;
            }
            if (hit == null) return "none";
            hit.SimulateTap();
            return hit.Text ?? string.Join(" ", hit.GetClasses());
        }

        static IEnumerable<UI.CandyButton> VisibleButtons()
        {
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                if (doc.rootVisualElement == null) continue;
                foreach (var button in doc.rootVisualElement.Query<UI.CandyButton>().ToList())
                    if (IsShown(button)) yield return button;
            }
        }

        static bool IsShown(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent)
            {
                if (v.resolvedStyle.display == DisplayStyle.None || v.resolvedStyle.visibility == Visibility.Hidden) return false;
                if (v.resolvedStyle.opacity <= 0.01f) return false;
            }
            return true;
        }

        static Texture2D Read(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }
    }
}
#endif
