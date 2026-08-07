using HautsFramework;
using HVPAA;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;

namespace HVPAA_CoolerPsycasts
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_ChemfuelSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (1f + p.GetStatValue(StatDefOf.ArmorRating_Heat)) / (p.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, p) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Burn, p));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (1f + p.GetStatValue(StatDefOf.ArmorRating_Heat)) / (p.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, p) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Burn, p));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                return this.FindPulseTarget(intPsycasts, psycast, niceToEvil, pawnTargets);
            }
            return 0f;
        }
        public float ignoreAllPawnsFasterThan;
    }
    public class UseCaseTags_Distortion : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon || p.pather.MovingNow || p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                return this.FindPulseTarget(intPsycasts, psycast, niceToEvil, pawnTargets);
            }
            return 0f;
        }
    }
    public class UseCaseTags_InduceRain : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Map map = psycast.pawn.Map;
            if (map == null || map.generatorDef.isUnderground || map.weatherManager.RainRate >= 0.01f)
            {
                return 0f;
            }
            return base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.Pawn.Spawned)
            {
                psycast.lti = intPsycasts.Pawn;
                Map map = intPsycasts.Pawn.Map;
                List<Thing> list = map.listerThings.ThingsOfDef(ThingDefOf.Fire);
                int rainableFires = 0;
                foreach (Thing t in list)
                {
                    RoofDef roof = map.roofGrid.RoofAt(t.Position);
                    if (roof == null)
                    {
                        rainableFires++;
                    }
                    else if (!roof.isThickRoof)
                    {
                        Thing edifice = t.Position.GetEdifice(t.Map);
                        if (edifice != null && edifice.def.holdsRoof)
                        {
                            rainableFires++;
                        }
                    }
                }
                return (rainableFires - 100f) * 10f;
            }
            return 0f;
        }
    }
    public class UseCaseTags_Timeshunt : UseCaseTags
    {
        public override float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(chanceToCast) ? base.PriorityScoreDebuff(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn) / 500f;
            }
            return 0f;
        }
        public float chanceToCast;
    }
}
