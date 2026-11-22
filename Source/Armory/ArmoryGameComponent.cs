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
        RestoreBackup,
        EquipSidearmTwo,
        DropExtraSidearm
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
        private Dictionary<int, int> handTypeOverrides = new Dictionary<int, int>();

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
            InvalidateSnapshot();
        }

        /// <summary>Player override for a pawn's hand weapon type, or Auto when none is set.</summary>
        public static HandType GetHandTypeOverride(Pawn pawn)
        {
            if (Instance == null || pawn == null || Instance.handTypeOverrides == null)
            {
                return HandType.Auto;
            }

            int value;
            if (!Instance.handTypeOverrides.TryGetValue(pawn.thingIDNumber, out value) ||
                value < (int)HandType.Ranged || value > (int)HandType.Melee)
            {
                return HandType.Auto;
            }
            return (HandType)value;
        }

        /// <summary>Sets or clears a pawn's hand-type override.</summary>
        public static void SetHandTypeOverride(Pawn pawn, HandType type)
        {
            if (Instance == null || pawn == null)
            {
                return;
            }
            if (Instance.handTypeOverrides == null)
            {
                Instance.handTypeOverrides = new Dictionary<int, int>();
            }

            if (type == HandType.Auto)
            {
                Instance.handTypeOverrides.Remove(pawn.thingIDNumber);
            }
            else
            {
                Instance.handTypeOverrides[pawn.thingIDNumber] = (int)type;
            }
            InvalidateSnapshot();
        }

        private static void InvalidateSnapshot()
        {
            Map map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            ArmoryMapComponent component = map.GetComponent<ArmoryMapComponent>();
            if (component != null)
            {
                component.Invalidate();
            }
        }

        public override void ExposeData()
        {
            // The decision log is deliberately session-only. Role and hand-type overrides
            // belong to the colony and must survive save/load, so they use pawn thing IDs.
            Scribe_Collections.Look(ref roleOverrides, "roleOverrides", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref handTypeOverrides, "handTypeOverrides", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (roleOverrides == null)
                {
                    roleOverrides = new Dictionary<int, int>();
                }
                if (handTypeOverrides == null)
                {
                    handTypeOverrides = new Dictionary<int, int>();
                }
            }
        }
    }
}
