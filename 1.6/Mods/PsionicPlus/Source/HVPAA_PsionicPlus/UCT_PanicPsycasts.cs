using HVPAA;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HVPAA_PsionicPlus
{
    public class UseCaseTags_PanicScream : UseCaseTags_Stun
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            MentalStateDef ms = p.MentalStateDef;
            if (ms != null && !ms.IsAggro)
            {
                return true;
            }
            return p.kindDef.isBoss || base.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return base.ApplicabilityScoreDebuff(intPsycasts, psycast, niceToEvil);
        }
    }
    public class UseCaseTags_PanicPulse : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (Rand.Chance(chanceToCast) || !HVPAA_Mod.settings.powerLimiting) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            MentalStateDef ms = p.MentalStateDef;
            if (ms != null && !ms.IsAggro)
            {
                return true;
            }
            return p.Downed || p.kindDef.isBoss ||  p.stances.stunner.Stunned || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.kindDef.isBoss || p.stances.stunner.Stunned || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.MarketValue / this.marketValueDivisor;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.GetStatValue(StatDefOf.PsychicSensitivity) * p.MarketValue / this.marketValueDivisor;
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
        public float marketValueDivisor = 1000f;
        public float chanceToCast;
    }
}
