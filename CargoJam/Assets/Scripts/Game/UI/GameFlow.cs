using System;
using System.Collections.Generic;
using CargoJam.Game.Feel;
using CargoJam.Game.Meta;
using CargoJam.Game.Presentation;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Owns the UI Toolkit document and sequences the game: Home → Level → Win (→ Chest → Unlock)
    /// → next Level, or Fail → Continue / Give up → Home. Wires boosters and continues to the
    /// economy, keeps board input off while anything covers it, and tells the camera and input
    /// which part of the screen belongs to the UI (<see cref="IHudLayout"/>).
    /// </summary>
    public sealed class GameFlow : MonoBehaviour, IHudLayout
    {
        static readonly string[] StyleSheets = { "UI/CargoJamBase", "UI/CargoJamScreens" };

        enum State
        {
            Home,
            Playing,
            Won,
            Failed,
        }

        GameController controller;
        Progression progression;
        UIDocument document;
        UiContext ctx;
        PanelSpace space;

        HomeScreen home;
        LevelHud hud;
        HardBanner banner;
        Tutorial tutorial;
        WinPanel win;
        FailPanel fail;
        PausePopup pause;
        BuyBoosterPopup buy;
        ChestPopup chest;
        UnlockPopup unlock;
        DailyPopup daily;

        State state = State.Home;
        int continuesUsed;
        bool dailyChecked;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        readonly List<KeyValuePair<float, Action>> timed = new List<KeyValuePair<float, Action>>();

        /// <summary>
        /// Loads the profile, builds the UI and takes over from the debug HUD. Returns false (and
        /// leaves the controller untouched) if anything fails, so the game still runs on the debug HUD.
        /// </summary>
        public bool Init(GameController gameController)
        {
            controller = gameController;
            try
            {
                progression = new Progression(ProfileStore.Load(), new SystemClock(), Environment.TickCount);
                progression.RegisterOpen();
                BuildDocument();
                BuildScreens();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                if (document != null) Destroy(document.gameObject);
                document = null;
                if (ctx != null && UiTween.Clock == ctx.Root) UiTween.Clock = null;
                ctx = null;
                return false;
            }

            progression.Changed += OnProgressChanged;
            ProfileStore.Save(progression.Data);
            UiFeedback.Audio = (controller.Fx as FeelDirector)?.Audio;

            controller.LevelStarted += OnLevelStarted;
            controller.LevelWon += OnLevelWon;
            controller.LevelFailed += OnLevelFailed;
            controller.ComboChanged += OnBoardChanged;
            controller.DockChanged += OnDockChanged;
            controller.BoosterApplied += OnBoosterApplied;
            controller.CraneArmedChanged += OnCraneArmed;
            controller.InputEnabled = false;
            controller.HudLayout = this;
            return true;
        }

        void Start()
        {
            if (ctx == null) return;
            ApplySafeArea();
            // The current level sits live (and dimmed) behind the home screen.
            ShowHome();
            controller.StartLevel(progression.LevelIndex);
        }

        void OnDestroy()
        {
            if (controller != null)
            {
                controller.LevelStarted -= OnLevelStarted;
                controller.LevelWon -= OnLevelWon;
                controller.LevelFailed -= OnLevelFailed;
                controller.ComboChanged -= OnBoardChanged;
                controller.DockChanged -= OnDockChanged;
                controller.BoosterApplied -= OnBoosterApplied;
                controller.CraneArmedChanged -= OnCraneArmed;
            }
            if (progression != null) progression.Changed -= OnProgressChanged;
            if (ctx != null && UiTween.Clock == ctx.Root) UiTween.Clock = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && progression != null) ProfileStore.Save(progression.Data);
        }

        void OnApplicationQuit()
        {
            if (progression != null) ProfileStore.Save(progression.Data);
        }

        void Update()
        {
            if (ctx == null) return;
            if (Screen.safeArea != lastSafeArea || Screen.width != lastScreen.x || Screen.height != lastScreen.y)
                ApplySafeArea();

            for (int i = 0; i < timed.Count; i++)
            {
                if (Time.time < timed[i].Key) continue;
                var action = timed[i].Value;
                timed.RemoveAt(i);
                action();
                i = -1; // the action may have changed the list
            }

            controller.InputEnabled = state == State.Playing && !ctx.AnyModalOpen && !home.Visible;
            tutorial.Update();
        }

        // ---- IHudLayout -------------------------------------------------------------------------

        /// <summary>The screen minus the safe-area insets, the top bar and the booster bar.</summary>
        public Rect GameViewport
        {
            get
            {
                float w = Mathf.Max(1, Screen.width), h = Mathf.Max(1, Screen.height);
                float k = space != null ? space.UnitsPerPixel : 1f;
                if (k <= 0f || float.IsNaN(k)) k = 1f;
                var safe = Screen.safeArea;
                float top = (safe.yMax - (LevelHud.TopBarHeight + 24f) / k) / h;
                float bottom = (safe.yMin + LevelHud.BottomBarHeight / k) / h;
                float left = safe.xMin / w + 0.03f;
                float right = safe.xMax / w - 0.03f;
                if (top - bottom < 0.2f || right - left < 0.2f) return new Rect(0.03f, 0.03f, 0.94f, 0.85f);
                return Rect.MinMaxRect(left, Mathf.Clamp01(bottom), right, Mathf.Clamp01(top));
            }
        }

        /// <summary>True when a press would hit the UI (or the board is covered).</summary>
        public bool Captures(Vector2 screenPosition)
        {
            if (ctx == null) return false;
            if (ctx.AnyModalOpen || home.Visible) return true;
            var panel = ctx.Root.panel;
            if (panel == null) return false;
            var picked = panel.Pick(space.ScreenToPanel(screenPosition));
            return picked != null && picked != ctx.Root && ctx.Root.Contains(picked);
        }

        // ---- Building ---------------------------------------------------------------------------

        void BuildDocument()
        {
            var settings = Resources.Load<PanelSettings>("UI/PanelSettings");
            if (settings == null) throw new InvalidOperationException("Resources/UI/PanelSettings is missing");

            // Inactive while configuring, so the document attaches once with its settings in place.
            var go = new GameObject("UI");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            document = go.AddComponent<UIDocument>();
            document.panelSettings = settings;
            go.SetActive(true);

            var docRoot = document.rootVisualElement;
            docRoot.pickingMode = PickingMode.Ignore;
            docRoot.style.position = Position.Absolute;
            docRoot.style.left = 0;
            docRoot.style.top = 0;
            docRoot.style.right = 0;
            docRoot.style.bottom = 0;
            foreach (var path in StyleSheets)
            {
                var sheet = Resources.Load<StyleSheet>(path);
                if (sheet != null) docRoot.styleSheets.Add(sheet);
                else Debug.LogWarning($"[UI] Missing style sheet Resources/{path}.uss");
            }

            var root = Ui.El(docRoot, "cj-root cj-fill");
            UiTween.Clock = root;
            space = new PanelSpace(root, settings);
            ctx = new UiContext
            {
                Root = root,
                HudLayer = Ui.El(root, "cj-fill"),
                HomeLayer = Ui.El(root, "cj-fill"),
                OverlayLayer = Ui.El(root, "cj-fill"),
                ModalLayer = Ui.El(root, "cj-fill"),
                TopLayer = Ui.El(root, "cj-fill"),
                FlyElement = Ui.El(root, "cj-fill"),
                Controller = controller,
                Progression = progression,
                Space = space,
            };
            ctx.Fly = new FlyLayer(ctx.FlyElement);

            var topSafe = Ui.El(ctx.TopLayer, "cj-fill cj-safe");
            var topRow = Ui.El(topSafe, "cj-top__row");
            topRow.style.height = LevelHud.TopBarHeight;
            ctx.Coins = new CoinCounter();
            topRow.Add(ctx.Coins);
            ctx.Coins.SetValue(progression.Coins, false);
        }

        void BuildScreens()
        {
            hud = new LevelHud(ctx);
            hud.PauseClicked += OnPauseClicked;
            hud.BoosterClicked += OnBoosterClicked;

            home = new HomeScreen(ctx);
            home.PlayClicked += OnPlayClicked;
            home.SettingsClicked += () =>
            {
                if (!ctx.AnyModalOpen) pause.ShowSettings(null);
            };
            home.DailyClicked += () =>
            {
                if (!ctx.AnyModalOpen) daily.Show(progression.DailyStatus(), home.Refresh);
            };

            var overlaySafe = Ui.El(ctx.OverlayLayer, "cj-fill cj-safe cj-overlay");
            // Toasts and tips stack just under the top bar.
            Ui.El(overlaySafe).style.height = LevelHud.TopBarHeight;
            ctx.Toast = new Toast(overlaySafe);
            tutorial = new Tutorial(ctx, overlaySafe);
            banner = new HardBanner(ctx.OverlayLayer);

            win = new WinPanel(ctx);
            fail = new FailPanel(ctx);
            fail.ContinueClicked += OnContinueClicked;
            fail.GiveUpClicked += OnGiveUpClicked;
            pause = new PausePopup(ctx);
            pause.RestartClicked += OnRestartClicked;
            pause.HomeClicked += OnPauseHomeClicked;
            buy = new BuyBoosterPopup(ctx);
            chest = new ChestPopup(ctx);
            unlock = new UnlockPopup(ctx);
            daily = new DailyPopup(ctx);
        }

        void ApplySafeArea()
        {
            lastSafeArea = Screen.safeArea;
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            var insets = space.SafeInsets;
            ctx.Root.Query<VisualElement>(className: "cj-safe").ForEach(e =>
            {
                e.style.paddingLeft = insets.x;
                e.style.paddingTop = insets.y;
                e.style.paddingRight = insets.z;
                e.style.paddingBottom = insets.w;
            });
            // Re-assigning the layout makes the controller refit the camera to the new viewport.
            if (controller.HudLayout == (IHudLayout)this) controller.HudLayout = this;
        }

        // ---- Flow -------------------------------------------------------------------------------

        void At(float time, Action action) => timed.Add(new KeyValuePair<float, Action>(time, action));

        void ShowHome()
        {
            state = State.Home;
            timed.Clear();
            hud.Hide();
            tutorial.Stop();
            banner.Stop();
            ctx.Toast.Hide();
            controller.CancelCrane();
            home.Show();

            if (dailyChecked) return;
            dailyChecked = true;
            var info = progression.DailyStatus();
            if (info == null || !info.Available) return;
            UiTween.Delay(0.7f, () =>
            {
                if (state == State.Home && !ctx.AnyModalOpen) daily.Show(progression.DailyStatus(), home.Refresh);
            });
        }

        void OnPlayClicked()
        {
            if (state != State.Home || ctx.AnyModalOpen) return;
            state = State.Playing;
            home.Hide();
            hud.Show();
            // Rebuild so the level's intro plays in full view.
            controller.StartLevel(progression.LevelIndex);
        }

        void StartNextLevel(BoosterType? unlocked)
        {
            state = State.Playing;
            hud.Show();
            controller.StartLevel(progression.LevelIndex);
            if (unlocked.HasValue)
            {
                var button = hud.Button(unlocked.Value);
                UiTween.Delay(0.5f, () => Anim.Punch(button, 0.3f, 0.45f));
            }
        }

        void OnLevelStarted(LevelStartInfo info)
        {
            continuesUsed = 0;
            timed.Clear();
            hud.SetLevel(info.Number, info.Hard);
            hud.RefreshBoosters();
            ctx.Toast.Hide();
            if (state == State.Playing)
            {
                if (info.Hard) banner.Play();
                tutorial.Begin(info.Index);
            }
            else
            {
                tutorial.Stop();
            }
        }

        void OnLevelWon(float showAt)
        {
            if (state != State.Playing) return;
            state = State.Won;
            tutorial.Stop();
            ctx.Toast.Hide();

            // Record now so the win is saved even if the app closes during the celebration; the
            // counter holds still until the panel flies the coins in.
            ctx.Coins.Freeze();
            WinResult result = null;
            try
            {
                result = progression.RecordWin(controller.IsHard);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            At(showAt, () => win.Show(result, () => AfterWin(result)));
        }

        void AfterWin(WinResult result)
        {
            if (progression.ChestReady)
                chest.Show(progression.NextChestIsBig, progression.OpenChest, () => AfterChest(result));
            else
                AfterChest(result);
        }

        void AfterChest(WinResult result)
        {
            var unlocked = result?.Unlocked;
            if (unlocked.HasValue) unlock.Show(unlocked.Value, () => StartNextLevel(unlocked));
            else StartNextLevel(null);
        }

        void OnLevelFailed(int parcelsLeft, float showAt)
        {
            if (state != State.Playing) return;
            state = State.Failed;
            tutorial.Stop();
            ctx.Toast.Hide();
            controller.CancelCrane();
            At(showAt, () => ShowFail(parcelsLeft));
        }

        void ShowFail(int parcelsLeft)
        {
            int cost = EconomyConfig.ContinueCost(continuesUsed);
            bool canGrow = controller.Session != null && controller.Session.CanAddHoldingSlot;
            fail.Show(parcelsLeft, cost, progression.CanAfford(cost), canGrow, progression.WinStreak);
        }

        void OnContinueClicked()
        {
            if (state != State.Failed) return;
            int cost = EconomyConfig.ContinueCost(continuesUsed);
            if (controller.Session == null || !controller.Session.CanAddHoldingSlot || !progression.TrySpend(cost))
            {
                fail.RejectContinue();
                return;
            }
            if (!controller.ContinueWithExtraSlot())
            {
                // Refund: the slot couldn't be added after all.
                progression.AddCoins(cost);
                fail.RejectContinue();
                return;
            }
            continuesUsed++;
            state = State.Playing;
            fail.Close();
            hud.RefreshBoosters();
        }

        void OnGiveUpClicked()
        {
            if (state != State.Failed) return;
            progression.RecordGiveUp();
            fail.Close(() =>
            {
                ShowHome();
                controller.StartLevel(progression.LevelIndex);
            });
        }

        void OnPauseClicked()
        {
            if (state != State.Playing || ctx.AnyModalOpen) return;
            int atRisk = controller.MovedThisLevel ? progression.WinStreak : 0;
            pause.ShowPause(atRisk, null);
        }

        void OnRestartClicked()
        {
            pause.Close(() =>
            {
                if (controller.MovedThisLevel) progression.RecordGiveUp();
                state = State.Playing;
                controller.Restart();
            });
        }

        void OnPauseHomeClicked()
        {
            pause.Close(() =>
            {
                if (controller.MovedThisLevel) progression.RecordGiveUp();
                ShowHome();
                controller.StartLevel(progression.LevelIndex);
            });
        }

        // ---- Boosters ---------------------------------------------------------------------------

        void OnBoosterClicked(BoosterType booster)
        {
            if (state != State.Playing || ctx.AnyModalOpen) return;
            var button = hud.Button(booster);

            if (!progression.IsUnlocked(booster))
            {
                Reject(button, $"Unlocks at Level {EconomyConfig.UnlockLevel(booster)}");
                return;
            }
            if (booster == BoosterType.Crane && controller.CraneArmed)
            {
                controller.CancelCrane();
                return;
            }
            if (!controller.CanUseBooster(booster))
            {
                Reject(button, WhyNot(booster));
                return;
            }
            if (progression.BoosterCount(booster) > 0)
            {
                // Charged in OnBoosterApplied, once the effect actually happens.
                controller.UseBooster(booster);
                return;
            }

            int price = EconomyConfig.BoosterPrice(booster);
            buy.Show(booster, progression.CanAfford(price), () => progression.TryBuyBooster(booster), () =>
            {
                hud.RefreshBoosters();
                if (state == State.Playing && controller.CanUseBooster(booster)) controller.UseBooster(booster);
            });
        }

        void Reject(BoosterButton button, string why)
        {
            button.Reject();
            UiFeedback.Nope();
            ctx.Toast.Show(why);
        }

        static string WhyNot(BoosterType booster)
        {
            switch (booster)
            {
                case BoosterType.Undo: return "Nothing in the dock to undo";
                case BoosterType.ExtraSlot: return "The dock can't get any bigger";
                default: return "Nothing to lift right now";
            }
        }

        void OnBoosterApplied(BoosterType booster)
        {
            if (!progression.TryConsumeBooster(booster))
                Debug.LogWarning($"[UI] {booster} applied with none owned");
            hud.RefreshBoosters();
            Anim.Punch(hud.Button(booster), 0.2f);
        }

        void OnCraneArmed(bool armed)
        {
            hud.RefreshBoosters();
            if (armed) ctx.Toast.ShowSticky("Tap any parcel to lift it");
            else ctx.Toast.Hide();
        }

        void OnBoardChanged(int _)
        {
            hud.RefreshBoosters();
            tutorial.OnMove();
        }

        void OnDockChanged(int count, int capacity) => OnBoardChanged(count);

        void OnProgressChanged()
        {
            ProfileStore.Save(progression.Data);
            ctx.Coins.SetValue(progression.Coins);
            hud.RefreshBoosters();
            if (home.Visible) home.Refresh();
        }
    }
}
