using System;
using System.Linq;
using Verse;

namespace Armory
{
    public class ArmoryMod : Mod
    {
        public static ArmoryMod Instance;
        public static ArmorySettings Settings;

        private static bool? competitorLoaded;

        public ArmoryMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<ArmorySettings>();
        }

        public override string SettingsCategory()
        {
            return "Armory";
        }

        public override void DoSettingsWindowContents(UnityEngine.Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        /// <summary>
        /// True when automatic equipping may run: enabled by the player, and either no other
        /// weapon manager is loaded or the player explicitly overrode that warning.
        /// </summary>
        public static bool AutomationActive
        {
            get
            {
                return Settings != null &&
                       Settings.autoEquipEnabled &&
                       (!CompetitorLoaded || Settings.overrideCompetitor);
            }
        }

        /// <summary>
        /// True when another mod that automatically equips weapons is loaded. Automation stays
        /// off in that case so two managers do not fight over the same pawns.
        /// </summary>
        public static bool CompetitorLoaded
        {
            get
            {
                if (!competitorLoaded.HasValue)
                {
                    competitorLoaded = LoadedModManager.RunningModsListForReading.Any(mod =>
                        string.Equals(mod.PackageId, "snues.autoarm", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(mod.PackageId, "lordkuper.equipmentmanager", StringComparison.OrdinalIgnoreCase));
                }
                return competitorLoaded.Value;
            }
        }
    }
}
