using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace HVPAA_Sleepy
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Chemskip : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.Faction == null || !Rand.Chance(this.chance))
            {
                return 0f;
            }
            List<Thing> list = psycast.pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse);
            if (list.Count > 0)
            {
                foreach (Thing t in list)
                {
                    if (t is Corpse corpse && corpse.InnerPawn != null && corpse.InnerPawn.Faction != null && corpse.InnerPawn.Faction.HostileTo(psycast.pawn.Faction))
                    {
                        WorldComponent_HautsDelayedResurrections WCDR = (WorldComponent_HautsDelayedResurrections)Find.World.GetComponent(typeof(WorldComponent_HautsDelayedResurrections));
                        if (WCDR != null && WCDR.CorpseHasDelayedResurrection(corpse))
                        {
                            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
                        }
                        if (ModsConfig.AnomalyActive)
                        {
                            Hediff_DeathRefusal deathres = corpse.InnerPawn.health.hediffSet.GetFirstHediff<Hediff_DeathRefusal>();
                            if (deathres != null)
                            {
                                return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
                            }
                        }
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing corpse = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Thing, float> thingTargets);
            if (corpse != null)
            {
                psycast.lti = corpse;
                return thingTargets.TryGetValue(corpse) * 2f;
            }
            return 0f;
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t is Corpse corpse && corpse.InnerPawn != null && corpse.InnerPawn.Faction != null && corpse.InnerPawn.Faction.HostileTo(psycast.pawn.Faction))
            {
                WorldComponent_HautsDelayedResurrections WCDR = (WorldComponent_HautsDelayedResurrections)Find.World.GetComponent(typeof(WorldComponent_HautsDelayedResurrections));
                if (WCDR != null && WCDR.CorpseHasDelayedResurrection(corpse))
                {
                    return this.CorpseMarketValue(corpse.InnerPawn);
                }
                if (ModsConfig.AnomalyActive)
                {
                    Hediff_DeathRefusal deathres = corpse.InnerPawn.health.hediffSet.GetFirstHediff<Hediff_DeathRefusal>();
                    if (deathres != null)
                    {
                        return this.CorpseMarketValue(corpse.InnerPawn);
                    }
                }
            }
            return 0f;
        }
        public float CorpseMarketValue(Pawn pawn)
        {
            return Math.Max(pawn.MarketValue, pawn.def.BaseMarketValue) - this.minMarketValue;
        }
        public float chance;
        public float minMarketValue;
    }
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
                        iNeedHealing += 1.5f * h.Severity / (hwc.def.lethalSeverity * Math.Max(1f, p.GetStatValue(StatDefOf.ImmunityGainSpeed)));
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
            return 0.75f * p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max((p.mindState.mentalBreaker.BreakThresholdMajor - p.needs.mood.CurLevel), 0f);
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
}
