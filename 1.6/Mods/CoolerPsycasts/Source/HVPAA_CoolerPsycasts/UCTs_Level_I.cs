using CoolPsycasts;
using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA_CoolerPsycasts
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml. Yeah I'm going to keep saying this. Actually, go read my user manual too.
     * CombatRelevantSkill is literally just applied to Shooting and Melee (you could make an argument it should work with Toughness from Toughness Skill too, if that ever becomes a thing).
     *   Skill Drain is used in combat on pawns that are good at these skills, and they handle which hediffs are applied to target and caster*/
    public class UseCaseTags_Despair : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(chanceToCast) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 1f / Math.Max(this.consciousnessMalus, this.consciousnessMalus + 0.30f - p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn) * 0.35f;
            }
            return 0f;
        }
        public float consciousnessMalus;
        public float chanceToCast;
    }
    public class UseCaseTags_EaseGravity : UseCaseTags
    {
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff) ? 1f : base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = intPsycasts.Pawn;
            psycast.lti = pawn;
            if (pawn.pather.Moving)
            {
                if (!pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                {
                    int situation = intPsycasts.GetSituation();
                    if (situation == 3 || situation == 5)
                    {
                        return 5f;
                    }
                    if (pawn.equipment == null || pawn.equipment.Primary == null || !pawn.equipment.Primary.def.IsRangedWeapon)
                    {
                        if (pawn.CurJob != null && pawn.CurJobDef == JobDefOf.AttackMelee)
                        {
                            return pawn.GetStatValue(StatDefOf.MeleeDPS);
                        }
                    }
                    else
                    {
                        return CoverUtility.TotalSurroundingCoverScore(pawn.pather.curPath.LastNode, pawn.Map);
                    }
                }
            }
            else if (pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
            {
                return 999998f;
            }
            return 0f;
        }
    }
    public class UseCaseTags_Emesis : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return situationCase == 1 ? 0f : base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.needs.food == null;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float multi = p.needs.food != null ? 1.5f * p.needs.food.CurLevelPercentage : 1f;
            CompAbilityEffect_RemoveHediffs rh = psycast.CompOfType<CompAbilityEffect_RemoveHediffs>();
            if (rh != null)
            {
                foreach (Hediff h in p.health.hediffSet.hediffs)
                {
                    if (rh.Props.hediffDefs.Contains(h.def))
                    {
                        multi -= 0.3f;
                    }
                }
            }
            return multi * p.GetStatValue(StatDefOf.PsychicSensitivity) * p.MarketValue / 1000f;
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
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            int numConditions = 0;
            CompAbilityEffect_RemoveHediffs rh = psycast.CompOfType<CompAbilityEffect_RemoveHediffs>();
            if (rh != null)
            {
                foreach (Hediff h in p.health.hediffSet.hediffs)
                {
                    if (rh.Props.hediffDefs.Contains(h.def))
                    {
                        numConditions++;
                    }
                }
            }
            return numConditions;
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
    }
    public class UseCaseTags_Flowers : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (Rand.Chance(chanceToUtilityCast) && (psycast.pawn.Faction == null || (psycast.pawn.needs.beauty != null && psycast.pawn.needs.beauty.CurLevelPercentage <= this.minBeautyForLikelierCasting) || (psycast.pawn.Map.ParentFaction != null && (psycast.pawn.Map.ParentFaction == psycast.pawn.Faction || psycast.pawn.Faction.RelationKindWith(psycast.pawn.Map.ParentFaction) == FactionRelationKind.Ally || (niceToEvil > 0 && psycast.pawn.Faction.RelationKindWith(psycast.pawn.Map.ParentFaction) == FactionRelationKind.Neutral))))) ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
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
                    if (tryNewPosition.InBounds(psycast.pawn.Map) && !tryNewPosition.Filled(psycast.pawn.Map) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = 0f;
                    foreach (IntVec3 iv3 in GenRadial.RadialCellsAround(tryNewPosition, 0f, this.aoe))
                    {
                        if (iv3.InBounds(psycast.pawn.Map) && GenSight.LineOfSightToEdges(tryNewPosition, iv3, psycast.pawn.Map, true, null))
                        {
                            if (this.plant.CanNowPlantAt(iv3, psycast.pawn.Map, false))
                            {
                                tryNewScore += 1f;
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
        public float chanceToUtilityCast;
        public float minBeautyForLikelierCasting;
        public ThingDef plant;
    }
    public class CombatRelevantSkill : DefModExtension
    {
        public CombatRelevantSkill() { }
        public HediffDef toTarget;
        public HediffDef toCaster;
    }
    public class UseCaseTags_SkillDrain : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.skills == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float multi = 1f;
            List<SkillDef> combatRelevantSkills = DefDatabase<SkillDef>.AllDefsListForReading.Where((SkillDef sd) => sd.HasModExtension<CombatRelevantSkill>() && p.skills.GetSkill(sd).Level > 0 && !psycast.pawn.skills.GetSkill(sd).TotallyDisabled).ToList();
            if (!combatRelevantSkills.NullOrEmpty())
            {
                SkillDef finalSd = combatRelevantSkills.RandomElementByWeight((SkillDef sd) => (float)p.skills.GetSkill(sd).Level / ((float)psycast.pawn.skills.GetSkill(sd).Level));
                multi = (float)p.skills.GetSkill(finalSd).Level / ((float)psycast.pawn.skills.GetSkill(finalSd).Level);
            }
            return multi * p.GetStatValue(StatDefOf.PsychicSensitivity);
        }
        public override float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.skills == null ? 0f : base.PriorityScoreDebuff(psycast, situationCase, pacifist, niceToEvil, usableFoci);
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
    public class UseCaseTags_VC : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.RaceProps.IsMechanoid;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsMiscUtility.DamageFactorFor(this.damageType, p) * (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f) / p.GetStatValue(StatDefOf.IncomingDamageFactor);
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
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.RaceProps.IsMechanoid || p.needs.energy == null;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.needs.energy != null)
            {
                float energy = p.needs.energy.CurLevelPercentage;
                if (energy <= this.keepMechEnergyAbove)
                {
                    return 1f - energy;
                }
            }
            return 0f;
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
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.Faction == null ? 0f : base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            return typeof(Building_Battery).IsAssignableFrom(p.GetType()) && p.Faction != null && (p.Faction == caster.Faction || (niceToEvil > 0 ? caster.Faction.RelationKindWith(p.Faction) != FactionRelationKind.Hostile : caster.Faction.RelationKindWith(p.Faction) == FactionRelationKind.Ally));
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            CompPowerBattery cpb = t.TryGetComp<CompPowerBattery>();
            if (cpb != null && cpb.StoredEnergy <= this.keepBatteryEnergyAbove)
            {
                return 1f - cpb.StoredEnergyPct;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing battery = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Thing, float> thingTargets, this.Range(psycast.ability) / 2f);
            if (battery != null)
            {
                psycast.lti = battery;
                return thingTargets.TryGetValue(battery);
            }
            return 0f;
        }
        public DamageDef damageType;
        public float keepMechEnergyAbove;
        public float keepBatteryEnergyAbove;
    }
}
