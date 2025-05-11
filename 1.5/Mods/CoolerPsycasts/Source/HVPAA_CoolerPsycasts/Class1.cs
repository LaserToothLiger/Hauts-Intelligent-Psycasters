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
using Verse;
using Verse.AI.Group;
using VFECore;
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
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_DestroyFactionRelations), nameof(CompAbilityEffect_DestroyFactionRelations.Apply), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) }),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_DFR_Prefix)));
        }
        public static bool HVPAA_DFR_Prefix(CompAbilityEffect_DestroyFactionRelations __instance)
        {
            Faction faction = __instance.parent.pawn.Faction;
            if (faction == null || faction != Faction.OfPlayerSilentFail)
            {
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
            float multi = 1f;
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
            return p.Downed || (p.RaceProps.Humanlike && !this.canHitHumanlike) || (p.IsColonist && !this.canHitColonist) || !p.HasPsylink;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float multi = 1f;
            float radius = this.aoe * p.GetPsylinkLevel();
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, radius, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.Downed && !p2.IsBurning())
                {
                    if (intPsycasts.foes.Contains(p2))
                    {
                        multi += p2.GetStatValue(StatDefOf.IncomingDamageFactor) * HautsUtility.DamageFactorFor(this.damageDef, p2);
                    }
                    else if (intPsycasts.allies.Contains(p2))
                    {
                        multi -= this.allyMultiplier * p2.GetStatValue(StatDefOf.IncomingDamageFactor) * HautsUtility.DamageFactorFor(this.damageDef, p2);
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
    //level 5
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
}
