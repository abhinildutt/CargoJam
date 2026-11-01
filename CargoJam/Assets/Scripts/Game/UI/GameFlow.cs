using System;
using System.Collections.Generic;
using CargoJam.Core;
using CargoJam.Game.Feel;
using CargoJam.Game.Meta;
using CargoJam.Game.Presentation;
using CargoJam.Game.Presentation.Fx;
using CargoJam.Meta;
using UnityEngine;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Owns the UI Toolkit document and sequences the game: Title (once per launch) → Home (⇄ Journey)
    /// → Level → Win (→ Chest → Unlock) → next Level, or Fail → Continue / Try again. A level that debuts a cargo kind
    /// shows its "New cargo!" card once the board has landed (after any arrival or HARD banner), then
    /// the tutorial hand. Wires boosters and continues
    /// to the economy, keeps board input off while anything covers it, tells the camera which part
    /// of the screen belongs to the UI (<see cref="IHudLayout"/>) and where to frame the menu
    /// diorama on the title and on Home (<see cref="IMenuLayout"/>), and hands the journey's arrival
    /// rules to the controller.
    /// </summary>
    public sealed class GameFlow : MonoBehaviour, IHudLayout, IMenuLayout
    {
        static readonly string[] StyleSheets = { "UI/CargoJamBase", "UI/CargoJamScreens" };
        static readonly Rect FallbackMenuViewport = new Rect(0.02f, 0.34f, 0.96f, 0.56f);
        static readonly Rect FallbackTitleViewport = new Rect(0.16f, 0.22f, 0.68f, 0.48f);
        /// <summary>
        /// Side margin of the title's establishing shot (0..1 of the width). Narrower than Home's, so
        /// the camera stands further back and takes in more of the destination around the depot.
        /// </summary>
        const float TitleSideMargin = 0.16f;
        /// <summary>Play on the title: the camera's glide from the establishing shot to the home framing.</summary>
        const float TitleGlide = 1.05f;
        /// <summary>Play on the title: when the home screen starts its own show, once the title has lifted away.</summary>
        const float TitleHomeAt = 0.45f;

        enum State
        {
            /// <summary>The launch title, up to and through its hand-over to Home.</summary>
            Title,
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

        TitleScreen title;
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
        CargoIntroPopup cargoIntro;
        DailyPopup daily;
        LevelPickerPopup picker;
        /// <summary>When the HARD banner starts on an ordinary level, in seconds after the level starts (as the board drops in).</summary>
        const float BannerAt = 0.3f;
        /// <summary>On an arrival level the HARD banner waits for the destination reveal and card, then this long.</summary>
        const float ArrivalBannerGap = 0.6f;
        /// <summary>From the board being ready (or the HARD banner ending) to the first cargo card.</summary>
        const float CargoCardGap = 0.25f;

        State state = State.Title;
        int continuesUsed;
        /// <summary>False while the title hides the coin pill; Home brings it back.</summary>
        bool coinsShown = true;
        int attemptIndex = -1;
        int attempt;
        /// <summary>The level index the Journey screen last showed, so the truck can drive from there.</summary>
        int journeySeenIndex;
        Rect lastSafeArea;
        Vector2Int lastScreen;
        /// <summary>Cargo cards still to show for the level that just started, in enum order.</summary>
        readonly List<CargoKind> cargoQueue = new List<CargoKind>();
        /// <summary>Bumped on every level start so a card callback from an older level does nothing.</summary>
        int cargoToken;
        /// <summary>
        /// Cards are queued or showing for the level that just started: the board takes no taps until the
        /// last "Got it", so nobody plays a new cargo kind before it has been explained.
        /// </summary>
        bool cardsPending;
        /// <summary>The cargo the hand teaches on <see cref="teachLevel"/>, so a retry points it out again.</summary>
        CargoKind teachKind;
        int teachLevel = -1;
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
            ShowTitle();
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

            controller.InputEnabled = state == State.Playing && !cardsPending && !ctx.AnyModalOpen && !home.Visible && !journey.Visible;
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
        /// name, journey strip, Level button): roughly the top two thirds. While the title holds the
        /// frame it is the establishing shot instead (<see cref="TitleViewport"/>).
        /// </summary>
        public Rect MenuViewport
        {
            get
            {
                if (title != null && title.Framing) return TitleViewport;
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

        /// <summary>
        /// The title's establishing shot: the diorama sits between the wordmark and the Play button,
        /// inset from the sides (<see cref="TitleSideMargin"/>) so the camera stands well back.
        /// </summary>
        Rect TitleViewport
        {
            get
            {
                if (space == null) return FallbackTitleViewport;
                var size = space.PanelSize;
                if (size.y <= 1f || float.IsNaN(size.y)) return FallbackTitleViewport;
                // A little air under the wordmark and above the button.
                float yMax = 1f - (title.StageTop + 40f) / size.y;
                float yMin = 1f - (title.StageBottom - 40f) / size.y;
                if (yMax - yMin < 0.25f) return FallbackTitleViewport;
                return Rect.MinMaxRect(TitleSideMargin, Mathf.Clamp01(yMin), 1f - TitleSideMargin, Mathf.Clamp01(yMax));
            }
        }

        /// <summary>True when a press would hit the UI (or the board is covered).</summary>
        public bool Captures(Vector2 screenPosition)
        {
            if (ctx == null) return false;
            if (ctx.AnyModalOpen || title.Visible || home.Visible || journey.Visible) return true;
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
                TitleLayer = Ui.El(root, "cj-fill"),
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
        }

        void BuildScreens()
        {
            hud = new LevelHud(ctx);
            hud.PauseClicked += OnPauseClicked;
            hud.BoosterClicked += OnBoosterClicked;

            title = new TitleScreen(ctx, ctx.TitleLayer);
            title.PlayClicked += LeaveTitle;
            title.SettingsClicked += () =>
            {
                if (title.Framing && !ctx.AnyModalOpen) pause.ShowSettings(null);
            };
            // The wordmark and Play block decide the establishing shot: refit when they lay out.
            title.LayoutChanged += () =>
            {
                if (title.Framing) controller.MenuLayout = this;
            };

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
            home.LevelsClicked += OpenLevelPicker;
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
            cargoIntro = new CargoIntroPopup(ctx);
            if (LevelPickerPopup.Enabled) picker = new LevelPickerPopup(ctx);
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

        // ---- Flow -------------------------------------------------------------------------------

        void At(float time, Action action) => timed.Add(new KeyValuePair<float, Action>(time, action));

        /// <summary>
        /// The launch title over the menu diorama in its establishing shot. Home lays out unseen
        /// beneath it (so the camera knows where it will end up) and the coin pill stays away until
        /// Home, so nothing on screen speaks of progress.
        /// </summary>
        void ShowTitle()
        {
            state = State.Title;
            // Title first, so the scene's first fit already reads the establishing shot.
            title.Show();
            home.Prepare();
            coinsShown = false;
            Ui.Show(ctx.Coins, false);
            controller.ShowMenuScene(progression.LevelIndex);
        }

        /// <summary>
        /// Play on the title: the wordmark and button lift away, the camera glides in from the
        /// establishing shot to the home framing (<see cref="TitleGlide"/>), and part-way through Home
        /// plays its own show over the same diorama (no rebuild, no cut).
        /// </summary>
        void LeaveTitle()
        {
            if (state != State.Title || !title.Framing || ctx.AnyModalOpen) return;
            // From here MenuViewport reads the home framing, which is what the glide aims at.
            title.Leave();
            controller.GlideMenuCamera(TitleGlide);
            UiTween.Delay(TitleHomeAt, () =>
            {
                if (state == State.Title) ShowHome(false);
            });
        }

        /// <summary>Drops the title at once (tools that jump straight into a level).</summary>
        void SkipTitle()
        {
            if (state != State.Title) return;
            title.HideNow();
            // The title's framing is gone: fit the home one now rather than gliding.
            controller.MenuLayout = this;
            ShowHome(false);
        }

        /// <summary>
        /// Home over its living diorama. <paramref name="buildScene"/> is false coming from the title,
        /// which already shows the same diorama for the same level.
        /// </summary>
        void ShowHome(bool buildScene = true)
        {
            state = State.Home;
            timed.Clear();
            cardsPending = false;
            hud.Hide();
            tutorial.Stop();
            banner.Stop();
            ctx.Toast.Hide();
            controller.CancelCrane();
            // The home screen has its own living diorama; the level loads when Play is tapped.
            if (buildScene) controller.ShowMenuScene(progression.LevelIndex);
            home.Show();
            if (!coinsShown)
            {
                coinsShown = true;
                Ui.Show(ctx.Coins, true);
                Anim.Fade(ctx.Coins, 0f, 1f, Anim.Panel);
            }
            // No daily popup: the Daily icon's dot on the left edge is the only prompt. Anything that
            // should ever open by itself on reaching Home goes here, after home.Show(), so it waits for
            // Home and never pops over the title.
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

        void OpenLevelPicker()
        {
            if (picker == null || state != State.Home || ctx.AnyModalOpen || journey.Visible) return;
            picker.Show(progression.LevelIndex, controller.LevelCount, JumpToLevel);
        }

        /// <summary>Dev picker and <c>PerfBench</c>: moves progress to the chosen level (0-based) and plays it.</summary>
        internal void JumpToLevel(int index)
        {
            SkipTitle();
            if (state != State.Home) return;
            var debuts = new List<(int, int)>();
            foreach (var (number, kind) in LevelPickerPopup.Debuts) debuts.Add((number - 1, (int)kind));
            progression.JumpTo(index, debuts);
            journeySeenIndex = progression.LevelIndex;
            ProfileStore.Save(progression.Data);
            home.Refresh();
            OnPlayClicked();
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
            cargoQueue.Clear();
            cargoToken++;
            cardsPending = false;
            if (state == State.Playing)
            {
                bool arrival = controller.EnvironmentShown.Arrival;
                float readyAt = BoardReadyAt(arrival);
                float bannerAt = arrival ? readyAt - Time.time + ArrivalBannerGap : BannerAt;
                if (info.Hard) banner.Play(bannerAt);
                QueueCargoCards(info);
                cardsPending = cargoQueue.Count > 0;
                if (cardsPending)
                {
                    // The cards come first (after the HARD banner, if any); the hand follows the last one.
                    tutorial.Stop();
                    float cardsAt = info.Hard ? Time.time + bannerAt + HardBanner.Duration : readyAt;
                    ShowCargoCardsAt(cardsAt + CargoCardGap, cargoToken);
                }
                else
                {
                    tutorial.Begin(info.Index);
                    // Retrying the debut level: the card is done with, the hand isn't.
                    if (attempt > 1 && teachLevel == info.Index) tutorial.BeginCargo(teachKind);
                }
            }
            else
            {
                tutorial.Stop();
            }
        }

        // ---- Cargo cards ------------------------------------------------------------------------

        /// <summary>Queues a card for every cargo kind in the level the player hasn't met yet.</summary>
        void QueueCargoCards(LevelStartInfo info)
        {
            if (info.CargoMask == 0) return;
            foreach (CargoKind kind in Enum.GetValues(typeof(CargoKind)))
                if (info.Uses(kind) && !progression.HasSeenCargo((int)kind)) cargoQueue.Add(kind);
            if (cargoQueue.Count == 0) return;

            // The hand teaches the first kind you can act on; a bollard has nothing to tap.
            teachKind = cargoQueue[0];
            foreach (var kind in cargoQueue)
            {
                if (kind == CargoKind.Bollard) continue;
                teachKind = kind;
                break;
            }
            teachLevel = info.Index;
        }

        /// <summary>
        /// Time.time when the level that just started is ready to be looked at: the board has landed
        /// (<see cref="GameController.IntroEndsAt"/>, which includes any destination reveal) and, on an
        /// arrival level, the destination card has gone (it plays even when there is nothing to pan over).
        /// </summary>
        float BoardReadyAt(bool arrival)
        {
            float ready = Mathf.Max(Time.time, controller.IntroEndsAt);
            return arrival ? Mathf.Max(ready, Time.time + ArrivalCard.Life) : ready;
        }

        /// <summary>Opens the cards at a time, waiting while a pause or settings sheet is up.</summary>
        void ShowCargoCardsAt(float due, int token)
        {
            At(due, () =>
            {
                if (token != cargoToken || state != State.Playing) return;
                if (ctx.AnyModalOpen)
                {
                    ShowCargoCardsAt(Time.time + 0.25f, token);
                    return;
                }
                ShowNextCargoCard(token);
            });
        }

        void ShowNextCargoCard(int token)
        {
            if (token != cargoToken || state != State.Playing) return;
            if (cargoQueue.Count == 0)
            {
                // All met: the board opens, and the hand shows where to start (unless they already have).
                cardsPending = false;
                if (!controller.MovedThisLevel) tutorial.BeginCargo(teachKind);
                return;
            }
            var kind = cargoQueue[0];
            cargoQueue.RemoveAt(0);
            cargoIntro.Show(kind, () =>
            {
                progression.MarkCargoSeen((int)kind);
                ShowNextCargoCard(token);
            });
        }

        void OnLevelWon(float showAt)
        {
            if (state != State.Playing) return;
            state = State.Won;
            cardsPending = false;
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
            cardsPending = false;
            tutorial.Stop();
            ctx.Toast.Hide();
            controller.CancelCrane();
            var reason = controller.Session != null ? controller.Session.FailReason : FailReason.OutOfSpace;
            Analytics.LogLevelFail(controller.LevelIndex, parcelsLeft, continuesUsed, reason);
            At(showAt, () => ShowFail(parcelsLeft, reason));
        }

        void ShowFail(int parcelsLeft, FailReason reason)
        {
            int cost = progression.ContinueCost(continuesUsed);
            // Only a full dock can be helped by another slot (not a stalled board or a missed express);
            // the sheet also hides Continue unless the reason is OutOfSpace.
            bool canGrow = controller.CanContinue;
            fail.Show(reason, parcelsLeft, cost, progression.CanAfford(cost), canGrow, UiText.StreakDropWarning(progression));
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
            // Only a real move ends the teaching: a rejected tap (a frozen crate, a padlock) keeps the hand up.
            if (controller.MovedThisLevel) tutorial.OnMove();
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
