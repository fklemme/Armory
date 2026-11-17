using UnityEngine;
using Verse;

namespace Armory
{
    public class ArmorySettings : ModSettings
    {
        public float upgradeThreshold = 0.10f;
        public int maxCandidates = 25;
        public int scanIntervalTicks = 120;
        public bool showBlocked = true;
        public bool debugLogging;
        public float rangedWeight = 1f;
        public float meleeWeight = 1f;

        public bool autoEquipEnabled;
        public bool overrideCompetitor;
        public int plannerIntervalTicks = 250;
        public int swapCooldownTicks = 2500;
        public int minimumAge = 3;
        public bool equipDrafted;
        public bool respectManualEquip = true;
        public bool respectSimpleSidearms = true;
        public bool manageSidearms;
        public bool replaceWorseSidearms = true;
        public int manualCooldownTicks = 15000;
        public int settingsVersion = 2;

        private Vector2 scrollPosition;
        private float contentHeight = 1200f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref upgradeThreshold, "upgradeThreshold", 0.10f);
            Scribe_Values.Look(ref maxCandidates, "maxCandidates", 25);
            Scribe_Values.Look(ref scanIntervalTicks, "scanIntervalTicks", 120);
            Scribe_Values.Look(ref showBlocked, "showBlocked", true);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref rangedWeight, "rangedWeight", 1f);
            Scribe_Values.Look(ref meleeWeight, "meleeWeight", 1f);

            Scribe_Values.Look(ref autoEquipEnabled, "autoEquipEnabled", false);
            Scribe_Values.Look(ref overrideCompetitor, "overrideCompetitor", false);
            Scribe_Values.Look(ref plannerIntervalTicks, "plannerIntervalTicks", 250);
            Scribe_Values.Look(ref swapCooldownTicks, "swapCooldownTicks", 2500);
            Scribe_Values.Look(ref minimumAge, "minimumAge", 3);
            Scribe_Values.Look(ref equipDrafted, "equipDrafted", false);
            Scribe_Values.Look(ref respectManualEquip, "respectManualEquip", true);
            Scribe_Values.Look(ref respectSimpleSidearms, "respectSimpleSidearms", true);
            Scribe_Values.Look(ref manageSidearms, "manageSidearms", false);
            Scribe_Values.Look(ref replaceWorseSidearms, "replaceWorseSidearms", true);
            Scribe_Values.Look(ref manualCooldownTicks, "manualCooldownTicks", 15000);
            Scribe_Values.Look(ref settingsVersion, "settingsVersion", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit && settingsVersion < 1)
            {
                // 0.2.1 shipped with a default minimum age of 13; lower it once so
                // existing colonies pick up the corrected child threshold.
                if (minimumAge == 13)
                {
                    minimumAge = 3;
                }
                settingsVersion = 1;
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && settingsVersion < 2)
            {
                // 0.6.0 replaces "total desired sidearms" with one hand + one opposite-type
                // backup, and makes per-type upgrades part of the default behaviour.
                replaceWorseSidearms = true;
                settingsVersion = 2;
            }
        }

        public void DoWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
            // Without this, Listing_Standard moves overflow controls into a second column
            // (off the right edge of the settings window) once the content grows past the
            // measured scroll height. Toggling a setting could therefore hide everything
            // below it.
            listing.maxOneColumn = true;
            listing.Begin(viewRect);

            listing.Label("Automation");
            listing.GapLine();
            listing.CheckboxLabeled("Enable automatic equipping", ref autoEquipEnabled);

            if (ArmoryMod.CompetitorLoaded)
            {
                listing.CheckboxLabeled("Run anyway while another weapon manager is loaded", ref overrideCompetitor);
                listing.Label("Two managers issuing equip jobs can fight over the same pawn. Enable only one unless you are testing.");
            }

            if (autoEquipEnabled)
            {
                listing.Label(string.Format("Planner interval: {0} ticks", plannerIntervalTicks));
                plannerIntervalTicks = Mathf.RoundToInt(listing.Slider(plannerIntervalTicks, 120f, 600f));

                listing.Label(string.Format("Swap cooldown: {0} ticks ({1:0}s)",
                    swapCooldownTicks, swapCooldownTicks / 60f));
                swapCooldownTicks = Mathf.RoundToInt(listing.Slider(swapCooldownTicks, 600f, 10000f));

                listing.Label(string.Format("Minimum colonist age: {0}  (vanilla has no weapon age gate)", minimumAge));
                minimumAge = Mathf.RoundToInt(listing.Slider(minimumAge, 0f, 18f));

                listing.CheckboxLabeled("Equip drafted colonists", ref equipDrafted);
                listing.CheckboxLabeled("Respect manual weapon changes", ref respectManualEquip);
                if (respectManualEquip)
                {
                    listing.Label(string.Format("Manual-change pause: {0}s", manualCooldownTicks / 60));
                    manualCooldownTicks = Mathf.RoundToInt(listing.Slider(manualCooldownTicks, 2500f, 30000f));
                }

                listing.Label("Only ready weapons are used. Armory never unforbids automatically.");
            }

            listing.GapLine();
            listing.Label("Simple Sidearms");
            listing.GapLine();
            if (ArmoryMod.SimpleSidearmsLoaded)
            {
                listing.CheckboxLabeled("Respect Simple Sidearms hand role and forced weapons",
                    ref respectSimpleSidearms);
                listing.Label(respectSimpleSidearms
                    ? "Armory follows the pawn's SS hand type, leaves forced weapons alone, and will not equip the other type while the pawn carries the role type. Backups are managed separately."
                    : "Armory ignores SS's hand type and forced states. Expect the two mods to fight over the hand weapon.");
                listing.Label("Simple Sidearms' primary mode is per pawn. Changing SS's default only affects newly generated pawns; use the SS gizmo on an existing colonist to switch that pawn to melee or by-skill.");

                listing.CheckboxLabeled("Manage backup weapons (one opposite-type weapon)", ref manageSidearms);
                if (manageSidearms)
                {
                    listing.Label("Armory keeps one hand weapon (the pawn's Simple Sidearms type) and one opposite-type backup. Ranged pawns get a melee backup; melee pawns get a ranged backup.");
                    listing.CheckboxLabeled("Upgrade a backup when a better valid one is available",
                        ref replaceWorseSidearms);
                    listing.Label("Upgrades replace only the worst carried weapon of the same type, never the only weapon of a type and never across types. If Simple Sidearms has a free slot, the new weapon is picked up first and the old one dropped after. If slots are full, the old one is dropped first and restored if the new one cannot be added. Dropped weapons stay on the ground.");
                }
            }
            else
            {
                listing.Label("Simple Sidearms is not loaded. Primary coexistence and sidearm management are inactive.");
            }

            listing.GapLine();
            listing.Label("Ranking");
            listing.GapLine();

            listing.Label(string.Format("Upgrade threshold: {0}%", Mathf.RoundToInt(upgradeThreshold * 100f)));
            upgradeThreshold = listing.Slider(upgradeThreshold, 0f, 0.5f);

            listing.Label(string.Format("Ranged preference: x{0:0.00}", rangedWeight));
            rangedWeight = listing.Slider(rangedWeight, 0.5f, 2f);

            listing.Label(string.Format("Melee preference: x{0:0.00}", meleeWeight));
            meleeWeight = listing.Slider(meleeWeight, 0.5f, 2f);

            listing.Label(string.Format("Ranked weapons shown per pawn: {0}", maxCandidates));
            maxCandidates = Mathf.RoundToInt(listing.Slider(maxCandidates, 5f, 100f));

            listing.CheckboxLabeled("Show blocked weapons by default", ref showBlocked);

            listing.GapLine();
            listing.Label("Performance");
            listing.Label(string.Format("Rescan interval: {0} ticks ({1:0.0}s)", scanIntervalTicks, scanIntervalTicks / 60f));
            scanIntervalTicks = Mathf.RoundToInt(listing.Slider(scanIntervalTicks, 30f, 600f));

            listing.GapLine();
            listing.Label("Diagnostics");
            listing.CheckboxLabeled("Log snapshot summary on map load", ref debugLogging);

            float measuredHeight = listing.CurHeight;
            listing.End();
            contentHeight = Mathf.Max(400f, measuredHeight + 20f);
            Widgets.EndScrollView();
        }
    }
}




