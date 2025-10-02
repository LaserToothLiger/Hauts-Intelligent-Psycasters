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
using VEF;
using static HarmonyLib.Code;
using static UnityEngine.GraphicsBuffer;
using VEF.AnimalBehaviours;
using VEF.Genes;

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
                               postfix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityEnslave_Apply_Postfix)));
            }
            /*harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_CastAbility), nameof(CompAbilityEffect_CastAbility.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_AbilityCastAbility_Apply_Prefix)));*/
        }
        public static void HVPAA_AbilityEnslave_Apply_Postfix(CompAbilityEffect_Enslave __instance, LocalTargetInfo target)
        {
            if (target.Pawn != null)
            {
                Pawn pawn = __instance.parent.pawn;
                if (pawn.Faction != null && pawn.Faction != Faction.OfPlayerSilentFail)
                {
                    Lord lord = pawn.GetLord();
                    if (lord != null)
                    {
                        lord.AddPawn(target.Pawn);
                    } else {
                        LordMaker.MakeNewLord(pawn.Faction, new LordJob_EscortPawn(pawn), pawn.Map, Gen.YieldSingle<Pawn>(target.Pawn));
                    }
                }
            }
        }
        public static bool HVPAA_AbilityCastAbility_Apply_Prefix(CompAbilityEffect_CastAbility __instance, LocalTargetInfo target, LocalTargetInfo dest)
        {
            Pawn caster = __instance.parent.pawn;
            caster.health.hediffSet.TryGetHediff(HVPAADefOf.HVPAA_AI, out Hediff ai);
            if (ai != null && caster.Spawned)
            {
                HediffComp_IntPsycasts hcip = ai.TryGetComp<HediffComp_IntPsycasts>();
                if (hcip != null)
                {
                    Dictionary<Psycast, float> extendableAbilities = new Dictionary<Psycast, float>();
                    float myRange = __instance.parent.verb.EffectiveRange;
                    if (hcip.highestPriorityPsycasts.NullOrEmpty())
                    {
                        hcip.highestPriorityPsycasts = hcip.ThreePriorityPsycasts(hcip.GetSituation());
                    }
                    foreach (PotentialPsycast pp in hcip.highestPriorityPsycasts)
                    {
                        Psycast a = pp.ability;
                        float aRange = a.verb.EffectiveRange;
                        if (a.def != __instance.parent.def && a.def.targetRequired && a.CanApplyOn(target) && aRange > 0f && aRange < myRange && a.FinalPsyfocusCost(target) < caster.psychicEntropy.CurrentPsyfocus)
                        {
                            if (!a.comps.Any((AbilityComp c) => c is CompAbilityEffect_WithDest) && !caster.psychicEntropy.WouldOverflowEntropy(a.def.EntropyGain))
                            {
                                UseCaseTags uct = pp.ability.def.GetModExtension<UseCaseTags>();
                                if (uct != null)
                                {
                                    float score = pp.score * uct.ApplicabilityScore(hcip, pp, hcip.niceToEvil);
                                    float dist = pp.lti.Cell.DistanceTo(caster.Position);
                                    if (pp.lti.IsValid && dist <= myRange && dist > aRange && score > 0)
                                    {
                                        extendableAbilities.Add(a, score);
                                    }
                                }
                            }
                        }
                    }
                    if (!extendableAbilities.NullOrEmpty())
                    {
                        extendableAbilities.RandomElementByWeight((KeyValuePair<Psycast, float> kvp) => kvp.Value).Key.Activate(target, dest);
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
            return p.CurrentEffectiveVerb.EffectiveRange* p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.CurrentEffectiveVerb.EffectiveRange* p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
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
        public override float MetaApplicability(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, List<PotentialPsycast> psycasts, int situationCase, float niceToEvil)
        {
            Pawn p = psycast.ability.pawn;
            if (p.psychicEntropy != null)
            {
                foreach (PotentialPsycast potPsy in psycasts)
                {
                    Psycast psy = potPsy.ability;
                    if (psy.def == this.chunkRain)
                    {
                        Psycast cs = psycast.ability;
                        UseCaseTags_ChunkRain uct = psy.def.GetModExtension<UseCaseTags_ChunkRain>();
                        if (uct != null && !p.psychicEntropy.WouldOverflowEntropy(cs.def.EntropyGain + psy.def.EntropyGain) && p.psychicEntropy.CurrentPsyfocus + 0.0005f >= cs.def.PsyfocusCost + psy.def.PsyfocusCost && p.psychicEntropy.CurrentPsyfocus - cs.def.PsyfocusCost - 0.0005f >= uct.minPsyfocusAfterChunkSkipToCast)
                        {
                            foreach (Thing t in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, uct.chunkGetRadius, true))
                            {
                                if (GenSight.LineOfSight(p.Position, t.Position, t.Map) && (t.HasThingCategory(ThingCategoryDefOf.Chunks) || t.HasThingCategory(ThingCategoryDefOf.StoneChunks)))
                                {
                                    return 0f;
                                }
                            }
                            psycast.lti = p.Position;
                            return potPsy.score;
                        }
                    }
                }
            }
            return 0f;
        }
        public AbilityDef chunkRain;
    }
    public class UseCaseTags_ChunkRain : UseCaseTags
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
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float numChunks = 0f;
            Pawn p = psycast.pawn;
            float getRadius = this.chunkGetRadius;
            if (p.abilities != null && p.psychicEntropy != null)
            {
                Ability cs = p.abilities.GetAbility(this.chunkSkip);
                if (cs != null && !p.psychicEntropy.WouldOverflowEntropy(cs.def.EntropyGain + psycast.def.EntropyGain) && p.psychicEntropy.CurrentPsyfocus + 0.0005f >= cs.def.PsyfocusCost + psycast.def.PsyfocusCost && p.psychicEntropy.CurrentPsyfocus - cs.def.PsyfocusCost - 0.0005f >= this.minPsyfocusAfterChunkSkipToCast)
                {
                    getRadius = this.chunkGetRadiusIfChunkSkip;
                }
            }
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, getRadius, true))
            {
                if (GenSight.LineOfSight(p.Position, t.Position, t.Map) && (t.HasThingCategory(ThingCategoryDefOf.Chunks) || t.HasThingCategory(ThingCategoryDefOf.StoneChunks)))
                {
                    numChunks += 1f;
                    if (numChunks >= 5f)
                    {
                        break;
                    }
                }
            }
            return Math.Min(numChunks,base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.canTargetHB = Rand.Chance(this.chanceCanTargetHarmlessBuildings);
            this.canTargetPawns = Rand.Chance(this.chanceCanTargetPawns);
            psycast.lti = IntVec3.Invalid;
            float app = 0f;
            float numChunks = 0f;
            Pawn pawn = intPsycasts.Pawn;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, this.chunkGetRadius, true))
            {
                if (GenSight.LineOfSight(pawn.Position, t.Position, t.Map) && (t.HasThingCategory(ThingCategoryDefOf.Chunks) || t.HasThingCategory(ThingCategoryDefOf.StoneChunks)))
                {
                    numChunks += 1f;
                    if (numChunks >= 5f)
                    {
                        break;
                    }
                }
            }
            if (numChunks > 0f)
            {
                Thing bestHit = null;
                float applic = 0f;
                foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, this.Range(psycast.ability), true))
                {
                    if (!GenSight.LineOfSight(t2.Position, pawn.Position, t2.Map))
                    {
                        continue;
                    }
                    if (t2.Position.GetRoof(t2.Map) == null || !t2.Position.GetRoof(t2.Map).isThickRoof)
                    {
                        float applicability = 0f;
                        foreach (Thing t3 in GenRadial.RadialDistinctThingsAround(t2.Position, pawn.Map, this.aoe, true))
                        {
                            if (t3 is Pawn p && this.canTargetPawns)
                            {
                                if (intPsycasts.foes.Contains(p))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p, 2))
                                    {
                                        applicability += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 2);
                                    }
                                } else if (intPsycasts.allies.Contains(p) && !this.OtherAllyDisqualifiers(psycast.ability, p, 2)) {
                                    applicability -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 2);
                                }
                            } else {
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
        public float chunkGetRadiusIfChunkSkip;
        public AbilityDef chunkSkip;
        public float minPsyfocusAfterChunkSkipToCast = 0.25f;
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
            return 2f*p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
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
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Pawn p = psycast.pawn;
            this.qualityMultiplier = 1f;
            CompAbilityEffect_CreateWeapon caecw = psycast.CompOfType<CompAbilityEffect_CreateWeapon>();
            if (caecw != null)
            {
                float quality = caecw.Props.statToQualityCurve.Evaluate(psycast.pawn.GetStatValue(StatDefOf.PsychicSensitivity));
                switch (quality)
                {
                    case 0:
                        this.qualityMultiplier = 0.8f;
                        break;
                    case 1:
                        this.qualityMultiplier = 0.9f;
                        break;
                    case 3:
                        this.qualityMultiplier = 1.1f;
                        break;
                    case 4:
                        this.qualityMultiplier = 1.2f;
                        break;
                    case 5:
                        this.qualityMultiplier = 1.45f;
                        break;
                    case 6:
                        this.qualityMultiplier = 1.65f;
                        break;
                    default:
                        break;
                }
            }
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.equipment == null || (p.equipment.Primary != null && p.equipment.Primary.def == this.avoidMakingTooMuchOfThing) || p.kindDef.destroyGearOnDrop || p.RaceProps.IsMechanoid || p.RaceProps.IsAnomalyEntity || p.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            bool isRanged = p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
            float howMuchBetterIsPsybladeThanAvg = ((1 + this.avgArmorPen) * this.avgDamage * this.qualityMultiplier / this.avgWeaponCooldown) - (isRanged ? (2f * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown))) : p.GetStatValue(StatDefOf.MeleeDPS));
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
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float avgDamage;
        public float avgWeaponCooldown;
        public float avgArmorPen;
        public float qualityMultiplier;
    }
    public class UseCaseTags_Skiprifle : UseCaseTags
    {
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Pawn p = psycast.pawn;
            this.qualityMultiplier = 1f;
            CompAbilityEffect_CreateWeapon caecw = psycast.CompOfType<CompAbilityEffect_CreateWeapon>();
            if (caecw != null)
            {
                float quality = caecw.Props.statToQualityCurve.Evaluate(psycast.pawn.GetStatValue(StatDefOf.PsychicSensitivity));
                switch (quality)
                {
                    case 0:
                        this.qualityMultiplier = 0.8f;
                        break;
                    case 1:
                        this.qualityMultiplier = 0.9f;
                        break;
                    case 3:
                        this.qualityMultiplier = 1.1f;
                        break;
                    case 4:
                        this.qualityMultiplier = 1.2f;
                        break;
                    case 5:
                        this.qualityMultiplier = 1.45f;
                        break;
                    case 6:
                        this.qualityMultiplier = 1.65f;
                        break;
                    default:
                        break;
                }
            }
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent) || p.equipment == null || (p.equipment.Primary != null && p.equipment.Primary.def == this.avoidMakingTooMuchOfThing) || p.kindDef.destroyGearOnDrop || p.RaceProps.IsMechanoid || p.RaceProps.IsAnomalyEntity || p.GetStatValue(StatDefOf.PsychicSensitivity) <= 0f)
            {
                return true;
            }
            if (p.story != null && !this.disallowingTraits.NullOrEmpty())
            {
                foreach (TraitDef t in this.disallowingTraits)
                {
                    if (p.story.traits.HasTrait(t))
                    {
                        return true;
                    }
                }
            }
            if (p.apparel != null)
            {
                List<Apparel> wornApparel = p.apparel.WornApparel;
                for (int i = 0; i < wornApparel.Count; i++)
                {
                    RimWorld.CompShield cs = wornApparel[i].TryGetComp<RimWorld.CompShield>();
                    if (cs != null && cs.Props.blocksRangedWeapons)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (intPsycasts.foes.Contains(p2))
                {
                    return 0f;
                }
            }
            bool isRanged = p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
            float howMuchBetterIsPsyblasterThanAvg = ((1 + this.avgArmorPen) * this.avgDamage * this.qualityMultiplier / this.avgWeaponCooldown) - (isRanged ? (2f * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown))) : p.GetStatValue(StatDefOf.MeleeDPS));
            return howMuchBetterIsPsyblasterThanAvg;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float avgDamage;
        public float avgWeaponCooldown;
        public float avgArmorPen;
        public List<TraitDef> disallowingTraits;
        public float qualityMultiplier;
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
    public class UseCaseTags_Echo : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 bestPosition = IntVec3.Invalid;
            List<Thing> allyShooters = new List<Thing>();
            List<Thing> foeShooters = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.scanRadius, true))
            {
                if (t.def.building != null && t.def.building.IsTurret && !t.Position.AnyGas(t.Map, GasType.BlindSmoke))
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        continue;
                    }
                    if (t.HostileTo(psycast.pawn))
                    {
                        foeShooters.Add(t);
                    } else if (HVPAAUtility.IsAlly(intPsycasts.niceToAnimals <= 0, psycast.pawn, t, niceToEvil)) {
                        allyShooters.Add(t);
                    }
                } else if (t is Pawn p) {
                    if (intPsycasts.allies.Contains(p))
                    {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            allyShooters.Add(t);
                        }
                    } else if (intPsycasts.foes.Contains(p)) {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            foeShooters.Add(p);
                        }
                    }
                }
            }
            float innerScanRadius = Math.Min(this.Range(psycast), this.shooterScanRadius);
            List<Thing> allyShootersIteratable = allyShooters;
            for (int k = allyShootersIteratable.Count - 1; k >=0; k--)
            {
                Thing t = allyShootersIteratable[k];
                if (t.Position.DistanceTo(psycast.pawn.Position) > innerScanRadius)
                {
                    allyShootersIteratable.Remove(t);
                }
            }
            if (allyShooters.Count > 0 && foeShooters.Count > 0 && foeShooters.Count - allyShooters.Count < 0)
            {
                for (int i = 10; i > 0; i--)
                {
                    if (allyShootersIteratable.NullOrEmpty())
                    {
                        break;
                    }
                    Thing ally = allyShootersIteratable.RandomElement();
                    allyShootersIteratable.Remove(ally);
                    if (ally is Pawn p && p.pather.MovingNow)
                    {
                        i++;
                        continue;
                    }
                    if (!positionTargets.ContainsKey(ally.Position))
                    {
                        bool nearbyEcho = false;
                        foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(ally.Position, psycast.pawn.Map, this.aoe, true))
                        {
                            if (t2.def == this.avoidMakingTooMuchOfThing)
                            {
                                nearbyEcho = true;
                                break;
                            }
                        }
                        if (!nearbyEcho)
                        {
                            bool nearbySkipshield = false;
                            foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(ally.Position, psycast.pawn.Map, this.otherThingAoE, true))
                            {
                                if (t2.def == this.otherThingToAvoidBeingNear)
                                {
                                    nearbySkipshield = true;
                                    break;
                                }
                            }
                            if (!nearbySkipshield)
                            {
                                float totalNearbyShooters = 1f;
                                foreach (Thing alli in allyShooters)
                                {
                                    if (alli.Position.DistanceTo(ally.Position) <= this.aoe)
                                    {
                                        totalNearbyShooters += 1f;
                                    }
                                }
                                positionTargets.Add(ally.Position, totalNearbyShooters);
                            }
                        }
                    }

                }
                if (positionTargets.Count > 0)
                {
                    bestPosition = positionTargets.Keys.RandomElement();
                }
            }
            return bestPosition;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position) * (intPsycasts.Pawn.equipment != null && intPsycasts.Pawn.equipment.Primary != null && intPsycasts.Pawn.equipment.Primary.def.IsRangedWeapon ? 1.5f : 1f);
            }
            return 0f;
        }
        public ThingDef otherThingToAvoidBeingNear;
        public float scanRadius;
        public float shooterScanRadius;
        public float otherThingAoE;
    }
    public class UseCaseTags_Embed : UseCaseTags
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
                        if (psycast.pawn.Faction != null)
                        {
                            Faction ownFaction = psycast.pawn.Faction;
                            foreach (Building b in GenRadial.RadialDistinctThingsAround(spot, psycast.pawn.Map, this.aoe, true).OfType<Building>().Distinct<Building>())
                            {
                                Faction bFaction = b.Faction;
                                if (bFaction != null && (bFaction == ownFaction || bFaction.RelationKindWith(ownFaction) != FactionRelationKind.Hostile))
                                {
                                    goNext = true;
                                    break;
                                }
                            }
                        }
                        if (goNext)
                        {
                            continue;
                        } else {
                            return spot;
                        }
                    }
                }
            }
            return IntVec3.Invalid;
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            foreach (Ability a in psycast.pawn.abilities.abilities)
            {
                UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                if (uct != null && uct.trapPower > 0 && a.def.verbProperties.range > 0f && a.def.targetRequired && (a.def.PsyfocusCost+psycast.def.PsyfocusCost) < psycast.pawn.psychicEntropy.CurrentPsyfocus && !psycast.pawn.psychicEntropy.WouldOverflowEntropy(a.def.EntropyGain+psycast.def.PsyfocusCost))
                {
                    if (ModsConfig.OdysseyActive && psycast.pawn.MapHeld.Biome.inVacuum)
                    {
                        if (a.VerbProperties.Any((VerbProperties vp) => !vp.useableInVacuum))
                        {
                            continue;
                        }
                    }
                    return base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count > 0 || Rand.Chance(this.spontaneousCastChance))
            {
                IntVec3 spot = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets, this.Range(psycast.ability));
                psycast.lti = spot;
                return 2f * Math.Min(10f, (intPsycasts.foes.Count + 1f));
            }
            return 0f;
        }
        public float spontaneousCastChance;
    }
    public class CompProperties_AbilitySpawnPsycastTrap_AIFriendly : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySpawnPsycastTrap_AIFriendly()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnPsycastTrap_AIFriendly);
        }
        public ThingDef thingDef;
    }
    public class CompAbilityEffect_SpawnPsycastTrap_AIFriendly : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnPsycastTrap_AIFriendly Props
        {
            get
            {
                return (CompProperties_AbilitySpawnPsycastTrap_AIFriendly)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = this.parent.pawn;
            IEnumerable<Ability> allAbilitiesForReading = caster.abilities.AllAbilitiesForReading;
            if (allAbilitiesForReading.Count() == 0)
            {
                return;
            }
            Func<Ability, bool> func = delegate (Ability a)
            {
                if (a.def.IsPsycast && a.def.level <= caster.GetPsylinkLevel() && a.CanApplyOn(target) && a.def.verbProperties.range > 0f && a.def.targetRequired && a.FinalPsyfocusCost(target) < caster.psychicEntropy.CurrentPsyfocus && a.Id != this.parent.Id)
                {
                    if (!a.comps.Any((AbilityComp c) => c is CompAbilityEffect_WithDest))
                    {
                        return !caster.psychicEntropy.WouldOverflowEntropy(a.def.EntropyGain);
                    }
                }
                return false;
            };
            List<Ability> trapSettableAbilities = allAbilitiesForReading.Where(func).ToList<Ability>();
            if (this.parent.pawn.IsColonistPlayerControlled)
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (Ability ability in trapSettableAbilities)
                {
                    Ability toCast = ability;
                    FloatMenuOption option = new FloatMenuOption(ability.def.label, delegate
                    {
                        Thing thing = GenSpawn.Spawn(this.Props.thingDef, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                        thing.SetFaction(caster.Faction, null);
                        if (thing.TryGetComp<CompPsycastTrap>() != null)
                        {
                            thing.TryGetComp<CompPsycastTrap>().storedPsycast = toCast;
                        }
                    }, MenuOptionPriority.Default, null, null, 30f, null, null, true, 0);
                    options.Add(option);
                }
                FloatMenu menu = new FloatMenu(options);
                menu.vanishIfMouseDistant = false;
                Find.WindowStack.Add(menu);
            } else {
                Thing thing = GenSpawn.Spawn(this.Props.thingDef, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                thing.SetFaction(caster.Faction, null);
                if (thing.TryGetComp<CompPsycastTrap>() != null)
                {
                    thing.TryGetComp<CompPsycastTrap>().storedPsycast = HVPAAUtility.StrongestTrapAbility(trapSettableAbilities, this.parent.pawn.Map,target.Cell);
                }
            }
        }
    }
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
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
            return p.GetStatValue(StatDefOf.MeleeDPS);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
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
    /*public class UseCaseTags_Extend : UseCaseTags
    {
        public override float MetaApplicability(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, List<PotentialPsycast> psycasts, int situationCase, float niceToEvil)
        {
            Pawn pawn = psycast.ability.pawn;
            float entropyGain = psycast.ability.def.EntropyGain;
            float psyfocusCost = psycast.ability.def.PsyfocusCost;
            if (pawn.psychicEntropy != null)
            {
                float myRange = psycast.ability.verb.EffectiveRange;
                float bestScore = 0f;
                foreach (PotentialPsycast potPsy in psycasts)
                {
                    Psycast ability = potPsy.ability;
                    float aRange = ability.verb.EffectiveRange;
                    if (!ability.def.targetRequired || aRange <= 0f || aRange > myRange || ability.comps.Any((AbilityComp c) => c is CompAbilityEffect_WithDest))
                    {
                        continue;
                    }
                    if (pawn.psychicEntropy.WouldOverflowEntropy(ability.def.EntropyGain + entropyGain) || ability.def.PsyfocusCost + psyfocusCost > pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                    {
                        continue;
                    }
                    UseCaseTags uct = ability.def.GetModExtension<UseCaseTags>();
                    if (uct != null)
                    {
                        float score = potPsy.score * uct.ApplicabilityScore(intPsycasts, potPsy, niceToEvil);
                        float dist = potPsy.lti.Cell.DistanceTo(pawn.Position);
                        if (potPsy.lti.IsValid && dist <= myRange && dist > aRange && score > 0)
                        {
                            if (psycast.lti == null || score > bestScore)
                            {
                                psycast.lti = potPsy.lti;
                                bestScore = score;
                            }
                        }
                    }
                }
                return bestScore;
            }
            return 0f;
        }
        public float minCombatPsyfocusCost;
        public float minUtilityPsyfocusCost;
    }*/
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
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
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
            return p.Downed || !p.RaceProps.IsFlesh;
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
    //transcendent HOP
    public class UseCaseTags_PsyfocusTransfer : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.abilities == null || p.psychicEntropy == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
            {
                return true;
            }
            if (!initialTarget && p.Downed)
            {
                return true;
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.abilities == null || p.psychicEntropy == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.psychicEntropy.CurrentPsyfocus < this.donorMinPsyfocusCutoff;
        }
        public float TotalCostOfRelevantPsycasts(Pawn p, bool combat)
        {
            float totalPsyfocusCost = 0f;
            foreach (Ability a in p.abilities.abilities)
            {
                UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                if (uct != null && (uct.healing || (combat ? (uct.damage || uct.defense || uct.debuff) : uct.utility)))
                {
                    totalPsyfocusCost += a.def.PsyfocusCost;
                }
            }
            return totalPsyfocusCost;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float biggestDiffInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p2 != psycast.pawn && p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && p.HasPsylink && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map))
                {
                    float p2sBiggestDiff = this.TotalCostOfRelevantPsycasts(p2,useCase != 5)*(1-p2.psychicEntropy.CurrentPsyfocus);
                    if (p2sBiggestDiff > biggestDiffInRange)
                    {
                        recipient = p2;
                        biggestDiffInRange = p2sBiggestDiff;
                    }
                }
            }
            biggestDiffInRange += this.TotalCostOfRelevantPsycasts(p, true) * p.psychicEntropy.CurrentPsyfocus;
            if (recipient == null)
            {
                return -1f;
            }
            this.targetPairs.Add(p, recipient);
            return Math.Max(0f, biggestDiffInRange);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float biggestDiffInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p != p2 && p2 != psycast.pawn && p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && p.HasPsylink && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map) && p.psychicEntropy.CurrentPsyfocus < this.recipientMaxPsyfocusCutoff)
                {
                    float scalar = 1 - p2.psychicEntropy.CurrentPsyfocus;
                    float p2sBiggestDiff = (this.TotalCostOfRelevantPsycasts(p2,useCase != 5)-this.TotalCostOfRelevantPsycasts(p, useCase != 5))*scalar;
                    if (p2sBiggestDiff > biggestDiffInRange)
                    {
                        recipient = p2;
                        biggestDiffInRange = p2sBiggestDiff;
                    }
                }
            }
            if (recipient == null)
            {
                return -1f;
            }
            this.targetPairs.Add(p, recipient);
            return Math.Max(0f, biggestDiffInRange);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn);
            if (pawn != null && this.recipientPawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn2 = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets2);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn2);
            if (pawn2 != null && this.recipientPawn != null)
            {
                psycast.lti = pawn2;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets2.TryGetValue(pawn2);
            }
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn);
            if (pawn != null && this.recipientPawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.targetPairs = new Dictionary<Pawn, Pawn>();
            return base.ApplicabilityScore(intPsycasts, psycast, niceToEvil);
        }
        public override Pawn FindEnemyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                Log.Message("yuh");
                if ((intPsycasts.foes.Contains(p) || p.Faction == null || (psycast.pawn.Faction != null && p.Faction != null && p.Faction.HostileTo(psycast.pawn.Faction))) && p.HasPsylink)
                {
                    Log.Error("p: " + p.Name.ToStringShort);
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        float pApplicability = this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
                        Log.Message("pappl " + pApplicability);
                        if (pApplicability > 0f)
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
        public override Pawn FindAllyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!intPsycasts.foes.Contains(p) && p.HasPsylink)
                {
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
                        if (pApplicability > 0f)
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
        public float donorToRecipientRange;
        public Pawn recipientPawn;
        public Dictionary<Pawn, Pawn> targetPairs;
        public float recipientMaxPsyfocusCutoff;
        public float donorMinPsyfocusCutoff;
    }
    public class UseCaseTags_MarkingPulse : UseCaseTags
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * (1+p.GetStatValue(StatDefOf.MeleeDodgeChance)) * (1+p.GetStatValue(VEF.Pawns.InternalDefOf.VEF_RangedDodgeChance)) /p.BodySize;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * this.PawnEnemyApplicability(intPsycasts,psycast,p,niceToEvil,useCase,initialTarget);
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
    }
    public class UseCaseTags_SquadronCall : UseCaseTags
    {
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            List<Pawn> foes = intPsycasts.foes.InRandomOrder<Pawn>().ToList();
            IntVec3 origin = intPsycasts.parent.pawn.Position;
            psycast.lti = origin;
            foreach (Pawn p in foes)
            {
                if (p.Position.DistanceTo(origin) <= this.Range(psycast.ability) && GenSight.LineOfSight(origin, p.Position, p.Map))
                {
                    psycast.lti = p.Position;
                }
            }
            return intPsycasts.parent.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(this.castChanceWhileNotInImmediateCombat)? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count < 1)
            {
                return 0f;
            }
            psycast.lti = intPsycasts.parent.pawn.Position;
            return intPsycasts.parent.pawn.GetStatValue(StatDefOf.PsychicSensitivity);
        }
        public float castChanceWhileNotInImmediateCombat;
    }
    public class UseCaseTags_TornadoLink : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            RoofDef roof = p.Map.roofGrid.RoofAt(p.Position);
            return roof != null && roof.isThickRoof;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            RoofDef roof = p.Map.roofGrid.RoofAt(p.Position);
            return roof != null && roof.isThickRoof;
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            foreach (HediffDef h in this.dontUseIfHave)
            {
                if (psycast.pawn.health.hediffSet.HasHediff(h))
                {
                    return 0f;
                }
            }
            return psycast.pawn.Faction == null ? 0f : base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            Map map = psycast.pawn.Map;
            Faction f = psycast.pawn.Faction;
            IntVec3 casterLoc = psycast.pawn.Position;
            for (int i = 0; i <= 5; i++)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(casterLoc, map, (int)(this.Range(psycast)), null, out tryNewPosition);
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(casterLoc, tryNewPosition, map))
                    {
                        RoofDef roof = map.roofGrid.RoofAt(tryNewPosition);
                        if (roof == null || !roof.isThickRoof)
                        {
                            break;
                        }
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -1f;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, map, aoe, true))
                    {
                        if (thing.def == this.avoidMakingTooMuchOfThing)
                        {
                            tryNewScore = -15f;
                            break;
                        }
                        if (thing is Plant plant)
                        {
                            Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
                            if (zone != null && zone is Zone_Growing && f != Faction.OfPlayerSilentFail && f.HostileTo(Faction.OfPlayerSilentFail))
                            {
                                tryNewScore += this.TornadoThingScore(plant);
                            }
                        } else if (thing is Building b && b.Faction != null) {
                            if (f != b.Faction && f.HostileTo(b.Faction))
                            {
                                tryNewScore += this.TornadoThingScore(b);
                            } else if (niceToEvil > 0 || f == b.Faction || f.RelationKindWith(b.Faction) == FactionRelationKind.Ally) {
                                tryNewScore -= this.TornadoThingScore(b);
                            }
                        } else if (thing is Pawn p) {
                            if (intPsycasts.allies.Contains(p) && !this.OtherAllyDisqualifiers(psycast, p, 1))
                            {
                                tryNewScore -= this.TornadoThingScore(p) * 1.5f;
                            } else if (intPsycasts.foes.Contains(p) && !this.OtherEnemyDisqualifiers(psycast, p, 1)) {
                                tryNewScore += this.TornadoThingScore(p);
                            }
                        }
                    }
                    if (tryNewScore > 0)
                    {
                        positionTargets.Add(tryNewPosition, tryNewScore);
                    }
                }
            }
            IntVec3 bestPosition = IntVec3.Invalid;
            if (positionTargets.Count > 0)
            {
                float value = -1f;
                foreach (KeyValuePair<IntVec3, float> kvp in positionTargets)
                {
                    if (!bestPosition.IsValid || kvp.Value >= value)
                    {
                        bestPosition = kvp.Key;
                        value = kvp.Value;
                    }
                }
            }
            return bestPosition;
        }
        public float TornadoThingScore(Thing t)
        {
            float scoreMulti = 1f;
            if (t is Building b && b.def.building != null && b.def.building.IsTurret)
            {
                CompPowerTrader cpt = b.TryGetComp<CompPowerTrader>();
                if (cpt == null || !cpt.PowerOn)
                {
                    scoreMulti = 2f;
                }
            } else if (t is Pawn p) {
                scoreMulti *= p.GetStatValue(StatDefOf.IncomingDamageFactor);
            } else if (t is Plant) {
                scoreMulti /= 2.5f;
            }
            return scoreMulti * HautsUtility.DamageFactorFor(this.damageDef, t) * t.MarketValue / 200f;
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
        public DamageDef damageDef;
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
    }
    public class UseCaseTags_WordOfBlessing : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 10f * this.PawnAllyApplicability(intPsycasts, psycast.ability, pawn, niceToEvil, 4);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed)
            {
                return true;
            }
            foreach (Hediff hediff in p.health.hediffSet.hediffs)
            {
                if (this.grantableHediffs.Contains(hediff.def))
                {
                    return true;
                }
            }
            return false;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (p.MarketValue - this.minMarketValue)/500f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minMarketValue;
        public List<HediffDef> grantableHediffs;
    }
    public class UseCaseTags_Voidquake : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.Faction == null || !psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail) || (situationCase != 1 && situationCase != 3))
            {
                return 0f;
            }
            if (pacifist)
            {
                return 0f;
            }
            switch (situationCase)
            {
                case 1:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    } else if (niceToEvil < 0f) {
                        return 2f;
                    } else {
                        return 1.7f;
                    }
                case 3:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    } else if (niceToEvil < 0f) {
                        return 2f;
                    } else {
                        return 1.7f;
                    }
                default:
                    return 0f;
            }
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (Rand.Chance(this.chancePerEvil * (-niceToEvil - this.minEvil)) && (intPsycasts.continuousTimeSpawned > 5000 || !HVPAA_Mod.settings.powerLimiting))
            {
                psycast.lti = new LocalTargetInfo(intPsycasts.Pawn);
                float score = 1f;
                int situation = intPsycasts.GetSituation();
                foreach (Pawn p in intPsycasts.allies)
                {
                    if (!this.ShouldRally(psycast.ability, p, situation))
                    {
                        if (ModsConfig.AnomalyActive && (p.IsMutant || p.RaceProps.IsAnomalyEntity))
                        {
                            score += p.MarketValue / 250f;
                        } else if (!p.kindDef.isBoss && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon) {
                            score -= p.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
                        }
                    }
                }
                foreach (Pawn p in intPsycasts.foes)
                {
                    if (ModsConfig.AnomalyActive && (p.IsMutant || p.RaceProps.IsAnomalyEntity))
                    {
                        score -= p.MarketValue / 250f;
                    } else if (!p.kindDef.isBoss && p.GetStatValue(StatDefOf.PsychicSensitivity) > float.Epsilon) {
                        score += p.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
                    }
                }
                return score;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return intPsycasts.GetSituation() != 3 ? 0f : this.ApplicabilityScoreDamage(intPsycasts, psycast, niceToEvil);
        }
        public override bool ShouldRally(Psycast psycast, Pawn p, int situation)
        {
            return p != psycast.pawn && (!ModsConfig.AnomalyActive || (!p.IsMutant && !p.RaceProps.IsAnomalyEntity)) && (p.Position.DistanceTo(psycast.pawn.Position) - this.rallyRadius) / p.GetStatValue(StatDefOf.MoveSpeed) <= psycast.def.verbProperties.warmupTime;
        }
        public float minEvil;
        public float chancePerEvil;
    }
}
