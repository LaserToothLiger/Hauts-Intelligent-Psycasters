using HVPAA;
using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HVPAA_PsionicPlus
{
    public class UseCaseTags_EnergyBlast : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (Rand.Chance(chanceToCast) || !HVPAA_Mod.settings.powerLimiting) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            psycast.lti = new LocalTargetInfo(intPsycasts.Pawn);
            float score = 0f;
            int situation = intPsycasts.GetSituation();
            foreach (Pawn p in intPsycasts.allies)
            {
                if (!p.kindDef.isBoss && p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe)
                {
                    score -= (p.MarketValue / (niceToEvil > 0f ? 250f : 500f))*this.allyMultiplier;
                }
            }
            foreach (Pawn p in intPsycasts.foes)
            {
                if (!p.kindDef.isBoss && p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe)
                {
                    score += p.MarketValue / 250f;
                }
            }
            return score;
        }
        public float chanceToCast;
    }
}
