using System;

namespace CargoJam.Meta
{
    /// <summary>
    /// Upgrades saved profiles to <see cref="ProfileData.CurrentVersion"/>. Engine-free so the rules
    /// can be tested; <c>ProfileStore</c> runs it after loading and <see cref="Progression"/> runs it
    /// again on construction (it is idempotent).
    /// </summary>
    public static class ProfileMigration
    {
        /// <summary>
        /// True when the profile predates v2: an older <see cref="ProfileData.version"/>, or a postcard
        /// array that is missing or the wrong length (how a v1 save can load, depending on the parser).
        /// </summary>
        public static bool NeedsUpgrade(ProfileData data) =>
            data != null &&
            (data.version < ProfileData.CurrentVersion || data.postcards == null || data.postcards.Length != Journey.StopCount);

        /// <summary>
        /// Brings a loaded profile up to the current version in place. Returns true when anything changed
        /// (so the caller can save it).
        /// <list type="bullet">
        /// <item>v1 → v2: earns the postcard of every destination reached so far, including Harbor Town
        /// (<see cref="MarkReachedPostcards"/>), and records the current leg's arrival as already shown,
        /// so existing players don't get a reveal for the place they are already in.</item>
        /// </list>
        /// </summary>
        public static bool Upgrade(ProfileData data)
        {
            if (!NeedsUpgrade(data)) return false;

            data.levelIndex = Math.Max(0, data.levelIndex);
            data.postcards = ResizePostcards(data.postcards);
            MarkReachedPostcards(data);
            data.lastArrivalShown = Math.Max(data.lastArrivalShown, Journey.LegFor(data.levelIndex));
            data.version = Math.Max(data.version, ProfileData.CurrentVersion);
            return true;
        }

        /// <summary>
        /// Marks the postcard of every destination the progress index has reached: stops
        /// 0..min(<see cref="Journey.LegFor"/>, StopCount-1), so a player on a later loop has them all.
        /// Resizes the array first when needed. Returns true when a postcard was added.
        /// </summary>
        public static bool MarkReachedPostcards(ProfileData data)
        {
            if (data == null) return false;
            data.postcards = ResizePostcards(data.postcards);
            int last = Math.Min(Journey.LegFor(data.levelIndex), Journey.StopCount - 1);
            bool added = false;
            for (int stop = 0; stop <= last; stop++)
            {
                if (data.postcards[stop]) continue;
                data.postcards[stop] = true;
                added = true;
            }
            return added;
        }

        /// <summary>A postcard array of exactly <see cref="Journey.StopCount"/>, keeping the known entries.</summary>
        public static bool[] ResizePostcards(bool[] postcards)
        {
            if (postcards != null && postcards.Length == Journey.StopCount) return postcards;
            var sized = new bool[Journey.StopCount];
            if (postcards != null) Array.Copy(postcards, sized, Math.Min(postcards.Length, sized.Length));
            return sized;
        }
    }
}
