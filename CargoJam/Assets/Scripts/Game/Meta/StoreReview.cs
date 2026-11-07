using System;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>
    /// Asks for an App Store rating at the moments <see cref="ReviewPrompt"/> allows, using the
    /// system sheet (<c>SKStoreReviewController</c> through Unity's <c>Device.RequestStoreReview</c>).
    /// Ask history lives in PlayerPrefs. Other platforms and the Editor only log the request.
    /// </summary>
    public static class StoreReview
    {
        const string AsksKey = "cargojam.review.asks";
        const string LastKey = "cargojam.review.last";

        /// <summary>Requests a review if this win qualifies. True when a request was made.</summary>
        /// <param name="levelWon">The level just won, 1-based.</param>
        public static bool MaybeAsk(WinResult result, int levelWon, bool hard)
        {
            if (result == null) return false;
            int asks = PlayerPrefs.GetInt(AsksKey, 0);
            DateTime today = DateTime.Now.Date;
            if (!ReviewPrompt.ShouldAsk(levelWon, hard, result.TierBefore, result.TierAfter,
                                        asks, PlayerPrefs.GetString(LastKey, ""), today))
                return false;

            PlayerPrefs.SetInt(AsksKey, asks + 1);
            PlayerPrefs.SetString(LastKey, Progression.DateKey(today));
            PlayerPrefs.Save();
            Analytics.Log(Analytics.ReviewRequested, ("level", levelWon), ("hard", hard), ("ask", asks + 1));
#if UNITY_IOS && !UNITY_EDITOR
            UnityEngine.iOS.Device.RequestStoreReview();
#endif
            return true;
        }
    }
}
