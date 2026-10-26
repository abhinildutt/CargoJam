using UnityEngine;

namespace CargoJam.Game.UI
{
    /// <summary>
    /// Installs the real UI on whatever scene has a <see cref="GameController"/>. Runs after every
    /// Awake and before any Start, so the controller sees its HudLayout and skips the debug HUD.
    /// </summary>
    static class UiBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var controller = Object.FindAnyObjectByType<GameController>();
            if (controller == null || controller.HudLayout != null) return;
            var flow = controller.gameObject.AddComponent<GameFlow>();
            if (!flow.Init(controller))
            {
                Debug.LogWarning("[UI] Falling back to the debug HUD.");
                Object.Destroy(flow);
            }
        }
    }
}
