using CombatPsycasts.Comps;
using HarmonyLib;
using System;
using System.Reflection;
using Verse;

namespace HVPAA_CombatPsycasts
{
    [StaticConstructorOnStartup]
    public class HVPAA_CombatPsycasts
    {
        private static readonly Type patchType = typeof(HVPAA_CombatPsycasts);
        static HVPAA_CombatPsycasts()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.HVPAA.combatpsycasts");
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_PsychicSustainedShoot), nameof(CompAbilityEffect_PsychicSustainedShoot.ShouldContinueFiring)),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_ShouldContinueFiringPrefix)));
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        //don't stop firing just because you're not player controlled!
        public static bool HVPAA_ShouldContinueFiringPrefix(ref bool __result, CompAbilityEffect_PsychicSustainedShoot __instance)
        {
            if (__instance.parent.pawn.drafter == null)
            {
                __result = __instance.parent.CanCast && (bool)__instance.GetType().GetField("shootCanReach", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) && (bool)__instance.GetType().GetMethod("ThingIsStillStanding", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
                return false;
            }
            return true;
        }
    }
}
