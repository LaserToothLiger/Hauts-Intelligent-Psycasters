using HautsFramework;
using HVPAA;
using RimWorld;
using Sleepys_MorePsycasts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Noise;
using VEF;

namespace HVPAA_Sleepy
{
    [StaticConstructorOnStartup]
    public class HVPAA_Sleepy
    {
    }
    //level 1
    public class UseCaseTags_Ignite : UseCaseTags
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
                if (tryNewPosition.IsValid && !possibleTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0) && FireUtility.NumFiresAt(tryNewPosition, psycast.pawn.Map) == 0)
                {
                    tryNewScore = 0f;
                    foreach (Thing thing in tryNewPosition.GetThingList(intPsycasts.Pawn.Map))
                    {
                        if (thing is Plant plant)
                        {
                            Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
                            if (zone != null && zone is Zone_Growing && intPsycasts.Pawn.Faction != null && intPsycasts.Pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
                            {
                                tryNewScore += plant.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, plant) * plant.MarketValue / 500f;
                            }
                        } else if (thing is Building b && b.Faction != null) {
                            if (intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                            {
                                tryNewScore += HVPAAUtility.LightningBuildingScore(b);
                            }
                            else if (niceToEvil > 0 || intPsycasts.Pawn.Faction == b.Faction || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                            {
                                tryNewScore -= HVPAAUtility.LightningBuildingScore(b);
                            }
                        }
                    }
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
    }
    //level 2
    public class UseCaseTags_ThisIsHowWeHeal : UseCaseTags
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
            float iNeedHealing = 0f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury hi && hi.CanHealNaturally())
                {
                    iNeedHealing += Math.Max(0f, h.Severity + h.BleedRate);
                }
            }
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * iNeedHealing;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_Immunize : UseCaseTags
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
            float iNeedHealing = 0f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is HediffWithComps hwc)
                {
                    HediffComp_Immunizable hcim = hwc.TryGetComp<HediffComp_Immunizable>();
                    if (hcim != null && hwc.def.lethalSeverity > 0f && !hwc.FullyImmune())
                    {
                        iNeedHealing += 1.5f * h.Severity / (hwc.def.lethalSeverity*Math.Max(1f, p.GetStatValue(StatDefOf.ImmunityGainSpeed)));
                    }
                }
            }
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * iNeedHealing;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_WordOfCalm : UseCaseTags
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
                return 10f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.needs.mood == null || p.needs.mood.CurLevel >= p.mindState.mentalBreaker.BreakThresholdMajor || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.InMentalState || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 0.75f*p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max((p.mindState.mentalBreaker.BreakThresholdMajor - p.needs.mood.CurLevel), 0f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_WordOfVigor : UseCaseTags
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
            return p.needs.rest == null || p.needs.rest.CurLevel >= this.restCutoff || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 1f / p.needs.rest.CurLevel;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float restCutoff;
    }
    //level 3
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
            Log.Error(p.Name.ToStringShort + " inh " + iNeedHealing);
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
            if (HVPAAUtility.SkipImmune(psycast.pawn,this.maxBodySize))
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
            if (HVPAAUtility.SkipImmune(psycast.pawn, this.maxBodySize))
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
    //level 4
    public class UseCaseTags_DeepRegen : UseCaseTags
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
            Hediff hediff;
            float iNeedHealing = 0f;
            BodyPartRecord bodyPartRecord;
            if (p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) <= this.consciousnessMalus || !HealthUtility.TryGetWorstHealthCondition(p, out hediff, out bodyPartRecord, null))
            {
                return 0f;
            }
            if (hediff != null)
            {
                iNeedHealing = Math.Max(0f, hediff.Severity + hediff.BleedRate);
                if (hediff.def.everCurableByItem && !hediff.FullyImmune())
                {
                    if (hediff.IsLethal && hediff.Severity / hediff.def.lethalSeverity >= 0.8f)
                    {
                        return 100f;
                    }
                    iNeedHealing *= (1f + hediff.Severity);
                }
                if (hediff.Part != null && hediff.Part == p.health.hediffSet.GetBrain())
                {
                    return 50f;
                }
            }
            if (bodyPartRecord != null)
            {
                return 30f;
            }
            if (!p.Downed && iNeedHealing <= 100f)
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
        public float consciousnessMalus;
    }
    public class UseCaseTags_Interdict : UseCaseTags
    {
        public bool RangedP(Pawn p)
        {
            return p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.WorkTagIsDisabled(WorkTags.Violent) && !p2.Downed && !p2.IsBurning() && p2.HostileTo(psycast.pawn))
                {
                    return 0f;
                }
            }
            return this.RangedP(psycast.pawn) ? 0f : base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p == psycast.pawn || HVPAAUtility.SkipImmune(p, this.maxBodySize) || p.Downed || p.Position.DistanceTo(psycast.pawn.Position) <= 3f || !this.RangedP(p) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
            {
                return true;
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || HVPAAUtility.SkipImmune(p, this.maxBodySize) || p.Position.DistanceTo(psycast.pawn.Position) <= 3f || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
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
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float netFoeMeleeDPS = -p.GetStatValue(StatDefOf.MeleeDPS);
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.WorkTagIsDisabled(WorkTags.Violent) && !p2.Downed && !p2.IsBurning())
                {
                    if (p2.HostileTo(p))
                    {
                        netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                    } else if (intPsycasts.allies.Contains(p2)) {
                        netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                    }
                }
            }
            return netFoeMeleeDPS;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDmg = IntVec3.Invalid;
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = intPsycasts.Pawn;
                return 2f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDef = intPsycasts.Pawn.PositionHeld;
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = intPsycasts.Pawn;
                return 2f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public IntVec3 bestDestDmg;
        public IntVec3 bestDestDef;
    }
    public class UseCaseTags_Skipscreen : UseCaseTags
    {
        public override bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            return p.HostileTo(caster) && useCase == 2;
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && t.def.building.IsMortar && t.HostileTo(psycast.pawn))
            {
                List<Thing> highShields = t.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
                for (int i = 0; i < highShields.Count; i++)
                {
                    CompProjectileInterceptor cpi = highShields[i].TryGetComp<CompProjectileInterceptor>();
                    if (cpi != null && cpi.Active && t.Position.InHorDistOf(highShields[i].PositionHeld, cpi.Props.radius))
                    {
                        return 0f;
                    }
                }
                CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                if (cpt != null && !cpt.PowerOn)
                {
                    return 0f;
                }
                return t.MarketValue;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing turret = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Thing, float> thingTargets);
            if (turret != null && turret.Spawned)
            {
                psycast.lti = turret.PositionHeld;
                return 25f;
            }
            return 0f;
        }
    }
    public class UseCaseTags_Energise : UseCaseTags
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
            return p.needs.energy == null || p.needs.energy.CurLevelPercentage*p.GetStatValue(StatDefOf.MechEnergyUsageFactor)*this.BaseFallPerDay(p) > this.daysToRunOutOfEnergyCutoff || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        private float BaseFallPerDay (Pawn pawn)
        {
            if (pawn.mindState != null && !pawn.mindState.IsIdle && !pawn.IsGestating())
            {
                return 10f;
            }
            return 3f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue/ (p.needs.energy.CurLevel*300f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float daysToRunOutOfEnergyCutoff;
    }
    //level 5
    public class UseCaseTags_MassBeckon : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.CurJob != null && p.CurJobDef == JobDefOf.GotoMindControlled) || p.Position.DistanceTo(psycast.pawn.Position) < 2f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.CurJob != null && p.CurJobDef == JobDefOf.GotoMindControlled) || p.Position.DistanceTo(psycast.pawn.Position) < 2f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * HVPAAUtility.ExpectedBeckonTime(p, psycast.pawn) * ((psycast.pawn.equipment != null && (psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsRangedWeapon)) ? 2f : 1f) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? (1f + CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)) : 1f) / 1000f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * HVPAAUtility.ExpectedBeckonTime(p, psycast.pawn) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? (1f + CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)) : 1f) / 1000f;
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
                                } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
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
                                    } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
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
    public class UseCaseTags_PU : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) || p.GetStatValue(StatDefOf.PsychicSensitivity)*p.GetStatValue(StatDefOf.MeleeDPS) <= this.minDPSxPsysensToBoost || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 2.5f * p.GetStatValue(StatDefOf.PsychicSensitivity)*p.GetStatValue(StatDefOf.MeleeDPS);
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
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minDPSxPsysensToBoost;
    }
    public class UseCaseTags_Recondition : UseCaseTags
    {
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            float multi = 0f;
            if (!t.def.useHitPoints || (t.def.category != ThingCategory.Building && t.def.category != ThingCategory.Item) || (t.MarketValue < this.minMarketValue && t.MaxHitPoints < this.minMaxHp))
            {
                return 0f;
            }
            if (t.Faction == null)
            {
                multi = 0.1f;
            } else if (psycast.pawn.Faction != null && (t.Faction == psycast.pawn.Faction || psycast.pawn.Faction.RelationKindWith(t.Faction) == FactionRelationKind.Ally || (niceToEvil > 0f && psycast.pawn.Faction.AllyOrNeutralTo(t.Faction)))) {
                multi = 3f;
            }
            return multi*(this.minMissingPercentHp-((float)t.HitPoints/(float)t.MaxHitPoints));
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing thing = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Thing, float> thingTargets);
            if (thing != null)
            {
                psycast.lti = thing;
                return thingTargets.TryGetValue(thing);
            }
            return 0f;
        }
        public float minMarketValue;
        public float minMaxHp;
        public float minMissingPercentHp;
    }
    public class UseCaseTags_Revitalise : UseCaseTags
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
            return !p.RaceProps.IsFlesh || p.health.hediffSet.HasRegeneration || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                float toAdd = 0f;
                if (h is Hediff_Injury)
                {
                    toAdd += h.Severity;
                } else if (h is Hediff_MissingPart && !p.health.hediffSet.PartOrAnyAncestorHasDirectlyAddedParts(h.Part)) {
                    toAdd += h.Part.coverageAbs;
                }
                if (toAdd > 0f)
                {
                    iNeedHealing += (h.Part == p.health.hediffSet.GetBrain() ? 10f : 1f)*toAdd;
                }
            }
            if (!p.Downed && iNeedHealing <= 100f)
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
    //level 6
    public class UseCaseTags_Resurrect : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.Faction == null ? 0f : base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t is Corpse corpse && corpse.InnerPawn != null && corpse.InnerPawn.Faction != null && (corpse.InnerPawn.Faction == psycast.pawn.Faction || psycast.pawn.Faction.RelationKindWith(corpse.InnerPawn.Faction) == FactionRelationKind.Ally || (niceToEvil > 0 && psycast.pawn.Faction.AllyOrNeutralTo(corpse.InnerPawn.Faction))) && corpse.Map.reachability.CanReach(psycast.pawn.Position, corpse.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)))
            {
                if (ModsConfig.AnomalyActive && corpse is UnnaturalCorpse)
                {
                    return 0f;
                }
                if (corpse.InnerPawn.RaceProps.Humanlike)
                {
                    return 40f;
                } else if (corpse.InnerPawn.RaceProps.Animal) {
                    return 1f;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing thing = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Thing, float> thingTargets);
            if (thing != null)
            {
                psycast.lti = thing;
                return thingTargets.TryGetValue(thing);
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_FertSkip : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            List<TerrainDef> terrainDefs = HautsUtility.FertilityTerrainDefs(psycast.pawn.Map);
            for (int i = 0; i <= 5; i++)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)(this.Range(psycast)), null, out tryNewPosition);
                    if (tryNewPosition.IsValid && !tryNewPosition.Filled(psycast.pawn.Map) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = 1f - this.minFertilizableCells;
                    foreach (IntVec3 iv3 in GenRadial.RadialCellsAround(tryNewPosition,0f,this.aoe))
                    {
                        if (iv3.InBounds(psycast.pawn.Map) && GenSight.LineOfSightToEdges(tryNewPosition,iv3,psycast.pawn.Map,true,null))
                        {
                            TerrainDef terrain = psycast.pawn.Map.terrainGrid.TerrainAt(iv3);
                            if (terrain.IsFloor || terrain.affordances.Contains(TerrainAffordanceDefOf.SmoothableStone))
                            {
                                tryNewScore = -1f;
                                break;
                            }
                            if (!terrain.IsRiver && !terrain.IsWater)
                            {
                                IOrderedEnumerable<TerrainDef> source = from e in terrainDefs.FindAll((TerrainDef e) => (double)e.fertility > (double)terrain.fertility && (double)e.fertility <= 1.0)
                                                                        orderby e.fertility
                                                                        select e;
                                if (source.Count<TerrainDef>() != 0)
                                {
                                    tryNewScore += 1f;
                                }
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
}
