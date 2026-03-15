using HautsPsycasts;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA_HOP
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Evict : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (Rand.Chance(chanceToCast) || !HVPAA_Mod.settings.powerLimiting) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || (p.BodySize < this.bodySizeThreshold && p.MarketValue < 5000f) || this.psyfocusCostPerVictimSize.Evaluate(p.BodySize) > psycast.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Max(p.MarketValue / 2500f, p.BodySize);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && t.HostileTo(psycast.pawn) && (this.psyfocusCostPerVictimSize.Evaluate(t.def.Size.x * t.def.Size.z) <= psycast.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f))
            {
                if (t.def.building.IsTurret && t.def.building.ai_combatDangerous && t.def.useHitPoints)
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        return 0f;
                    }
                    return t.HitPoints;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            float app = 0f;
            Thing turret = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Thing, float> thingTargets);
            if (turret != null)
            {
                psycast.lti = turret;
                app = thingTargets.TryGetValue(turret);
            }
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null && pawnTargets.TryGetValue(pawn) > app)
            {
                psycast.lti = pawn;
                app = pawnTargets.TryGetValue(pawn);
            }
            return app;
        }
        public float chanceToCast;
        public float bodySizeThreshold;
        public SimpleCurve psyfocusCostPerVictimSize;
    }
    public class UseCaseTags_HiveCall : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            for (int i = 0; i <= 5; i++)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)range, null, out tryNewPosition);
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map) && psycast.verb.ValidateTarget(tryNewPosition, false))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -this.minScoreToCast;
                    if (intPsycasts.Pawn.Faction != null)
                    {
                        if (intPsycasts.Pawn.Faction == Faction.OfInsects)
                        {
                            tryNewScore = Rand.Value * 30f;
                        }
                        else
                        {
                            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, aoe, true))
                            {
                                if (thing is Building b && b.Faction != null)
                                {
                                    if (intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                                    {
                                        tryNewScore += this.foeBuildingScore;
                                    }
                                    else if (niceToEvil > 0 || intPsycasts.Pawn.Faction == b.Faction || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                                    {
                                        tryNewScore -= this.allyBuildingScore;
                                    }
                                }
                                else if (thing is Pawn p)
                                {
                                    if (intPsycasts.allies.Contains(p))
                                    {
                                        tryNewScore -= this.allyPawnScore;
                                    }
                                    else if (intPsycasts.foes.Contains(p))
                                    {
                                        tryNewScore += this.foePawnScore;
                                    }
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
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            CompAbilityEffect_SpawnInfestation caesi = psycast.ability.CompOfType<CompAbilityEffect_SpawnInfestation>();
            if (caesi != null)
            {
                int hivesToSpawn = (int)Math.Ceiling(caesi.HivesToSpawn / 10f);
                IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<IntVec3, float> positionTargets, Math.Min(hivesToSpawn * 20f, this.Range(psycast.ability)));
                if (position.IsValid)
                {
                    psycast.lti = position;
                    return positionTargets.TryGetValue(position);
                }
            }
            return 0f;
        }
        public float minScoreToCast;
        public float allyBuildingScore;
        public float allyPawnScore;
        public float foeBuildingScore;
        public float foePawnScore;
    }
    public class UseCaseTags_RJSkip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.RaceProps.IsFlesh;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            if (p.health.hediffSet.BleedRateTotal > 0.01f)
            {
                iNeedHealing = Math.Max(this.ticksToFatalBloodLossCutoff - HealthUtility.TicksUntilDeathDueToBloodLoss(p), 0f);
            }
            float injuryCount = -this.minInjurySeverity;
            float mbCount = -this.minMissingPartSeverity;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury hi)
                {
                    if (hi.IsPermanent() && hi.Part == p.health.hediffSet.GetBrain())
                    {
                        iNeedHealing += this.bonusBrainInjurySeverity;
                    }
                    injuryCount += h.Severity;
                }
                else if (h is Hediff_MissingPart && h.Part != null)
                {
                    mbCount += h.Part.def.GetMaxHealth(p);
                }
            }
            return Math.Max(0f, injuryCount) + Math.Max(0f, mbCount) + iNeedHealing;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public int ticksToFatalBloodLossCutoff;
        public float minInjurySeverity;
        public float minMissingPartSeverity;
        public float bonusBrainInjurySeverity;
    }
    public class UseCaseTags_Stuporzone : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) - this.consciousnessCurve.Evaluate(p.GetStatValue(StatDefOf.PsychicSensitivity)));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * Math.Max(0f, 1.25f * (p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) - this.consciousnessCurve.Evaluate(p.GetStatValue(StatDefOf.PsychicSensitivity))));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) + 8f);
            if (pawnTargets.Count > 0)
            {
                List<Pawn> topTargets = this.TopTargets(5, pawnTargets);
                IntVec3 finalPos = IntVec3.Invalid;
                float finalScore = 0f;
                if (topTargets.Count > 0)
                {
                    IntVec3 bestTargetPos;
                    foreach (Pawn p in topTargets)
                    {
                        bestTargetPos = IntVec3.Invalid;
                        float pTargetHits = pawnTargets.TryGetValue(p);
                        if (p.pather.curPath != null && p.pather.curPath.Found)
                        {
                            for (int i = p.pather.curPath.NodesLeftCount - 1; i > 0; i--)
                            {
                                IntVec3 pos = p.pather.curPath.Peek(i);
                                float distance = pos.DistanceTo(p.Position);
                                if (distance <= 1.25f * p.GetStatValue(StatDefOf.MoveSpeed))
                                {
                                    break;
                                }
                                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, pos, intPsycasts.Pawn.Map) && distance > p.GetStatValue(StatDefOf.MoveSpeed) / 3f && pos.InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                                {
                                    bestTargetPos = pos;
                                }
                            }
                        }
                        else
                        {
                            bestTargetPos = p.Position;
                        }
                        bool goNext = false;
                        if (bestTargetPos.IsValid)
                        {
                            List<IntVec3> radius = GenRadial.RadialCellsAround(bestTargetPos, this.aoe * 0.8f, true).ToList();
                            foreach (Thing t in GenRadial.RadialDistinctThingsAround(bestTargetPos, intPsycasts.Pawn.Map, this.aoe * 1.5f, true))
                            {
                                if (t.def == this.avoidMakingTooMuchOfThing)
                                {
                                    goNext = true;
                                    break;
                                }
                                else if (t is Pawn p2 && p2.pather.curPath != null && p2.pather.curPath.Found)
                                {
                                    if (intPsycasts.foes.Contains(p2))
                                    {
                                        if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2) && this.IntersectsRadius(radius, p2.pather.curPath.NodesReversed))
                                        {
                                            pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                        }
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2) && this.IntersectsRadius(radius, p2.pather.curPath.NodesReversed))
                                    {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                            }
                        }
                        if (goNext)
                        {
                            continue;
                        }
                        if (pTargetHits > finalScore)
                        {
                            finalPos = bestTargetPos;
                            finalScore = pTargetHits;
                        }
                    }
                    if (finalScore > 0f)
                    {
                        psycast.lti = finalPos;
                        return finalScore;
                    }
                }
            }
            return 0f;
        }
        public bool IntersectsRadius(List<IntVec3> radius, List<IntVec3> curPathNodes)
        {
            foreach (IntVec3 iv3 in curPathNodes)
            {
                if (radius.Contains(iv3))
                {
                    return true;
                }
            }
            return false;
        }
        public SimpleCurve consciousnessCurve;
    }
}
