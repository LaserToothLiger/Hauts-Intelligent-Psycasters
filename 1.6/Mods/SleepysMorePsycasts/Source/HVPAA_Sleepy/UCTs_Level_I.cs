using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace HVPAA_Sleepy
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Ignite : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            int num = GenRadial.NumCellsInRadius(this.Range(psycast));
            for (int i = 0; i < num; i++)
            {
                tryNewPosition = psycast.pawn.Position + GenRadial.RadialPattern[i];
                if (tryNewPosition.IsValid && !possibleTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0) && FireUtility.NumFiresAt(tryNewPosition, psycast.pawn.Map) == 0)
                {
                    tryNewScore = 0f;
                    foreach (Thing thing in tryNewPosition.GetThingList(intPsycasts.Pawn.Map))
                    {
                        if (thing is Plant plant)
                        {
                            Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
                            if (zone != null && zone is Zone_Growing && intPsycasts.Pawn.Faction != null && intPsycasts.Pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
                            {
                                tryNewScore += plant.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, plant) * plant.MarketValue / 500f;
                            }
                        }
                        else if (thing is Building b && b.Faction != null)
                        {
                            if (intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                            {
                                tryNewScore += HVPAA_DecisionMakingUtility.LightningBuildingScore(b);
                            }
                            else if (niceToEvil > 0 || intPsycasts.Pawn.Faction == b.Faction || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                            {
                                tryNewScore -= HVPAA_DecisionMakingUtility.LightningBuildingScore(b);
                            }
                        }
                    }
                    possibleTargets.Add(tryNewPosition, tryNewScore);
                }
            }
            if (possibleTargets != null && possibleTargets.Count > 0)
            {
                float highestValue = 0f;
                foreach (KeyValuePair<IntVec3, float> kvp in possibleTargets)
                {
                    if (kvp.Value > highestValue)
                    {
                        highestValue = kvp.Value;
                    }
                }
                foreach (KeyValuePair<IntVec3, float> kvp in possibleTargets)
                {
                    if (kvp.Value >= highestValue / (Math.Max(1f, highestValue - 1f)))
                    {
                        positionTargets.Add(kvp.Key, kvp.Value);
                    }
                }
                if (positionTargets != null && positionTargets.Count > 0)
                {
                    return positionTargets.RandomElement().Key;
                }
            }
            return IntVec3.Invalid;
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
    }
}
