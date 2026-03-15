using CoolPsycasts;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HVPAA_CoolerPsycasts
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Awaken : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return situationCase == 1 ? 0f : base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float numConditions = 0f;
            CompAbilityEffect_RemoveHediffs rh = psycast.CompOfType<CompAbilityEffect_RemoveHediffs>();
            if (rh != null)
            {
                foreach (Hediff h in p.health.hediffSet.hediffs)
                {
                    if (rh.Props.hediffDefs.Contains(h.def))
                    {
                        numConditions += 1f;
                    }
                }
            }
            if (p.needs.rest != null)
            {
                numConditions /= p.needs.rest.CurLevelPercentage;
            }
            return numConditions;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
    }
    public class UseCaseTags_Strip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.apparel == null || !p.apparel.AnyApparel;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.apparel != null && p.apparel.AnyApparel)
            {
                float duration = p.GetStatValue(StatDefOf.PsychicSensitivity) * this.duration;
                float totalTimeToStrip = 0f;
                float totalArmor = 0f;
                foreach (Apparel a in p.apparel.WornApparel)
                {
                    totalTimeToStrip += a.GetStatValue(StatDefOf.EquipDelay, true, -1);
                    totalArmor += Math.Max(0f, a.GetStatValue(StatDefOf.ArmorRating_Blunt) + a.GetStatValue(StatDefOf.ArmorRating_Sharp) + (a.GetStatValue(StatDefOf.ArmorRating_Heat) / 2f) - this.minArmorToCountAsArmor);
                }
                totalArmor /= p.apparel.WornApparel.Count * 1f;
                totalArmor += 1f;
                return Math.Min(duration, totalTimeToStrip) * totalArmor;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float duration;
        public float minArmorToCountAsArmor;
    }
}
