using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_Sleepy
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
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
            if (p == psycast.pawn || HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize) || p.Downed || p.Position.DistanceTo(psycast.pawn.Position) <= 3f || !this.RangedP(p) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
            {
                return true;
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || HVPAA_DecisionMakingUtility.SkipImmune(p, this.maxBodySize) || p.Position.DistanceTo(psycast.pawn.Position) <= 3f || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
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
                    }
                    else if (intPsycasts.allies.Contains(p2))
                    {
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
            return p.needs.energy == null || p.needs.energy.CurLevelPercentage * p.GetStatValue(StatDefOf.MechEnergyUsageFactor) * this.BaseFallPerDay(p) > this.daysToRunOutOfEnergyCutoff || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        private float BaseFallPerDay(Pawn pawn)
        {
            if (pawn.mindState != null && !pawn.mindState.IsIdle && !pawn.IsGestating())
            {
                return 10f;
            }
            return 3f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue / (p.needs.energy.CurLevel * 300f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float daysToRunOutOfEnergyCutoff;
    }
}
