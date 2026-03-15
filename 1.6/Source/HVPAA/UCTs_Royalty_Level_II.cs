using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
     * PlayerOnlyTargetColonist is needed for Neural Heat Dump, which is coded to normally only work on colonists. This replaces that stupid ass bullshit so that NPCasters only cast on pawns of their own faction instead*/
    public class UseCaseTags_BlindingPulse : UseCaseTags
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
            return p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) * p.GetStatValue(StatDefOf.PsychicSensitivity) * this.sightReduction * (p.CurJob != null && p.CurJob.verbToUse != null ? 2f : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) * p.GetStatValue(StatDefOf.PsychicSensitivity) * this.sightReduction * (p.CurJob != null && p.CurJob.verbToUse != null ? 2f : 1f);
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
        public float sightReduction;
    }
    public class UseCaseTags_NHD : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Faction == null || p.Faction != psycast.pawn.Faction || p.MarketValue > 1000f || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.HasPsylink;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Max(1000f - p.MarketValue, 0f);
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (!SellcastUtility.IsSelfDirected(intPsycasts.Pawn) && (psycast.Caster.psychicEntropy.EntropyRelativeValue >= 0.9f || (psycast.Caster.psychicEntropy.EntropyRelativeValue >= 0.8f && Rand.Chance(0.25f))))
            {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    return 1000f;
                }
            }
            return 0f;
        }
    }
    public class CompProperties_AbilityPlayerOnlyTargetColonist : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityPlayerOnlyTargetColonist()
        {
            this.compClass = typeof(CompAbilityEffect_PlayerOnlyTargetColonist);
        }
    }
    public class CompAbilityEffect_PlayerOnlyTargetColonist : CompAbilityEffect
    {
        public new CompProperties_AbilityPlayerOnlyTargetColonist Props
        {
            get
            {
                return (CompProperties_AbilityPlayerOnlyTargetColonist)this.props;
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            return pawn != null && this.parent.pawn.Faction != null && pawn.Faction != null && this.parent.pawn.Faction == pawn.Faction;
        }
    }
    public class UseCaseTags_Waterskip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.Downed && !p.stances.stunner.Stunned && p.GetStatValue(StatDefOf.MoveSpeed) >= 1f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float fireSize = 0f;
            Fire attachedFire = (Fire)p.GetAttachment(ThingDefOf.Fire);
            if (attachedFire != null)
            {
                fireSize += attachedFire.CurrentSize();
            }
            return p.GetStatValue(StatDefOf.Flammability) * fireSize;
        }
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            Faction f = psycast.pawn.Faction;
            foreach (Fire fire in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.Range(psycast), true).OfType<Fire>().Distinct<Fire>())
            {
                IntVec3 pos = fire.Position;
                if (pos.Filled(psycast.pawn.Map) || !GenSight.LineOfSight(psycast.pawn.Position, pos, psycast.pawn.Map, true, null, 0, 0))
                {
                    bool goNext = true;
                    for (int i = 0; i < 8; i++)
                    {
                        IntVec3 intVec = pos + GenRadial.RadialPattern[i];
                        if (!intVec.Filled(psycast.pawn.Map) && GenSight.LineOfSight(psycast.pawn.Position, intVec, psycast.pawn.Map, true, null, 0, 0))
                        {
                            pos = intVec;
                            goNext = false;
                            break;
                        }
                    }
                    if (goNext)
                    {
                        continue;
                    }
                }
                if (!possibleTargets.ContainsKey(pos))
                {
                    int numFires = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        IntVec3 intVec = pos + GenRadial.RadialPattern[i];
                        List<Thing> ctl = intVec.GetThingList(psycast.pawn.Map);
                        if (ctl != null)
                        {
                            foreach (Thing t in ctl)
                            {
                                if (t is Fire || t.IsBurning())
                                {
                                    numFires++;
                                }
                                if ((t is Pawn p && intPsycasts.foes.Contains(p)) || (f != null && ((t.Faction != null && f.HostileTo(t.Faction)) || (t is Plant p2 && HVPAA_DecisionMakingUtility.IsPlantInHostileFactionGrowZone(p2, f)))))
                                {
                                    numFires--;
                                }
                            }
                        }
                    }
                    possibleTargets.Add(pos, numFires);
                }
            }
            if (!possibleTargets.NullOrEmpty())
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
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return 100f * positionTargets.TryGetValue(position);
            }
            return 0f;
        }
    }
    public class UseCaseTags_WordOfJoy : UseCaseTags
    {
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 10f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.needs.mood == null || p.needs.mood.CurLevel >= p.mindState.mentalBreaker.BreakThresholdMajor || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || PawnCapacityUtility.CalculatePartEfficiency(p.health.hediffSet, p.health.hediffSet.GetBrain(), false, null) < (0.31f - this.brainEfficiencyOffset) || p.InMentalState || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max((p.mindState.mentalBreaker.BreakThresholdMajor - p.needs.mood.CurLevel), 0f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float brainEfficiencyOffset;
    }
}
