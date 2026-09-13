using HVPAA;
using RimWorld;
using Verse;
using Verse.AI;

namespace HVPAA_PsionicPlus
{
    //LifeSavingComa but with Word "anyone you could run to in a reasonable time" targeting
    public class UseCaseTags_BreathOfLife : UseCaseTags_LifeSavingComa
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return base.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)); ;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    //Focus but also w Word targeting
    public class UseCaseTags_WordOfConfidence : UseCaseTags_Focus
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return base.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)); ;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
}
