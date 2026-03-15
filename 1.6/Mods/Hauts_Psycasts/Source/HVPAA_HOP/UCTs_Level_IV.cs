using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;

namespace HVPAA_HOP
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_BoosterLink : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p == psycast.pawn || p.Downed || (useCase == 5 ? (!p.RaceProps.Humanlike || p.skills == null) : p.WorkTagIsDisabled(WorkTags.Violent)) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(0f, Math.Max(0f, (p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation) - this.manipCutoff)) + Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.movingCutoff));
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
        public override float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            foreach (HediffDef h in this.dontUseIfHave)
            {
                if (intPsycasts.Pawn.health.hediffSet.HasHediff(h))
                {
                    return 0f;
                }
            }
            return base.ApplicabilityScore(intPsycasts, psycast, niceToEvil);
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
        public override float Range(Psycast psycast)
        {
            return base.Range(psycast) / 1.5f;
        }
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
        public float manipCutoff;
        public float movingCutoff;
        public float chanceToUtilityCast;
        public int minUtilitySkillLevel;
    }
    public class UseCaseTags_Flare : UseCaseTags
    {
        public override float MetaApplicability(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, List<PotentialPsycast> psycasts, int situationCase, float niceToEvil)
        {
            if (psycast.ability.pawn.psychicEntropy != null)
            {
                foreach (PotentialPsycast potPsy in psycasts)
                {
                    UseCaseTags uct = potPsy.ability.def.GetModExtension<UseCaseTags>();
                    if (uct != null && (psycast.ability.pawn.psychicEntropy.WouldOverflowEntropy(potPsy.ability.def.EntropyGain) || (psycast.ability.pawn.GetStatValue(HautsDefOf.Hauts_PsycastFocusRefund) < 1f && (potPsy.ability.def.PsyfocusCost >= (situationCase == 1 ? this.minCombatPsyfocusCost : this.minUtilityPsyfocusCost)))))
                    {
                        psycast.lti = intPsycasts.Pawn;
                        return 99999f;
                    }
                }
            }
            return 0f;
        }
        public float minCombatPsyfocusCost;
        public float minUtilityPsyfocusCost;
    }
    public class UseCaseTags_Replicate : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (Rand.Chance(this.chanceToUtilityCast) && (psycast.pawn.Faction == null || (psycast.pawn.MapHeld.ParentFaction != null && (psycast.pawn.Faction == psycast.pawn.MapHeld.ParentFaction || psycast.pawn.Faction.RelationKindWith(psycast.pawn.MapHeld.ParentFaction) == FactionRelationKind.Ally || (niceToEvil > 0 && psycast.pawn.Faction.RelationKindWith(psycast.pawn.MapHeld.ParentFaction) == FactionRelationKind.Neutral) || Rand.Chance(this.chanceToUtilityCast))))) ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.thingCategories != null && this.allowedItemCategories.ContainsAny((ThingCategoryDef tcd) => t.HasThingCategory(tcd)) && this.IsValidThing(psycast.pawn, t, niceToEvil, useCase) && t.MarketValue <= this.marketValueLimitItem)
            {
                float val = Math.Min(this.marketValueLimitStack, t.MarketValue * t.stackCount);
                return val * val;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            float app = 0f;
            Thing item = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Thing, float> thingTargets);
            if (item != null)
            {
                psycast.lti = item;
                app = thingTargets.TryGetValue(item) / 50;
            }
            return app;
        }
        public float chanceToUtilityCast;
        public List<ThingCategoryDef> allowedItemCategories;
        public int marketValueLimitItem;
        public int marketValueLimitStack;
    }
    public class UseCaseTags_Skiptheft : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = 0f;
            bool hasPet = false;
            Pawn_EquipmentTracker pet = p.equipment;
            if (pet != null && pet.Primary != null)
            {
                hasPet = true;
                bool melee = !pet.Primary.def.IsRangedWeapon;
                foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                {
                    if (p2.HostileTo(p))
                    {
                        melee = true;
                    }
                }
                List<VerbProperties> list = pet.Primary.def.Verbs;
                List<Tool> list2 = pet.Primary.def.tools;
                if (!melee)
                {
                    app = pet.Primary.MarketValue * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) * p.GetStatValue(StatDefOf.AimingDelayFactor) * p.GetStatValue(StatDefOf.RangedCooldownFactor) / 100f;
                }
                else
                {
                    app = (from x in VerbUtility.GetAllVerbProperties(list, list2)
                           where x.verbProps.IsMeleeAttack
                           select x).AverageWeighted((VerbUtility.VerbPropertiesWithSource x) => x.verbProps.AdjustedMeleeSelectionWeight(x.tool, p, pet.Primary, null, false), (VerbUtility.VerbPropertiesWithSource x) => x.verbProps.AdjustedMeleeDamageAmount(x.tool, p, pet.Primary, null));
                    float cd = (from x in VerbUtility.GetAllVerbProperties(list, list2)
                                where x.verbProps.IsMeleeAttack
                                select x).AverageWeighted((VerbUtility.VerbPropertiesWithSource x) => x.verbProps.AdjustedMeleeSelectionWeight(x.tool, p, pet.Primary, null, false), (VerbUtility.VerbPropertiesWithSource x) => x.verbProps.AdjustedCooldown(x.tool, p, pet.Primary));
                    app /= cd;
                }
                app *= (p.CurJob != null && p.CurJob.verbToUse != null ? 0.5f : 0.25f);
            }
            if (!hasPet)
            {
                Pawn_ApparelTracker pat = p.apparel;
                if (pat != null && pat.WornApparelCount > 0)
                {
                    foreach (Apparel a in pat.WornApparel)
                    {
                        app = a.GetStatValue(StatDefOf.ArmorRating_Blunt) + a.GetStatValue(StatDefOf.ArmorRating_Sharp) + (a.GetStatValue(StatDefOf.ArmorRating_Heat) * 0.4f);
                        app = Math.Max(app - this.minApparelArmor, -0.1f);
                    }
                }
            }
            return app;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.Pawn.inventory == null || intPsycasts.Pawn.inventory.innerContainer.Count >= inventoryLimit)
            {
                return 0f;
            }
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float minApparelArmor;
        public int inventoryLimit;
    }
    public class UseCaseTags_TurretSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.Position.AnyGas(p.Map, GasType.BlindSmoke);
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
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map) && psycast.verb.ValidateTarget(tryNewPosition, false))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = 0f;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, aoe, true))
                    {
                        if (thing is Building b && b.Faction != null)
                        {
                            if (b.def.building != null && b.def.building.IsTurret && intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                            {
                                CompPowerTrader cpt = b.TryGetComp<CompPowerTrader>();
                                if (cpt == null || !cpt.PowerOn)
                                {
                                    tryNewScore += 1f;
                                }
                            }
                        }
                        else if (thing is Pawn p && intPsycasts.foes.Contains(p) && !this.OtherEnemyDisqualifiers(psycast, p, 1))
                        {
                            tryNewScore += 1f;
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
    }
}
