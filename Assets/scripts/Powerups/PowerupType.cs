/// <summary>
/// Defines the types of field power-ups available to the player.
/// </summary>
public enum PowerupType
{
    ArrowShower,
    Fence,
    StoneShower,
    ShieldDome,
    FireArrows,
    WoodRolling,
    BigFireball,
    ArcherTower,
    ReinforcementCart,
    SteelTrap
}

/// <summary>
/// The 4 distinct gameplay categories of power-ups.
/// Each category contains non-overlapping power-ups with unique battlefield roles.
/// </summary>
public enum PowerupCategory
{
    /// <summary>Direct bombardment from above (e.g. Arrow Shower, Fire Arrows, Stone Shower)</summary>
    AirAssault = 0,

    /// <summary>Heavy area destruction and ground crowd-control (e.g. Fireball, The Log / Wood Rolling)</summary>
    HeavyDamage = 1,

    /// <summary>Barricades and concealed area denial traps (e.g. Fence, Spike Trap)</summary>
    TacticalDefense = 2,

    /// <summary>Support summons and battlefield reinforcements (e.g. Archer Tower, Shield Dome, Troop Cart)</summary>
    SupportSummon = 3
}
