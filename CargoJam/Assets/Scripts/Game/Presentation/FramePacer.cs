using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CargoJam.Game.Presentation
{
    /// <summary>
    /// Battery saver: once nothing has moved for <see cref="IdleAfter"/> seconds, only every
    /// <see cref="IdleInterval"/>th frame is rendered (30 fps); the moment something moves it is back to
    /// every frame. Updates keep running at full rate, so a touch is read at once and shows on the
    /// next frame. Whatever animates calls <see cref="KeepAwake"/> while it runs (game and UI tweens,
    /// particles, camera pans, slow motion, the home screen's convoy); slow ambient drift doesn't,
    /// since it reads fine at 30. In practice the saving is on the board between moves.
    /// </summary>
    public sealed class FramePacer : MonoBehaviour
    {
        /// <summary>Seconds of stillness before dropping the rate; covers short effects started by a tap.</summary>
        public const float IdleAfter = 2.5f;

        /// <summary>Frames per rendered frame while idle (60 fps target / 2 = 30 fps).</summary>
        public const int IdleInterval = 2;

        static float lastActive;

        /// <summary>Something is moving this frame: render at full rate for at least <see cref="IdleAfter"/> more seconds.</summary>
        public static void KeepAwake() => lastActive = Time.unscaledTime;

        public static bool Idle => Time.unscaledTime - lastActive >= IdleAfter;

        // Domain reload is off for Play mode, so statics survive between sessions: start each one awake.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            lastActive = 0f;
            OnDemandRendering.renderFrameInterval = 1;
        }

        // LateUpdate, so everything that animated in this frame's Update has had its say.
        void LateUpdate()
        {
            var pointer = Pointer.current;
            if (pointer != null && pointer.press.isPressed) KeepAwake();
            OnDemandRendering.renderFrameInterval = Idle ? IdleInterval : 1;
        }

        void OnDisable() => OnDemandRendering.renderFrameInterval = 1;
    }
}
