using UnityEngine;
using Verse;

namespace Armory
{
    public class ArmorySettings : ModSettings
    {
        public float upgradeThreshold = 0.10f;
        public int maxCandidates = 10;
        public int maxSidearmCandidates = 5;
        public int scanIntervalTicks = 120;
        public bool showBlocked = true;
        public bool debugLogging;
        public float rangedWeight = 1f;
        public float meleeWeight = 1f;
        public int engagementOffset;
        public float varietyStrength = 0.5f;

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
        public int settingsVersion = 4;

        private Vector2 scrollPosition;
        private float contentHeight = 1200f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref upgradeThreshold, "upgradeThreshold", 0.10f);
            Scribe_Values.Look(ref maxCandidates, "maxCandidates", 10);
            Scribe_Values.Look(ref maxSidearmCandidates, "maxSidearmCandidates", 5);
            Scribe_Values.Look(ref scanIntervalTicks, "scanIntervalTicks", 120);
            Scribe_Values.Look(ref showBlocked, "showBlocked", true);
            Scribe_Values.Look(ref debugLogging, "debugLogging", false);
            Scribe_Values.Look(ref rangedWeight, "rangedWeight", 1f);
            Scribe_Values.Look(ref meleeWeight, "meleeWeight", 1f);
            Scribe_Values.Look(ref engagementOffset, "engagementOffset", 0);
            Scribe_Values.Look(ref varietyStrength, "varietyStrength", 0.5f);

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

            if (Scribe.mode == LoadSaveMode.PostLoadInit && settingsVersion < 3)
            {
                // 0.7.11 splits the old single list limit into hand and backup limits.
                // The old default (25) and the common manual minimum (5) move to the
                // new hand default of 10; explicit other values are preserved.
                if (maxCandidates == 25 || maxCandidates == 5)
                {
                    maxCandidates = 10;
                }
                maxSidearmCandidates = 5;
                settingsVersion = 3;
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit && settingsVersion < 4)
            {
                // 0.8.0 adds range-aware scoring and variety. The field defaults are
                // intentional for existing colonies, so no value translation is needed.
                settingsVersion = 4;
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
                listing.Label("Two managers issuing equip jobs can fight over the same colonist. Enable only one unless you are testing.");
            }

            if (autoEquipEnabled)
            {
                listing.Label(string.Format("Check for upgrades every {0} ticks ({1:0.0}s)",
                    plannerIntervalTicks, plannerIntervalTicks / 60f));
                plannerIntervalTicks = Mathf.RoundToInt(listing.Slider(plannerIntervalTicks, 120f, 600f));

                listing.Label(string.Format("Wait after a weapon swap: {0} ticks ({1:0}s)",
                    swapCooldownTicks, swapCooldownTicks / 60f));
                swapCooldownTicks = Mathf.RoundToInt(listing.Slider(swapCooldownTicks, 600f, 10000f));

                listing.Label(string.Format("Minimum colonist age: {0}  (vanilla has no weapon age gate)", minimumAge));
                minimumAge = Mathf.RoundToInt(listing.Slider(minimumAge, 0f, 18f));

                listing.CheckboxLabeled("Equip drafted colonists", ref equipDrafted);
                listing.CheckboxLabeled("Respect manual weapon changes", ref respectManualEquip);
                if (respectManualEquip)
                {
                    listing.Label(string.Format("Pause after a manual weapon change: {0}s", manualCooldownTicks / 60));
                    manualCooldownTicks = Mathf.RoundToInt(listing.Slider(manualCooldownTicks, 2500f, 30000f));
                }

                listing.Label("Armory only equips weapons it can use now. It never unforbids anything automatically.");
            }

            listing.GapLine();
            listing.Label("Simple Sidearms");
            listing.GapLine();
            if (ArmoryMod.SimpleSidearmsLoaded)
            {
                listing.CheckboxLabeled("Respect Simple Sidearms hand type and forced weapons",
                    ref respectSimpleSidearms);
                listing.Label(respectSimpleSidearms
                    ? "Armory follows the colonist's Simple Sidearms hand type, leaves forced weapons alone, and will not equip the other type while the colonist carries the hand type. Backups are managed separately."
                    : "Armory ignores Simple Sidearms' hand type and forced states. Expect the two mods to fight over the hand weapon.");
                listing.Label("Simple Sidearms' primary mode is per colonist. Changing the mod's default only affects newly generated colonists; use the Simple Sidearms gizmo on an existing colonist to switch that colonist to melee or by skill.");

                listing.CheckboxLabeled("Manage backup weapons (one opposite-type weapon)", ref manageSidearms);
                if (manageSidearms)
                {
                    listing.Label("Armory keeps one hand weapon (the colonist's Simple Sidearms type) and one opposite-type backup. Ranged colonists get a melee backup; melee colonists get a ranged backup.");
                    listing.CheckboxLabeled("Upgrade a backup when a better valid one is available",
                        ref replaceWorseSidearms);
                    listing.Label("Upgrades replace only the worst carried weapon of the same type, never the only weapon of a type and never across types. If Simple Sidearms has a free slot, the new weapon is picked up first and the old one dropped after. If slots are full, the old one is dropped first and restored if the new one cannot be added. Dropped weapons stay on the ground.");
                }
            }
            else
            {
                listing.Label("Simple Sidearms is not loaded. Hand-type coexistence and backup management are inactive.");
            }

            listing.GapLine();
            listing.Label("Ranking");
            listing.GapLine();

            listing.Label(string.Format("Minimum upgrade gain: {0}%", Mathf.RoundToInt(upgradeThreshold * 100f)));
            upgradeThreshold = listing.Slider(upgradeThreshold, 0f, 0.5f);
            listing.Label("A weapon must be at least this much better before Armory recommends or equips it.");

            listing.Label(string.Format("Ranged score weight: x{0:0.00}", rangedWeight));
            rangedWeight = listing.Slider(rangedWeight, 0.5f, 2f);

            listing.Label(string.Format("Melee score weight: x{0:0.00}", meleeWeight));
            meleeWeight = listing.Slider(meleeWeight, 0.5f, 2f);
            listing.Label("Weights multiply the final score. 1.00 is neutral; raise one to favour that weapon class.");

            string bias = engagementOffset > 0 ? "+" + engagementOffset : engagementOffset.ToString();
            listing.Label(string.Format("Combat distance bias: {0} tiles", bias));
            engagementOffset = Mathf.RoundToInt(listing.Slider(engagementOffset, -5f, 5f));
            listing.Label("Negative prefers closer fights (killbox), positive prefers longer fights (open field). Default 0 is balanced.");

            listing.Label(string.Format("Squad variety: {0}%", Mathf.RoundToInt(varietyStrength * 100f)));
            varietyStrength = listing.Slider(varietyStrength, 0f, 1f);
            listing.Label("Adds a small per-colonist weapon preference and makes duplicate top choices less attractive. Set to 0% to disable both.");

            listing.Label(string.Format("Hand candidates shown per colonist: {0}", maxCandidates));
            maxCandidates = Mathf.RoundToInt(listing.Slider(maxCandidates, 5f, 100f));
            listing.Label(string.Format("Backup candidates shown per colonist: {0}", maxSidearmCandidates));
            maxSidearmCandidates = Mathf.RoundToInt(listing.Slider(maxSidearmCandidates, 1f, 50f));

            listing.CheckboxLabeled("Show blocked candidates by default", ref showBlocked);

            listing.GapLine();
            listing.Label("Performance");
            listing.Label(string.Format("Snapshot refresh interval: {0} ticks ({1:0.0}s)", scanIntervalTicks, scanIntervalTicks / 60f));
            scanIntervalTicks = Mathf.RoundToInt(listing.Slider(scanIntervalTicks, 30f, 600f));
            listing.Label("How often Armory recalculates the lists while its window is open.");

            listing.GapLine();
            listing.Label("Diagnostics");
            listing.CheckboxLabeled("Write a snapshot summary to the log when a map loads", ref debugLogging);

            float measuredHeight = listing.CurHeight;
            listing.End();
            contentHeight = Mathf.Max(400f, measuredHeight + 20f);
            Widgets.EndScrollView();
        }
    }
}




