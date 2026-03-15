using RimWorld;
using System.Collections.Generic;
using Verse;

namespace HVPAA
{
    /*this wizard war is so fucked
     * For use with the Torsion Psycast mod. I'm not making a whole other directory just for one psycast. Again, see Psycasts_Patch_Royalty.xml and UCT_0Basic.cs*/
    public class UseCaseTags_CBT : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.gender != Gender.Male || this.excludeRaces.Contains(p.def) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.health.hediffSet.HasHediff(this.alsoCantHave);
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
            return p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) * ((painFactor * this.painOffset) + (2.5f * p.health.hediffSet.PainTotal / p.GetStatValue(StatDefOf.PainShockThreshold)));
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
        public HediffDef alsoCantHave;
        public List<ThingDef> excludeRaces;
    }
}
