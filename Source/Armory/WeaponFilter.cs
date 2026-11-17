using Verse;

namespace Armory
{
    /// <summary>
    /// Vanilla's ThingRequestGroup.Weapon is broader than it sounds: wood and other
    /// stuffable resources inherit a verb, and beer/meals have ingestible verbs.
    /// This is the single definition of "a real weapon" used by ranking and display.
    /// </summary>
    public static class WeaponFilter
    {
        public static bool IsWeaponDef(ThingDef def)
        {
            if (def == null)
            {
                return false;
            }
            if (def.category != ThingCategory.Item || def.IsStuff || def.IsDrug || def.IsIngestible ||
                def.IsMedicine || def.IsApparel || def.IsShell)
            {
                return false;
            }
            return def.IsRangedWeapon || def.IsMeleeWeapon || def.Verbs != null && def.Verbs.Count > 0;
        }
    }
}
