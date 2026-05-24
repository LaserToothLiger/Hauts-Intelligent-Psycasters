using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_SleepyV2
{
    //level 2. See Psycasts_Patch_Royalty.xml comments for more info
    public class UseCaseTags_BeastBond : UseCaseTags
    {
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil, false, true);
        }
        public Pawn FindBestAnimalTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, out Dictionary<Pawn, float> pawnTargets, List<Pawn> animals)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = psycast.pawn.Position;
            foreach (Pawn p in animals)
            {
                if (p.Position.DistanceTo(origin) <= this.Range(psycast))
                {
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && psycast.CanApplyPsycastTo(p))
                    {
                        float pApplicability = this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, 3, true);
                        if (pApplicability > 0f)
                        {
                            pawnTargets.Add(p, pApplicability);
                        }
                    }
                }
            }
            if (pawnTargets.Count > 0)
            {
                return this.BestPawnFound(pawnTargets);
            }
            return null;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn p = intPsycasts.Pawn;
            List<Pawn> animals = new List<Pawn>();
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.Range(psycast.ability), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.AnimalOrWildMan() || p2.InMentalState || p2.Downed || (!this.canTargetTameAnimals && p2.Faction != null) || intPsycasts.allies.Contains(p2) || p2.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
                {
                    continue;
                }
                animals.Add(p2);
            }
            Pawn pawn = this.FindBestAnimalTarget(intPsycasts, psycast.ability, niceToEvil, out Dictionary<Pawn, float> pawnTargets, animals);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public bool canTargetTameAnimals;
    }
    public class UseCaseTags_Guidance : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Pawn_PsychicEntropyTracker ppet = psycast.pawn.psychicEntropy;
            if (ppet != null && ppet.CurrentPsyfocus <= this.dontCastBelowThisPsyfocus)
            {
                return 0f;
            }
            return base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 2f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Faction != Faction.OfPlayerSilentFail || p.skills == null || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float dontCastBelowThisPsyfocus;
    }
    //level 3
    public class UseCaseTags_MyStrengthIsYours_Melee : UseCaseTags_MyStrengthIsYours
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return base.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    //level 4
    public class UseCaseTags_Gaspop : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || GasUtility.AnyGas(p.Position,p.Map,GasType.ToxGas);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || GasUtility.AnyGas(p.Position, p.Map, GasType.ToxGas);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue * (1f-p.GetStatValue(StatDefOf.ToxicEnvironmentResistance));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.MarketValue * (1f - p.GetStatValue(StatDefOf.ToxicEnvironmentResistance));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
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
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 1))
                                    {
                                        pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 1);
                                    }
                                } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 1)) {
                                    pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 1);
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
                                        if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 1))
                                        {
                                            pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 1);
                                        }
                                    } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 1)) {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 1);
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
                        return bestTargetHits / 300f;
                    }
                }
            }
            return 0f;
        }
    }
    //level 6
    public class UseCaseTags_HealAoE : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase == 1)
            {
                return 0f;
            }
            return base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury hi && hi.CanHealNaturally())
                {
                    iNeedHealing += Math.Max(0f, h.Severity + h.BleedRate);
                } else {
                    HediffComp_Immunizable hcim = h.TryGetComp<HediffComp_Immunizable>();
                    if (hcim != null && h.def.lethalSeverity > 0f && !h.FullyImmune())
                    {
                        iNeedHealing += 1.5f * h.Severity / (h.def.lethalSeverity * Math.Max(1f, p.GetStatValue(StatDefOf.ImmunityGainSpeed)));
                    }
                }
            }
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * iNeedHealing;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
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
                                if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
                                    pTargetHits += this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
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
                                    if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
                                        pTargetHits += this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
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
                        return bestTargetHits / 300f;
                    }
                }
            }
            return 0f;
        }
    }
}