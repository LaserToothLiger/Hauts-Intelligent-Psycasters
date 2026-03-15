using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA_CoolerPsycasts
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml. These psycasts only show up if you have HOP
    public class UseCaseTags_PsyfocusTransfer : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.abilities == null || p.psychicEntropy == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
            {
                return true;
            }
            if (!initialTarget && p.Downed)
            {
                return true;
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.abilities == null || p.psychicEntropy == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.psychicEntropy.CurrentPsyfocus < this.donorMinPsyfocusCutoff;
        }
        public float TotalCostOfRelevantPsycasts(Pawn p, bool combat)
        {
            float totalPsyfocusCost = 0f;
            foreach (Ability a in p.abilities.abilities)
            {
                UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                if (uct != null && (uct.healing || (combat ? (uct.damage || uct.defense || uct.debuff) : uct.utility)))
                {
                    totalPsyfocusCost += a.def.PsyfocusCost;
                }
            }
            return totalPsyfocusCost;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float biggestDiffInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p2 != psycast.pawn && p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && p.HasPsylink && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map))
                {
                    float p2sBiggestDiff = this.TotalCostOfRelevantPsycasts(p2, useCase != 5) * (1 - p2.psychicEntropy.CurrentPsyfocus);
                    if (p2sBiggestDiff > biggestDiffInRange)
                    {
                        recipient = p2;
                        biggestDiffInRange = p2sBiggestDiff;
                    }
                }
            }
            biggestDiffInRange += this.TotalCostOfRelevantPsycasts(p, true) * p.psychicEntropy.CurrentPsyfocus;
            if (recipient == null)
            {
                return -1f;
            }
            this.targetPairs.Add(p, recipient);
            return Math.Max(0f, biggestDiffInRange);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float biggestDiffInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p != p2 && p2 != psycast.pawn && p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && p.HasPsylink && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map) && p.psychicEntropy.CurrentPsyfocus < this.recipientMaxPsyfocusCutoff)
                {
                    float scalar = 1 - p2.psychicEntropy.CurrentPsyfocus;
                    float p2sBiggestDiff = (this.TotalCostOfRelevantPsycasts(p2, useCase != 5) - this.TotalCostOfRelevantPsycasts(p, useCase != 5)) * scalar;
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
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn);
            if (pawn != null && this.recipientPawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn2 = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets2);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn2);
            if (pawn2 != null && this.recipientPawn != null)
            {
                psycast.lti = pawn2;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets2.TryGetValue(pawn2);
            }
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
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
                if ((intPsycasts.foes.Contains(p) || p.Faction == null || (psycast.pawn.Faction != null && p.Faction != null && p.Faction.HostileTo(psycast.pawn.Faction))) && p.HasPsylink)
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
        public override Pawn FindAllyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!intPsycasts.foes.Contains(p) && p.HasPsylink)
                {
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
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
        public float donorToRecipientRange;
        public Pawn recipientPawn;
        public Dictionary<Pawn, Pawn> targetPairs;
        public float recipientMaxPsyfocusCutoff;
        public float donorMinPsyfocusCutoff;
    }
    public class UseCaseTags_MarkingPulse : UseCaseTags
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * (1 + p.GetStatValue(StatDefOf.MeleeDodgeChance)) * (1 + p.GetStatValue(VEF.Pawns.InternalDefOf.VEF_RangedDodgeChance)) / p.BodySize;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
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
    public class UseCaseTags_SquadronCall : UseCaseTags
    {
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            List<Pawn> foes = intPsycasts.foes.InRandomOrder<Pawn>().ToList();
            IntVec3 origin = intPsycasts.parent.pawn.Position;
            psycast.lti = origin;
            foreach (Pawn p in foes)
            {
                if (p.Position.DistanceTo(origin) <= this.Range(psycast.ability) && GenSight.LineOfSight(origin, p.Position, p.Map))
                {
                    psycast.lti = p.Position;
                }
            }
            return intPsycasts.parent.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(this.castChanceWhileNotInImmediateCombat) ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count < 1)
            {
                return 0f;
            }
            psycast.lti = intPsycasts.parent.pawn.Position;
            return intPsycasts.parent.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
        }
        public float castChanceWhileNotInImmediateCombat;
    }
    public class UseCaseTags_TornadoLink : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            RoofDef roof = p.Map.roofGrid.RoofAt(p.Position);
            return roof != null && roof.isThickRoof;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            RoofDef roof = p.Map.roofGrid.RoofAt(p.Position);
            return roof != null && roof.isThickRoof;
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            foreach (HediffDef h in this.dontUseIfHave)
            {
                if (psycast.pawn.health.hediffSet.HasHediff(h))
                {
                    return 0f;
                }
            }
            return psycast.pawn.Faction == null ? 0f : base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            Map map = psycast.pawn.Map;
            Faction f = psycast.pawn.Faction;
            IntVec3 casterLoc = psycast.pawn.Position;
            for (int i = 0; i <= 5; i++)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(casterLoc, map, (int)(this.Range(psycast)), null, out tryNewPosition);
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(casterLoc, tryNewPosition, map))
                    {
                        RoofDef roof = map.roofGrid.RoofAt(tryNewPosition);
                        if (roof == null || !roof.isThickRoof)
                        {
                            break;
                        }
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -1f;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, map, aoe, true))
                    {
                        if (thing.def == this.avoidMakingTooMuchOfThing)
                        {
                            tryNewScore = -15f;
                            break;
                        }
                        if (thing is Plant plant)
                        {
                            Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
                            if (zone != null && zone is Zone_Growing && f != Faction.OfPlayerSilentFail && f.HostileTo(Faction.OfPlayerSilentFail))
                            {
                                tryNewScore += this.TornadoThingScore(plant);
                            }
                        }
                        else if (thing is Building b && b.Faction != null)
                        {
                            if (f != b.Faction && f.HostileTo(b.Faction))
                            {
                                tryNewScore += this.TornadoThingScore(b);
                            }
                            else if (niceToEvil > 0 || f == b.Faction || f.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                            {
                                tryNewScore -= this.TornadoThingScore(b);
                            }
                        }
                        else if (thing is Pawn p)
                        {
                            if (intPsycasts.allies.Contains(p) && !this.OtherAllyDisqualifiers(psycast, p, 1))
                            {
                                tryNewScore -= this.TornadoThingScore(p) * 1.5f;
                            }
                            else if (intPsycasts.foes.Contains(p) && !this.OtherEnemyDisqualifiers(psycast, p, 1))
                            {
                                tryNewScore += this.TornadoThingScore(p);
                            }
                        }
                    }
                    if (tryNewScore > 0)
                    {
                        positionTargets.Add(tryNewPosition, tryNewScore);
                    }
                }
            }
            IntVec3 bestPosition = IntVec3.Invalid;
            if (positionTargets.Count > 0)
            {
                float value = -1f;
                foreach (KeyValuePair<IntVec3, float> kvp in positionTargets)
                {
                    if (!bestPosition.IsValid || kvp.Value >= value)
                    {
                        bestPosition = kvp.Key;
                        value = kvp.Value;
                    }
                }
            }
            return bestPosition;
        }
        public float TornadoThingScore(Thing t)
        {
            float scoreMulti = 1f;
            if (t is Building b && b.def.building != null && b.def.building.IsTurret)
            {
                CompPowerTrader cpt = b.TryGetComp<CompPowerTrader>();
                if (cpt == null || !cpt.PowerOn)
                {
                    scoreMulti = 2f;
                }
            }
            else if (t is Pawn p)
            {
                scoreMulti *= p.GetStatValue(StatDefOf.IncomingDamageFactor);
            }
            else if (t is Plant)
            {
                scoreMulti /= 2.5f;
            }
            return scoreMulti * HautsMiscUtility.DamageFactorFor(this.damageDef, t) * t.MarketValue / 200f;
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
        public DamageDef damageDef;
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
    }
    public class UseCaseTags_WordOfBlessing : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 10f * this.PawnAllyApplicability(intPsycasts, psycast.ability, pawn, niceToEvil, 4);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed)
            {
                return true;
            }
            foreach (Hediff hediff in p.health.hediffSet.hediffs)
            {
                if (this.grantableHediffs.Contains(hediff.def))
                {
                    return true;
                }
            }
            return false;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (p.MarketValue - this.minMarketValue) / 500f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minMarketValue;
        public List<HediffDef> grantableHediffs;
    }
    public class UseCaseTags_Voidquake : UseCaseTags
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
                    if (!this.ShouldRally(psycast.ability, p, situation))
                    {
                        if (ModsConfig.AnomalyActive && (p.IsMutant || p.RaceProps.IsAnomalyEntity))
                        {
                            score += p.MarketValue / 250f;
                        }
                        else if (!p.kindDef.isBoss && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon)
                        {
                            score -= p.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
                        }
                    }
                }
                foreach (Pawn p in intPsycasts.foes)
                {
                    if (ModsConfig.AnomalyActive && (p.IsMutant || p.RaceProps.IsAnomalyEntity))
                    {
                        score -= p.MarketValue / 250f;
                    }
                    else if (!p.kindDef.isBoss && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon)
                    {
                        score += p.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
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
            return p != psycast.pawn && (!ModsConfig.AnomalyActive || (!p.IsMutant && !p.RaceProps.IsAnomalyEntity)) && (p.Position.DistanceTo(psycast.pawn.Position) - this.rallyRadius) / p.GetStatValue(StatDefOf.MoveSpeed) <= psycast.def.verbProperties.warmupTime;
        }
        public float minEvil;
        public float chancePerEvil;
    }
}
