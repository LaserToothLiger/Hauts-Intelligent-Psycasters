using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;
using Verse.AI;

namespace HVPAA
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
     * ArcticPinhole: Sleepy's More Psycasts (Arctic and Endothermic Pinhole), [FSF] More Psycast Powers (Arctic Pinhole)
     * BloodStaunch: Sleepy's and FSF's Haemostasis
     * Dart: Sleepy's Dart, Cooler Psycasts' Meteor
     * DurabilityBuff: Extra Psycasts' Fortitude, FSF's Steelskin
     * EMPPulse: Sleepy's Static Burst, FSF's EMP Pulse
     * Entomb: More Psycasts' Entomb, FSF's Entomb
     * FIYAH: Sleepy's Engulf, FSF's Flamebolt
     * SinkholeSkip: Cooler Psycasts' Trapraise, HOP's Sinkhole Skip
     * XavierAttack: Extra Psycasts' Psychic Lance, FSF's Psychic Shock*/
    public class UseCaseTags_ArcticPinhole : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Map.glowGrid.GroundGlowAt(p.Position, false, false) > 0.3f)
            {
                return true;
            }
            if (HVPAA_DecisionMakingUtility.DebilitatedByLight(p, true, true))
            {
                return false;
            }
            return false;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Spawned)
            {
                if (HVPAA_DecisionMakingUtility.MovesFasterInLight(p) && !p.Downed)
                {
                    if (p.pather.Moving)
                    {
                        if (useCase == 3)
                        {
                            return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f || HVPAA_DecisionMakingUtility.DebilitatedByLight(p, true, false);
                        }
                    }
                    if (useCase == 5)
                    {
                        return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f;
                    }
                }
                if (useCase == 4 && !p.Position.UsesOutdoorTemperature(p.Map))
                {
                    p.health.hediffSet.TryGetHediff(HediffDefOf.Heatstroke, out Hediff heat);
                    if (heat != null && heat.Severity >= 0.04f)
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
            }
            else if (useCase == 4)
            {
                return p.AmbientTemperature - p.GetStatValue(StatDefOf.ComfyTemperatureMax);
            }
            else if (useCase == 5)
            {
                return 1f;
            }
            return 1f;
        }
        public override bool TooMuchThingAdditionalCheck(Thing thing, Psycast psycast)
        {
            return WanderUtility.InSameRoom(psycast.pawn.Position, thing.Position, thing.Map);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                Dictionary<Pawn, float> pawnTargetsNonNegative = new Dictionary<Pawn, float>();
                foreach (KeyValuePair<Pawn, float> kvp in pawnTargets)
                {
                    pawnTargetsNonNegative.Add(kvp.Key, Math.Max(kvp.Value, 0f));
                }
                List<Pawn> topTargets = this.TopTargets(5, pawnTargetsNonNegative);
                if (topTargets.Count > 0)
                {
                    Pawn bestTarget = topTargets.First();
                    int bestTargetHits = 0;
                    foreach (Pawn p in topTargets)
                    {
                        int pTargetHits = 0;
                        foreach (Pawn p2 in (List<Pawn>)p.Map.mapPawns.AllPawnsSpawned)
                        {
                            if (p2.Position.DistanceTo(p.Position) <= this.aoe && GenSight.LineOfSight(p.Position, p2.Position, p.Map))
                            {
                                if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 3))
                                {
                                    pTargetHits++;
                                }
                                else if (intPsycasts.foes.Contains(p2))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 3))
                                    {
                                        pTargetHits++;
                                    }
                                    else if (p2.pather.Moving && HVPAA_DecisionMakingUtility.MovesFasterInLight(p2))
                                    {
                                        pTargetHits--;
                                    }
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
                        psycast.lti = bestTarget.Position;
                        return ((Rand.Value * 0.4f) + 0.8f) * pawnTargets.Count * this.scoreFactor;
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Room room = intPsycasts.Pawn.Position.GetRoom(intPsycasts.Pawn.Map);
            if (room != null)
            {
                int solarPinholes = 0;
                List<IntVec3> solarCells = new List<IntVec3>();
                float hotTemps = 0f;
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
                            hotTemps += this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 4);
                        }
                        if (!this.OtherAllyDisqualifiers(psycast.ability, p, 5) && p.jobs.curDriver != null && p.jobs.curDriver.ActiveSkill != null && this.light)
                        {
                            laborers.Add(p);
                        }
                    }
                }
                CompAbilityEffect_Spawn caes = psycast.ability.CompOfType<CompAbilityEffect_Spawn>();
                if (caes != null)
                {
                    if (hotTemps > 0f)
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
                            return hotTemps * this.scoreFactor;
                        }
                    }
                    else if (laborers.Count > 0)
                    {
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
        public float scoreFactor = 1f;
    }
    public class UseCaseTags_BloodStaunch : UseCaseTags
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
            return (this.scalesOffPsysens ? Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) : 1f) * Math.Max(this.ticksToFatalBloodLossCutoff - HealthUtility.TicksUntilDeathDueToBloodLoss(p), 0f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public int ticksToFatalBloodLossCutoff;
        public bool scalesOffPsysens;
    }
    public class UseCaseTags_Dart : UseCaseTags
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
                    if (tryNewPosition.IsValid && !tryNewPosition.Filled(psycast.pawn.Map) && (!tryNewPosition.Roofed(psycast.pawn.Map) || !tryNewPosition.GetRoof(psycast.pawn.Map).isThickRoof) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = 1f;
                    foreach (IntVec3 iv3 in GenRadial.RadialCellsAround(tryNewPosition, 0f, this.aoe))
                    {
                        if (iv3.InBounds(psycast.pawn.Map) && GenSight.LineOfSightToEdges(tryNewPosition, iv3, psycast.pawn.Map, true, null))
                        {
                            List<Thing> things = iv3.GetThingList(psycast.pawn.Map);
                            foreach (Thing thing in things)
                            {
                                if (thing == psycast.pawn)
                                {
                                    tryNewScore = 0f;
                                    break;
                                }
                                else if (psycast.pawn.Faction != null)
                                {
                                    if (thing is Building && (thing.Faction == null || !psycast.pawn.Faction.HostileTo(thing.Faction)))
                                    {
                                        tryNewScore = 0f;
                                        break;
                                    }
                                    else if (thing is Plant)
                                    {
                                        Zone zone = thing.Map.zoneManager.ZoneAt(thing.Position);
                                        if (zone != null && zone is Zone_Growing && !psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
                                        {
                                            tryNewScore = 0f;
                                            break;
                                        }
                                    }
                                }
                                else if (thing is Pawn p && !psycast.pawn.HostileTo(p))
                                {
                                    tryNewScore = 0f;
                                }
                            }
                            if (tryNewScore == 0f)
                            {
                                break;
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
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position);
            }
            return 0f;
        }
        public int minFertilizableCells;
    }
    public class UseCaseTags_DurabilityBuff : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.IncomingDamageFactor);
            float netFoeMeleeDPS = 0f;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.Downed && !p2.IsBurning() && intPsycasts.foes.Contains(p2) && !p2.WorkTagIsDisabled(WorkTags.Violent))
                {
                    netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                }
            }
            if (netFoeMeleeDPS > 0f)
            {
                return app * netFoeMeleeDPS;
            }
            return p.GetStatValue(StatDefOf.IncomingDamageFactor) * (p.pather.Moving ? p.GetStatValue(StatDefOf.MoveSpeed) / 4f : 1f);
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
    }
    public class UseCaseTags_EMPPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.stances.stunner.Stunned || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !HautsMiscUtility.ReactsToEMP(p);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.stances.stunner.Stunned || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !HautsMiscUtility.ReactsToEMP(p);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.MarketValue;
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
                        return bestTargetHits / 300f;
                    }
                }
            }
            return 0f;
        }
    }
    public class UseCaseTags_Entomb : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent))
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
            CompAbilityEffect_Wallraise caew = psycast.CompOfType<CompAbilityEffect_Wallraise>();
            if (caew != null && !caew.Valid(new LocalTargetInfo(p.Position), false))
            {
                return 0f;
            }
            if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.MeleeDPS);
            }
            else
            {
                return 10 * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
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
    public class UseCaseTags_FIYAH : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan;
        }
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
                if (tryNewPosition.IsValid && !possibleTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0))
                {
                    tryNewScore = 0f;
                    HVPAA_DecisionMakingUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, this.aoe, ref tryNewScore);
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
        public float ignoreAllPawnsFasterThan;
    }
    public class UseCaseTags_SinkholeSkip : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            if (this.avoidMakingTooMuchOfThing != null)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)this.Range(psycast), null, out IntVec3 spot);
                    if (spot.InBounds(psycast.pawn.Map) && GenSight.LineOfSight(psycast.pawn.Position, spot, psycast.pawn.Map) && !spot.Filled(psycast.pawn.Map) && spot.GetEdifice(psycast.pawn.Map) == null && (this.avoidMakingTooMuchOfThing.terrainAffordanceNeeded == null || spot.GetTerrain(psycast.pawn.Map).affordances.Contains(this.avoidMakingTooMuchOfThing.terrainAffordanceNeeded)))
                    {
                        if (this.TooMuchThingNearby(psycast, spot, this.aoe))
                        {
                            return IntVec3.Invalid;
                        }
                        Zone zone = psycast.pawn.Map.zoneManager.ZoneAt(spot);
                        if (zone != null && zone is Zone_Growing && !psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
                        {
                            continue;
                        }
                        bool goNext = false;
                        foreach (IntVec3 c in GenAdj.OccupiedRect(spot, this.avoidMakingTooMuchOfThing.defaultPlacingRot, this.avoidMakingTooMuchOfThing.Size).ExpandedBy(1))
                        {
                            if (c.InBounds(psycast.pawn.Map))
                            {
                                List<Thing> list = psycast.pawn.Map.thingGrid.ThingsListAt(c);
                                for (int i = 0; i < list.Count; i++)
                                {
                                    Thing thing2 = list[i];
                                    if ((thing2 is Pawn p && intPsycasts.allies.Contains(p)) || (thing2.def.category == ThingCategory.Building && thing2.def.building.isTrap) || ((thing2.def.IsBlueprint || thing2.def.IsFrame) && thing2.def.entityDefToBuild is ThingDef && ((ThingDef)thing2.def.entityDefToBuild).building.isTrap))
                                    {
                                        goNext = true;
                                    }
                                    else if ((!this.canDisplaceTrees && thing2.def.plant != null && thing2.def.plant.IsTree) || thing2.def == ThingDefOf.Plant_GrassAnima)
                                    {
                                        goNext = true;
                                    }
                                }
                            }
                        }
                        if (goNext)
                        {
                            continue;
                        }
                        else
                        {
                            return spot;
                        }
                    }
                }
            }
            return IntVec3.Invalid;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count > 0 || Rand.Chance(this.spontaneousCastChance))
            {
                IntVec3 spot = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets, this.Range(psycast.ability));
                psycast.lti = spot;
                return 10f * Math.Min(10f, (intPsycasts.foes.Count + 1f));
            }
            return 0f;
        }
        public float spontaneousCastChance;
        public bool canDisplaceTrees;
    }
    public class UseCaseTags_XavierAttack : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float chance = Rand.Value;
            this.canHitHumanlike = chance <= this.chanceToCastHumanlike || !HVPAA_Mod.settings.powerLimiting;
            this.canHitColonist = chance <= this.chanceToCastColonist || !HVPAA_Mod.settings.powerLimiting;
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.MarketValue < 1000f || (p.RaceProps.Humanlike && !this.canHitHumanlike) || (p.IsColonist && !this.canHitColonist);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue / 500f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float chanceToCast;
        public float chanceToCastHumanlike;
        public float chanceToCastColonist;
        private bool canHitHumanlike;
        private bool canHitColonist;
    }
}
