using System;
using CargoJam.Game.Feel;
using CargoJam.Game.Meta;
using UnityEngine;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// Greybox HUD drawn with IMGUI: level label, restart, sound and haptics toggles, the
    /// end-of-level card, and (in development builds) level skip buttons and an FPS readout.
    /// Replaced by real UI in the art pass.
    /// </summary>
    public sealed class Hud : MonoBehaviour, IHudLayout
    {
        const float VirtualHeight = 1600f;
        const float BarHeight = 210f;

        enum Card { None, Won, Failed }

        Card card;
        float cardAt;
        GUIStyle label, button, title, cardStyle, small, toggleOn, toggleOff;
        float fps = 60f;

        public string LevelLabel { get; set; }
        public AudioService Audio { get; set; }
        public event Action Restart;
        public event Action Next;
        public event Action Previous;

        /// <summary>The safe area as a 0..1 viewport rect, minus the top bar, for the camera to fit into.</summary>
        public Rect GameViewport
        {
            get
            {
                var safe = Screen.safeArea;
                float bar = BarHeight * Scale;
                float top = (safe.yMax - bar) / Screen.height;
                float bottom = safe.yMin / Screen.height + 0.02f;
                float left = safe.xMin / Screen.width + 0.03f;
                float right = safe.xMax / Screen.width - 0.03f;
                return Rect.MinMaxRect(left, bottom, right, top);
            }
        }

        float Scale => Screen.height / VirtualHeight;

        public void ShowWon(float at) => Show(Card.Won, at);
        public void ShowFailed(float at) => Show(Card.Failed, at);
        public void HideCard() => card = Card.None;

        void Show(Card value, float at)
        {
            card = value;
            cardAt = at;
        }

        /// <summary>True when a press at this screen position (origin bottom-left) belongs to the HUD.</summary>
        public bool Captures(Vector2 screenPosition)
        {
            if (card != Card.None) return true;
            float barBottom = Screen.safeArea.yMax - BarHeight * Scale;
            return screenPosition.y >= barBottom;
        }

        void Update()
        {
            if (Time.unscaledDeltaTime > 0f) fps = Mathf.Lerp(fps, 1f / Time.unscaledDeltaTime, 0.05f);
        }

        bool Button(Rect r, string text, GUIStyle style = null)
        {
            if (!GUI.Button(r, text, style ?? button)) return false;
            Audio?.Play(Sfx.Click);
            return true;
        }

        void OnGUI()
        {
            EnsureStyles();
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));

            float width = Screen.width / s;
            float top = (Screen.height - Screen.safeArea.yMax) / s;
            float pad = 24f;

            GUI.Label(new Rect(pad, top + 20f, 400f, 90f), LevelLabel, label);
            if (Button(new Rect(width - pad - 150f, top + 20f, 150f, 90f), "Restart")) Restart?.Invoke();

            float row2 = top + 125f;
            bool sound = Settings.SoundOn, haptics = Settings.HapticsOn;
            if (GUI.Button(new Rect(pad, row2, 170f, 64f), sound ? "Sound on" : "Sound off", sound ? toggleOn : toggleOff))
            {
                Settings.SoundOn = !sound;
                Audio?.Play(Sfx.Click);
            }
            if (GUI.Button(new Rect(pad + 185f, row2, 170f, 64f), haptics ? "Haptics on" : "Haptics off", haptics ? toggleOn : toggleOff))
            {
                Settings.HapticsOn = !haptics;
                if (!haptics) Haptics.Play(Haptic.Tap);
                Audio?.Play(Sfx.Click);
            }

            if (Debug.isDebugBuild)
            {
                if (Button(new Rect(width - pad - 150f - 20f - 90f * 2 - 10f, top + 20f, 90f, 90f), "<")) Previous?.Invoke();
                if (Button(new Rect(width - pad - 150f - 20f - 90f, top + 20f, 90f, 90f), ">")) Next?.Invoke();
                GUI.Label(new Rect(width - pad - 200f, row2, 200f, 64f), $"{Mathf.RoundToInt(fps)} fps", small);
            }

            if (card == Card.None || Time.time < cardAt) return;

            float h = Screen.height / s;
            var box = new Rect(width * 0.5f - 300f, h * 0.5f - 220f, 600f, 440f);
            GUI.Box(box, GUIContent.none, cardStyle);
            bool won = card == Card.Won;
            GUI.Label(new Rect(box.x, box.y + 50f, box.width, 120f), won ? "Level complete!" : "Out of space!", title);
            if (Button(new Rect(box.x + 120f, box.y + 260f, box.width - 240f, 120f), won ? "Next" : "Retry"))
            {
                card = Card.None;
                if (won) Next?.Invoke();
                else Restart?.Invoke();
            }
        }

        void EnsureStyles()
        {
            // Check the last style built: a script reload in Play mode can keep older styles but not newer ones.
            if (toggleOff != null) return;
            label = new GUIStyle(GUI.skin.label) { fontSize = 52, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            label.normal.textColor = new Color(0.2f, 0.2f, 0.24f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 40, fontStyle = FontStyle.Bold };
            small = new GUIStyle(label) { fontSize = 30, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleRight };
            toggleOn = new GUIStyle(button) { fontSize = 28 };
            toggleOff = new GUIStyle(toggleOn);
            toggleOff.normal.textColor = toggleOff.hover.textColor = new Color(1f, 1f, 1f, 0.45f);
            title = new GUIStyle(label) { fontSize = 64, alignment = TextAnchor.MiddleCenter };
            title.normal.textColor = Color.white;
            cardStyle = new GUIStyle(GUI.skin.box);
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0.15f, 0.15f, 0.2f, 0.92f));
            tex.Apply();
            cardStyle.normal.background = tex;
        }
    }
}
