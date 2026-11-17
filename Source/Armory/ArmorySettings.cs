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
        public int manualCooldownTicks = 15000;
        public int settingsVersion = 1;

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
        }

        public void DoWindowContents(Rect inRect)
        {
            Rect viewRect = new Rect(0f, 0f, inRect.width - 18f, contentHeight);
            Widgets.BeginScrollView(inRect, ref scrollPosition, viewRect);

            Listing_Standard listing = new Listing_Standard();
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




