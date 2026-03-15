using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_BerserkPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.InAggroMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.InAggroMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil, false);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil, false);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
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
                                return bestTargetHits * Math.Max((intPsycasts.foes.Count - intPsycasts.allies.Count) / 15f, 1f);
                            }
                        }
                        psycast.lti = bestTarget;
                        return bestTargetHits * Math.Max((intPsycasts.foes.Count - intPsycasts.allies.Count) / 15f, 1f);
                    }
                }
            }
            return 0f;
        }
    }
    public class UseCaseTags_MassChaosSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize) || p.stances.stunner.Stunned || p.Downed;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.ChaosSkipApplicability(p, psycast);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.ChaosSkipApplicability(p, psycast);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
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
                        float pTargetHits = this.scoreOffset;
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
                            float pTargetHits = this.scoreOffset;
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
                                psycast.ltiDest = psycast.ability.CompOfType<CompAbilityEffect_WithDest>().GetDestination(psycast.lti);
                                return bestTargetHits;
                            }
                        }
                        psycast.lti = bestTarget;
                        psycast.ltiDest = psycast.ability.CompOfType<CompAbilityEffect_WithDest>().GetDestination(psycast.lti);
                        return bestTargetHits;
                    }
                }
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public float scoreOffset;
    }
    public class UseCaseTags_MHPulse : UseCaseTags
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
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)(this.Range(psycast)), null, out tryNewPosition);
                    if (tryNewPosition.IsValid && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -10f;
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.AnimalOrWildMan() || (p2.InMentalState && (p2.MentalStateDef == MentalStateDefOf.Manhunter || p2.MentalStateDef == MentalStateDefOf.ManhunterPermanent)) || p2.Downed || p2.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
                        {
                            continue;
                        }
                        if (intPsycasts.allies.Contains(p2))
                        {
                            tryNewScore -= p2.MarketValue / 500f;
                        }
                        tryNewScore += HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p2, psycast, niceToEvil, false, true);
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
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position);
            }
            return 0f;
        }
    }
    public class UseCaseTags_Neuroquake : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.Faction == null || !psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail) || (situationCase != 1 && situationCase != 3))
            {
                return 0f;
            }
            if (pacifist)
            {
                return 0f;
            }
            switch (situationCase)
            {
                case 1:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
                        return 1.7f;
                    }
                case 3:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
                        return 1.7f;
                    }
                default:
                    return 0f;
            }
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (Rand.Chance(this.chancePerEvil * (-niceToEvil - this.minEvil)) && (intPsycasts.continuousTimeSpawned > 5000 || !HVPAA_Mod.settings.powerLimiting))
            {
                psycast.lti = new LocalTargetInfo(intPsycasts.Pawn);
                float score = 1f;
                int situation = intPsycasts.GetSituation();
                foreach (Pawn p in intPsycasts.allies)
                {
                    if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe && !p.kindDef.isBoss)
                    {
                        if (this.ShouldRally(psycast.ability, p, situation))
                        {
                            score += p.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
                        }
                        else
                        {
                            score -= (p.MarketValue / (niceToEvil > 0f ? 250f : 1000f));
                            score += HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p, psycast.ability, niceToEvil, false);
                        }
                    }
                }
                foreach (Pawn p in intPsycasts.Pawn.Map.mapPawns.AllPawnsSpawned)
                {
                    if (!intPsycasts.allies.Contains(p) && p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe && !p.kindDef.isBoss)
                    {
                        score += HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p, psycast.ability, niceToEvil, false);
                    }
                }
                return score;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return intPsycasts.GetSituation() != 3 ? 0f : this.ApplicabilityScoreDamage(intPsycasts, psycast, niceToEvil);
        }
        public override bool ShouldRally(Psycast psycast, Pawn p, int situation)
        {
            return p != psycast.pawn && (p.Position.DistanceTo(psycast.pawn.Position) - this.rallyRadius) / p.GetStatValue(StatDefOf.MoveSpeed) <= psycast.def.verbProperties.warmupTime;
        }
        public float minEvil;
        public float chancePerEvil;
    }
    public class UseCaseTags_Skipshield : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 bestPosition = IntVec3.Invalid;
            List<Pawn> allyMelee = new List<Pawn>();
            List<Thing> allyShooters = new List<Thing>();
            List<Pawn> foeMelee = new List<Pawn>();
            List<Thing> foeShooters = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.aoe, true))
            {
                if (t.def.building != null && t.def.building.IsTurret && !t.Position.AnyGas(t.Map, GasType.BlindSmoke))
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        continue;
                    }
                    if (t.HostileTo(psycast.pawn))
                    {
                        foeShooters.Add(t);
                    }
                    else if (HVPAA_DecisionMakingUtility.IsAlly(intPsycasts.niceToAnimals <= 0, psycast.pawn, t, niceToEvil))
                    {
                        allyShooters.Add(t);
                    }
                }
                else if (t is Pawn p)
                {
                    if (intPsycasts.allies.Contains(p))
                    {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            allyShooters.Add(t);
                        }
                        else
                        {
                            allyMelee.Add(p);
                        }
                    }
                    else if (intPsycasts.foes.Contains(p))
                    {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            foeShooters.Add(p);
                        }
                        else
                        {
                            foeMelee.Add(p);
                        }
                    }
                }
                int outgunned = Math.Max(foeShooters.Count - allyShooters.Count, allyMelee.Count);
                if (allyMelee.Count > 0 && foeShooters.Count > 0 && outgunned > 0)
                {
                    foreach (Thing ally in allyMelee)
                    {
                        Thing foe = foeShooters.RandomElement();
                        float percent = (Rand.Value + Rand.Value) / 2f;
                        int x = Math.Min(ally.Position.x, foe.Position.x) + (int)(Rand.Value * Math.Abs(ally.Position.x - foe.Position.x));
                        int z = Math.Min(ally.Position.z, foe.Position.z) + (int)(Rand.Value * Math.Abs(ally.Position.z - foe.Position.z));
                        IntVec3 randPosBetween = new IntVec3(x, ally.Position.y, z);
                        if (randPosBetween.IsValid && !positionTargets.ContainsKey(randPosBetween) && GenSight.LineOfSight(psycast.pawn.Position, randPosBetween, psycast.pawn.Map) && randPosBetween.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast) && !positionTargets.Keys.Contains(randPosBetween))
                        {
                            bool nearbySkipshield = false;
                            foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(randPosBetween, psycast.pawn.Map, 3f, true))
                            {
                                if (t2.def == this.avoidMakingTooMuchOfThing)
                                {
                                    nearbySkipshield = true;
                                    break;
                                }
                            }
                            if (!nearbySkipshield)
                            {
                                positionTargets.Add(randPosBetween, outgunned);
                            }
                        }
                    }
                    if (positionTargets.Count > 0)
                    {
                        bestPosition = positionTargets.Keys.RandomElement();
                    }
                }
            }
            return bestPosition;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position) * (intPsycasts.Pawn.equipment != null && intPsycasts.Pawn.equipment.Primary != null && intPsycasts.Pawn.equipment.Primary.def.IsRangedWeapon ? 1f : 1.5f);
            }
            return 0f;
        }
    }
}
