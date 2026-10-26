using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// Screen-space FX on its own UIDocument: danger, win and fail vignettes, combo callouts and the
    /// truck-full check badge. Every element ignores picking so it never blocks input. Animated from
    /// one Update with no per-frame allocations.
    /// </summary>
    public sealed class FxOverlay : MonoBehaviour
    {
        /// <summary>
        /// Draw order against other documents on the shared panel. Below the UI stream's documents
        /// (default 0) so panels and popups cover the vignette and callouts.
        /// </summary>
        public const float SortingOrder = -5f;

        const int CalloutCount = 3;
        const int BadgeCount = 3;
        const float CalloutLife = 1.15f;
        const float BadgeLife = 0.95f;
        const float BadgeSize = 120f;

        static readonly Color DangerColor = new Color(0.95f, 0.33f, 0.29f);
        static readonly Color FailColor = new Color(0.17f, 0.17f, 0.24f);
        static readonly Color Outline = new Color(0.23f, 0.18f, 0.16f);

        sealed class Callout
        {
            public Label Label;
            public float Start = -1f;
            public Vector2 Anchor;
            public float Tilt;
        }

        sealed class Badge
        {
            public CheckBadge Element;
            public float Start = -1f;
            public Transform Follow;
            public Vector3 Offset;
        }

        UIDocument document;
        IPanel panel;
        Camera cam;

        EdgeVignette danger, flash, fail;
        readonly Callout[] callouts = new Callout[CalloutCount];
        readonly Badge[] badges = new Badge[BadgeCount];
        int nextCallout, nextBadge;

        int dangerLevel;
        float dangerEnvelope;
        float flashStart = -1f, flashDuration = 1f;
        float failStart = -1f;
        float failEnvelope;

        /// <summary>Creates the overlay document on a new child of <paramref name="parent"/>.</summary>
        public static FxOverlay Create(Transform parent)
        {
            var go = new GameObject("FX Overlay");
            go.SetActive(false);
            go.transform.SetParent(parent, false);
            var overlay = go.AddComponent<FxOverlay>();
            overlay.document = go.AddComponent<UIDocument>();
            var settings = Resources.Load<PanelSettings>("UI/PanelSettings");
            if (settings == null)
            {
                Debug.LogWarning("FxOverlay: Resources/UI/PanelSettings missing; screen FX disabled.");
            }
            else
            {
                overlay.document.panelSettings = settings;
                overlay.document.sortingOrder = SortingOrder;
            }
            go.SetActive(true);
            return overlay;
        }

        /// <summary>The camera used to place world-anchored callouts and badges.</summary>
        public Camera Camera
        {
            get => cam;
            set => cam = value;
        }

        // ---- API ----------------------------------------------------------------------------------

        /// <summary>Dock danger: 0 off, 1 one slot left (gentle pulse), 2 full (strong, fast pulse).</summary>
        public void SetDanger(int level) => dangerLevel = Mathf.Clamp(level, 0, 2);

        /// <summary>A quick full-edge flash in <paramref name="color"/> that fades over <paramref name="duration"/>.</summary>
        public void Flash(Color color, float duration)
        {
            if (!EnsureBuilt()) return;
            flash.SetColor(color);
            flashStart = Time.time;
            flashDuration = Mathf.Max(0.05f, duration);
        }

        /// <summary>The fail mood: a greyish tint and dark edges that breathe until cleared.</summary>
        public void SetFailMood(bool on) => failStart = on ? Time.time : -1f;

        /// <summary>Floating callout text at a world point.</summary>
        public void ShowCallout(string text, Color fill, float fontSize, Vector3 world)
        {
            if (!EnsureBuilt() || !TryWorldToPanel(world, out var anchor)) return;
            var c = callouts[nextCallout];
            nextCallout = (nextCallout + 1) % CalloutCount;
            var label = c.Label;
            label.text = text;
            label.style.color = fill;
            label.style.fontSize = fontSize;
            c.Anchor = anchor;
            c.Start = Time.time;
            c.Tilt = Random.Range(-7f, 7f);
            label.style.display = DisplayStyle.Flex;
            label.BringToFront();
            Place(label, anchor, 0f, 0f, 0f, 0f);
        }

        /// <summary>A check badge that pops above <paramref name="follow"/> and rides along with it.</summary>
        public void ShowCheck(Transform follow, Vector3 offset)
        {
            if (!EnsureBuilt() || follow == null) return;
            var b = badges[nextBadge];
            nextBadge = (nextBadge + 1) % BadgeCount;
            b.Follow = follow;
            b.Offset = offset;
            b.Start = Time.time;
            b.Element.style.display = DisplayStyle.Flex;
            b.Element.style.scale = new Scale(Vector2.zero);
        }

        /// <summary>Hides everything immediately (level teardown).</summary>
        public void ClearAll()
        {
            dangerLevel = 0;
            dangerEnvelope = 0f;
            flashStart = -1f;
            failStart = -1f;
            failEnvelope = 0f;
            if (danger == null) return;
            danger.SetIntensity(0f);
            flash.SetIntensity(0f);
            fail.SetIntensity(0f);
            foreach (var c in callouts)
            {
                c.Start = -1f;
                c.Label.style.display = DisplayStyle.None;
            }
            foreach (var b in badges)
            {
                b.Start = -1f;
                b.Follow = null;
                b.Element.style.display = DisplayStyle.None;
            }
        }

        // ---- Build --------------------------------------------------------------------------------

        bool EnsureBuilt()
        {
            if (danger != null) return panel != null;
            if (document == null || document.panelSettings == null) return false;
            var root = document.rootVisualElement;
            if (root == null) return false;
            panel = root.panel;
            if (panel == null) return false;

            EdgeVignette.Fill(root);

            fail = new EdgeVignette(FailColor, tintAlpha: 0.22f);
            danger = new EdgeVignette(DangerColor);
            flash = new EdgeVignette(Color.white);
            root.Add(fail);
            root.Add(danger);
            root.Add(flash);

            for (int i = 0; i < BadgeCount; i++)
            {
                badges[i] = new Badge { Element = new CheckBadge(BadgeSize) };
                root.Add(badges[i].Element);
            }
            for (int i = 0; i < CalloutCount; i++)
            {
                var label = new Label { pickingMode = PickingMode.Ignore };
                var s = label.style;
                s.position = Position.Absolute;
                s.unityFontStyleAndWeight = FontStyle.Bold;
                s.unityTextAlign = TextAnchor.MiddleCenter;
                s.whiteSpace = WhiteSpace.NoWrap;
                s.unityTextOutlineColor = Outline;
                s.unityTextOutlineWidth = 3f;
                s.textShadow = new TextShadow { offset = new Vector2(0f, 6f), blurRadius = 0f, color = new Color(0f, 0f, 0f, 0.35f) };
                s.display = DisplayStyle.None;
                root.Add(label);
                callouts[i] = new Callout { Label = label };
            }
            return true;
        }

        // ---- Animation ----------------------------------------------------------------------------

        void Update()
        {
            if (!EnsureBuilt()) return;
            float now = Time.time;
            float dt = Time.deltaTime;

            // Danger: ease the envelope toward the level, then pulse on top of it.
            float target = dangerLevel == 0 ? 0f : 1f;
            dangerEnvelope = Mathf.MoveTowards(dangerEnvelope, target, dt * (target > dangerEnvelope ? 4f : 2.5f));
            float pulse = dangerLevel >= 2
                ? 0.72f + 0.28f * Mathf.Sin(now * Mathf.PI * 2f * 2.4f)
                : 0.42f + 0.18f * Mathf.Sin(now * Mathf.PI * 2f * 1.2f);
            danger.SetIntensity(dangerEnvelope * pulse * (1f - failEnvelope));

            if (flashStart >= 0f)
            {
                float k = (now - flashStart) / flashDuration;
                if (k >= 1f)
                {
                    flashStart = -1f;
                    flash.SetIntensity(0f);
                }
                else
                {
                    flash.SetIntensity(1f - Ease.OutCubic(k));
                }
            }

            float failTarget = failStart >= 0f ? 1f : 0f;
            failEnvelope = Mathf.MoveTowards(failEnvelope, failTarget, dt * 2.5f);
            float breathe = 0.85f + 0.15f * Mathf.Sin((now - failStart) * Mathf.PI * 2f * 0.6f);
            fail.SetIntensity(failEnvelope * (failStart >= 0f ? breathe : 1f));

            for (int i = 0; i < CalloutCount; i++) StepCallout(callouts[i], now);
            for (int i = 0; i < BadgeCount; i++) StepBadge(badges[i], now);
        }

        void StepCallout(Callout c, float now)
        {
            if (c.Start < 0f) return;
            float t = (now - c.Start) / CalloutLife;
            if (t >= 1f)
            {
                c.Start = -1f;
                c.Label.style.display = DisplayStyle.None;
                return;
            }
            float scale = t < 0.16f
                ? 1.25f * Ease.OutBack(t / 0.16f)
                : Mathf.Lerp(1.25f, 1f, Mathf.Clamp01((t - 0.16f) / 0.2f));
            float rise = -110f * Ease.OutCubic(t);
            float alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            Place(c.Label, c.Anchor, rise, scale, alpha, c.Tilt * (1f - t));
        }

        void StepBadge(Badge b, float now)
        {
            if (b.Start < 0f) return;
            float t = (now - b.Start) / BadgeLife;
            if (t >= 1f || b.Follow == null || !b.Follow.gameObject.activeInHierarchy)
            {
                b.Start = -1f;
                b.Follow = null;
                b.Element.style.display = DisplayStyle.None;
                return;
            }
            if (!TryWorldToPanel(b.Follow.position + b.Offset, out var p))
            {
                b.Element.style.opacity = 0f;
                return;
            }
            float scale = t < 0.22f ? Ease.OutBack(t / 0.22f) : 1f;
            float alpha = t < 0.75f ? 1f : 1f - (t - 0.75f) / 0.25f;
            var s = b.Element.style;
            s.left = p.x;
            s.top = p.y - 30f * Ease.OutCubic(t);
            s.scale = new Scale(new Vector2(scale, scale));
            s.opacity = alpha;
        }

        static void Place(VisualElement e, Vector2 anchor, float rise, float scale, float alpha, float tilt)
        {
            var s = e.style;
            s.left = anchor.x;
            s.top = anchor.y + rise;
            s.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            s.scale = new Scale(new Vector2(scale, scale));
            s.rotate = new Rotate(Angle.Degrees(tilt));
            s.opacity = alpha;
        }

        bool TryWorldToPanel(Vector3 world, out Vector2 position)
        {
            position = default;
            if (cam == null || panel == null) return false;
            var screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f) return false;
            // Screen space is y-up; ScreenToPanel expects y-down from the top-left.
            position = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, Screen.height - screen.y));
            return true;
        }
    }
}
