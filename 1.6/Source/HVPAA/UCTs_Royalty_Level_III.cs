using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Beckon : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.CurJob != null && p.CurJobDef == JobDefOf.GotoMindControlled) || p.Position.DistanceTo(psycast.pawn.Position) < 2f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * HVPAA_DecisionMakingUtility.ExpectedBeckonTime(p, psycast.pawn) * ((psycast.pawn.equipment != null && (psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsRangedWeapon)) ? 2f : 1f) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? (1f + CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)) : 1f) / 1000f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
    }
    public class UseCaseTags_ChaosSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize) || p.stances.stunner.Stunned || p.Downed;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.ChaosSkipApplicability(p, psycast);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = psycast.ability.CompOfType<CompAbilityEffect_WithDest>().GetDestination(psycast.lti);
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
    }
    public class UseCaseTags_VertigoPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 4f * p.GetStatValue(StatDefOf.PsychicSensitivity) * (p.CurJob != null && p.CurJob.verbToUse != null ? 1.25f : 1f) * (!p.RaceProps.IsFlesh ? 0.4f : 1f) * (this.Digesting(p) ? 100f : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.GetStatValue(StatDefOf.PsychicSensitivity) * (p.CurJob != null && p.CurJob.verbToUse != null ? 1.25f : 1f) * (!p.RaceProps.IsFlesh ? 0.4f : 1f) * (this.Digesting(p) ? 2.5f : 1f);
        }
        public bool Digesting(Pawn p)
        {
            if (ModsConfig.AnomalyActive)
            {
                CompDevourer cd;
                if (p.TryGetComp(out cd) && cd.Digesting)
                {
                    return true;
                }
            }
            return false;
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
    public class UseCaseTags_WordOfLove : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.GetWorstRelation(pawn, out int worstRelation);
                return this.PawnAllyApplicability(intPsycasts, psycast.ability, pawn, niceToEvil, 4);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.relations == null || !p.RaceProps.Humanlike || p.IsMutant || p.ageTracker.AgeBiologicalYearsFloat < this.minAge || (p == psycast.pawn && Rand.Chance(0.9f)) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || SocialCardUtility.PawnsForSocialInfo(p).Count == 0 || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)))
            {
                return true;
            }
            return false;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            this.GetWorstRelation(p, out int worstRelation);
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(-1f * worstRelation, 0f);
        }
        public Pawn GetWorstRelation(Pawn p, out int worstRelation)
        {
            Pawn pawn = p;
            worstRelation = 0;
            foreach (Pawn other in SocialCardUtility.PawnsForSocialInfo(p))
            {
                if (other.Spawned && other.Map == p.Map && p.relations.OpinionOf(other) < worstRelation && RelationsUtility.PawnsKnowEachOther(p, other) && (!ModsConfig.AnomalyActive || !other.IsMutant))
                {
                    if (other.ageTracker.AgeBiologicalYearsFloat < this.minAge || (this.mustMatchOrientation && !RelationsUtility.AttractedToGender(p, other.gender)))
                    {
                        continue;
                    }
                    worstRelation = p.relations.OpinionOf(other);
                    pawn = other;
                }
            }
            worstRelation += p.IsPrisoner ? this.prisonerRelationOffset : 0;
            return pawn == p ? null : pawn;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minAge;
        public bool mustMatchOrientation;
        public int prisonerRelationOffset;
    }
}
