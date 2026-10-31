using CargoJam.Game.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.Presentation.Fx
{
    /// <summary>
    /// Screen-space FX on its own UIDocument: danger, win and fail vignettes, combo and delivery
    /// callouts, the truck-full check badge and the arrival card. Design v3 type (Nunito Black, soft
    /// shadow, no outline) and palette tokens. Every element ignores picking so it never blocks input.
    /// Animated from one Update with no per-frame allocations.
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

        static readonly Color DangerColor = UiPalette.Danger;
        static readonly Color FailColor = UiPalette.Ink;
        /// <summary>The danger vignette never goes past this, so it reads as a warning rather than an alarm.</summary>
        const float DangerPeak = 0.7f;

        sealed class Callout
        {
            public Label Label;
            public float Start = -1f;
            public Vector2 Anchor;
            public float Tilt;
            public bool Celebrate;
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

        ArrivalCard arrival;
        int dangerLevel;
        float dangerEnvelope;
        float flashStart = -1f, flashDuration = 1f, flashStrength = 1f;
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

        /// <summary>
        /// A quick full-edge flash in <paramref name="color"/> that fades over <paramref name="duration"/>,
        /// peaking at <paramref name="strength"/> (0..1) of the vignette's full opacity.
        /// </summary>
        public void Flash(Color color, float duration, float strength = 1f)
        {
            if (!EnsureBuilt()) return;
            flash.SetColor(color);
            flashStart = Time.time;
            flashDuration = Mathf.Max(0.05f, duration);
            flashStrength = Mathf.Clamp01(strength);
        }

        /// <summary>The fail mood: a greyish tint and dark edges that breathe until cleared.</summary>
        public void SetFailMood(bool on) => failStart = on ? Time.time : -1f;

        /// <summary>
        /// Floating callout text at a world point. A <paramref name="celebrate"/> callout (combos) pops
        /// with a little overshoot and tilt; otherwise it fades up calmly (delivery ladder, notes).
        /// </summary>
        public void ShowCallout(string text, Color fill, float fontSize, Vector3 world, bool celebrate = true)
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
            c.Celebrate = celebrate;
            c.Tilt = celebrate ? Random.Range(-4f, 4f) : 0f;
            label.style.display = DisplayStyle.Flex;
            label.BringToFront();
            Place(label, anchor, 0f, 0f, 0f, 0f);
        }

        /// <summary>The arrival reveal's destination card ("Arrived" over the name, plus the stamp), starting now.</summary>
        public void ShowArrival(string destination)
        {
            if (!EnsureBuilt()) return;
            arrival.Show(destination);
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

        /// <summary>Hides any floating callouts (the level just ended; they'd read as praise for a fail).</summary>
        public void ClearCallouts()
        {
            if (callouts == null) return;
            foreach (var c in callouts)
            {
                c.Start = -1f;
                c.Label.style.display = DisplayStyle.None;
            }
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
            arrival.Hide();
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
                s.unityTextAlign = TextAnchor.MiddleCenter;
                s.whiteSpace = WhiteSpace.NoWrap;
                FxType.Display(label);
                s.display = DisplayStyle.None;
                root.Add(label);
                callouts[i] = new Callout { Label = label };
            }
            arrival = new ArrivalCard();
            root.Add(arrival);
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
            // Calm v3 pulse: a slow breath at one slot left, a firmer (still unhurried) one when full.
            float pulse = dangerLevel >= 2
                ? 0.78f + 0.22f * Mathf.Sin(now * Mathf.PI * 2f * 1.5f)
                : 0.42f + 0.12f * Mathf.Sin(now * Mathf.PI * 2f * 0.8f);
            danger.SetIntensity(DangerPeak * dangerEnvelope * pulse * (1f - failEnvelope));

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
                    flash.SetIntensity(flashStrength * (1f - Ease.OutCubic(k)));
                }
            }

            float failTarget = failStart >= 0f ? 1f : 0f;
            failEnvelope = Mathf.MoveTowards(failEnvelope, failTarget, dt * 2.5f);
            float breathe = 0.85f + 0.15f * Mathf.Sin((now - failStart) * Mathf.PI * 2f * 0.6f);
            fail.SetIntensity(failEnvelope * (failStart >= 0f ? breathe : 1f));

            for (int i = 0; i < CalloutCount; i++) StepCallout(callouts[i], now);
            for (int i = 0; i < BadgeCount; i++) StepBadge(badges[i], now);
            arrival.Step(now);
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
            float scale, alpha;
            if (c.Celebrate)
            {
                // A small pop (combos are a reward moment), settling to rest by a third of the way.
                scale = t < 0.16f
                    ? 1.1f * Ease.OutBack(t / 0.16f)
                    : Mathf.Lerp(1.1f, 1f, Mathf.Clamp01((t - 0.16f) / 0.2f));
                alpha = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            }
            else
            {
                float k = Mathf.Clamp01(t / 0.2f);
                scale = 0.94f + 0.06f * (1f - Mathf.Pow(1f - k, 5f));
                alpha = t < 0.2f ? k : t < 0.72f ? 1f : 1f - (t - 0.72f) / 0.28f;
            }
            float rise = (c.Celebrate ? -110f : -70f) * Ease.OutCubic(t);
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
            position = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screen.x, ScreenInfo.Height - screen.y));
            return true;
        }
    }
}
