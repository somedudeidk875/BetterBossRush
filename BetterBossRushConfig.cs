using System;
using System.Collections.Generic;
using System.ComponentModel;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;

namespace BetterBossRush
{
    // difficulty condition for a custom exemption, "any" means no difficulty requirement
    public enum ExemptionDifficulty
    {
        Any,
        Expert,
        Master,
        Revengeance,
        Death,
        Infernum,
        Eternity,
        Masochist,
        EternityRevengeance,
        MasochistDeath,
        Ragnarok
    }

    // a single player-defined exemption: which boss, plus optional conditions. both conditions must pass for the exemption to apply
    public class ExemptionEntry
    {
        [Label("Boss")]
        [Tooltip("The boss to exempt. Exempt bosses wait until the arena is empty, then spawn alone.\nBosses from mods you don't have installed are ignored automatically, so you don't need to worry about them.\nThis mod prints out the full list of bosses and their NPC IDs in client.log. There you can easily find the internal NPC you're looking for.")]
        public NPCDefinition Boss { get; set; } = new NPCDefinition();

        [Label("Only If Difficulty Is")]
        [DefaultValue(ExemptionDifficulty.Any)]
        [Tooltip("Only exempt this boss while the chosen difficulty is active.\n'Any' means no difficulty requirement. (default:Any)")]
        public ExemptionDifficulty RequiredDifficulty { get; set; } = ExemptionDifficulty.Any;

        [Label("Only If Mod Is Installed")]
        [DefaultValue("")]
        [Tooltip("Only exempt this boss while the given mod is installed.\nUse the mod's INTERNAL name (e.g. CalamityHunt, NoxusBoss, SOTS) - the name shown in client.log when the mod loads.\nLeave blank for no mod requirement.")]
        public string RequiredMod { get; set; } = "";

        // tmodloader compares config objects for change detection and multiplayer sync, so custom config classes need proper value equality.
        public override bool Equals(object obj)
        {
            if (obj is ExemptionEntry other)
            {
                return object.Equals(Boss, other.Boss)
                    && RequiredDifficulty == other.RequiredDifficulty
                    && RequiredMod == other.RequiredMod;
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Boss, RequiredDifficulty, RequiredMod);
        }
    }

    public class BetterBossRushConfig : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        [Header("Headers.BlitzSettings")]

        [ReloadRequired]
        [Label("Tier 1")]
        [DefaultValue(true)]
        [Tooltip("Controls whether tier 1 is a blitz (default:true)")]
        public bool Tier1Blitz { get; set; }

        [ReloadRequired]
        [Label("Tier 2")]
        [DefaultValue(true)]
        [Tooltip("Controls whether tier 2 is a blitz (default:true)")]
        public bool Tier2Blitz { get; set; }

        [ReloadRequired]
        [Label("Tier 3")]
        [DefaultValue(false)]
        [Tooltip("Controls whether tier 3 is a blitz (default:false)")]
        public bool Tier3Blitz { get; set; }

        [ReloadRequired]
        [Label("Tier 4")]
        [DefaultValue(false)]
        [Tooltip("Controls whether tier 4 is a blitz (default:false)")]
        public bool Tier4Blitz { get; set; }

        [ReloadRequired]
        [Label("Tier 5")]
        [DefaultValue(false)]
        [Tooltip("Controls whether tier 5 is a blitz (default:false)")]
        public bool Tier5Blitz { get; set; }

        [ReloadRequired]
        [Label("Tier 6")]
        [DefaultValue(false)]
        [Tooltip("Controls whether IEOR's tier 6 is a blitz. Does nothing if IEOR is not enabled (default:false)")]
        public bool Tier6Blitz { get; set; }

        [Header("Headers.SlotSettings")]

        [ReloadRequired]
        [Range(1, 5)]
        [DefaultValue(3)]
        [Tooltip("Controls how many bosses can be alive at once during tier 1 (does nothing if this tier's blitz toggle is set to false) (default:3)")]
        public int SlotsTier1 { get; set; }

        [ReloadRequired]
        [Range(1, 5)]
        [DefaultValue(2)]
        [Tooltip("Controls how many bosses can be alive at once during tier 2 (does nothing if this tier's blitz toggle is set to false) (default:2)")]
        public int SlotsTier2 { get; set; }

        [ReloadRequired]
        [Range(1, 5)]
        [DefaultValue(2)]
        [Tooltip("Controls how many bosses can be alive at once during tier 3 (does nothing if this tier's blitz toggle is set to false) (default:2)")]
        public int SlotsTier3 { get; set; }

        [ReloadRequired]
        [Range(1, 5)]
        [DefaultValue(2)]
        [Tooltip("Controls how many bosses can be alive at once during tier 4 (does nothing if this tier's blitz toggle is set to false) (default:2)")]
        public int SlotsTier4 { get; set; }

        [ReloadRequired]
        [Range(1, 5)]
        [DefaultValue(2)]
        [Tooltip("Controls how many bosses can be alive at once during tier 5 (does nothing if this tier's blitz toggle is set to false) (default:2)")]
        public int SlotsTier5 { get; set; }

        [ReloadRequired]
        [Range(1, 5)]
        [DefaultValue(2)]
        [Tooltip("Controls how many bosses can be alive at once during IEOR's tier 6 (does nothing if this tier's blitz toggle is set to false and/or IEOR is not enabled) (default:2)")]
        public int SlotsTier6 { get; set; }

        // tooltips already explain most stuff
        [Header("Headers.ExemptionSettings")]

        [Label("Use Built-in Exemptions")]
        [DefaultValue(true)]
        [Tooltip("Keeps the mod's built-in exemption list (bosses that break the blitz, e.g. ones that teleport you somewhere else, ones that create small arenas, etc).\nTurn this off to use ONLY your own exemptions below.\nSince I don't know if there's a way to do this here, a list of the default exemption list is on this mod's workshop page.\nNote: the last boss of each tier is always exempt no matter what, since tier progression depends on it. (default:true)")]
        public bool UseDefaultExemptions { get; set; }

        [Label("Disabled Built-in Exemptions")]
        [Tooltip("Bosses here will NOT be exempt, even if they're one of the mod's built-in exemptions.\nUse this to un-exempt a single default instead of turning all of them off.\nHas no effect on the last boss of a tier, which is always exempt.\nThis mod prints out the full list of bosses and their NPC IDs in client.log. There you can easily find the internal NPC you're looking for.")]
        public List<NPCDefinition> DisabledDefaultExemptions { get; set; } = new List<NPCDefinition>();

        [Label("Custom Exemptions")]
        [Tooltip("Bosses you want fought alone. An exempt boss waits until the arena is clear, then spawns by itself.\nEach entry can optionally require a difficulty and/or an installed mod; if you set both, BOTH must be true.")]
        [SeparatePage]
        public List<ExemptionEntry> CustomExemptions { get; set; } = new List<ExemptionEntry>();

        // rebuild the live exemption list the moment the config changes, because im getting really sick of having to reload my mod every single time
        // guarded because OnChanged also fires while still at the main menu, before the boss roster and tier map exist
        public override void OnChanged()
        {
            if (!Main.gameMenu && BetterBossRushSystem.TierEndIndices.Count > 0)
            {
                BetterBossRushSystem.RebuildExemptions();
            }
        }
    }
}
