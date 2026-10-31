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
    /// Owns the UI Toolkit document and sequences the game: Home (⇄ Journey) → Level → Win
    /// (→ Chest → Unlock) → next Level, or Fail → Continue / Try again. Wires boosters and continues
    /// to the economy, keeps board input off while anything covers it, tells the camera which part
    /// of the screen belongs to the UI (<see cref="IHudLayout"/>) and where to frame the home
    /// diorama (<see cref="IMenuLayout"/>), and hands the journey's arrival rules to the controller.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour, IHudLayout, IMenuLayout
    {
        static readonly string[] StyleSheets = { "UI/CargoJamBase", "UI/CargoJamScreens" };
        static readonly Rect FallbackMenuViewport = new Rect(0.02f, 0.34f, 0.96f, 0.56f);

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
        Action stopAnalytics;

        HomeScreen home;
        JourneyScreen journey;
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
        VisualElement splash;
        const string SplashSeenKey = "cj.splashSeen";

        State state = State.Home;
        int continuesUsed;
        int attemptIndex = -1;
        int attempt;
        /// <summary>The level index the Journey screen last showed, so the truck can drive from there.</summary>
        int journeySeenIndex;
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
            stopAnalytics = Analytics.Observe(progression);
            UiFeedback.Audio = (controller.Fx as FeelDirector)?.Audio;
            journeySeenIndex = progression.LevelIndex;

            controller.LevelStarted += OnLevelStarted;
            controller.LevelWon += OnLevelWon;
            controller.LevelFailed += OnLevelFailed;
            controller.ComboChanged += OnBoardChanged;
            controller.DockChanged += OnDockChanged;
            controller.BoosterApplied += OnBoosterApplied;
            controller.CraneArmedChanged += OnCraneArmed;
            controller.InputEnabled = false;
            controller.ShouldRevealArrival = progression.ShouldPlayArrival;
            controller.MenuLayout = this;
            controller.HudLayout = this;
            return true;
        }

        void Start()
        {
            if (ctx == null) return;
            ApplySafeArea();
            ShowHome();
            // The wordmark splash is a first-launch moment; returning players go straight to Home.
            if (PlayerPrefs.GetInt(SplashSeenKey, 0) == 0)
            {
                PlayerPrefs.SetInt(SplashSeenKey, 1);
                PlayerPrefs.Save();
                PlaySplash();
            }
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
            stopAnalytics?.Invoke();
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
            if (ScreenInfo.SafeArea != lastSafeArea || ScreenInfo.Width != lastScreen.x || ScreenInfo.Height != lastScreen.y)
                ApplySafeArea();

            for (int i = 0; i < timed.Count; i++)
            {
                if (Time.time < timed[i].Key) continue;
                var action = timed[i].Value;
                timed.RemoveAt(i);
                action();
                i = -1; // the action may have changed the list
            }

            controller.InputEnabled = state == State.Playing && !ctx.AnyModalOpen && !home.Visible && !journey.Visible;
            tutorial.Update();
        }

        // ---- IHudLayout / IMenuLayout -----------------------------------------------------------

        /// <summary>The screen minus the safe-area insets, the top row and the booster row.</summary>
        public Rect GameViewport
        {
            get
            {
                float w = Mathf.Max(1, ScreenInfo.Width), h = Mathf.Max(1, ScreenInfo.Height);
                float k = space != null ? space.UnitsPerPixel : 1f;
                if (k <= 0f || float.IsNaN(k)) k = 1f;
                var safe = ScreenInfo.SafeArea;
                float top = (safe.yMax - (LevelHud.TopBarHeight + 24f) / k) / h;
                float bottom = (safe.yMin + LevelHud.BottomBarHeight / k) / h;
                float left = safe.xMin / w + 0.03f;
                float right = safe.xMax / w - 0.03f;
                if (top - bottom < 0.2f || right - left < 0.2f) return new Rect(0.03f, 0.03f, 0.94f, 0.85f);
                return Rect.MinMaxRect(left, Mathf.Clamp01(bottom), right, Mathf.Clamp01(top));
            }
        }

        /// <summary>
        /// The home diorama fills the screen between the top row and the bottom block (destination
        /// name, journey strip, Level button): roughly the top two thirds.
        /// </summary>
        public Rect MenuViewport
        {
            get
            {
                if (space == null || home == null) return FallbackMenuViewport;
                var size = space.PanelSize;
                if (size.y <= 1f || float.IsNaN(size.y)) return FallbackMenuViewport;
                var insets = space.SafeInsets;
                float topPanel = insets.y + LevelHud.TopBarHeight;
                // Let the scene tuck a little under the destination caption; the shade keeps text legible.
                float bottomPanel = home.BottomBlockTop + 60f;
                float yMax = 1f - topPanel / size.y;
                float yMin = 1f - bottomPanel / size.y;
                if (yMax - yMin < 0.25f) return FallbackMenuViewport;
                return Rect.MinMaxRect(0.03f, Mathf.Clamp01(yMin), 0.97f, Mathf.Clamp01(yMax));
            }
        }

        /// <summary>True when a press would hit the UI (or the board is covered).</summary>
        public bool Captures(Vector2 screenPosition)
        {
            if (ctx == null) return false;
            if (ctx.AnyModalOpen || home.Visible || journey.Visible) return true;
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
            // Above the FX overlay (callouts, vignettes), which shares this panel.
            document.sortingOrder = 10;
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
                ScreenLayer = Ui.El(root, "cj-fill"),
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

            splash = Ui.El(root, "cj-fill cj-splash", true);
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
            home.ChestClicked += () =>
            {
                if (!ctx.AnyModalOpen && progression.ChestReady)
                    chest.Show(progression.NextChestIsBig, progression.OpenChest, home.Refresh);
            };
            home.JourneyClicked += OpenJourney;
            // The bottom block's real height decides the diorama's frame: refit when it lays out.
            home.LayoutChanged += () =>
            {
                if (home.Visible) controller.MenuLayout = this;
            };

            journey = new JourneyScreen(ctx, ctx.ScreenLayer);
            journey.BackClicked += CloseJourney;

            var overlaySafe = Ui.El(ctx.OverlayLayer, "cj-fill cj-safe cj-overlay");
            // Toasts, tips and the hard-level notice stack just under the top row.
            Ui.El(overlaySafe).style.height = LevelHud.TopBarHeight;
            ctx.Toast = new Toast(overlaySafe);
            tutorial = new Tutorial(ctx, overlaySafe);
            banner = new HardBanner(overlaySafe);

            win = new WinPanel(ctx);
            fail = new FailPanel(ctx);
            fail.ContinueClicked += OnContinueClicked;
            fail.TryAgainClicked += OnTryAgainClicked;
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
            lastSafeArea = ScreenInfo.SafeArea;
            lastScreen = new Vector2Int(ScreenInfo.Width, ScreenInfo.Height);
            var insets = space.SafeInsets;
            ctx.Root.Query<VisualElement>(className: "cj-safe").ForEach(e =>
            {
                e.style.paddingLeft = insets.x;
                e.style.paddingTop = insets.y;
                e.style.paddingRight = insets.z;
                e.style.paddingBottom = insets.w;
            });
            ctx.Root.Query<VisualElement>(className: "cj-safe-top").ForEach(e =>
            {
                e.style.paddingLeft = insets.x;
                e.style.paddingTop = insets.y;
                e.style.paddingRight = insets.z;
            });
            // Re-assigning the layouts makes the controller refit the camera to the new viewport.
            if (controller.HudLayout == (IHudLayout)this) controller.HudLayout = this;
            if (controller.MenuLayout == (IMenuLayout)this) controller.MenuLayout = this;
        }

        /// <summary>The launch splash: the wordmark over a dark wash, gone after a moment or a tap.</summary>
        void PlaySplash()
        {
            var logo = new Logo();
            splash.Add(logo);
            Ui.Show(splash, true);
            logo.PlayIntro();
            bool gone = false;
            Action dismiss = () =>
            {
                if (gone) return;
                gone = true;
                Anim.Fade(splash, 1f, 0f, Anim.Panel, 0f, () =>
                {
                    Ui.Show(splash, false);
                    splash.Clear();
                });
            };
            splash.RegisterCallback<PointerDownEvent>(_ => dismiss());
            UiTween.Delay(1.5f, dismiss);
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
            // The home screen has its own living diorama; the level loads when Play is tapped.
            controller.ShowMenuScene(progression.LevelIndex);
            home.Show();
            // No daily popup: the Daily icon's dot on the left edge is the only prompt.
        }

        void OpenJourney()
        {
            if (state != State.Home || ctx.AnyModalOpen || journey.Visible) return;
            journey.Show(journeySeenIndex);
            journeySeenIndex = progression.LevelIndex;
            // The Journey screen is opaque: stop drawing the 3D scene under it once it has slid in.
            UiTween.Delay(Anim.Panel, () => { if (journey.Visible) SetSceneVisible(false); });
        }

        void CloseJourney()
        {
            if (!journey.Visible) return;
            SetSceneVisible(true);
            journey.Hide(() => { if (state == State.Home) home.Refresh(); });
        }

        void SetSceneVisible(bool on)
        {
            if (controller.Camera != null) controller.Camera.enabled = on;
        }

        void OnPlayClicked()
        {
            if (state != State.Home || ctx.AnyModalOpen || journey.Visible) return;
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
                UiTween.Delay(0.5f, () => Anim.PopInReward(button, 0f, 0.4f));
            }
        }

        void OnLevelStarted(LevelStartInfo info)
        {
            continuesUsed = 0;
            timed.Clear();
            progression.BeginLevel();
            if (controller.EnvironmentShown.Arrival) progression.MarkArrivalShown(info.Index);
            attempt = info.Index == attemptIndex ? attempt + 1 : 1;
            attemptIndex = info.Index;
            Analytics.LogLevelStart(info.Index, info.Hard, attempt);

            hud.SetLevel(info.Number, info.Hard);
            hud.RefreshBoosters();
            ctx.Toast.Hide();
            if (state == State.Playing)
            {
                if (info.Hard) banner.Play(controller.EnvironmentShown.Arrival ? 2.5f : 0.3f);
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
            banner.Stop();
            ctx.Toast.Hide();

            // Record now so the win is saved even if the app closes during the celebration; the
            // counter holds still until the card flies the coins in.
            int wonIndex = controller.LevelIndex;
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
            At(showAt, () => win.Show(result, wonIndex, () => AfterWin(result)));
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
            int cost = progression.ContinueCost(continuesUsed);
            // A board stalled by padlocks fails with dock room to spare; another slot wouldn't help.
            bool canGrow = controller.CanContinue;
            fail.Show(parcelsLeft, cost, progression.CanAfford(cost), canGrow, UiText.StreakDropWarning(progression));
        }

        void OnContinueClicked()
        {
            if (state != State.Failed) return;
            int cost = progression.ContinueCost(continuesUsed);
            if (!controller.CanContinue || !progression.TrySpend(cost))
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

        /// <summary>Try again: the level counts as given up (the streak drops a tier), then replays.</summary>
        void OnTryAgainClicked()
        {
            if (state != State.Failed) return;
            progression.RecordGiveUp();
            fail.Close(() =>
            {
                state = State.Playing;
                controller.Restart();
            });
        }

        void OnPauseClicked()
        {
            if (state != State.Playing || ctx.AnyModalOpen) return;
            pause.ShowPause(controller.MovedThisLevel ? UiText.StreakDropWarning(progression) : null, null);
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
            });
        }

        // ---- Boosters ---------------------------------------------------------------------------

        void OnBoosterClicked(BoosterType booster)
        {
            if (state != State.Playing || ctx.AnyModalOpen) return;
            var button = hud.Button(booster);

            if (!progression.IsUnlocked(booster))
            {
                Reject(button, $"Unlocks at level {EconomyConfig.UnlockLevel(booster)}");
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
            if (progression.UsableBoosterCount(booster) > 0)
            {
                // Charged in OnBoosterApplied, once the effect actually happens (free charges first).
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
            Anim.Punch(hud.Button(booster), 0.08f);
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
