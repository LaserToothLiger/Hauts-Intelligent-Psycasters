using CoolPsycasts;
using HarmonyLib;
using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI.Group;
using Verse.Noise;
using VFECore;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;

namespace HVPAA_CoolerPsycasts
{
    [StaticConstructorOnStartup]
    public class HVPAA_CoolerPsycasts
    {
        private static readonly Type patchType = typeof(HVPAA_CoolerPsycasts);
        static HVPAA_CoolerPsycasts()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.HVPAA.cooler");
            if (ModsConfig.IdeologyActive)
            {
                harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_Enslave), nameof(CompAbilityEffect_Enslave.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                               prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityEnslave_Apply_Prefix)));
            }
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_GrantPsycast), nameof(CompAbilityEffect_GrantPsycast.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityGP_Apply_Prefix)));
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_SkillDrain), nameof(CompAbilityEffect_SkillDrain.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilitySD_Apply_Prefix)));
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_VanometricCharge), nameof(CompAbilityEffect_VanometricCharge.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityVC_Apply_Prefix)));
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_Duplicate), nameof(CompAbilityEffect_Duplicate.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityDuplicate_Apply_Prefix)));
        }
        public static bool HVPAA_AbilityEnslave_Apply_Prefix(CompAbilityEffect_Enslave __instance, LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                Pawn pawn = __instance.parent.pawn;
                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
                {
                    target.Pawn.guest.SetGuestStatus(pawn.Faction, GuestStatus.Slave);
                    Lord lord = pawn.GetLord();
                    if (lord != null)
                    {
                        lord.AddPawn(target.Pawn);
                    } else {
                        LordMaker.MakeNewLord(pawn.Faction, new LordJob_EscortPawn(pawn), pawn.Map, Gen.YieldSingle<Pawn>(target.Pawn));
                    }
                    return false;
                }
            }
            return true;
        }
        public static bool HVPAA_AbilityGP_Apply_Prefix(CompAbilityEffect_GrantPsycast __instance, LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                Pawn pawn = __instance.parent.pawn;
                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
                {
                    List<AbilityDef> validPsycasts = DefDatabase<AbilityDef>.AllDefs.Where(x => x.IsPsycast && x.level >= __instance.Props.minLevel && x.level <= __instance.Props.maxLevel && x.level <= target.Pawn.GetPsylinkLevel() && target.Pawn.abilities.GetAbility(x) == null).ToList();
                    if (!validPsycasts.NullOrEmpty())
                    {
                        pawn.abilities.GainAbility(validPsycasts.RandomElementByWeight((AbilityDef a) => a.level));
                    }
                    return false;
                }
            }
            return true;
        }
        public static bool HVPAA_AbilitySD_Apply_Prefix(CompAbilityEffect_SkillDrain __instance, LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                Pawn pawn = __instance.parent.pawn;
                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
                {
                    List<SkillDef> combatRelevantSkills = DefDatabase<SkillDef>.AllDefsListForReading.Where((SkillDef sd) => sd.HasModExtension<CombatRelevantSkill>() && target.Pawn.skills.GetSkill(sd).Level > 0 && !pawn.skills.GetSkill(sd).TotallyDisabled).ToList();
                    if (!combatRelevantSkills.NullOrEmpty())
                    {
                        SkillDef finalSd = combatRelevantSkills.RandomElementByWeight((SkillDef sd) => (float)target.Pawn.skills.GetSkill(sd).Level / ((float)pawn.skills.GetSkill(sd).Level));
                        CombatRelevantSkill crs = finalSd.GetModExtension<CombatRelevantSkill>();
                        if (crs != null)
                        {
                            int amount = (int)(Mathf.Min(__instance.Props.baseStacks, target.Pawn.skills.GetSkill(finalSd).Level) * target.Pawn.GetStatValue(__instance.Props.scaleWithTargetStat) * pawn.GetStatValue(__instance.Props.scaleWithTargetStat));
                            while (amount > 0)
                            {
                                target.Pawn.health.AddHediff(crs.toTarget, null);
                                pawn.health.AddHediff(crs.toCaster, null);
                                amount--;
                            }
                        }
                    }
                    return false;
                }
            }
            return true;
        }
        public static bool HVPAA_AbilityVC_Apply_Prefix(CompAbilityEffect_VanometricCharge __instance, LocalTargetInfo target)
        {
            if (target.Pawn != null && target.Pawn.RaceProps.IsMechanoid)
            {
                Pawn pawn = __instance.parent.pawn;
                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
                {
                    if (pawn.HostileTo(target.Pawn))
                    {
                        target.Pawn.TakeDamage(new DamageInfo(DamageDefOf.ElectricalBurn, __instance.Props.damageAmount, 5, -1, target.Pawn, target.Pawn.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, BodyPartTagDefOf.BloodPumpingSource).RandomElement()));
                    }
                    else if (target.Pawn.needs.energy != null)
                    {
                        target.Pawn.needs.energy.CurLevel += __instance.Props.mechChargeAmount / target.Pawn.BodySize;
                    }
                    return false;
                }
            }
            return true;
        }
        public static bool HVPAA_AbilityDuplicate_Apply_Prefix(CompAbilityEffect_Duplicate __instance, LocalTargetInfo target)
        {
            Pawn pawn = __instance.parent.pawn;
            if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
            {
                for (int i = 1; i <= __instance.Props.count; i++)
                {
                    Pawn copy = Find.PawnDuplicator.Duplicate(pawn);
                    GenSpawn.Spawn(copy, target.Cell, pawn.Map);
                    while (true)
                    {
                        Ability ability = copy.abilities.abilities.FirstOrDefault((Ability x) => x.def.level != 0);
                        if (ability is null) { break; }
                        copy.abilities.abilities.Remove(ability);
                    }
                    copy.health.AddHediff(__instance.Props.hediff).Severity = __instance.Props.severity * pawn.GetStatValue(StatDefOf.PsychicSensitivity);
                    if (copy.psychicEntropy != null)
                    {
                        copy.psychicEntropy.TryAddEntropy(pawn.psychicEntropy.EntropyValue);
                    }
                }
                return false;
            }
            return true;
        }
    }
    //level 1
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
                return pawnTargets.TryGetValue(pawn)*0.35f;
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
                    } else {
                        return CoverUtility.TotalSurroundingCoverScore(pawn.pather.curPath.LastNode, pawn.Map);
                    }
                }
            } else if (pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff)) {
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
            float multi = p.needs.food != null ? 1.5f*p.needs.food.CurLevelPercentage : 1f;
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
            return (Rand.Chance(chanceToUtilityCast) && (psycast.pawn.Faction == null || (psycast.pawn.needs.beauty != null && psycast.pawn.needs.beauty.CurLevelPercentage <= this.minBeautyForLikelierCasting) || (psycast.pawn.Map.ParentFaction != null && (psycast.pawn.Map.ParentFaction == psycast.pawn.Faction || psycast.pawn.Faction.RelationKindWith(psycast.pawn.Map.ParentFaction) == FactionRelationKind.Ally || (niceToEvil > 0 && psycast.pawn.Faction.RelationKindWith(psycast.pawn.Map.ParentFaction) == FactionRelationKind.Neutral)))))? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
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
                    if (tryNewPosition.IsValid && !tryNewPosition.Filled(psycast.pawn.Map) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid)
                {
                    tryNewScore = 0f;
                    foreach (IntVec3 iv3 in GenRadial.RadialCellsAround(tryNewPosition, 0f, this.aoe))
                    {
                        if (iv3.InBounds(psycast.pawn.Map) && GenSight.LineOfSightToEdges(tryNewPosition, iv3, psycast.pawn.Map, true, null))
                        {
                            if (this.plant.CanNowPlantAt(iv3,psycast.pawn.Map,false))
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
            return HautsUtility.DamageFactorFor(this.damageType, p) * (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f) / p.GetStatValue(StatDefOf.IncomingDamageFactor);
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
            Thing battery = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Thing, float> thingTargets, this.Range(psycast.ability)/2f);
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
    //level 2
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
            return (1f+p.GetStatValue(StatDefOf.ArmorRating_Heat))/(p.GetStatValue(StatDefOf.Flammability)*HautsUtility.DamageFactorFor(DamageDefOf.Flame,p)*HautsUtility.DamageFactorFor(DamageDefOf.Burn,p));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (1f + p.GetStatValue(StatDefOf.ArmorRating_Heat)) / (p.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, p) * HautsUtility.DamageFactorFor(DamageDefOf.Burn, p));
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
                        if (randAoE1 != null)
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
            return p.CurrentEffectiveVerb.EffectiveRange* p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.CurrentEffectiveVerb.EffectiveRange* p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
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
                        if (randAoE1 != null)
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
                    } else if (!roof.isThickRoof) {
                        Thing edifice = t.Position.GetEdifice(t.Map);
                        if (edifice != null && edifice.def.holdsRoof)
                        {
                            rainableFires++;
                        }
                    }
                }
                return (rainableFires - 100f)*10f;
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
                return pawnTargets.TryGetValue(pawn)/500f;
            }
            return 0f;
        }
        public float chanceToCast;
    }
    //level 3
    public class UseCaseTags_ChunkSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || (p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) >= this.ignoreAllPawnsFasterThan);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * HautsUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && this.IsValidThing(psycast.pawn, t, niceToEvil, useCase))
            {
                float allyOrFoe = t.HostileTo(psycast.pawn) ? 1f : (HVPAAUtility.IsAlly(false, psycast.pawn, t, niceToEvil) ? -1f : 0f);
                if (t.def.building.IsTurret && t.def.building.ai_combatDangerous)
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        return 0f;
                    }
                    return allyOrFoe * t.MarketValue * HautsUtility.DamageFactorFor(this.damageType, t) / 200f;
                }
                else if (this.canTargetHB)
                {
                    return allyOrFoe * t.MarketValue * HautsUtility.DamageFactorFor(this.damageType, t) / 1000f;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.canTargetHB = Rand.Chance(this.chanceCanTargetHarmlessBuildings);
            this.canTargetPawns = Rand.Chance(this.chanceCanTargetPawns);
            float numChunks = 0f;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.chunkGetRadius, true))
            {
                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, t.Position, t.Map) && (t.HasThingCategory(ThingCategoryDefOf.Chunks) || t.HasThingCategory(ThingCategoryDefOf.StoneChunks)))
                {
                    numChunks += 1f;
                }
            }
            if (numChunks > 5f)
            {
                numChunks = 5f;
            }
            psycast.lti = IntVec3.Invalid;
            float app = 0f;
            if (numChunks > 0f)
            {
                Thing bestHit = null;
                float applic = 0f;
                foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.Range(psycast.ability), true))
                {
                    if (!GenSight.LineOfSight(t2.Position, intPsycasts.Pawn.Position, t2.Map))
                    {
                        continue;
                    }
                    if (t2.Position.GetRoof(t2.Map) == null || !t2.Position.GetRoof(t2.Map).isThickRoof)
                    {
                        float applicability = 0f;
                        foreach (Thing t3 in GenRadial.RadialDistinctThingsAround(t2.Position, intPsycasts.Pawn.Map, this.aoe, true))
                        {
                            if (t3 is Pawn p && this.canTargetPawns)
                            {
                                if (intPsycasts.foes.Contains(p))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p, 2))
                                    {
                                        applicability += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 2);
                                    }
                                }
                                else if (intPsycasts.allies.Contains(p) && !this.OtherAllyDisqualifiers(psycast.ability, p, 2))
                                {
                                    applicability -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 2);
                                }
                            }
                            else
                            {
                                applicability += this.ThingApplicability(psycast.ability, t3, niceToEvil, 1);
                            }
                        }
                        if (applicability > applic)
                        {
                            applic = applicability;
                            bestHit = t2;
                        }
                    }
                }
                if (app < applic)
                {
                    app = applic;
                    psycast.lti = bestHit;
                }
            }
            return app;
        }
        public DamageDef damageType;
        public float ignoreAllPawnsFasterThan;
        public bool canTargetHB = false;
        public bool canTargetPawns = false;
        public float chanceCanTargetPawns;
        public float chanceCanTargetHarmlessBuildings;
        public float chunkGetRadius;
    }
    public class UseCaseTags_Illusion : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2.HostileTo(p))
                {
                    return 0f;
                }
            }
            if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.MeleeDPS);
            }
            return 2f*p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return this.appFactor*pawnTargets.TryGetValue(pawn) / 2f;
            }
            return 0f;
        }
        public float appFactor = 1f;
    }
    public class CompProperties_AbilitySpawnWithHediffAI : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySpawnWithHediffAI()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnWithHediffAI);
        }
        public ThingDef thingDef;
        public PawnKindDef pawnKind;
        public HediffDef hediff;
        public float severity;
        public StatDef stat;
        public bool allowOnBuildings = true;
        public bool sendSkipSignal = true;
    }
    public class CompAbilityEffect_SpawnWithHediffAI : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnWithHediffAI Props
        {
            get
            {
                return (CompProperties_AbilitySpawnWithHediffAI)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (this.Props.pawnKind != null)
            {
                Pawn pawn2 = PawnGenerator.GeneratePawn(new PawnGenerationRequest(this.Props.pawnKind, this.parent.pawn.Faction, PawnGenerationContext.NonPlayer, -1, false, false, false, true, false, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                pawn2 = (Pawn)GenSpawn.Spawn(pawn2, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                float severity = this.Props.severity;
                bool flag2 = this.Props.stat != null;
                if (flag2)
                {
                    severity *= this.parent.pawn.GetStatValue(this.Props.stat, true, -1);
                }
                pawn2.health.AddHediff(this.Props.hediff, null, null, null).Severity = severity;
                if (pawn2.Faction == null || pawn2.Faction != Faction.OfPlayerSilentFail)
                {
                    Lord lord = this.parent.pawn.GetLord();
                    if (lord != null)
                    {
                        lord.AddPawn(pawn2);
                    } else {
                        LordMaker.MakeNewLord(pawn2.Faction, new LordJob_EscortPawn(this.parent.pawn), pawn2.Map, Gen.YieldSingle<Pawn>(pawn2));
                    }
                }
            } else {
                GenSpawn.Spawn(this.Props.thingDef, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
            }
            bool sendSkipSignal = this.Props.sendSkipSignal;
            if (sendSkipSignal)
            {
                CompAbilityEffect_Teleport.SendSkipUsedSignal(target, this.parent.pawn);
            }
        }
    }
    public class UseCaseTags_Phase : UseCaseTags
    {
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = intPsycasts.Pawn;
            psycast.lti = pawn;
            if (pawn.CurJob.jobGiver != null && pawn.CurJob.jobGiver is JobGiver_AISapper)
            {
                return 0f;
            }
            if (pawn.pather.Moving && !pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
            {
                return 999997f;
            }
            return 0f;
        }
    }
    public class UseCaseTags_Feedback : UseCaseTags
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
            return p.Downed || (p.RaceProps.Humanlike && !this.canHitHumanlike) || (p.IsColonist && !this.canHitColonist);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float multi = 1f;
            if (p.psychicEntropy != null)
            {
                CompAbilityEffect_ExplosionFromHeat efh = psycast.CompOfType<CompAbilityEffect_ExplosionFromHeat>();
                if (efh != null)
                {
                    float radius = this.aoe * efh.Props.heatToRadiusCurve.Evaluate(p.psychicEntropy.EntropyValue);
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, radius, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.Downed && !p2.IsBurning())
                        {
                            if (intPsycasts.foes.Contains(p2))
                            {
                                multi += p2.GetStatValue(StatDefOf.IncomingDamageFactor) * HautsUtility.DamageFactorFor(this.damageDef, p2);
                            } else if (intPsycasts.allies.Contains(p2)) {
                                multi -= this.allyMultiplier * p2.GetStatValue(StatDefOf.IncomingDamageFactor) * HautsUtility.DamageFactorFor(this.damageDef, p2);
                            }
                        }
                    }
                }
            }
            return multi*p.GetStatValue(StatDefOf.PsychicSensitivity)/p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
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
        public DamageDef damageDef;
        private bool canHitHumanlike;
        private bool canHitColonist;
    }
    public class UseCaseTags_Skipblade : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Pawn pawn = psycast.pawn;
            if (pawn.equipment == null || (pawn.equipment.Primary != null && pawn.equipment.Primary.def == this.avoidMakingTooMuchOfThing))
            {
                return 0f;
            }
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn p = intPsycasts.Pawn;
            psycast.lti = p;
            float qualityMultiplier = 1f;
            CompAbilityEffect_CreateWeapon caecw = psycast.ability.CompOfType<CompAbilityEffect_CreateWeapon>();
            if (caecw != null)
            {
                float quality = caecw.Props.statToQualityCurve.Evaluate(intPsycasts.Pawn.GetStatValue(StatDefOf.PsychicSensitivity));
                switch (quality)
                {
                    case 0:
                        qualityMultiplier = 0.8f;
                        break;
                    case 1:
                        qualityMultiplier = 0.9f;
                        break;
                    case 3:
                        qualityMultiplier = 1.1f;
                        break;
                    case 4:
                        qualityMultiplier = 1.2f;
                        break;
                    case 5:
                        qualityMultiplier = 1.45f;
                        break;
                    case 6:
                        qualityMultiplier = 1.65f;
                        break;
                    default:
                        break;
                }
            }
            bool isRanged = p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
            float howMuchBetterIsPsybladeThanAvg = ((1 + this.avgArmorPen) * this.avgDamage * qualityMultiplier / this.avgWeaponCooldown) - (isRanged ? (2f*p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown))) : p.GetStatValue(StatDefOf.MeleeDPS));
            if (howMuchBetterIsPsybladeThanAvg <= 0f)
            {
                return 0f;
            }
            float netFoeMeleePower = 0f;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2.HostileTo(p))
                {
                    netFoeMeleePower += p2.GetStatValue(StatDefOf.MeleeDPS) * p2.HealthScale * p2.GetStatValue(StatDefOf.IncomingDamageFactor);
                }
            }
            if (netFoeMeleePower > 0f || p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
            {
                netFoeMeleePower += howMuchBetterIsPsybladeThanAvg;
            }
            return netFoeMeleePower;
        }
        public float avgDamage;
        public float avgWeaponCooldown;
        public float avgArmorPen;
    }
    public class UseCaseTags_Skiprifle : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Pawn pawn = psycast.pawn;
            if (pawn.equipment == null || (pawn.equipment.Primary != null && pawn.equipment.Primary.def == this.avoidMakingTooMuchOfThing))
            {
                return 0f;
            }
            if (!this.disallowingTraits.NullOrEmpty() && pawn.story != null)
            {
                foreach (TraitDef t in this.disallowingTraits)
                {
                    if (pawn.story.traits.HasTrait(t))
                    {
                        return 0f;
                    }
                }
            }
            if (pawn.apparel != null)
            {
                List<Apparel> wornApparel = pawn.apparel.WornApparel;
                for (int i = 0; i < wornApparel.Count; i++)
                {
                    RimWorld.CompShield cs = wornApparel[i].TryGetComp<RimWorld.CompShield>();
                    if (cs != null && cs.Props.blocksRangedWeapons)
                    {
                        return 0f;
                    }
                }
            }
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn p = intPsycasts.Pawn;
            psycast.lti = p;
            float qualityMultiplier = 1f;
            CompAbilityEffect_CreateWeapon caecw = psycast.ability.CompOfType<CompAbilityEffect_CreateWeapon>();
            if (caecw != null)
            {
                float quality = caecw.Props.statToQualityCurve.Evaluate(intPsycasts.Pawn.GetStatValue(StatDefOf.PsychicSensitivity));
                switch (quality)
                {
                    case 0:
                        qualityMultiplier = 0.9f;
                        break;
                    case 5:
                        qualityMultiplier = 1.25f;
                        break;
                    case 6:
                        qualityMultiplier = 1.5f;
                        break;
                    default:
                        break;
                }
            }
            bool isRanged = p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
            float howMuchBetterIsPsybladeThanAvg = ((1 + this.avgArmorPen) * this.avgDamage * qualityMultiplier / this.avgWeaponCooldown) - (isRanged ? (p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown))) : p.GetStatValue(StatDefOf.MeleeDPS));
            if (howMuchBetterIsPsybladeThanAvg <= 0f)
            {
                return 0f;
            }
            float netFoes = 0f;
            foreach (Pawn p2 in intPsycasts.foes)
            {
                float dist = p2.Position.DistanceTo(p.Position);
                if (dist <= this.aoe)
                {
                    return 0f;
                } else {
                    netFoes += p2.HealthScale * p2.GetStatValue(StatDefOf.IncomingDamageFactor);
                }
            }
            if (netFoes > 0f)
            {
                netFoes += howMuchBetterIsPsybladeThanAvg;
            }
            return netFoes;
        }
        public float avgDamage;
        public float avgWeaponCooldown;
        public float avgArmorPen;
        public List<TraitDef> disallowingTraits;
    }
    //level 4
    public class UseCaseTags_Awaken : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return situationCase == 1 ? 0f : base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float numConditions = 0f;
            CompAbilityEffect_RemoveHediffs rh = psycast.CompOfType<CompAbilityEffect_RemoveHediffs>();
            if (rh != null)
            {
                foreach (Hediff h in p.health.hediffSet.hediffs)
                {
                    if (rh.Props.hediffDefs.Contains(h.def))
                    {
                        numConditions += 1f;
                    }
                }
            }
            if (p.needs.rest != null)
            {
                numConditions /= p.needs.rest.CurLevelPercentage;
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
    public class UseCaseTags_Strip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.apparel == null || !p.apparel.AnyApparel;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.apparel != null && p.apparel.AnyApparel)
            {
                float duration = p.GetStatValue(StatDefOf.PsychicSensitivity) * this.duration;
                float totalTimeToStrip = 0f;
                float totalArmor = 0f;
                foreach (Apparel a in p.apparel.WornApparel)
                {
                    totalTimeToStrip += a.GetStatValue(StatDefOf.EquipDelay, true, -1);
                    totalArmor += Math.Max(0f,a.GetStatValue(StatDefOf.ArmorRating_Blunt)+a.GetStatValue(StatDefOf.ArmorRating_Sharp)+(a.GetStatValue(StatDefOf.ArmorRating_Heat)/2f)-this.minArmorToCountAsArmor);
                }
                totalArmor /= p.apparel.WornApparel.Count*1f;
                totalArmor += 1f;
                return Math.Min(duration, totalTimeToStrip)*totalArmor;
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
        public float duration;
        public float minArmorToCountAsArmor;
    }
    //level 5
    public class UseCaseTags_Execute : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (initialTarget)
            {
                return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
            } else {
                return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent);
            }
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (initialTarget)
            {
                return p.MarketValue / 1000f;
            }
            if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
            return p.GetStatValue(StatDefOf.MeleeDPS);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
            return p.GetStatValue(StatDefOf.MeleeDPS);
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            this.canHitColonist = Rand.Value <= this.chanceToCastColonist || !HVPAA_Mod.settings.powerLimiting;
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
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
                        foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                        {
                            if (!p2.HostileTo(p) && (p2.CurJob == null || p2.CurJob.targetA.Pawn == null || p2.CurJob.targetA.Pawn != p))
                            {
                                if (intPsycasts.foes.Contains(p2))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2))
                                    {
                                        pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2, false);
                                    }
                                } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
                                    pTargetHits += this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2, false);
                                }
                            }
                        }
                        if (pTargetHits > bestTargetHits)
                        {
                            bestTarget = p;
                            bestTargetHits = pTargetHits;
                        }
                    }
                    psycast.lti = bestTarget;
                    return bestTargetHits;
                }
            }
            return 0f;
        }
        private bool canHitColonist;
        public float chanceToCastColonist;
    }
    public class UseCaseTags_Jskip : UseCaseTags
    {
        public override bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            if (p.HostileTo(caster))
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
                return 1f;
            }
            return 0f;
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff) ? 1f : base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = intPsycasts.Pawn;
            psycast.lti = pawn;
            int numFoes = 0;
            foreach (Pawn p in intPsycasts.foes)
            {
                if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe)
                {
                    numFoes++;
                }
            }
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.aoe, true))
            {
                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, t.Position, t.Map))
                {
                    if (this.IsValidThing(intPsycasts.Pawn, t, niceToEvil, 2))
                    {
                        float tApplicability = this.ThingApplicability(psycast.ability, t, 2);
                        if (tApplicability > 0f)
                        {
                            numFoes++;
                        }
                    }
                }
            }
            if (numFoes > 0)
            {
                if (!intPsycasts.Pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                {
                    return 999999f;
                }
            } else if (intPsycasts.Pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff)) {
                return 999999f;
            }
            return 0f;
        }
    }
    public class UseCaseTags_OSkip : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float chance = Rand.Value;
            this.canHitHumanlike = chance <= this.chanceToCast || !HVPAA_Mod.settings.powerLimiting;
            this.canHitColonist = chance <= this.chanceToCastColonist || !HVPAA_Mod.settings.powerLimiting;
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.RaceProps.Humanlike || !this.canHitHumanlike || (p.IsColonist && !this.canHitColonist);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float multi = 1f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h.def.spawnThingOnRemoved != null && h is Hediff_AddedPart && (h.Part == null || !organsList.Contains(h.Part.def.defName)))
                {
                    multi += this.bionicCountsFor;
                }
            }
            foreach (BodyPartRecord bpr in p.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined,BodyPartDepth.Undefined))
            {
                if (organsList.Contains(bpr.def.defName))
                {
                    multi += this.intactOrganCountsFor;
                }
            }
            return multi;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn)*0.2f;
            }
            return 0f;
        }
        public float chanceToCast;
        public float chanceToCastColonist;
        public float intactOrganCountsFor;
        public float bionicCountsFor;
        private bool canHitHumanlike;
        private bool canHitColonist;
        List<string> organsList = new List<string> { "Heart", "Lung", "Lung", "Kidney", "Kidney", "Liver" };
    }
    //level 6
    public class UseCaseTags_Duplicate : UseCaseTags
    {
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn caster = intPsycasts.Pawn;
            if (caster.WorkTagIsDisabled(WorkTags.Violent))
            {
                return 0f;
            }
            Pawn pawn = intPsycasts.foes.Where((Pawn p) => p.Position.DistanceTo(caster.Position) <= this.Range(psycast.ability) && GenSight.LineOfSight(caster.Position, p.Position, p.Map)).RandomElement();
            if (pawn != null)
            {
                psycast.lti = pawn.Position;
                return this.flatApplicability;
            }
            return 0f;
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(this.chanceToUtilityCast) ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            psycast.lti = intPsycasts.Pawn.Position;
            return this.flatApplicability;
        }
        public float flatApplicability;
        public float chanceToUtilityCast;
    }
    public class UseCaseTags_Enslave : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.guest == null || p.Faction == psycast.pawn.Faction || p.InMentalState || p.RaceProps.intelligence != Intelligence.Humanlike || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (p.MarketValue - this.minMarketValue) / 1000f;
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return ((niceToEvil > this.minEvil && HVPAA_Mod.settings.powerLimiting) || psycast.pawn.Faction == null) ? 0f : base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
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
        public float minMarketValue;
        public float minEvil;
    }
    public class UseCaseTags_MassReflect : UseCaseTags
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
                    if (tryNewPosition.IsValid && !tryNewPosition.Filled(psycast.pawn.Map) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid)
                {
                    tryNewScore = 0f;
                    List<IntVec3> AoECells = GenRadial.RadialCellsAround(tryNewPosition, this.aoe, true).ToList<IntVec3>();
                    for (int k = 0; k < AoECells.Count<IntVec3>(); k++)
                    {
                        List<Thing> cellList = AoECells[i].GetThingList(intPsycasts.Pawn.Map);
                        for (int j = 0; j < cellList.Count; j++)
                        {
                            if (cellList[j] is Projectile p && p.Launcher != null && p.Launcher is Pawn pawn)
                            {
                                if (intPsycasts.allies.Contains(pawn))
                                {
                                    tryNewScore -= this.allyMultiplier * p.DamageAmount;
                                } else if (intPsycasts.foes.Contains(pawn)) {
                                    tryNewScore += p.DamageAmount;
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
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position);
            }
            return 0f;
        }
    }
    //transcendent
    public class UseCaseTags_GammaRay : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.RaceProps.IsMechanoid;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float baseVal = 0f;
            foreach (IntVec3 iv3 in this.AffectedCells(p,psycast))
            {
                List<Thing> thingList = iv3.GetThingList(p.Map);
                foreach (Pawn p2 in thingList.OfType<Pawn>().Distinct<Pawn>())
                {
                    if (intPsycasts.foes.Contains(p2))
                    {
                        baseVal += this.GammaValue(p2);
                    } else if (intPsycasts.allies.Contains(p2)) {
                        baseVal -= this.allyMultiplier*this.GammaValue(p2);
                    }
                }
            }
            return baseVal;
        }
        public float GammaValue(Pawn p)
        {
            return p.MarketValue * HautsUtility.DamageFactorFor(this.damageDef, p) / (1000f * (1f + p.GetStatValue(StatDefOf.ToxicResistance)));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 origin = intPsycasts.Pawn.Position;
            Pawn pawn = intPsycasts.foes.Where((Pawn p) => p.Position.DistanceTo(origin) <= this.Range(psycast.ability) && GenSight.LineOfSight(origin, p.Position, p.Map) && !this.OtherEnemyDisqualifiers(psycast.ability,p,1)).RandomElement();
            if (pawn != null)
            {
                psycast.lti = pawn;
                return this.PawnEnemyApplicability(intPsycasts,psycast.ability,pawn,niceToEvil,1);
            }
            return 0f;
        }
        private List<IntVec3> AffectedCells(LocalTargetInfo target, Psycast psycast)
        {
            float range = this.Range(psycast);
            tmpCells.Clear();
            Vector3 vector = psycast.pawn.Position.ToVector3Shifted().Yto0();
            IntVec3 intVec = target.Cell.ClampInsideMap(psycast.pawn.Map);
            if (psycast.pawn.Position == intVec)
            {
                return tmpCells;
            }
            float lengthHorizontal = (intVec - psycast.pawn.Position).LengthHorizontal;
            float num = (float)(intVec.x - psycast.pawn.Position.x) / lengthHorizontal;
            float num2 = (float)(intVec.z - psycast.pawn.Position.z) / lengthHorizontal;
            intVec.x = Mathf.RoundToInt((float)psycast.pawn.Position.x + num * range);
            intVec.z = Mathf.RoundToInt((float)psycast.pawn.Position.z + num2 * range);
            float target2 = Vector3.SignedAngle(intVec.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up);
            float num3 = this.lineWidthEnd / 2f;
            float num4 = Mathf.Sqrt(Mathf.Pow((intVec - psycast.pawn.Position).LengthHorizontal, 2f) + Mathf.Pow(num3, 2f));
            float num5 = 57.29578f * Mathf.Asin(num3 / num4);
            int num6 = GenRadial.NumCellsInRadius(range);
            for (int i = 0; i < num6; i++)
            {
                IntVec3 intVec2 = psycast.pawn.Position + GenRadial.RadialPattern[i];
                if (CanUseCell(intVec2) && Mathf.Abs(Mathf.DeltaAngle(Vector3.SignedAngle(intVec2.ToVector3Shifted().Yto0() - vector, Vector3.right, Vector3.up), target2)) <= num5)
                {
                    tmpCells.Add(intVec2);
                }
            }
            List<IntVec3> list = GenSight.BresenhamCellsBetween(psycast.pawn.Position, intVec);
            for (int j = 0; j < list.Count; j++)
            {
                IntVec3 intVec3 = list[j];
                if (!tmpCells.Contains(intVec3) && CanUseCell(intVec3))
                {
                    tmpCells.Add(intVec3);
                }
            }
            return tmpCells;
            bool CanUseCell(IntVec3 c)
            {
                if (!c.InBounds(psycast.pawn.Map))
                {
                    return false;
                }
                if (c == psycast.pawn.Position)
                {
                    return false;
                }
                if (!c.InHorDistOf(psycast.pawn.Position, range))
                {
                    return false;
                }
                ShootLine resultingLine;
                return psycast.verb.TryFindShootLineFromTo(psycast.pawn.Position, c, out resultingLine);
            }
        }
        public DamageDef damageDef;
        public float lineWidthEnd;
        private readonly List<IntVec3> tmpCells = new List<IntVec3>();
    }
    public class UseCaseTags_GravityPulse : UseCaseTags
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
            return HautsUtility.DamageFactorFor(this.damageType, p);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * HautsUtility.DamageFactorFor(this.damageType, p);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
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
                        if (randAoE1 != null)
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
        public DamageDef damageType;
    }
    public class UseCaseTags_Annihilate : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.ThreatDisabled(psycast.pawn) || (p.GetStatValue(StatDefOf.IncomingDamageFactor) > this.incomingDamageFactorMax && p.MarketValue < this.marketValueMin && p.health.LethalDamageThreshold < this.lethalDamageMin);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 10f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * (10f + niceToEvil);
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (niceToEvil>this.minEvil && HVPAA_Mod.settings.powerLimiting) ?0f:base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
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
                        if (randAoE1 != null)
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
        public float marketValueMin;
        public float lethalDamageMin;
        public float incomingDamageFactorMax;
        public float minEvil;
    }
    public class UseCaseTags_GrantPsycast : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 1000f * this.PawnAllyApplicability(intPsycasts, psycast.ability, pawn, niceToEvil, 5);
            }
            return 0f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            int highestLevelMissingPsycast = 0;
            foreach (AbilityDef a in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                if (a.level <= this.maxLevel && p.abilities.GetAbility(a) == null && a.IsPsycast)
                {
                    highestLevelMissingPsycast = Math.Max(highestLevelMissingPsycast,a.level);
                }
            }
            return highestLevelMissingPsycast;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.HasPsylink;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public int maxLevel;
    }
}
