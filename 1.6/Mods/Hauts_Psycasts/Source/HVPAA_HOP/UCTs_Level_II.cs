using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_HOP
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Carezone : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return (!p.Downed && !p.InBed()) || !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            if (!this.TooMuchThingNearby(psycast, p.Position, this.aoe))
            {
                if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= (this.Range(psycast) + this.aoe))
                {
                    foreach (Hediff h in p.health.hediffSet.hediffs)
                    {
                        if (h is HediffWithComps hwc)
                        {
                            HediffComp_Immunizable hcim = hwc.TryGetComp<HediffComp_Immunizable>();
                            if (hcim != null && hwc.def.lethalSeverity > 0f && !hwc.FullyImmune())
                            {
                                iNeedHealing += (3f * h.Severity / hwc.def.lethalSeverity) + (1f / Math.Max(1f, p.GetStatValue(StatDefOf.ImmunityGainSpeed)));
                            }
                            if (h is Hediff_Injury hi && hi.CanHealNaturally())
                            {
                                iNeedHealing += Math.Max(0f, h.Severity + 4f * h.BleedRate);
                            }
                        }
                    }
                }
            }
            return iNeedHealing > 0f ? iNeedHealing + 1f : 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) + this.aoe);
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
                        foreach (Pawn p2 in intPsycasts.allies)
                        {
                            if (p2.Position.DistanceTo(p.Position) <= this.aoe && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                            {
                                pTargetHits += pawnTargets.TryGetValue(p2);
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
                            foreach (Pawn p2 in intPsycasts.allies)
                            {
                                if (p2.Position.DistanceTo(randAoE1) <= this.aoe && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
                                    pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
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
    public class UseCaseTags_ParalysisLink : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.severityPerDay * p.GetStatValue(StatDefOf.PsychicSensitivity) * (1f + (p.GetStatValue(StatDefOf.ArmorRating_Blunt) + p.GetStatValue(StatDefOf.ArmorRating_Sharp) + (p.GetStatValue(StatDefOf.ArmorRating_Heat) / 2f))) / (HautsMiscUtility.HitPointTotalFor(p) * Math.Max(0.01f, p.GetStatValue(StatDefOf.IncomingDamageFactor)));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            foreach (HediffDef h in this.dontUseIfHave)
            {
                if (intPsycasts.Pawn.health.hediffSet.HasHediff(h))
                {
                    return 0f;
                }
            }
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return base.Range(psycast) / 1.5f;
        }
        public float severityPerDay;
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
    }
    public class UseCaseTags_Sensitize : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if ((useCase == 2 || useCase == 5) && !p.HasPsylink)
            {
                return true;
            }
            return p.Downed;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (useCase == 2)
            {
                return this.SumNHcosts(p) - this.sumNeuralHeatCosts;
            }
            if (useCase == 5)
            {
                float result = this.SumUtilityCosts(p) - this.sumPsyfocusCosts;
                return result;
            }
            return 0f;
        }
        public float SumNHcosts(Pawn pawn)
        {
            float sum = -this.ownEntropyCost;
            if (pawn.abilities != null && pawn.psychicEntropy != null)
            {
                foreach (Ability a in pawn.abilities.abilities)
                {
                    if (a is Psycast pc && pc.def.PsyfocusCost <= pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                    {
                        sum += a.def.EntropyGain;
                    }
                }
            }
            return sum;
        }
        public float SumUtilityCosts(Pawn pawn)
        {
            float sum = 0f;
            if (pawn.abilities != null && pawn.psychicEntropy != null)
            {
                foreach (Ability a in pawn.abilities.abilities)
                {
                    if (a.def.HasModExtension<SensitizeScalar>() && a is Psycast pc && pc.def.PsyfocusCost <= pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                    {
                        sum += a.def.PsyfocusCost;
                    }
                }
            }
            return sum;
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.health.hediffSet.HasHediff(this.dontCastIfSufferingFrom))
            {
                return 0f;
            }
            this.sumNeuralHeatCosts = this.SumNHcosts(psycast.pawn);
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn) * 5f;
            }
            return 0f;
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.health.hediffSet.HasHediff(this.dontCastIfSufferingFrom))
            {
                return 0f;
            }
            this.sumPsyfocusCosts = this.SumUtilityCosts(psycast.pawn);
            return base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn) * 5f;
            }
            return 0f;
        }
        public HediffDef dontCastIfSufferingFrom;
        public float sumNeuralHeatCosts = 0f;
        public float sumPsyfocusCosts = 0f;
        public float ownEntropyCost;
    }
    public class UseCaseTags_SkillTransfer : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.Downed || p.skills == null || !p.skills.skills.ContainsAny((SkillRecord sr) => !sr.TotallyDisabled);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.skills == null || !p.skills.skills.ContainsAny((SkillRecord sr) => !sr.TotallyDisabled);
        }
        public List<SkillDef> AllTransferrableSkills(Pawn pawn)
        {
            List<SkillDef> skillDefs = new List<SkillDef>();
            if (pawn.skills != null)
            {
                foreach (SkillRecord sr in pawn.skills.skills)
                {
                    if (!sr.TotallyDisabled)
                    {
                        skillDefs.Add(sr.def);
                    }
                }
            }
            return skillDefs;
        }
        public int BiggestSkillLevelDifference(Pawn donor, Pawn recipient, List<SkillDef> skillDefs)
        {
            int biggestDiff = 0;
            if (donor.skills != null && recipient.skills != null)
            {
                foreach (SkillRecord sr in recipient.skills.skills)
                {
                    if (!sr.TotallyDisabled && skillDefs.Contains(sr.def))
                    {
                        int skillDiff = donor.skills.GetSkill(sr.def).Level - sr.Level;
                        if (skillDiff > biggestDiff)
                        {
                            biggestDiff = skillDiff;
                        }
                    }
                }
            }
            return biggestDiff;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            List<SkillDef> skillDefs = this.AllTransferrableSkills(p);
            float biggestDiffInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map))
                {
                    int p2sBiggestDiff = this.BiggestSkillLevelDifference(p, p2, skillDefs);
                    if (p2sBiggestDiff > biggestDiffInRange)
                    {
                        recipient = p2;
                        biggestDiffInRange = p2sBiggestDiff;
                    }
                }
            }
            if (recipient == null)
            {
                return -1f;
            }
            this.targetPairs.Add(p, recipient);
            return Math.Max(0f, biggestDiffInRange);
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn);
            if (pawn != null && this.recipientPawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.targetPairs = new Dictionary<Pawn, Pawn>();
            return base.ApplicabilityScore(intPsycasts, psycast, niceToEvil);
        }
        public override Pawn FindEnemyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (intPsycasts.foes.Contains(p) || p.Faction == null || (psycast.pawn.Faction != null && p.Faction.HostileTo(psycast.pawn.Faction)))
                {
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        float pApplicability = this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
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
        public float baseFractionTransferred;
        public float donorToRecipientRange;
        public float maxXPtakenPerSkill;
        public Pawn recipientPawn;
        public Dictionary<Pawn, Pawn> targetPairs;
    }
    public class UseCaseTags_TetherSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
            {
                foreach (Pawn p2 in intPsycasts.allies)
                {
                    if (p2.Position.DistanceTo(p.Position) <= 2f * p.GetStatValue(StatDefOf.MoveSpeed))
                    {
                        return 0f;
                    }
                }
                return p.GetStatValue(StatDefOf.MeleeDPS) / 2f;
            }
            else if (p.pather != null && p.pather.curPath != null && p.pather.LastPassableCellInPath.IsValid)
            {
                return CoverUtility.TotalSurroundingCoverScore(p.pather.LastPassableCellInPath, p.Map);
            }
            return 0f;
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
        public float maxBodySize;
    }
    public class UseCaseTags_WordOfConte : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.HasPsylink || p.psychicEntropy == null || (p != psycast.pawn && p.psychicEntropy.CurrentPsyfocus > 0.25f) || !p.psychicEntropy.IsCurrentlyMeditating || p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) * this.brainEfficiencyFactor <= 0.31f || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 1f / p.GetStatValue(StatDefOf.MeditationFocusGain);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float brainEfficiencyFactor;
    }
}
