using System.Collections.Generic;
using Verse;

namespace Armory
{
    /// <summary>What the automation did; the Log tab maps this to a label and colour.</summary>
    public enum DecisionKind
    {
        EquipHand,
        EquipBackup,
        SkipUpgrade,
        DropBackup,
        DropFailed,
        RestoreBackup
    }

    /// <summary>Session-only decision log for the automation tab.</summary>
    public class ArmoryGameComponent : GameComponent
    {
        public sealed class Decision
        {
            public int Tick;
            public string Colonist;
            public string Weapon;
            public DecisionKind Kind;
            public string Reason;
        }

        public static ArmoryGameComponent Instance;

        private readonly List<Decision> decisions = new List<Decision>(128);
        private Dictionary<int, int> roleOverrides = new Dictionary<int, int>();

        public ArmoryGameComponent(Game game)
        {
            Instance = this;
            ArmoryPlanner.Reset();
        }

        public static List<Decision> Decisions
        {
            get { return Instance == null ? null : Instance.decisions; }
        }

        public static void Record(int tick, Pawn pawn, ThingWithComps weapon, DecisionKind kind,
            string reason)
        {
            if (Instance == null)
            {
                return;
            }
            Instance.decisions.Add(new Decision
            {
                Tick = tick,
                Colonist = pawn == null ? "?" : pawn.LabelShort,
                Weapon = weapon == null ? "?" : weapon.LabelCap,
                Kind = kind,
                Reason = reason
            });
            if (Instance.decisions.Count > 300)
            {
                Instance.decisions.RemoveAt(0);
            }
        }

        public static void ClearDecisions()
        {
            if (Instance != null)
            {
                Instance.decisions.Clear();
            }
        }

        /// <summary>Player override for a pawn's ranged combat role, or Auto when none is set.</summary>
        public static CombatRole GetRoleOverride(Pawn pawn)
        {
            if (Instance == null || pawn == null || Instance.roleOverrides == null)
            {
                return CombatRole.Auto;
            }

            int value;
            if (!Instance.roleOverrides.TryGetValue(pawn.thingIDNumber, out value))
            {
                return CombatRole.Auto;
            }
            if (value < (int)CombatRole.Cqb || value > (int)CombatRole.Marksman)
            {
                return CombatRole.Auto;
            }
            return (CombatRole)value;
        }

        /// <summary>Sets or clears a pawn's ranged combat role override.</summary>
        public static void SetRoleOverride(Pawn pawn, CombatRole role)
        {
            if (Instance == null || pawn == null)
            {
                return;
            }
            if (Instance.roleOverrides == null)
            {
                Instance.roleOverrides = new Dictionary<int, int>();
            }

            if (role == CombatRole.Auto)
            {
                Instance.roleOverrides.Remove(pawn.thingIDNumber);
            }
            else
            {
                Instance.roleOverrides[pawn.thingIDNumber] = (int)role;
            }
        }

        public override void ExposeData()
        {
            // The decision log is deliberately session-only. Role overrides belong to
            // the colony and must survive save/load, so they use pawn thing IDs.
            Scribe_Collections.Look(ref roleOverrides, "roleOverrides", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && roleOverrides == null)
            {
                roleOverrides = new Dictionary<int, int>();
            }
        }
    }
}
