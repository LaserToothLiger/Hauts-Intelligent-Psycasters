using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;
using static UnityEngine.GraphicsBuffer;

namespace HVPAA_EPC
{
    [StaticConstructorOnStartup]
    public class HVPAA_EPC
    {
    }
    //ai
    public class UseCaseTags_XLR8Dheal : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.RaceProps.IsFlesh;
        }
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (situationCase == 1 ? this.combatApplicabilityFactor : 1f) * base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
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
                        iNeedHealing += (3f * h.Severity / hwc.def.lethalSeverity) + (1f / Math.Max(1f, p.GetStatValue(StatDefOf.ImmunityGainSpeed)));
                    }
                    if (h is Hediff_Injury hi && hi.CanHealNaturally())
                    {
                        iNeedHealing += Math.Max(0f, h.Severity + 1.5f * h.BleedRate);
                    }
                }
            }
            if (iNeedHealing > 0f)
            {
                iNeedHealing += 1f;
            }
            return iNeedHealing * p.MarketValue / 1000f;
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
        public float combatApplicabilityFactor;
    }
    public class UseCaseTags_BrainBooster : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Intellectual) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.jobs.curDriver != null && p.jobs.curDriver.ActiveSkill != null && p.jobs.curDriver.ActiveSkill == SkillDefOf.Intellectual)
            {
                return this.baseApplicability;
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
        public float chanceToUtilityCast;
        public float baseApplicability;
    }
    public class UseCaseTags_BodyBooster : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            bool fleeing = intPsycasts.GetSituation() == 3;
            float app = Math.Max(0f, Math.Max(0f, useCase == 5 ? p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation) - this.manipCutoff : 0f) + Math.Max(0f, p.GetStatValue(StatDefOf.MoveSpeed) - this.moveSpeedCutoff));
            if (useCase == 5)
            {
                if (p.CurJob != null && p.CurJob.RecipeDef != null && p.CurJob.RecipeDef.workSpeedStat == StatDefOf.GeneralLaborSpeed)
                {
                    return 1.5f * app;
                }
                return 0f;
            } else {
                app *= (!fleeing && p.WorkTagIsDisabled(WorkTags.Violent)) ? 0.1f : 2.5f;
                if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
                {
                    app *= p.GetStatValue(StatDefOf.MeleeDPS);
                }
            }
            return app * (p == psycast.pawn && fleeing ? 2.5f : 1f);
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
        public float manipCutoff;
        public float moveSpeedCutoff;
        public float chanceToUtilityCast;
        public int minUtilitySkillLevel;
    }
    public class UseCaseTags_Pyrokinesis : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) >= this.ignoreAllPawnsFasterThan;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) >= this.ignoreAllPawnsFasterThan;
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
                if (tryNewPosition.IsValid && !possibleTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0) && FireUtility.NumFiresAt(tryNewPosition,psycast.pawn.Map) == 0)
                {
                    tryNewScore = 0f;
                    HVPAAUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, 1.5f, ref tryNewScore);
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
                    if (kvp.Value > highestValue / (Math.Max(1f, highestValue - 1f)))
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
    public class UseCaseTags_SootheMind : UseCaseTags
    {
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
            return !p.InMentalState || p.MentalStateDef == MentalStateDefOf.PanicFlee || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.InMentalState)
            {
                if (p.InAggroMentalState)
                {
                    float meleeDmg = p.GetStatValue(StatDefOf.MeleeDPS);
                    if (p.GetStatValue(StatDefOf.MoveSpeed) * this.movingFactor <= 0.15f)
                    {
                        return 2f * meleeDmg;
                    }
                    return meleeDmg;
                }
                return 1f;
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
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.InMentalState || p.MentalStateDef == MentalStateDefOf.PanicFlee || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.InAggroMentalState)
            {
                float threat = (p.GetStatValue(StatDefOf.MoveSpeed)-(p.GetStatValue(StatDefOf.MoveSpeed) * this.movingFactor))*p.GetStatValue(StatDefOf.MeleeDPS);
                Pawn closestPawn = null;
                foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, p.GetStatValue(StatDefOf.MoveSpeed) * 3f, true).OfType<Pawn>().Distinct<Pawn>())
                {
                    if (p2 != p && (closestPawn == null || p.Position.DistanceTo(p2.Position) <= p.Position.DistanceTo(closestPawn.Position)))
                    {
                        closestPawn = p2;
                    }
                }
                if (closestPawn != null)
                {
                    if (intPsycasts.allies.Contains(closestPawn))
                    {
                        return threat * 3f;
                    } else if (intPsycasts.foes.Contains(closestPawn)) {
                        return 0f;
                    }
                }
                return threat*0.1f;
            }
            return 0f;
        }
        public float movingFactor;
    }
    public class UseCaseTags_SenseThoughts : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase != 2 || psycast.pawn.WorkTagIsDisabled(WorkTags.Social) || psycast.pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff) || (!Rand.Chance(this.chanceToUtilityCast) && psycast.pawn.trader == null))
            {
                return 0f;
            }
            return base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            psycast.lti = intPsycasts.Pawn;
            return 1f;
        }
        public float chanceToUtilityCast;
    }
    public class UseCaseTags_Precog : UseCaseTags
    {
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.WorkTagIsDisabled(WorkTags.Violent) || psycast.pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
            {
                return 0f;
            }
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            float targets = 0f;
            foreach (Pawn p in intPsycasts.foes)
            {
                if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.foeScanRange)
                {
                    targets += 0.1f;
                }
            }
            psycast.lti = intPsycasts.Pawn;
            return targets;
        }
        public float foeScanRange;
    }
    public class UseCaseTags_Stasis : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.RaceProps.IsMechanoid;
        }
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return situationCase == 1 ? 0f : base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (this.ticksToFatalBloodLossCutoff - HealthUtility.TicksUntilDeathDueToBloodLoss(p))/1250f;
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
        public int ticksToFatalBloodLossCutoff;
    }
    //Soothe Mind rework
    public class CompAbilityEffect_SootheMind : CompAbilityEffect_GiveHediff
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Thing != null && target.Thing is Pawn p && !p.InMentalState)
            {
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }
}
