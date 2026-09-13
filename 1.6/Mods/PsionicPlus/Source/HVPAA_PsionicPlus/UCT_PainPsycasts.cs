using HVPAA;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HVPAA_PsionicPlus
{
    public class UseCaseTags_PainImpulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * ((painFactor * this.painOffset) + (2.5f * p.health.hediffSet.PainTotal / p.GetStatValue(StatDefOf.PainShockThreshold)));
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
        public float painOffset;
    }
    public class UseCaseTags_PainPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * ((painFactor * this.painOffset) + (2.5f * p.health.hediffSet.PainTotal / p.GetStatValue(StatDefOf.PainShockThreshold)));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
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
            return this.allyMultiplier * p.GetStatValue(StatDefOf.PsychicSensitivity) * ((painFactor * this.painOffset) + (2.5f * p.health.hediffSet.PainTotal / p.GetStatValue(StatDefOf.PainShockThreshold)));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                return this.FindPulseTarget(intPsycasts, psycast, niceToEvil, pawnTargets);
            }
            return 0f;
        }
        public float painOffset;
    }
}
