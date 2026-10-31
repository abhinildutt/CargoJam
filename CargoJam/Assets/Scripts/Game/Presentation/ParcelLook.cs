namespace CargoJam.Game.Presentation
{
    /// <summary>How a yard parcel starts out: which blockers <see cref="ParcelView.Create"/> dresses it with.</summary>
    public struct ParcelLook
    {
        /// <summary>A mystery parcel, still under its tarp.</summary>
        public bool Hidden;
        /// <summary>Padlocked until its key is loaded.</summary>
        public bool Locked;
        /// <summary>The key to a padlocked parcel.</summary>
        public bool IsKey;
        /// <summary>Layers of ice, 0 when not frozen.</summary>
        public int Frost;
        /// <summary>Moves left for an express parcel, 0 when not express.</summary>
        public int Express;
    }
}
