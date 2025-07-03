using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using VFECore;

namespace HVPAA_MorePsycasts
{
    [StaticConstructorOnStartup]
    public class HVPAA_MorePsycasts
    {
    }
    public class UseCaseTags_MechDisease : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.RaceProps.Humanlike || p.RaceProps.IsMechanoid || p.skills == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.Faction == null || p.Faction != psycast.pawn.Faction;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.skills != null && p.jobs.curDriver != null && p.jobs.curDriver.ActiveSkill != null)
            {
                float painFactor = 1f;
                foreach (Hediff h in p.health.hediffSet.hediffs)
                {
                    painFactor *= h.PainFactor;
                }
                if (ModsConfig.BiotechActive && p.genes != null)
                {
                    painFactor *= p.genes.PainFactor;
                }
                float projectedPain = p.GetStatValue(StatDefOf.PainShockThreshold) - (p.health.hediffSet.PainTotal + (painFactor * this.painOffset));
                return projectedPain > 0f ? projectedPain* Math.Max(0f, Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation) - this.manipCutoff) + Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.movingCutoff)) * (p.skills.GetSkill(p.jobs.curDriver.ActiveSkill).Level - minUtilitySkillLevel) : 0f;
            }
            return 0f;
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (!Rand.Chance(this.chanceToUtilityCast))
            {
                return 0f;
            }
            return psycast.pawn.Faction != null ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float manipCutoff;
        public float movingCutoff;
        public float painOffset;
        public float chanceToUtilityCast;
        public int minUtilitySkillLevel;
    }
    public class UseCaseTags_HeartAttack : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.RaceProps.IsMechanoid || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float painFactor = 1f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                painFactor *= h.PainFactor;
            }
            if (ModsConfig.BiotechActive && p.genes != null)
            {
                painFactor *= p.genes.PainFactor;
            }
            float projectedPain = p.GetStatValue(StatDefOf.PainShockThreshold) - (p.health.hediffSet.PainTotal + (painFactor * this.painOffset));
            return projectedPain * (p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness)*this.consciousnessFactor <= 0.3f ? 2f : 1f);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float consciousnessFactor;
        public float painOffset;
    }
}
