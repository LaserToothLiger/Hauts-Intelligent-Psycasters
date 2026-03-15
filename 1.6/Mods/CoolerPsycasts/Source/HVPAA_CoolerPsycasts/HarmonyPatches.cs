using CoolPsycasts;
using HarmonyLib;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI.Group;

namespace HVPAA_CoolerPsycasts
{
    [StaticConstructorOnStartup]
    public class HVPAA_CoolerPsycasts
    {
        private static readonly Type patchType = typeof(HVPAA_CoolerPsycasts);
        static HVPAA_CoolerPsycasts()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.HVPAA.cooler");
            if (ModsConfig.IdeologyActive)
            {
                harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_Enslave), nameof(CompAbilityEffect_Enslave.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                               postfix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityEnslave_Apply_Postfix)));
            }
            /*harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_CastAbility), nameof(CompAbilityEffect_CastAbility.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityCastAbility_Apply_Prefix)));*/
        }
        //if the NPCaster casts Enslave, the victim escorts the caster around
        public static void HVPAA_AbilityEnslave_Apply_Postfix(CompAbilityEffect_Enslave __instance, LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                Pawn pawn = __instance.parent.pawn;
                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
                {
                    Lord lord = pawn.GetLord();
                    if (lord != null)
                    {
                        lord.AddPawn(target.Pawn);
                    } else {
                        LordMaker.MakeNewLord(pawn.Faction, new LordJob_EscortPawn(pawn), pawn.Map, Gen.YieldSingle<Pawn>(target.Pawn));
                    }
                }
            }
        }
        //for the NYI psycasting AI of Extend
        public static bool HVPAA_AbilityCastAbility_Apply_Prefix(CompAbilityEffect_CastAbility __instance, LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = __instance.parent.pawn;
            caster.health.hediffSet.TryGetHediff(HVPAADefOf.HVPAA_AI, out Hediff ai);
            if (ai != null && caster.Spawned)
            {
                HediffComp_IntPsycasts hcip = ai.TryGetComp<HediffComp_IntPsycasts>();
                if (hcip != null)
                {
                    Dictionary<Psycast, float> extendableAbilities = new Dictionary<Psycast, float>();
                    float myRange = __instance.parent.verb.EffectiveRange;
                    if (hcip.highestPriorityPsycasts.NullOrEmpty())
                    {
                        hcip.highestPriorityPsycasts = hcip.ThreePriorityPsycasts(hcip.GetSituation());
                    }
                    foreach (PotentialPsycast pp in hcip.highestPriorityPsycasts)
                    {
                        Psycast a = pp.ability;
                        float aRange = a.verb.EffectiveRange;
                        if (a.def != __instance.parent.def && a.def.targetRequired && a.CanApplyOn(target) && aRange > 0f && aRange < myRange && a.FinalPsyfocusCost(target) < caster.psychicEntropy.CurrentPsyfocus)
                        {
                            if (!a.comps.Any((AbilityComp c) => c is CompAbilityEffect_WithDest) && !caster.psychicEntropy.WouldOverflowEntropy(a.def.EntropyGain))
                            {
                                UseCaseTags uct = pp.ability.def.GetModExtension<UseCaseTags>();
                                if (uct != null)
                                {
                                    float score = pp.score * uct.ApplicabilityScore(hcip, pp, hcip.niceToEvil);
                                    float dist = pp.lti.Cell.DistanceTo(caster.Position);
                                    if (pp.lti.IsValid && dist <= myRange && dist > aRange && score > 0)
                                    {
                                        extendableAbilities.Add(a, score);
                                    }
                                }
                            }
                        }
                    }
                    if (!extendableAbilities.NullOrEmpty())
                    {
                        extendableAbilities.RandomElementByWeight((KeyValuePair<Psycast, float> kvp) => kvp.Value).Key.Activate(target, dest);
                    }
                }
                return false;
            }
            return true;
        }
    }
}
