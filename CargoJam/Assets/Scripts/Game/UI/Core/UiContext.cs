using CargoJam.Meta;
using UnityEngine.UIElements;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// What every screen and popup shares: the layers of the UI tree, the game and meta APIs, and
    /// the global widgets (coin counter, fly-ins, toast).
    /// </summary>
    public sealed class UiContext
    {
        /// <summary>The styled root (class cj-root) that holds every layer.</summary>
        public VisualElement Root;
        /// <summary>In-level HUD (top bar and booster bar).</summary>
        public VisualElement HudLayer;
        public VisualElement HomeLayer;
        /// <summary>Non-blocking overlays: HARD banner, tutorial hand, tips, toasts.</summary>
        public VisualElement OverlayLayer;
        /// <summary>Panels and popups, each with its own scrim.</summary>
        public VisualElement ModalLayer;
        /// <summary>Above the modals: the coin counter, so rewards can fly into it.</summary>
        public VisualElement TopLayer;
        public VisualElement FlyElement;

        public GameController Controller;
        public Progression Progression;
        public PanelSpace Space;

        public CoinCounter Coins;
        public FlyLayer Fly;
        public Toast Toast;

        int openModals;

        /// <summary>True while any panel or popup is up (board input off).</summary>
        public bool AnyModalOpen => openModals > 0;

        internal void ModalOpened() => openModals++;

        internal void ModalClosed()
        {
            if (openModals > 0) openModals--;
        }
    }
}
