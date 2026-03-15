using HarmonyLib;
using HVPAA;
using HautsPsycasts;
using System;
using Verse;

namespace HVPAA_HOP
{
    [StaticConstructorOnStartup]
    public class HVPAA_HOP
    {
        private static readonly Type patchType = typeof(HVPAA_HOP);
        static HVPAA_HOP()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.HVPAA_hop");
            harmony.Patch(AccessTools.Method(typeof(HediffComp_LinkRevoker), nameof(HediffComp_LinkRevoker.AIShouldRecallOtherQualification)),
                          postfix: new HarmonyMethod(patchType, nameof(AIShouldRecallOtherQualificationPostfix)));
        }
        //Tether Skip is going to recall your ass if you're my enemy!!1!
        public static void AIShouldRecallOtherQualificationPostfix(HediffComp_LinkRevoker __instance, Hediff h, ref bool __result)
        {
            if (HVPAA_DecisionMakingUtility.CanPsycast(__instance.Pawn, 0) && HVPAA_DecisionMakingUtility.IsEnemy(__instance.Pawn, h.pawn))
            {
                __result = true;
                return;
            }
        }
    }
}
