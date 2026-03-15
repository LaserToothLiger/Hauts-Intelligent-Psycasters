using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI.Group;

namespace HVPAA
{
    //pawn kinds tagged with this DME can be part of a Templar Strike Squad. obviously, only Persona Templar and Archotemplar have it
    public class StrikeSquaddie : DefModExtension
    {
        public StrikeSquaddie()
        {

        }
    }
    //straightforward attack. Only StrikeSquaddies can be part of it.
    public class RaidStrategyWorker_TemplarStrikeSquad : RaidStrategyWorker_WithRequiredPawnKinds
    {
        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            IntVec3 intVec = (parms.spawnCenter.IsValid ? parms.spawnCenter : pawns[0].PositionHeld);
            if (parms.attackTargets != null && parms.attackTargets.Count > 0)
            {
                return new LordJob_AssaultThings(parms.faction, parms.attackTargets, 1f, false);
            }
            if (parms.faction.HostileTo(Faction.OfPlayer))
            {
                Faction faction = parms.faction;
                bool canTimeoutOrFlee = parms.canTimeoutOrFlee;
                return new LordJob_AssaultColony(faction, parms.canKidnap, canTimeoutOrFlee, false, false, parms.canSteal, false, false);
            }
            IntVec3 intVec2;
            RCellFinder.TryFindRandomSpotJustOutsideColony(intVec, map, out intVec2);
            return new LordJob_AssistColony(parms.faction, intVec2);
        }
        protected override bool MatchesRequiredPawnKind(PawnKindDef kind)
        {
            return kind.HasModExtension<StrikeSquaddie>();
        }

        protected override int MinRequiredPawnsForPoints(float pointsTotal, Faction faction = null)
        {
            return 1;
        }
    }
}
