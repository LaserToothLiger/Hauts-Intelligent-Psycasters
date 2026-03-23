using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF.AnimalBehaviours;
using Verse;

namespace HVPAA_HOP
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_DPC : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if ((Rand.Chance(this.spontaneousCastChance) && (intPsycasts.Pawn.Faction == null || (intPsycasts.Pawn.Map.ParentFaction != null && intPsycasts.Pawn.Faction == intPsycasts.Pawn.Map.ParentFaction))) || Rand.Chance(this.spontaneousCastChanceAway))
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, (int)this.aoe, null, out IntVec3 spot);
                    if (spot.IsValid && GenSight.LineOfSight(intPsycasts.Pawn.Position, spot, intPsycasts.Pawn.Map) && !spot.Standable(intPsycasts.Pawn.Map))
                    {
                        RoofDef rd = spot.GetRoof(intPsycasts.Pawn.Map);
                        if (rd != null)
                        {
                            if (rd.isThickRoof)
                            {
                                continue;
                            }
                            if (!JoyUtility.EnjoyableOutsideNow(intPsycasts.Pawn, null))
                            {
                                continue;
                            }
                            if (ModsConfig.OdysseyActive)
                            {
                                BiomeDef bd = intPsycasts.Pawn.Map.Biome;
                                if (bd != null && bd.inVacuum)
                                {
                                    continue;
                                }
                            }
                        }
                        psycast.lti = spot;
                        return 2f;
                    }
                }
            }
            return 0f;
        }
        public float spontaneousCastChance;
        public float spontaneousCastChanceAway;
    }
    public class UseCaseTags_Lightning : UseCaseTags
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
                if (tryNewPosition.IsValid && !possibleTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0) && !psycast.pawn.Map.roofGrid.Roofed(tryNewPosition))
                {
                    tryNewScore = 0f;
                    HVPAA_DecisionMakingUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, 1.5f, ref tryNewScore);
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
    public class UseCaseTags_PsyphonLink : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.HasPsylink || Math.Min(0.25f, psycast.pawn.psychicEntropy.CurrentPsyfocus) >= p.psychicEntropy.CurrentPsyfocus;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsMiscUtility.TotalPsycastLevel(p) + (p.GetPsylinkLevel() / 2f);
        }
        public override float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.Pawn.psychicEntropy.IsCurrentlyMeditating)
            {
                return 0f;
            }
            return base.ApplicabilityScore(intPsycasts, psycast, niceToEvil);
        }
        public override float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.psychicEntropy.CurrentPsyfocus < this.canDebuffBelowPsyfocusLvl ? base.PriorityScoreDebuff(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float highestPsyfocusCost = 0f;
            foreach (Ability a in psycast.pawn.abilities.abilities)
            {
                if (a.def.PsyfocusCost > highestPsyfocusCost)
                {
                    highestPsyfocusCost = a.def.PsyfocusCost;
                }
            }
            return psycast.pawn.psychicEntropy.CurrentPsyfocus < highestPsyfocusCost ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
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
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float highestPsyfocusCost = 0f;
            foreach (Ability a in p.abilities.abilities)
            {
                if (a.def.PsyfocusCost > highestPsyfocusCost)
                {
                    highestPsyfocusCost = a.def.PsyfocusCost;
                }
            }
            if (intPsycasts.foes.Contains(p))
            {
                return highestPsyfocusCost * HautsMiscUtility.TotalPsycastLevel(p) / 1.5f;
            }
            if (intPsycasts.Pawn.GetPsylinkLevel() < p.GetPsylinkLevel() && intPsycasts.GetSituation() == 1)
            {
                return 0f;
            }
            return 1f / (highestPsyfocusCost * p.GetPsylinkLevel());
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Dictionary<Pawn, float> pawnTargets = new Dictionary<Pawn, float>();
            foreach (Pawn p in intPsycasts.Pawn.Map.mapPawns.AllHumanlikeSpawned)
            {
                if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast.ability))
                {
                    if (GenSight.LineOfSight(intPsycasts.Pawn.Position, p.Position, p.Map) && psycast.ability.CanApplyPsycastTo(p) && !this.OtherAllyDisqualifiers(psycast.ability, p, 4))
                    {
                        if (this.avoidTargetsWithHediff != null && p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                        {
                            continue;
                        }
                        float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 4);
                        pawnTargets.Add(p, pApplicability);
                    }
                }
            }
            if (pawnTargets.Count > 0)
            {
                Pawn pawn = this.BestPawnFound(pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    return pawnTargets.TryGetValue(pawn);
                }
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return base.Range(psycast) / 1.5f;
        }
        public float canDebuffBelowPsyfocusLvl;
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
    }
    public class UseCaseTags_Shield : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.IncomingDamageFactor);
            int foeShooters = 0;
            foreach (Thing t in this.hostileShooters)
            {
                if (t.Position.DistanceTo(p.Position) <= this.aoe)
                {
                    foeShooters++;
                }
            }
            app *= foeShooters;
            return app / 10f;
        }
        public override Pawn FindAllyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            int choices = Math.Min(3, intPsycasts.allies.Count);
            bool fleeing = intPsycasts.GetSituation() == 3;
            bool consideredSelf = false;
            List<Pawn> allies = intPsycasts.allies;
            while (choices > 0)
            {
                choices--;
                Pawn p = !consideredSelf && fleeing ? psycast.pawn : allies.RandomElement();
                if (p != null)
                {
                    if (p == psycast.pawn)
                    {
                        consideredSelf = true;
                    }
                    if (allies.Contains(p))
                    {
                        allies.Remove(p);
                    }
                    if (p.Position.DistanceTo(origin) <= (range == -999 ? this.Range(psycast) : range))
                    {
                        if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget))
                        {
                            if (this.avoidTargetsWithHediff != null && p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                            {
                                continue;
                            }
                            float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
                            if (pApplicability > 0f)
                            {
                                pawnTargets.Add(p, pApplicability);
                            }
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
            if (intPsycasts.foes.Count > 0 && Rand.Chance(this.spontaneousCastChance))
            {
                this.hostileShooters = new List<Thing>();
                foreach (Pawn p in intPsycasts.foes)
                {
                    if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.scanForShootersDistance && p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                    {
                        this.hostileShooters.Add(p);
                    }
                }
                foreach (Building b in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.scanForShootersDistance, true).OfType<Building>().Distinct<Building>())
                {
                    if (b.Faction != null)
                    {
                        if (b.def.building != null && b.def.building.IsTurret && intPsycasts.Pawn.Faction.HostileTo(b.Faction) && !b.Position.AnyGas(b.Map, GasType.BlindSmoke))
                        {
                            CompPowerTrader cpt = b.TryGetComp<CompPowerTrader>();
                            if (cpt == null || !cpt.PowerOn)
                            {
                                this.hostileShooters.Add(b);
                            }
                        }
                    }
                }
                if (this.hostileShooters.Count > 0)
                {
                    Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null)
                    {
                        psycast.lti = pawn;
                        return pawnTargets.TryGetValue(pawn);
                    }
                }
            }
            return 0f;
        }
        public List<Thing> hostileShooters;
        public float scanForShootersDistance;
        public float spontaneousCastChance;
    }
    public class UseCaseTags_Tremorzone : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.pather.Moving || p.GetStatValue(StatDefOf.StaggerDurationFactor) <= float.Epsilon || StaticCollectionsClass.floating_animals.Contains(p);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.pather.Moving || (this.tickPeriodicity / 60f) * p.GetStatValue(StatDefOf.MoveSpeed) >= 2f * this.aoe || p.GetStatValue(StatDefOf.StaggerDurationFactor) <= float.Epsilon || StaticCollectionsClass.floating_animals.Contains(p);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Min(this.speedMax, p.GetStatValue(StatDefOf.MoveSpeed)) * p.GetStatValue(StatDefOf.StaggerDurationFactor);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Min(this.speedMax, p.GetStatValue(StatDefOf.MoveSpeed)) * p.GetStatValue(StatDefOf.StaggerDurationFactor);
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
        public float speedMax;
        public int tickPeriodicity;
    }
    public class UseCaseTags_VaultSkip : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase >= 5 && psycast.pawn.Faction != null && psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
            {
                return 1f;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            float netValue = -this.minNetValueToSteal;
            int itemCount = -this.minCountToSteal;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.aoe * intPsycasts.Pawn.GetStatValue(HautsDefOf.Hauts_SkipcastRangeFactor), true))
            {
                if (t.def.EverHaulable)
                {
                    netValue += t.MarketValue * t.stackCount;
                    itemCount++;
                }
            }
            if (netValue > 0f || itemCount >= 0)
            {
                psycast.lti = new LocalTargetInfo(intPsycasts.Pawn);
                return netValue;
            }
            return 0f;
        }
        public float minNetValueToSteal;
        public int minCountToSteal;
    }
}
