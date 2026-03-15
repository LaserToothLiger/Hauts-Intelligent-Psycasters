using HautsFramework;
using HVPAA;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_Sleepy
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Resurrect : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.Faction == null ? 0f : base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t is Corpse corpse && corpse.InnerPawn != null && corpse.InnerPawn.Faction != null && (corpse.InnerPawn.Faction == psycast.pawn.Faction || psycast.pawn.Faction.RelationKindWith(corpse.InnerPawn.Faction) == FactionRelationKind.Ally || (niceToEvil > 0 && psycast.pawn.Faction.AllyOrNeutralTo(corpse.InnerPawn.Faction))) && corpse.Map.reachability.CanReach(psycast.pawn.Position, corpse.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)))
            {
                if (ModsConfig.AnomalyActive && corpse is UnnaturalCorpse)
                {
                    return 0f;
                }
                if (corpse.InnerPawn.RaceProps.Humanlike)
                {
                    return 40f;
                }
                else if (corpse.InnerPawn.RaceProps.Animal)
                {
                    return 1f;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing thing = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Thing, float> thingTargets);
            if (thing != null)
            {
                psycast.lti = thing;
                return thingTargets.TryGetValue(thing);
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_FertSkip : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            List<TerrainDef> terrainDefs = HautsMiscUtility.FertilityTerrainDefs(psycast.pawn.Map);
            for (int i = 0; i <= 5; i++)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)(this.Range(psycast)), null, out tryNewPosition);
                    if (tryNewPosition.IsValid && !tryNewPosition.Filled(psycast.pawn.Map) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = 1f - this.minFertilizableCells;
                    foreach (IntVec3 iv3 in GenRadial.RadialCellsAround(tryNewPosition, 0f, this.aoe))
                    {
                        if (iv3.InBounds(psycast.pawn.Map) && GenSight.LineOfSightToEdges(tryNewPosition, iv3, psycast.pawn.Map, true, null))
                        {
                            TerrainDef terrain = psycast.pawn.Map.terrainGrid.TerrainAt(iv3);
                            if (terrain.IsFloor || terrain.affordances.Contains(TerrainAffordanceDefOf.SmoothableStone))
                            {
                                tryNewScore = -1f;
                                break;
                            }
                            if (!terrain.IsRiver && !terrain.IsWater)
                            {
                                IOrderedEnumerable<TerrainDef> source = from e in terrainDefs.FindAll((TerrainDef e) => (double)e.fertility > (double)terrain.fertility && (double)e.fertility <= 1.0)
                                                                        orderby e.fertility
                                                                        select e;
                                if (source.Count<TerrainDef>() != 0)
                                {
                                    tryNewScore += 1f;
                                }
                            }
                        }
                    }
                    positionTargets.Add(tryNewPosition, tryNewScore);
                }
            }
            IntVec3 bestPosition = IntVec3.Invalid;
            float value = -1f;
            foreach (KeyValuePair<IntVec3, float> kvp in positionTargets)
            {
                if (!bestPosition.IsValid || kvp.Value >= value)
                {
                    bestPosition = kvp.Key;
                    value = kvp.Value;
                }
            }
            return bestPosition;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position);
            }
            return 0f;
        }
        public int minFertilizableCells;
    }
}
