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

        public bool Ready
        {
            get { return string.IsNullOrEmpty(Blocker); }
        }
    }

    /// <summary>All weapon evaluations for one pawn.</summary>
    public sealed class PawnAnalysis
    {
        public Pawn Pawn;
        public ThingWithComps Current;
        public float CurrentScore;

        /// <summary>
        /// True when the pawn's hand slot is ranged, false when melee, null when no typed
        /// slot model applies (Simple Sidearms absent). Derived from SS's primary mode, then
        /// the current weapon, then skill.
        /// </summary>
        public bool? RoleIsRanged;

        /// <summary>Human-readable source of <see cref="RoleIsRanged"/>.</summary>
        public string RoleSource;

        /// <summary>Null when the pawn is on the map; otherwise why they are unavailable.</summary>
        public string UnavailableReason;

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
            if (def == null || !RoleIsRanged.HasValue)
            {
                return false;
            }
            return RoleIsRanged.Value ? def.IsMeleeWeapon : def.IsRangedWeapon;
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
        /// Score the hand slot must beat. If the equipped weapon is the other type (SS has
        /// not switched yet, or the pawn is waiting), its score is not comparable to hand
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

    /// <summary>Aggregated unmet demand for one weapon def.</summary>
    public sealed class DemandRow
    {
        public ThingDef Def;
        public string Label;
        public int WantedHand;
        public int WantedBackup;
        public int BlockedHand;
        public int BlockedBackup;
        public int Stock;
        public int Forbidden;

        public int Wanted
        {
            get { return WantedHand + WantedBackup; }
        }

        public int Blocked
        {
            get { return BlockedHand + BlockedBackup; }
        }

        public int Shortage
        {
            get { return System.Math.Max(0, Wanted - Stock); }
        }

        /// <summary>"hand", "backup", "both", or "-" for display.</summary>
        public string Uses
        {
            get
            {
                bool hand = WantedHand > 0 || BlockedHand > 0;
                bool backup = WantedBackup > 0 || BlockedBackup > 0;
                if (hand && backup)
                {
                    return "both";
                }
                if (hand)
                {
                    return "hand";
                }
                return backup ? "backup" : "-";
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
