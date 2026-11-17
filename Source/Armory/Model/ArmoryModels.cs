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

    /// <summary>A weapon already in the pawn's inventory, with its sidearm score.</summary>
    public sealed class CarriedSidearm
    {
        public ThingWithComps Thing;
        public float Score;
    }

    /// <summary>One sidearm evaluated for one pawn. Kept separate from primary candidates.</summary>
    public sealed class SidearmCandidate
    {
        public WeaponInfo Weapon;
        public float Score;

        /// <summary>Null when the pawn could pick this up as a sidearm right now.</summary>
        public string Blocker;

        /// <summary>True when this sidearm is the opposite type from the pawn's primary role.</summary>
        public bool Diverse;

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

        /// <summary>Null when the pawn is on the map; otherwise why they are unavailable.</summary>
        public string UnavailableReason;

        /// <summary>Weapons carried in the pawn's inventory (Simple Sidearms sidearms and other carried weapons).</summary>
        public readonly List<ThingWithComps> Sidearms = new List<ThingWithComps>();

        public readonly List<CarriedSidearm> CarriedSidearms = new List<CarriedSidearm>();

        public string SidearmText;

        /// <summary>Simple Sidearms primary/forced state. Null when SS is absent or has no memory comp.</summary>
        public SimpleSidearmsAdapter.PawnState SidearmState;

        /// <summary>
        /// Best weapon the pawn already carries in Simple Sidearms' preferred primary role.
        /// Used as the upgrade baseline so Armory does not queue a downgrade SS would undo.
        /// </summary>
        public ThingWithComps BestCarriedPrimary;

        public float BestCarriedPrimaryScore;

        /// <summary>Loose weapons evaluated as Simple Sidearms sidearms, best first.</summary>
        public readonly List<SidearmCandidate> SidearmCandidates = new List<SidearmCandidate>();

        public bool Available
        {
            get { return string.IsNullOrEmpty(UnavailableReason); }
        }

        /// <summary>Weapons this pawn could use in principle, best first. May still be blocked.</summary>
        public readonly List<Candidate> Ranked = new List<Candidate>();

        /// <summary>Weapons this pawn can never use (biocoded elsewhere, role, bonded).</summary>
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
            if (CurrentScore <= 0.01f)
            {
                return 1f;
            }
            return candidate.Score / CurrentScore - 1f;
        }
    }

    /// <summary>A weapon on the map, plus the pawn that would benefit most from it.</summary>
    public sealed class WeaponAnalysis
    {
        public WeaponInfo Weapon;
        public PawnAnalysis BestPawn;
        public float BestScore;
        public PawnAnalysis BestAnyPawn;
        public float BestAnyScore;

        /// <summary>Best pawn that can pick this up as a sidearm right now.</summary>
        public PawnAnalysis BestSidearmPawn;
        public float BestSidearmScore;

        /// <summary>Best pawn that could carry it as a sidearm but is blocked.</summary>
        public PawnAnalysis BestAnySidearmPawn;
        public float BestAnySidearmScore;

        public string Status;
        public readonly List<Candidate> PawnCandidates = new List<Candidate>();
    }

    /// <summary>Aggregated unmet demand for one weapon def.</summary>
    public sealed class DemandRow
    {
        public ThingDef Def;
        public string Label;
        public int Wanted;
        public int Blocked;
        public int Stock;
        public int Forbidden;

        public int Shortage
        {
            get { return System.Math.Max(0, Wanted - Stock); }
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
