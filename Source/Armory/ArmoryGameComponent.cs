using System.Collections.Generic;
using Verse;

namespace Armory
{
    /// <summary>Session-only decision log for the automation tab.</summary>
    public class ArmoryGameComponent : GameComponent
    {
        public sealed class Decision
        {
            public int Tick;
            public string Colonist;
            public string Weapon;
            public string Action;
            public string Detail;
        }

        public static ArmoryGameComponent Instance;

        private readonly List<Decision> decisions = new List<Decision>(128);

        public ArmoryGameComponent(Game game)
        {
            Instance = this;
            ArmoryPlanner.Reset();
        }

        public static List<Decision> Decisions
        {
            get { return Instance == null ? null : Instance.decisions; }
        }

        public static void Record(int tick, Pawn pawn, ThingWithComps weapon, string action, string detail)
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
                Action = action,
                Detail = detail
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

        public override void ExposeData()
        {
            // The decision log is deliberately session-only.
        }
    }
}
