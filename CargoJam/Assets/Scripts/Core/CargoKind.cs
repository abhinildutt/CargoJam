namespace CargoJam.Core
{
    /// <summary>
    /// The special cargo a level can use, in debut order. The int values are persisted in the player's
    /// profile (which kinds have been introduced), so never reorder or renumber them; only append.
    /// </summary>
    public enum CargoKind
    {
        /// <summary>'#': a permanent obstacle.</summary>
        Bollard = 0,
        /// <summary>A frozen crate, which thaws after a number of moves.</summary>
        Frost = 1,
        /// <summary>A covered parcel whose color shows once it is free.</summary>
        Mystery = 2,
        /// <summary>A parcel that must leave the yard within a number of moves.</summary>
        Express = 3,
        /// <summary>A padlocked parcel and its key.</summary>
        Lock = 4,
        /// <summary>Two strapped parcels that travel together.</summary>
        Strap = 5,
        /// <summary>'+': a customs gate that opens after enough loads of its color.</summary>
        Gate = 6,
    }
}
