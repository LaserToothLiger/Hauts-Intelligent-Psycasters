using HVPAA;
using RimWorld;
using Sleepys_MorePsycasts;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_Sleepy
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_ComfortShield : UseCaseTags
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
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * Math.Max(p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature,p.AmbientTemperature - p.GetStatValue(StatDefOf.ComfyTemperatureMax));
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float painOffset;
    }
    public class UseCaseTags_Dash : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(this.chanceToUtilityCast)? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            bool result = p.Downed || p.pather == null || p.pather.curPath == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
            return result;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.MoveSpeed) * ((useCase <= 4 && !p.WorkTagIsDisabled(WorkTags.Violent) && (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)) ? 2.5f : 1f) * (p == psycast.pawn ? 2.5f : 1f);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability)/4f);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
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
        public float chanceToUtilityCast;
    }
    public class UseCaseTags_FlashHeal : UseCaseTags
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
                return 6f*pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury hi && hi.CanHealNaturally())
                {
                    iNeedHealing += Math.Max(0f, h.Severity + h.BleedRate);
                }
            }
            if (!p.Downed)
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
    public class UseCaseTags_Flashstep : UseCaseTags
    {
        public bool RangedP(Pawn p)
        {
            return p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (HVPAA_DecisionMakingUtility.SkipImmune(psycast.pawn,this.maxBodySize))
            {
                return 0f;
            }
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.WorkTagIsDisabled(WorkTags.Violent) && !p2.Downed && !p2.IsBurning() && p2.HostileTo(psycast.pawn))
                {
                    return 0f;
                }
            }
            return this.RangedP(psycast.pawn) ? 0f : base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (HVPAA_DecisionMakingUtility.SkipImmune(psycast.pawn, this.maxBodySize))
            {
                return 0f;
            }
            if (situationCase == 3 || situationCase == 5)
            {
                return 1f;
            }
            return this.RangedP(psycast.pawn) ? base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || p.Position.DistanceTo(psycast.pawn.Position) <= 3f)
            {
                return true;
            }
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2.HostileTo(p))
                {
                    return true;
                }
            }
            return false;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (psycast.pawn.GetStatValue(StatDefOf.MeleeDPS) * psycast.pawn.GetStatValue(StatDefOf.IncomingDamageFactor)) - (p.GetStatValue(StatDefOf.MeleeDPS) * p.GetStatValue(StatDefOf.IncomingDamageFactor));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDmg = IntVec3.Invalid;
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = intPsycasts.Pawn;
                psycast.ltiDest = pawn.Position;
                return 2f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDef = IntVec3.Invalid;
            int situation = intPsycasts.GetSituation();
            if (situation == 3 || situation == 5)
            {
                if (intPsycasts.Pawn.pather != null && intPsycasts.Pawn.pather.curPath != null && intPsycasts.Pawn.pather.curPath.Found)
                {
                    int pathDistance = 0;
                    for (int i = 1; i < intPsycasts.Pawn.pather.curPath.NodesLeftCount - 1; i++)
                    {
                        pathDistance++;
                        if (GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                        {
                            this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                        }
                    }
                    psycast.lti = intPsycasts.Pawn;
                    psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                    return pathDistance;
                }
            } else if (this.RangedP(intPsycasts.Pawn)) {
                this.bestDestDef = intPsycasts.Pawn.PositionHeld;
                float netFoeMeleeDPS = -intPsycasts.Pawn.GetStatValue(StatDefOf.MeleeDPS);
                foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                {
                    if (!p2.WorkTagIsDisabled(WorkTags.Violent) && !p2.Downed && !p2.IsBurning())
                    {
                        if (p2.HostileTo(intPsycasts.Pawn))
                        {
                            netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                        } else if (intPsycasts.allies.Contains(p2)) {
                            netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                        }
                    }
                }
                if (netFoeMeleeDPS > 0f)
                {
                    List<Thing> foeTargetCache = new List<Thing>();
                    foeTargetCache.AddRange(from a in intPsycasts.Pawn.Map.attackTargetsCache.GetPotentialTargetsFor(intPsycasts.Pawn) where !a.ThreatDisabled(intPsycasts.Pawn) select a.Thing);
                    psycast.lti = intPsycasts.Pawn;
                    psycast.ltiDest = CellFinderLoose.GetFallbackDest(intPsycasts.Pawn, foeTargetCache, this.Range(psycast.ability), 5f, 5f, 20, (IntVec3 c) => c.IsValid && GenSight.LineOfSight(c, psycast.lti.Cell, intPsycasts.Pawn.Map));
                    return netFoeMeleeDPS;
                }
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public IntVec3 bestDestDmg;
        public IntVec3 bestDestDef;
    }
    public class UseCaseTags_MassBurden : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.MoveSpeed) <= 1.5f || p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) <= this.imposedMovingCap || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.pather.Moving;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.MoveSpeed) <= 1.5f || p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) <= this.imposedMovingCap || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.pather.Moving;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.MoveSpeed) * Math.Max(p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.imposedMovingCap, 0f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.MoveSpeed) * Math.Max(p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.imposedMovingCap, 0f);
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
    public class UseCaseTags_StunPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.MarketValue / 1000f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.MarketValue / 1000f;
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
    }
    public class UseCaseTags_SupRegen : UseCaseTags
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
            return !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            Hediff_Injury hediff_Injury = SLP_Utilities.SLP_FindPermanentInjury(p, null, Array.Empty<HediffDef>());
            if (hediff_Injury == null)
            {
                return 0f;
            }
            float iNeedHealing = Math.Max(0f, hediff_Injury.Severity);
            if (!p.Downed)
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
    public class UseCaseTags_SupernovaPinhole : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Spawned && useCase == 4)
            {
                if (!p.Position.UsesOutdoorTemperature(p.Map))
                {
                    p.health.hediffSet.TryGetHediff(HediffDefOf.Hypothermia, out Hediff hypo);
                    if (hypo != null && hypo.Severity >= 0.04f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (useCase == 3)
            {
                return p.MarketValue;
            } else if (useCase == 4) {
                return p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature;
            } else if (useCase == 5) {
                return 1f;
            }
            return 1f;
        }
        public override bool TooMuchThingAdditionalCheck(Thing thing, Psycast psycast)
        {
            return WanderUtility.InSameRoom(psycast.pawn.Position, thing.Position, thing.Map);
        }
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            if (useCase == 5 && intPsycasts.Pawn.Faction != null && (intPsycasts.Pawn.Faction == Faction.OfPlayerSilentFail || intPsycasts.Pawn.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Ally || (niceToEvil > 0 && intPsycasts.Pawn.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Neutral))) {
                Map map = intPsycasts.Pawn.Map;
                Dictionary<Plant, float> glowPoints = new Dictionary<Plant, float>();
                List<Plant> plants = GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, map, this.Range(psycast), true).OfType<Plant>().Distinct<Plant>().ToList();
                if (!plants.NullOrEmpty())
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Plant p = plants.RandomElement();
                        if (!p.def.plant.cavePlant && !glowPoints.ContainsKey(p))
                        {
                            Zone zone = map.zoneManager.ZoneAt(p.Position);
                            if (zone != null && zone is Zone_Growing && map.roofGrid.Roofed(p.Position))
                            {
                                float glow = p.def.plant.growMinGlow - map.glowGrid.GroundGlowAt(p.Position);
                                foreach (Plant p2 in GenRadial.RadialDistinctThingsAround(p.Position, map, this.aoe, true).OfType<Plant>().Distinct<Plant>().ToList())
                                {
                                    if (p2.def.plant.cavePlant)
                                    {
                                        glow = 0f;
                                        break;
                                    }
                                    if (map.roofGrid.Roofed(p2.Position) && map.zoneManager.ZoneAt(p2.Position) != null)
                                    {
                                        glow += p2.def.plant.growMinGlow - map.glowGrid.GroundGlowAt(p2.Position);
                                    }
                                }
                                if (glow > 0f && !positionTargets.ContainsKey(p.Position))
                                {
                                    glowPoints.Add(p, glow);
                                    positionTargets.Add(p.Position, glow);
                                }
                            }
                        }
                    }
                    if (glowPoints.Count > 0)
                    {
                        KeyValuePair<Plant, float> toPick = glowPoints.First();
                        foreach (KeyValuePair<Plant, float> kvp in glowPoints)
                        {
                            if (kvp.Value > toPick.Value)
                            {
                                toPick = kvp;
                            }
                        }
                        return toPick.Key.Position;
                    }
                }
            }
            return IntVec3.Invalid;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Room room = intPsycasts.Pawn.Position.GetRoom(intPsycasts.Pawn.Map);
            if (room != null)
            {
                int solarPinholes = 0;
                List<IntVec3> solarCells = new List<IntVec3>();
                float coldTemps = 0f;
                List<Pawn> laborers = new List<Pawn>();
                foreach (Thing t in room.ContainedAndAdjacentThings)
                {
                    if (t.def == this.avoidMakingTooMuchOfThing)
                    {
                        solarPinholes++;
                        solarCells.Add(t.Position);
                        if (solarPinholes >= this.thingLimit)
                        {
                            return 0f;
                        }
                    }
                    else if (t is Pawn p && intPsycasts.allies.Contains(p))
                    {
                        if (!this.OtherAllyDisqualifiers(psycast.ability, p, 4))
                        {
                            coldTemps += this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 4);
                        }
                        if (!this.OtherAllyDisqualifiers(psycast.ability, p, 5) && p.jobs.curDriver != null && p.jobs.curDriver.ActiveSkill != null)
                        {
                            laborers.Add(p);
                        }
                    }
                }
                CompAbilityEffect_Spawn caes = psycast.ability.CompOfType<CompAbilityEffect_Spawn>();
                if (caes != null)
                {
                    if (coldTemps > 0f)
                    {
                        IntVec3 bestCell = IntVec3.Invalid;
                        float darkness = 200f;
                        foreach (IntVec3 cell in room.Cells)
                        {
                            if (caes.Valid(new LocalTargetInfo(cell), false) && !solarCells.Contains(cell) && cell.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast.ability) && GenSight.LineOfSight(intPsycasts.Pawn.Position, cell, intPsycasts.Pawn.Map))
                            {
                                float light = intPsycasts.Pawn.Map.glowGrid.GroundGlowAt(cell, false, false);
                                if (light < darkness)
                                {
                                    darkness = light;
                                    bestCell = cell;
                                }
                            }
                        }
                        if (bestCell.IsValid)
                        {
                            psycast.lti = bestCell;
                            return coldTemps * this.scoreFactor;
                        }
                    } else if (laborers.Count > 0) {
                        for (int i = 0; i < 5; i++)
                        {
                            IntVec3 cell = laborers.RandomElement().Position;
                            if (caes.Valid(new LocalTargetInfo(cell), false) && !solarCells.Contains(cell) && cell.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast.ability) && GenSight.LineOfSight(intPsycasts.Pawn.Position, cell, intPsycasts.Pawn.Map))
                            {
                                psycast.lti = cell;
                                return laborers.Count * this.scoreFactor;
                            }
                        }
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return 100f * positionTargets.TryGetValue(position);
            }
            return 0f;
        }
        public bool useToKillFires = false;
        public float scoreFactor = 0.01f;
    }
}
