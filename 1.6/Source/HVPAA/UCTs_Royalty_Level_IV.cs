using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VEF;
using Verse;
using Verse.AI;

namespace HVPAA
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
     * GiveToDoctor is specifically integrated with Skip's healing use: teleport a downed pawn who's going to bleed out to a nearby ally with the best Medical skill, then use GTD to tell that pawn to tend the teleported pawn*/
    public class UseCaseTags_Focus : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || (useCase == 5 ? (!p.RaceProps.Humanlike || p.skills == null) : p.WorkTagIsDisabled(WorkTags.Violent)) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(0f, Math.Max(0f, (p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) - this.sightCutoff)) + Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.movingCutoff));
            if (useCase == 5)
            {
                if (p.skills != null && p.jobs.curDriver != null && p.jobs.curDriver.ActiveSkill != null)
                {
                    return app *= (p.skills.GetSkill(p.jobs.curDriver.ActiveSkill).Level - minUtilitySkillLevel);
                }
                return 0f;
            }
            else
            {
                app *= 2.5f;
                if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
                {
                    app *= p.GetStatValue(StatDefOf.MeleeDPS);
                }
                else
                {
                    app *= 10 * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
                }
            }
            return app * (p == psycast.pawn && intPsycasts.GetSituation() == 3 ? 2.5f : 1f);
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
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (!Rand.Chance(this.chanceToUtilityCast))
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
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float sightCutoff;
        public float movingCutoff;
        public float chanceToUtilityCast;
        public int minUtilitySkillLevel;
    }
    public class UseCaseTags_Smokepop : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.IsBurning() || (!p.Downed && p.GetStatValue(StatDefOf.MoveSpeed) >= 1f);
        }
        public override bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            if (p.HostileTo(caster) && useCase == 2)
            {
                return true;
            }
            else if (p.Faction != null && caster.Faction != null && (p.Faction == caster.Faction || (niceToEvil > 0 && p.Faction.RelationKindWith(caster.Faction) == FactionRelationKind.Ally)) && useCase == 3)
            {
                return true;
            }
            return false;
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && t.def.building.IsTurret && t.def.building.ai_combatDangerous && !t.Position.AnyGas(t.Map, GasType.BlindSmoke) && t.HostileTo(psycast.pawn))
            {
                CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                if (cpt != null && !cpt.PowerOn)
                {
                    return 0f;
                }
                return t.MarketValue / 200f;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing turret = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Thing, float> thingTargets);
            if (turret != null && turret.SpawnedOrAnyParentSpawned)
            {
                psycast.lti = turret.PositionHeld;
                float netMarketValue = turret.MarketValue / 50f;
                foreach (Thing t in thingTargets.Keys)
                {
                    if (t != turret)
                    {
                        netMarketValue += t.MarketValue / 200f;
                    }
                }
                return netMarketValue;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (psycast.Caster.Position.AnyGas(psycast.Caster.Map, GasType.BlindSmoke))
            {
                return 0f;
            }
            int numShooters = (int)this.ApplicabilityScoreDefense(intPsycasts, psycast, niceToEvil);
            foreach (Pawn p in (List<Pawn>)psycast.Caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                {
                    if (p.Position.DistanceTo(p.Position) <= this.aoe && intPsycasts.allies.Contains(p))
                    {
                        return 0f;
                    }
                    if (p.Position.DistanceTo(p.Position) <= this.Range(psycast.ability) && !p.Position.AnyGas(p.Map, GasType.BlindSmoke))
                    {
                        if (intPsycasts.foes.Contains(p) && GenSight.LineOfSight(psycast.Caster.Position, p.Position, p.Map))
                        {
                            numShooters++;
                        }
                    }
                }
            }
            psycast.lti = psycast.Caster.pather.nextCell.IsValid ? psycast.Caster.pather.nextCell : psycast.Caster.Position;
            return 2f * numShooters;
        }
        public override float Range(Psycast psycast)
        {
            return this.rangeOffset;
        }
    }
    public class UseCaseTags_WordOfSerenity : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase != 1)
            {
                return 0f;
            }
            return base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public Pawn FindMentallyBrokenTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if ((!this.requiresLoS || GenSight.LineOfSight(origin, p.Position, p.Map)) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget))
                {
                    float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
                    if (pApplicability > 0f)
                    {
                        CompAbilityEffect_StopMentalState sms = psycast.CompOfType<CompAbilityEffect_StopMentalState>();
                        if (sms != null && sms.PsyfocusCostForTarget(p) <= psycast.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
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
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindMentallyBrokenTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) / 4f);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindMentallyBrokenTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.InMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (!p.RaceProps.Humanlike && p.MarketValue < 500) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            CompAbilityEffect_StopMentalState sms = psycast.CompOfType<CompAbilityEffect_StopMentalState>();
            float smsMulti = 1f;
            if (sms != null)
            {
                switch (sms.TargetMentalBreakIntensity(p))
                {
                    case MentalBreakIntensity.Extreme:
                        smsMulti = useCase == 5 ? 100 : 15;
                        break;
                    case MentalBreakIntensity.Major:
                        smsMulti = useCase == 5 ? 20 : 5;
                        break;
                    default:
                        smsMulti = useCase == 5 ? 2 : 1;
                        break;
                }
            }
            int multi;
            switch (p.MentalStateDef.category)
            {
                case MentalStateCategory.Aggro:
                    multi = 20;
                    break;
                case MentalStateCategory.Malicious:
                    multi = useCase == 5 ? 2 : 0;
                    break;
                default:
                    multi = useCase == 5 ? 1 : 0;
                    break;
            }
            return multi * smsMulti;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_Skip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize))
            {
                return true;
            }
            if (initialTarget)
            {
                switch (useCase)
                {
                    case 2:
                        foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                        {
                            if (p2.HostileTo(p))
                            {
                                return true;
                            }
                        }
                        break;
                    case 3:
                        if (!this.RangedP(p))
                        {
                            return true;
                        }
                        break;
                    default:
                        break;
                }
            }
            if (useCase != 4)
            {
                if (p.Downed)
                {
                    return true;
                }
                if (!initialTarget)
                {
                    if (!this.RangedP(p) || p.WorkTagIsDisabled(WorkTags.Violent))
                    {
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 2:
                    if (!this.RangedP(p))
                    {
                        return true;
                    }
                    break;
                default:
                    break;
            }
            return HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize);
        }
        public bool RangedP(Pawn p)
        {
            return p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 2:
                    if (initialTarget)
                    {
                        float cover = 1f;
                        Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.skipRange, false, p);
                        if (pawn != null)
                        {
                            cover = Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(pawn.Position, pawn.Map)), 1f);
                            this.bestDestDeb = pawn.Position;
                            return Math.Max(0f, ((p.GetStatValue(StatDefOf.MeleeDPS) * cover) - pawn.GetStatValue(StatDefOf.MeleeDPS)) * (pawn.Position.DistanceTo(p.Position) / (float)Math.Sqrt(p.GetStatValue(StatDefOf.MoveSpeed))));
                        }
                    }
                    else
                    {
                        return p.GetStatValue(StatDefOf.MeleeDPS);
                    }
                    break;
                case 3:
                    float netFoeMeleeDPS = -p.GetStatValue(StatDefOf.MeleeDPS);
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.WorkTagIsDisabled(WorkTags.Violent) && !p2.Downed && !p2.IsBurning())
                        {
                            if (p2.HostileTo(p))
                            {
                                netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                            }
                            else if (intPsycasts.allies.Contains(p2))
                            {
                                netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                            }
                        }
                    }
                    this.bestDestDef = psycast.pawn.PositionHeld;
                    if (netFoeMeleeDPS > 0f)
                    {
                        List<Thing> foeTargetCache = new List<Thing>();
                        foeTargetCache.AddRange(from a in p.Map.attackTargetsCache.GetPotentialTargetsFor(p) where !a.ThreatDisabled(p) select a.Thing);
                        this.bestDestDef = CellFinderLoose.GetFallbackDest(p, foeTargetCache, this.skipRange, 2f, 2f, 20, (IntVec3 c) => c.IsValid && (!this.requiresLoS || GenSight.LineOfSight(c, p.Position, intPsycasts.Pawn.Map)));
                    }
                    return Math.Max(0f, netFoeMeleeDPS);
                case 4:
                    if (p.Downed && HealthUtility.TicksUntilDeathDueToBloodLoss(p) <= 10000)
                    {
                        Pawn bestDoctorInRange = null;
                        float bestDoctorLevel = -1f;
                        foreach (Pawn p2 in intPsycasts.allies)
                        {
                            if (p2.CurJobDef == JobDefOf.TendPatient && p2.CurJob.targetA.Pawn != null && p2.CurJob.targetA.Pawn == p)
                            {
                                return 0f;
                            }
                            if (p2.Downed || p2.IsPlayerControlled || p2.HasPsylink || p.Position.DistanceTo(p2.Position) > this.skipRange)
                            {
                                continue;
                            }
                            float doctorLevel = -1f;
                            if (!p2.WorkTagIsDisabled(WorkTags.Caring) || (p2.RaceProps.mechEnabledWorkTypes != null && p2.RaceProps.mechEnabledWorkTypes.Contains(WorkTypeDefOf.Doctor)))
                            {
                                doctorLevel = p2.GetStatValue(StatDefOf.MedicalTendQuality) * p2.GetStatValue(StatDefOf.MedicalTendSpeed);
                            }
                            if (doctorLevel > bestDoctorLevel || (bestDoctorInRange != null && doctorLevel == bestDoctorLevel && p.Position.DistanceTo(p2.Position) <= p.Position.DistanceTo(bestDoctorInRange.Position)))
                            {
                                bestDoctorInRange = p2;
                                bestDoctorLevel = doctorLevel;
                            }
                        }
                        if (bestDoctorInRange != null)
                        {
                            this.bestDestHeal = bestDoctorInRange.Position;
                            CompAbilityEffect_GiveToDoctor gtd = psycast.CompOfType<CompAbilityEffect_GiveToDoctor>();
                            if (gtd != null)
                            {
                                gtd.doctor = bestDoctorInRange;
                            }
                            return bestDoctorLevel;
                        }
                    }
                    break;
                default:
                    break;
            }
            return -1f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 1:
                    Building bestTrap = null;
                    float bestTrapChance = 0f;
                    foreach (Building b in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.skipRange, true).OfType<Building>().Distinct<Building>())
                    {
                        if (b is Building_Trap bpt && (!this.requiresLoS || GenSight.LineOfSight(b.Position, p.Position, p.Map)))
                        {
                            float tsc = this.TrapSpringChance(bpt, p);
                            if (tsc > bestTrapChance)
                            {
                                bestTrapChance = tsc;
                                bestTrap = bpt;
                            }
                        }
                    }
                    if (bestTrap != null)
                    {
                        this.bestDestDmg = bestTrap.Position;
                        return bestTrapChance;
                    }
                    return 0f;
                case 2:
                    if (initialTarget)
                    {
                        float cover = Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)), 1f);
                        Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.skipRange, false, p);
                        if (pawn != null)
                        {
                            this.bestDestDeb = pawn.Position;
                            return Math.Max(0f, ((pawnTargets.TryGetValue(pawn) * cover) - p.GetStatValue(StatDefOf.MeleeDPS)) * (pawn.Position.DistanceTo(p.Position) / (float)Math.Sqrt(pawn.GetStatValue(StatDefOf.MoveSpeed))));
                        }
                    }
                    else
                    {
                        return 1f / p.GetStatValue(StatDefOf.MeleeDPS);
                    }
                    break;
                case 3:
                    float netFoeMeleeDPS = p.GetStatValue(StatDefOf.MeleeDPS);
                    bool anyNearbyAllies = false;
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.Downed && !p2.IsBurning() && !p2.WorkTagIsDisabled(WorkTags.Violent))
                        {
                            if (intPsycasts.allies.Contains(p2))
                            {
                                anyNearbyAllies = true;
                                netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                            }
                            else if (intPsycasts.foes.Contains(p2))
                            {
                                netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                            }
                        }
                    }
                    if (anyNearbyAllies)
                    {
                        List<Thing> foeTargetCache = new List<Thing>();
                        foeTargetCache.AddRange(from a in p.Map.attackTargetsCache.GetPotentialTargetsFor(p) where !a.ThreatDisabled(p) select a.Thing);
                        this.bestDestDef = CellFinderLoose.GetFallbackDest(p, foeTargetCache, this.skipRange, 2f, 2f, 20, (IntVec3 c) => c.IsValid && (!this.requiresLoS || GenSight.LineOfSight(c, p.Position, intPsycasts.Pawn.Map)));
                        return netFoeMeleeDPS;
                    }
                    break;
                default:
                    break;
            }
            return 0f;
        }
        public float TrapSpringChance(Building_Trap bpt, Pawn p)
        {
            float num = 1f;
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }
            if (bpt.KnowsOfTrap(p))
            {
                if (p.Faction == null)
                {
                    if (p.IsAnimal)
                    {
                        num = 0.2f;
                        num *= bpt.def.building.trapPeacefulWildAnimalsSpringChanceFactor;
                    }
                    else
                    {
                        num = 0.3f;
                    }
                }
                else if (p.Faction == bpt.Faction)
                {
                    num = 0.005f;
                }
                else
                {
                    num = 0f;
                }
            }
            num *= bpt.GetStatValue(StatDefOf.TrapSpringChance, true, -1) * p.GetStatValue(StatDefOf.PawnTrapSpringChance, true, -1);
            return Mathf.Clamp01(num);
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase == 3 || situationCase == 5)
            {
                return 1f;
            }
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDmg = IntVec3.Invalid;
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = (this.bestDestDmg.IsValid ? this.bestDestDmg : psycast.Caster.Position);
                return 2f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDeb = IntVec3.Invalid;
            int situation = intPsycasts.GetSituation();
            if (Rand.Chance(0.5f))
            {
                Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    psycast.ltiDest = (this.bestDestDeb.IsValid ? this.bestDestDeb : psycast.Caster.Position);
                    return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                }
            }
            else
            {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    psycast.ltiDest = (this.bestDestDeb.IsValid ? this.bestDestDeb : psycast.Caster.Position);
                    return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                }
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
                        if ((!this.requiresLoS || GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map)) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                        {
                            this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                        }
                    }
                    psycast.lti = intPsycasts.Pawn;
                    psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                    return pathDistance * this.scoreFactor;
                }
            }
            else
            {
                if (Rand.Chance(0.5f))
                {
                    Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null && this.bestDestDef.IsValid)
                    {
                        psycast.lti = pawn;
                        psycast.ltiDest = this.bestDestDef;
                        return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                    }
                }
                else
                {
                    Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null)
                    {
                        psycast.lti = pawn;
                        psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                        return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestHeal = IntVec3.Invalid;
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null && this.bestDestHeal.IsValid)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.bestDestHeal;
                return 2f * pawnTargets.TryGetValue(pawn) * 5000f * this.scoreFactor / HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            int situation = intPsycasts.GetSituation();
            if (situation != 2 && intPsycasts.Pawn.pather != null && intPsycasts.Pawn.pather.curPath != null && intPsycasts.Pawn.pather.curPath.Found)
            {
                int pathDistance = 0;
                for (int i = 0; i < intPsycasts.Pawn.pather.curPath.NodesLeftCount; i++)
                {
                    pathDistance++;
                    if ((!this.requiresLoS || GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map)) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                    {
                        this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                    }
                }
                psycast.lti = intPsycasts.Pawn;
                psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                return pathDistance * this.scoreFactor;
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public IntVec3 bestDestDmg;
        public IntVec3 bestDestDeb;
        public IntVec3 bestDestDef;
        public IntVec3 bestDestHeal;
        public float skipRange;
        public float scoreFactor = 1f;
    }
    public class CompProperties_AbilityGiveToDoctor : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityGiveToDoctor()
        {
            this.compClass = typeof(CompAbilityEffect_GiveToDoctor);
        }
    }
    public class CompAbilityEffect_GiveToDoctor : CompAbilityEffect
    {
        public new CompProperties_AbilityGiveToDoctor Props
        {
            get
            {
                return (CompProperties_AbilityGiveToDoctor)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Pawn != null && doctor != null && !doctor.DeadOrDowned && doctor.CurJobDef != JobDefOf.TendPatient && !doctor.IsPlayerControlled && !doctor.HasPsylink)
            {
                doctor.jobs.StartJob(JobMaker.MakeJob(JobDefOf.TendPatient, target.Pawn, HealthAIUtility.FindBestMedicine(doctor, target.Pawn, true)), JobCondition.InterruptForced);
                doctor = null;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look<Pawn>(ref this.doctor, "doctor", false);
        }
        public Pawn doctor;
    }
}
