#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using System;
using CargoJam.Meta;
using UnityEngine;

namespace CargoJam.Game.Meta
{
    /// <summary>
    /// Local (on-device) reminder notifications. <see cref="NotificationPlan"/> decides what to send;
    /// this schedules it when the app goes to the background and cancels everything when it comes
    /// back, so a player who is playing never gets one. iOS uses UNUserNotificationCenter through
    /// Plugins/iOS/CargoJamNotifications.mm. Android and the Editor only log the plan for now.
    /// </summary>
    public static class LocalNotifications
    {
        const string AskedKey = "cargojam.notify.asked";

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void CargoJam_NotifyRequestAuthorization();
        [DllImport("__Internal")] static extern void CargoJam_NotifySchedule(int id, string title, string body, double delaySeconds);
        [DllImport("__Internal")] static extern void CargoJam_NotifyCancelAll();
#endif

        /// <summary>Whether this install has already shown the permission prompt.</summary>
        public static bool PermissionAsked => PlayerPrefs.GetInt(AskedKey, 0) == 1;

        /// <summary>
        /// Shows the system permission prompt once, when the player has won enough levels
        /// (<see cref="NotificationPlan.ShouldAskPermission"/>). True when it was shown now.
        /// </summary>
        public static bool AskPermissionIfDue(Progression progression)
        {
            if (PermissionAsked || !NotificationPlan.ShouldAskPermission(progression)) return false;
            PlayerPrefs.SetInt(AskedKey, 1);
            PlayerPrefs.Save();
            Analytics.Log(Analytics.NotifyPermissionAsked, ("level", progression.LevelIndex));
#if UNITY_IOS && !UNITY_EDITOR
            CargoJam_NotifyRequestAuthorization();
#endif
            return true;
        }

        /// <summary>Replaces anything pending with the current plan. Call when the app is backgrounded or quits.</summary>
        public static void Reschedule(Progression progression)
        {
            CancelAll();
            if (!PermissionAsked || progression == null) return;
            DateTime now = DateTime.Now;
            var plan = NotificationPlan.For(progression, now);
            foreach (var n in plan)
            {
                double delay = (n.FireAt - now).TotalSeconds;
                if (delay <= 0) continue;
#if UNITY_IOS && !UNITY_EDITOR
                CargoJam_NotifySchedule(n.Id, n.Title, n.Body, delay);
#else
                if (Debug.isDebugBuild) Debug.Log("[notify] would schedule " + n);
#endif
            }
            if (plan.Count > 0) Analytics.Log(Analytics.NotifyScheduled, ("count", plan.Count));
        }

        /// <summary>Removes pending and delivered notifications. Call at launch and when the app resumes.</summary>
        public static void CancelAll()
        {
#if UNITY_IOS && !UNITY_EDITOR
            CargoJam_NotifyCancelAll();
#endif
        }
    }
}
