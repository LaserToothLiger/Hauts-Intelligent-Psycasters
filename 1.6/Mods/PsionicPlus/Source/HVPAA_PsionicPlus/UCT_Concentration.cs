using HVPAA;

namespace HVPAA_PsionicPlus
{
    public class UseCaseTags_Concentration : UseCaseTags
    {
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (this.avoidTargetsWithHediff != null && intPsycasts.Pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
            {
                return -1f;
            }
            psycast.lti = intPsycasts.Pawn;
            return this.flatValue;
        }
        public float flatValue;
    }
}
