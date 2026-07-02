using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace BetterBossRush
{
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
    }
}
