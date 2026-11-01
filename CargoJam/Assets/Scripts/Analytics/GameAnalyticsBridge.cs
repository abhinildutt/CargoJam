using System;
using System.Collections.Generic;
using CargoJam.Game.Meta;
using CargoJam.Meta;
using GameAnalyticsSDK;
using UnityEngine;

namespace CargoJam.Analytics
{
    /// <summary>
    /// Forwards the game's <see cref="CargoJam.Game.Meta.Analytics"/> events to GameAnalytics and feeds
    /// its Remote Configs into <see cref="Tuning"/> (docs/monetization-and-growth-plan.md, section 7).
    /// This assembly only compiles when the com.gameanalytics.sdk package is installed (a version
    /// define), so the game itself never depends on the SDK.
    ///
    /// Mapping:
    /// <list type="bullet">
    /// <item><c>level_start / level_complete / level_fail</c> → progression events on <c>level_NNN</c>;
    /// a fail also sends the design event <c>level_fail:&lt;reason&gt;</c>.</item>
    /// <item><c>coins_source / coins_sink</c> → resource events in the <c>coins</c> currency; the item
    /// type is the reason (booster purchases are type <c>booster</c> with the booster as the id).</item>
    /// <item>Everything else → a design event named after the event, plus a detail part where one
    /// helps (<c>booster_used:crane</c>, <c>destination_reached:farm</c>, …).</item>
    /// </list>
    /// Every event carries its fields as GameAnalytics custom fields.
    /// </summary>
    public static class GameAnalyticsBridge
    {
        public const string Currency = "coins";

        /// <summary>Resource item types; GameAnalytics drops resource events whose type isn't declared.</summary>
        static readonly string[] ItemTypes =
        {
            CoinReason.Win, CoinReason.Chest, CoinReason.BigChest, CoinReason.Daily, CoinReason.Grant,
            CoinReason.Refund, CoinReason.Spend, CoinReason.Continue, "booster",
        };

        const string BoosterPrefix = "booster_";
        const int MaxCustomFields = 50;

        static bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (started) return;
            started = true;

            if (UnityEngine.Object.FindAnyObjectByType<GameAnalytics>() == null)
                new GameObject("GameAnalytics").AddComponent<GameAnalytics>();

            var settings = GameAnalytics.SettingsGA;
            if (settings != null)
            {
                if (!settings.ResourceCurrencies.Contains(Currency)) settings.ResourceCurrencies.Add(Currency);
                foreach (var type in ItemTypes)
                    if (!settings.ResourceItemTypes.Contains(type)) settings.ResourceItemTypes.Add(type);
            }

            CargoJam.Game.Meta.Analytics.Logged += Forward;
            GameAnalytics.OnRemoteConfigsUpdatedEvent += PullRemoteConfigs;
            GameAnalytics.Initialize();
            if (GameAnalytics.IsRemoteConfigsReady()) PullRemoteConfigs();
        }

        /// <summary>Copies every known <see cref="TuningKeys"/> value GameAnalytics has into the tuning cache.</summary>
        static void PullRemoteConfigs()
        {
            try
            {
                if (!GameAnalytics.IsRemoteConfigsReady()) return;
                var values = new Dictionary<string, string>();
                foreach (var key in TuningKeys.All)
                {
                    string value = GameAnalytics.GetRemoteConfigsValueAsString(key, null);
                    if (!string.IsNullOrEmpty(value)) values[key] = value;
                }
                TuningStore.Store(values);
                if (Debug.isDebugBuild) Debug.Log($"GameAnalyticsBridge: {values.Count} remote values applied.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static void Forward(AnalyticsEvent e)
        {
            try
            {
                Send(e);
            }
            catch (Exception ex)
            {
                // Analytics must never break the game.
                Debug.LogException(ex);
            }
        }

        static void Send(AnalyticsEvent e)
        {
            var fields = CustomFields(e);
            switch (e.Name)
            {
                case CargoJam.Game.Meta.Analytics.LevelStart:
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Start, LevelName(e), fields);
                    return;
                case CargoJam.Game.Meta.Analytics.LevelComplete:
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Complete, LevelName(e), fields);
                    return;
                case CargoJam.Game.Meta.Analytics.LevelFail:
                    GameAnalytics.NewProgressionEvent(GAProgressionStatus.Fail, LevelName(e), fields);
                    GameAnalytics.NewDesignEvent(Design(e.Name, e.Get("reason")), fields);
                    return;
                case CargoJam.Game.Meta.Analytics.CoinsSource:
                case CargoJam.Game.Meta.Analytics.CoinsSink:
                    SendCoins(e, fields);
                    return;
            }

            string id = Design(e.Name, DetailFor(e));
            object value = ValueFor(e);
            if (value != null) GameAnalytics.NewDesignEvent(id, Convert.ToSingle(value), fields);
            else GameAnalytics.NewDesignEvent(id, fields);
        }

        static void SendCoins(AnalyticsEvent e, IDictionary<string, object> fields)
        {
            var flow = e.Name == CargoJam.Game.Meta.Analytics.CoinsSource ? GAResourceFlowType.Source : GAResourceFlowType.Sink;
            string reason = Part(e.Get("reason")) ?? CoinReason.Grant;
            string type = reason, item = reason;
            if (reason.StartsWith(BoosterPrefix, StringComparison.Ordinal))
            {
                type = "booster";
                item = reason.Substring(BoosterPrefix.Length);
            }
            float amount = e.Get("amount") is IConvertible a ? Convert.ToSingle(a) : 0f;
            if (amount <= 0f) return;
            GameAnalytics.NewResourceEvent(flow, Currency, amount, type, item, fields);
        }

        /// <summary>The second part of a design event id, when the event has an obvious one.</summary>
        static object DetailFor(AnalyticsEvent e)
        {
            switch (e.Name)
            {
                case CargoJam.Game.Meta.Analytics.BoosterUsed:
                case CargoJam.Game.Meta.Analytics.BoosterBought:
                    return e.Get("booster");
                case CargoJam.Game.Meta.Analytics.LevelGiveUp:
                    return e.Get("from");
                case CargoJam.Game.Meta.Analytics.DestinationReached:
                    return e.Get("stop") is int stop ? Journey.IdOf(stop) : null;
                case CargoJam.Game.Meta.Analytics.StreakTierChanged:
                    return e.Get("to") is int to ? "tier" + to : null;
                case CargoJam.Game.Meta.Analytics.ChestOpened:
                    return e.Get("big") is bool big && big ? "big" : "normal";
                case CargoJam.Game.Meta.Analytics.DailyClaimed:
                    return e.Get("day") is int day ? "day" + (day + 1) : null;
                case CargoJam.Game.Meta.Analytics.AdOfferShown:
                case CargoJam.Game.Meta.Analytics.AdRewardedStarted:
                case CargoJam.Game.Meta.Analytics.AdRewardedCompleted:
                    return e.Get("placement");
                case CargoJam.Game.Meta.Analytics.IapOfferShown:
                case CargoJam.Game.Meta.Analytics.IapPurchased:
                    return e.Get("sku");
                default:
                    return null;
            }
        }

        /// <summary>A numeric value worth charting on the design event, or null.</summary>
        static object ValueFor(AnalyticsEvent e)
        {
            switch (e.Name)
            {
                case CargoJam.Game.Meta.Analytics.ContinueUsed: return e.Get("cost");
                case CargoJam.Game.Meta.Analytics.BoosterBought: return e.Get("price");
                case CargoJam.Game.Meta.Analytics.ChestOpened: return e.Get("coins");
                case CargoJam.Game.Meta.Analytics.DailyClaimed: return e.Get("coins");
                default: return null;
            }
        }

        static string LevelName(AnalyticsEvent e) =>
            e.Get("level") is int level ? "level_" + level.ToString("D3") : "level_unknown";

        static string Design(string name, object detail)
        {
            string part = Part(detail);
            return part == null ? name : name + ":" + part;
        }

        /// <summary>A design-id part: GameAnalytics allows letters, digits, spaces and -_.()!? up to 64 characters.</summary>
        static string Part(object value)
        {
            if (value == null) return null;
            string text = value is IFormattable f ? f.ToString(null, System.Globalization.CultureInfo.InvariantCulture) : value.ToString();
            var chars = new System.Text.StringBuilder(Math.Min(text.Length, 64));
            foreach (char c in text)
            {
                if (chars.Length == 64) break;
                bool ok = char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' ||
                          c == '(' || c == ')' || c == '!' || c == '?';
                chars.Append(ok ? char.ToLowerInvariant(c) : '_');
            }
            return chars.Length == 0 ? null : chars.ToString();
        }

        /// <summary>The event's fields as GameAnalytics custom fields: numbers stay numbers, bools become 0/1, the rest strings.</summary>
        static Dictionary<string, object> CustomFields(AnalyticsEvent e)
        {
            var fields = new Dictionary<string, object>();
            foreach (var (key, value) in e.Fields)
            {
                if (fields.Count == MaxCustomFields) break;
                if (string.IsNullOrEmpty(key) || value == null) continue;
                switch (value)
                {
                    case bool b: fields[key] = b ? 1 : 0; break;
                    case int _:
                    case long _:
                    case float _:
                    case double _: fields[key] = Convert.ToDouble(value); break;
                    default:
                        string text = value.ToString();
                        fields[key] = text.Length > 256 ? text.Substring(0, 256) : text;
                        break;
                }
            }
            return fields;
        }
    }
}
