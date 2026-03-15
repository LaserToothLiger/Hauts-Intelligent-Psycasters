using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_Sleepy
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_MassBeckon : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.CurJob != null && p.CurJobDef == JobDefOf.GotoMindControlled) || p.Position.DistanceTo(psycast.pawn.Position) < 2f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.CurJob != null && p.CurJobDef == JobDefOf.GotoMindControlled) || p.Position.DistanceTo(psycast.pawn.Position) < 2f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * HVPAA_DecisionMakingUtility.ExpectedBeckonTime(p, psycast.pawn) * ((psycast.pawn.equipment != null && (psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsRangedWeapon)) ? 2f : 1f) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? (1f + CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)) : 1f) / 1000f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * HVPAA_DecisionMakingUtility.ExpectedBeckonTime(p, psycast.pawn) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? (1f + CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)) : 1f) / 1000f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                List<Pawn> topTargets = this.TopTargets(5, pawnTargets);
                if (topTargets.Count > 0)
                {
                    Pawn bestTarget = topTargets.First();
                    IntVec3 bestTargetPos = bestTarget.Position;
                    float bestTargetHits = 0f;
                    foreach (Pawn p in topTargets)
                    {
                        float pTargetHits = 0f;
                        foreach (Pawn p2 in (List<Pawn>)p.Map.mapPawns.AllPawnsSpawned)
                        {
                            if (p2.Position.DistanceTo(p.Position) <= this.aoe)
                            {
                                if (intPsycasts.foes.Contains(p2))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2))
                                    {
                                        pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
                                    pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                }
                            }
                        }
                        if (pTargetHits > bestTargetHits)
                        {
                            bestTarget = p;
                            bestTargetHits = pTargetHits;
                        }
                    }
                    if (bestTarget != null && pawnTargets.TryGetValue(bestTarget) > 0f)
                    {
                        bestTargetPos = bestTarget.Position;
                        CellFinder.TryFindRandomCellNear(topTargets.RandomElement().Position, bestTarget.Map, (int)this.aoe, null, out IntVec3 randAoE1);
                        if (randAoE1.IsValid)
                        {
                            float pTargetHits = 0f;
                            foreach (Pawn p2 in (List<Pawn>)bestTarget.Map.mapPawns.AllPawnsSpawned)
                            {
                                if (p2.Position.DistanceTo(randAoE1) <= this.aoe)
                                {
                                    if (intPsycasts.foes.Contains(p2))
                                    {
                                        if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2))
                                        {
                                            pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                        }
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                            }
                            if (pTargetHits > bestTargetHits)
                            {
                                bestTargetPos = randAoE1;
                                bestTargetHits = pTargetHits;
                                psycast.lti = bestTargetPos;
                                return bestTargetHits;
                            }
                        }
                        psycast.lti = bestTarget;
                        return bestTargetHits;
                    }
                }
            }
            return 0f;
        }
        public float imposedMovingCap;
    }
    public class UseCaseTags_PU : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) || p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.MeleeDPS) <= this.minDPSxPsysensToBoost || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 2.5f * p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.MeleeDPS);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minDPSxPsysensToBoost;
    }
    public class UseCaseTags_Recondition : UseCaseTags
    {
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            float multi = 0f;
            if (!t.def.useHitPoints || (t.def.category != ThingCategory.Building && t.def.category != ThingCategory.Item) || (t.MarketValue < this.minMarketValue && t.MaxHitPoints < this.minMaxHp))
            {
                return 0f;
            }
            if (t.Faction == null)
            {
                multi = 0.1f;
            }
            else if (psycast.pawn.Faction != null && (t.Faction == psycast.pawn.Faction || psycast.pawn.Faction.RelationKindWith(t.Faction) == FactionRelationKind.Ally || (niceToEvil > 0f && psycast.pawn.Faction.AllyOrNeutralTo(t.Faction))))
            {
                multi = 3f;
            }
            return multi * (this.minMissingPercentHp - ((float)t.HitPoints / (float)t.MaxHitPoints));
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
        public float minMarketValue;
        public float minMaxHp;
        public float minMissingPercentHp;
    }
    public class UseCaseTags_Revitalise : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase == 1)
            {
                return 0f;
            }
            return base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 6f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.RaceProps.IsFlesh || p.health.hediffSet.HasRegeneration || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                float toAdd = 0f;
                if (h is Hediff_Injury)
                {
                    toAdd += h.Severity;
                }
                else if (h is Hediff_MissingPart && !p.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(h.Part))
                {
                    toAdd += h.Part.coverageAbs;
                }
                if (toAdd > 0f)
                {
                    iNeedHealing += (h.Part == p.health.hediffSet.GetBrain() ? 10f : 1f) * toAdd;
                }
            }
            if (!p.Downed && iNeedHealing <= 100f)
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
                if (((painFactor * this.painOffset) + p.health.hediffSet.PainTotal) >= 0.9f * p.GetStatValue(StatDefOf.PainShockThreshold))
                {
                    return 0f;
                }
            }
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * iNeedHealing;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float painOffset;
    }
}
