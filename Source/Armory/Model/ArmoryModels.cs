using System.Collections.Generic;
using Armory.Compatibility;
using Verse;

namespace Armory
{
    /// <summary>A weapon instance found on the current map, with its pawn-independent power.</summary>
    public sealed class WeaponInfo
    {
        public ThingWithComps Thing;
        public string Label;
        public bool IsRanged;
        public float Power;
        public float Range;
    }

    /// <summary>One weapon evaluated for one pawn.</summary>
    public sealed class Candidate
    {
        public WeaponInfo Weapon;
        public float Score;

        /// <summary>Null when the pawn could pick this up right now.</summary>
        public string Blocker;

        /// <summary>True when no player action can fix it (biocoded, role, bonded).</summary>
        public bool Hard;

        public string Ledger;

        public bool Ready
        {
            get { return string.IsNullOrEmpty(Blocker); }
        }
    }

    /// <summary>A weapon already in the pawn's inventory, with its backup-slot score.</summary>
    public sealed class CarriedSidearm
    {
        public ThingWithComps Thing;
        public float Score;
    }

    /// <summary>
    /// One weapon evaluated for the pawn's backup slot (the opposite type from the hand
    /// weapon). Kept separate from hand candidates so ranged and melee are never ranked
    /// against each other.
    /// </summary>
    public sealed class SidearmCandidate
    {
        public WeaponInfo Weapon;
        public float Score;

        /// <summary>Null when the pawn could pick this up as a backup right now.</summary>
        public string Blocker;

        /// <summary>
        /// True when the only reason this is not ready is Simple Sidearms' slot or
        /// free-space cap, so dropping the worst carried backup could make room.
        /// </summary>
        public bool UpgradeAfterDrop;

        /// <summary>
        /// True when the pawn already has this weapon def in the hand or inventory.
        /// Armory must not add a second copy of the same weapon type.
        /// </summary>
        public bool AlreadyCarried;

        public bool Ready
        {
            get { return string.IsNullOrEmpty(Blocker); }
        }
    }

    /// <summary>Hand weapon type, or Auto when Armory should derive it.</summary>
    public enum HandType
    {
        Auto = 0,
        Ranged = 1,
        Melee = 2
    }

    /// <summary>All weapon evaluations for one pawn.</summary>
    public sealed class PawnAnalysis
    {
        public Pawn Pawn;
        public ThingWithComps Current;
        public float CurrentScore;

        /// <summary>
        /// Resolved hand type: true for ranged, false for melee. Armory always resolves a
        /// type now; Simple Sidearms owns it when active and respected, otherwise passion,
        /// traits, skills and the player's per-pawn override do.
        /// </summary>
        public bool? RoleIsRanged;

        /// <summary>Human-readable source of <see cref="RoleIsRanged"/>.</summary>
        public string RoleSource;

        /// <summary>The player's per-pawn hand-type choice, or Auto.</summary>
        public HandType HandTypeOverride;

        /// <summary>True when Simple Sidearms is active and respected, so it owns the hand type.</summary>
        public bool HandTypeFromSimpleSidearms;

        /// <summary>True when the equipped weapon is the other type and should be swapped.</summary>
        public bool HandTypeMismatch;

        /// <summary>Resolved ranged combat role for this pawn.</summary>
        public CombatRole CombatRole;

        /// <summary>Preferred engagement distance in tiles.</summary>
        public int PreferredRange;

        /// <summary>True when the player fixed the role instead of deriving it.</summary>
        public bool RoleOverridden;

        /// <summary>Human-readable origin of <see cref="CombatRole"/>.</summary>
        public string CombatRoleSource;

        /// <summary>Null when the pawn is on the map; otherwise why they are unavailable.</summary>
        public string UnavailableReason;

        /// <summary>
        /// Why automation is not acting on this pawn, or null when it may act. Shown in
        /// the colonist list/detail so the status is never unexplained.
        /// </summary>
        public string AutomationNote;

        /// <summary>True when the note is a permanent/hard gate (pacifist, age, downed...).</summary>
        public bool AutomationBlocked;

        /// <summary>
        /// True when the pawn's bonded persona weapon is currently equipped, so Armory
        /// must not replace the hand weapon. Backups can still be managed.
        /// </summary>
        public bool HandLocked;

        public string HandLockReason;

        /// <summary>Weapons carried in the pawn's inventory (Simple Sidearms sidearms and other carried weapons).</summary>
        public readonly List<ThingWithComps> Sidearms = new List<ThingWithComps>();

        public readonly List<CarriedSidearm> CarriedSidearms = new List<CarriedSidearm>();

        public string SidearmText;

        /// <summary>Simple Sidearms primary/forced state. Null when SS is absent or has no memory comp.</summary>
        public SimpleSidearmsAdapter.PawnState SidearmState;

        /// <summary>
        /// Best weapon the pawn already carries for Simple Sidearms' hand role.
        /// Used as the hand upgrade baseline so Armory does not queue a downgrade SS would undo.
        /// </summary>
        public ThingWithComps BestCarriedPrimary;

        public float BestCarriedPrimaryScore;

        /// <summary>Best opposite-type weapon already in the pawn's inventory (backup baseline).</summary>
        public ThingWithComps BestCarriedBackup;

        public float BestCarriedBackupScore;

        /// <summary>Loose weapons evaluated for the backup slot, best first.</summary>
        public readonly List<SidearmCandidate> SidearmCandidates = new List<SidearmCandidate>();

        public bool Available
        {
            get { return string.IsNullOrEmpty(UnavailableReason); }
        }

        /// <summary>
        /// True when a hand type was resolved. Armory always resolves one for analyzed
        /// pawns; the property remains for callers that still treat a missing type as
        /// "not managed".
        /// </summary>
        public bool HasRole
        {
            get { return RoleIsRanged.HasValue; }
        }

        /// <summary>True when the weapon belongs in this pawn's hand slot.</summary>
        public bool IsHandType(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (!RoleIsRanged.HasValue)
            {
                return true;
            }
            return RoleIsRanged.Value ? def.IsRangedWeapon : def.IsMeleeWeapon;
        }

        /// <summary>True when the weapon belongs in this pawn's opposite-type backup slot.</summary>
        public bool IsBackupType(ThingDef def)
        {
            if (def == null || !RoleIsRanged.HasValue || !SimpleSidearmsAdapter.Active)
            {
                return false;
            }
            return RoleIsRanged.Value ? def.IsMeleeWeapon : def.IsRangedWeapon;
        }

        /// <summary>
        /// True when the equipped hand or any carried weapon already uses this def.
        /// Used to stop the planner and Demand from adding a second gladius/axe/etc.
        /// </summary>
        public bool AlreadyCarries(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (Current != null && Current.def == def)
            {
                return true;
            }
            for (int i = 0; i < CarriedSidearms.Count; i++)
            {
                ThingWithComps thing = CarriedSidearms[i].Thing;
                if (thing != null && thing.def == def)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Weapons this pawn could use in the hand slot, best first. May still be blocked.</summary>
        public readonly List<Candidate> Ranked = new List<Candidate>();

        /// <summary>Hand-slot weapons this pawn can never use (biocoded elsewhere, role, bonded).</summary>
        public readonly List<Candidate> Incompatible = new List<Candidate>();

        public Candidate TopOverall
        {
            get { return Ranked.Count > 0 ? Ranked[0] : null; }
        }

        public Candidate TopReady
        {
            get
            {
                for (int i = 0; i < Ranked.Count; i++)
                {
                    if (Ranked[i].Ready)
                    {
                        return Ranked[i];
                    }
                }
                return null;
            }
        }

        public float TopDeltaPct
        {
            get { return DeltaPct(TopOverall); }
        }

        public float TopReadyDeltaPct
        {
            get { return DeltaPct(TopReady); }
        }

        public float DeltaPct(Candidate candidate)
        {
            if (candidate == null)
            {
                return 0f;
            }
            float baseline = HandBaselineScore;
            if (baseline <= 0.01f)
            {
                return 1f;
            }
            return candidate.Score / baseline - 1f;
        }

        /// <summary>
        /// Score the hand slot must beat. If the equipped weapon is the other type (the hand
        /// type changed, or SS has not switched yet), its score is not comparable to hand
        /// candidates and is excluded; the best carried hand weapon is the baseline instead.
        /// </summary>
        public float HandBaselineScore
        {
            get
            {
                if (!RoleIsRanged.HasValue)
                {
                    return CurrentScore;
                }
                bool currentIsHand = Current != null && IsHandType(Current.def);
                float baseline = BestCarriedPrimaryScore;
                if (currentIsHand && CurrentScore > baseline)
                {
                    baseline = CurrentScore;
                }
                return baseline;
            }
        }
    }

    /// <summary>A weapon on the map, plus the pawns that would use it in each slot.</summary>
    public sealed class WeaponAnalysis
    {
        public WeaponInfo Weapon;

        /// <summary>Best pawn that can use it in the hand slot right now.</summary>
        public PawnAnalysis BestHandPawn;
        public float BestHandScore;

        /// <summary>Best pawn that could use it in the hand slot but is blocked.</summary>
        public PawnAnalysis BestAnyHandPawn;
        public float BestAnyHandScore;

        /// <summary>Best pawn that can carry it in the backup slot right now.</summary>
        public PawnAnalysis BestBackupPawn;
        public float BestBackupScore;

        /// <summary>Best pawn that could carry it in the backup slot but is blocked.</summary>
        public PawnAnalysis BestAnyBackupPawn;
        public float BestAnyBackupScore;

        public string Status;
        public readonly List<Candidate> PawnCandidates = new List<Candidate>();
    }

    /// <summary>Where a demand row belongs in the tab, in action-priority order.</summary>
    public enum DemandSection
    {
        /// <summary>Recipe available now; the colony can craft it immediately.</summary>
        Craft = 0,

        /// <summary>An unfinished research project unlocks crafting.</summary>
        Research = 1,

        /// <summary>No crafting route yet; a trader is the only source.</summary>
        Buy = 2
    }

    /// <summary>One colonist contributing to a demand row.</summary>
    public sealed class DemandWant
    {
        public string Colonist;
        public bool Hand;
        public float Gain;

        /// <summary>This colonist's contribution to the row's total score.</summary>
        public float Score;

        /// <summary>True when the pawn has no weapon for that slot, so there is no baseline.</summary>
        public bool IsNew;

        /// <summary>True when the pawn holds the other hand type, so this would switch type.</summary>
        public bool TypeSwitch;

        /// <summary>True when this def is the pawn's best option within its section.</summary>
        public bool IsTop;

        /// <summary>Weapon the pawn currently uses for that slot, or "nothing".</summary>
        public string CurrentLabel;

        /// <summary>Combat role label at planning time.</summary>
        public string Role;

        /// <summary>Preferred engagement distance at planning time.</summary>
        public int PreferredRange;
    }

    /// <summary>
    /// Ideal loadout minus owned copies for one weapon def. Rows are independent options:
    /// a colonist appears in every row that would improve their slot, so the wants of
    /// different rows overlap. Owned copies include equipped, carried and loose weapons.
    /// </summary>
    public sealed class DemandRow
    {
        public ThingDef Def;
        public string Label;
        public int WantedHand;
        public int WantedBackup;
        public int Owned;
        public int Forbidden;
        public DemandSection Section;

        /// <summary>Colonists for whom this is the best option within its section.</summary>
        public int BestCount;

        public bool CraftableNow;
        public bool Buyable;

        /// <summary>
        /// True when the recipe is unlocked, at least one copy is missing, and a valid
        /// player-owned bench is built on the current map.
        /// </summary>
        public bool CanCraft;
        public string BenchLabel;
        public string MissingResearch;

        /// <summary>Normal-quality, pawn-independent weapon strength for display.</summary>
        public float Power;

        /// <summary>Maximum firing range in tiles; zero for melee weapons.</summary>
        public float Range;

        /// <summary>Sum of relative gains; used as a secondary ordering key.</summary>
        public float TotalGain;

        /// <summary>
        /// Sum of range-aware candidate scores across the contributing colonists.
        /// Primary ordering key inside a section: role fit, effective DPS and breadth
        /// all matter.
        /// </summary>
        public float TotalScore;

        /// <summary>Sum of gains that have a baseline (weapons, not empty slots).</summary>
        public float KnownGain;

        public int KnownCount;
        public int NewCount;
        public int TypeSwitchCount;

        public readonly List<DemandWant> Wants = new List<DemandWant>();

        public int Wanted
        {
            get { return WantedHand + WantedBackup; }
        }

        /// <summary>Copies that can be used without player action; forbidden stock excluded.</summary>
        public int Available
        {
            get { return System.Math.Max(0, Owned - Forbidden); }
        }

        public int Need
        {
            get { return System.Math.Max(0, Wanted - Available); }
        }

        public float AverageGain
        {
            get { return KnownCount > 0 ? KnownGain / KnownCount : 0f; }
        }

        /// <summary>"Hand", "Backup", or "2 Hand · 1 Backup" for display.</summary>
        public string Uses
        {
            get
            {
                if (WantedHand > 0 && WantedBackup > 0)
                {
                    return WantedHand + " Hand · " + WantedBackup + " Backup";
                }
                return WantedHand > 0 ? "Hand" : "Backup";
            }
        }
    }

    /// <summary>Everything the tab draws, rebuilt on a timer.</summary>
    public sealed class ArmorySnapshot
    {
        public Map Map;
        public int BuiltTick;
        public readonly List<WeaponInfo> Weapons = new List<WeaponInfo>();
        public readonly List<PawnAnalysis> Pawns = new List<PawnAnalysis>();
        public readonly List<WeaponAnalysis> WeaponRows = new List<WeaponAnalysis>();
        public readonly List<DemandRow> Demand = new List<DemandRow>();
    }
}
