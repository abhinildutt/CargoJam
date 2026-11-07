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
        /// <summary>Core <c>CargoKind.Mystery</c> as an int (Meta can't reference Core; a test keeps them in step).</summary>
        public const int MysteryKind = 2;
        /// <summary>Core <c>CargoKind.Lock</c> as an int.</summary>
        public const int LockKind = 4;
        /// <summary>First level index (0-based) where Mystery cargo already appeared in v3 content (level 15).</summary>
        public const int MysteryDebutLevelIndex = 14;
        /// <summary>First level index (0-based) where padlocks already appeared in v3 content (level 25).</summary>
        public const int LockDebutLevelIndex = 24;

        /// <summary>
        /// True when the profile predates the current version: an older <see cref="ProfileData.version"/>,
        /// or a postcard / cargo-seen array that is missing or the wrong length (how an older save can load,
        /// depending on the parser).
        /// </summary>
        public static bool NeedsUpgrade(ProfileData data) =>
            data != null &&
            (data.version < ProfileData.CurrentVersion ||
             data.postcards == null || data.postcards.Length != Journey.StopCount ||
             data.cargoSeen == null || data.cargoSeen.Length != ProfileData.CargoKinds);

        /// <summary>
        /// Brings a loaded profile up to the current version in place. Returns true when anything changed
        /// (so the caller can save it). Each step runs only for saves that predate it.
        /// <list type="bullet">
        /// <item>v1 → v2: earns the postcard of every destination reached so far, including Harbor Town
        /// (<see cref="MarkReachedPostcards"/>), and records the current leg's arrival as already shown,
        /// so existing players don't get a reveal for the place they are already in.</item>
        /// <item>v2 → v3: sizes <see cref="ProfileData.cargoSeen"/> and marks the cargo they already met in
        /// v3 content as seen (<see cref="MarkCargoAlreadyMet"/>), so only genuinely new cargo gets a card.</item>
        /// </list>
        /// </summary>
        public static bool Upgrade(ProfileData data)
        {
            if (!NeedsUpgrade(data)) return false;

            data.levelIndex = Math.Max(0, data.levelIndex);
            bool postcardsBad = data.postcards == null || data.postcards.Length != Journey.StopCount;
            if (data.version < 2 || postcardsBad)
            {
                data.postcards = ResizePostcards(data.postcards);
                MarkReachedPostcards(data);
                data.lastArrivalShown = Math.Max(data.lastArrivalShown, Journey.LegFor(data.levelIndex));
            }

            data.cargoSeen = ResizeCargoSeen(data.cargoSeen);
            if (data.version < 3) MarkCargoAlreadyMet(data);

            data.version = Math.Max(data.version, ProfileData.CurrentVersion);
            return true;
        }

        /// <summary>
        /// Marks the cargo a pre-v4 player has already met: Mystery from level 15 and Lock from level 25
        /// (the levelIndex they have reached, so existing players aren't re-introduced to them).
        /// Resizes the array first when needed. Returns true when a kind was added.
        /// </summary>
        public static bool MarkCargoAlreadyMet(ProfileData data)
        {
            if (data == null) return false;
            data.cargoSeen = ResizeCargoSeen(data.cargoSeen);
            bool added = false;
            if (data.levelIndex >= MysteryDebutLevelIndex && !data.cargoSeen[MysteryKind])
            {
                data.cargoSeen[MysteryKind] = true;
                added = true;
            }
            if (data.levelIndex >= LockDebutLevelIndex && !data.cargoSeen[LockKind])
            {
                data.cargoSeen[LockKind] = true;
                added = true;
            }
            return added;
        }

        /// <summary>A cargo-seen array of exactly <see cref="ProfileData.CargoKinds"/>, keeping the known entries.</summary>
        public static bool[] ResizeCargoSeen(bool[] seen)
        {
            if (seen != null && seen.Length == ProfileData.CargoKinds) return seen;
            var sized = new bool[ProfileData.CargoKinds];
            if (seen != null) Array.Copy(seen, sized, Math.Min(seen.Length, sized.Length));
            return sized;
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
