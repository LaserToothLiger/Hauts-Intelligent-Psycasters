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
using Verse.AI;
using VEF;

namespace HVPAA_FSF
{
    [StaticConstructorOnStartup]
    public class HVPAA_FSF
    {
    }
    //level 2
    public class UseCaseTags_Disarm : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.equipment == null || p.equipment.Primary == null;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = 0f;
            Pawn_EquipmentTracker pet = p.equipment;
            if (pet != null && pet.Primary != null)
            {
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
                else if (list != null && list2 != null)
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
            return p.health.capacities.GetLevel(PawnCapacityDefOf.Manipulation)*app;
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
    public class UseCaseTags_Frostbite : UseCaseTags
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
            return p.Downed || (p.RaceProps.Humanlike && !this.canHitHumanlike) || (p.IsColonist && !this.canHitColonist) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.GetStatValue(StatDefOf.IncomingDamageFactor) <= float.Epsilon;
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
        public DamageDef damageType;
        public float chanceToCast;
        public float chanceToCastHumanlike;
        public float chanceToCastColonist;
        private bool canHitHumanlike;
        private bool canHitColonist;
    }
    //level 4
    public class UseCaseTags_ElementalShield : UseCaseTags
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
            return !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float worstCondition = Math.Max(0f,Math.Max(p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature, p.AmbientTemperature - p.GetStatValue(StatDefOf.ComfyTemperatureMax)));
            p.health.hediffSet.TryGetHediff(HediffDefOf.ToxicBuildup, out Hediff tb);
            if (tb != null)
            {
                worstCondition += tb.Severity * 10f;
            }
            if (p.IsBurning())
            {
                worstCondition += 1000;
            }
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * worstCondition;
        }
    }
    //level 5
    public class UseCaseTags_Fracture : UseCaseTags
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
            return  p.GetStatValue(StatDefOf.PsychicSensitivity) * (p.HealthScale + (p.GetStatValue(StatDefOf.ArmorRating_Blunt)+(p.GetStatValue(StatDefOf.ArmorRating_Heat)/2f)+p.GetStatValue(StatDefOf.ArmorRating_Sharp)));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.GetStatValue(StatDefOf.PsychicSensitivity) * (p.HealthScale + (p.GetStatValue(StatDefOf.ArmorRating_Blunt) + (p.GetStatValue(StatDefOf.ArmorRating_Heat) / 2f) + p.GetStatValue(StatDefOf.ArmorRating_Sharp)));
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
    public class UseCaseTags_Psyblade : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
            {
                return 0f;
            }
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn p = intPsycasts.Pawn;
            psycast.lti = p;
            float howMuchBetterIsPsybladeThanAvg = ((1+this.avgArmorPen)*this.avgDamage/this.avgWeaponCooldown) - p.GetStatValue(StatDefOf.MeleeDPS);
            if (howMuchBetterIsPsybladeThanAvg <= 0f)
            {
                return 0f;
            }
            float netFoeMeleePower = 0f;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2.HostileTo(p))
                {
                    netFoeMeleePower += p2.GetStatValue(StatDefOf.MeleeDPS)*p2.HealthScale*p2.GetStatValue(StatDefOf.IncomingDamageFactor);
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
    //level 6
    public class UseCaseTags_Firestorm : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.Downed && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.Downed && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan;
        }
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
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
                        return 1.7f;
                    }
                case 3:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
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
                float score = -15f;
                HVPAAUtility.LightningApplicability(this, intPsycasts, psycast.ability, intPsycasts.Pawn.Position, niceToEvil, this.aoe, ref score);
                return score;
            }
            return 0f;
        }
        public float minEvil;
        public float chancePerEvil;
        public float ignoreAllPawnsFasterThan;
    }
    public class UseCaseTags_MassInvisibility : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.IsPsychologicallyInvisible() || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.IsPsychologicallyInvisible() || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(p.MarketValue / 1000f, 1f) * Math.Max(p.GetPsylinkLevel() / 2f, 1f) * Math.Max(p.Map.attackTargetsCache.GetPotentialTargetsFor(p).Count, 1f) * (p.WorkTagIsDisabled(WorkTags.Violent) ? (niceToEvil >= 0 ? niceToEvil : 0.5f) : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * this.PawnEnemyApplicability(intPsycasts,psycast,p,niceToEvil,useCase,initialTarget);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
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
                                        pTargetHits -= this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
                                    pTargetHits += this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
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
                                            pTargetHits -= this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                        }
                                    } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
                                        pTargetHits += this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
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
    public class UseCaseTags_MassSkip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (HVPAAUtility.SkipImmune(p, this.maxBodySize) || p.Downed)
            {
                return true;
            }
            if (initialTarget)
            {
                switch (useCase)
                {
                    case 2:
                        foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                        {
                            if (p2.HostileTo(p))
                            {
                                return true;
                            }
                        }
                        break;
                    case 3:
                        if (!this.RangedP(p))
                        {
                            return true;
                        }
                        break;
                    default:
                        break;
                }
            }
            if (!initialTarget && useCase != 4)
            {
                if (!this.RangedP(p) || p.WorkTagIsDisabled(WorkTags.Violent))
                {
                    return true;
                }
                return false;
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 2:
                    if (!this.RangedP(p))
                    {
                        return true;
                    }
                    break;
                default:
                    break;
            }
            return HVPAAUtility.SkipImmune(p, this.maxBodySize);
        }
        public bool RangedP(Pawn p)
        {
            return p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 2:
                    if (initialTarget)
                    {
                        float cover = 1f;
                        Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.skipRange, false, p);
                        if (pawn != null)
                        {
                            float jumperMeleeDPS = p.GetStatValue(StatDefOf.MeleeDPS);
                            float defenderMeleeDPS = pawn.GetStatValue(StatDefOf.MeleeDPS);
                            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                            {
                                if (p2 != p && p2 != pawn)
                                {
                                    if (p2.HostileTo(pawn))
                                    {
                                        jumperMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                                    } else if (p2.HostileTo(p)) {
                                        defenderMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                                    }
                                }
                            }
                            cover = Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(pawn.Position, pawn.Map)), 1f);
                            this.bestDestDeb = pawn.Position;
                            return Math.Max(0f, ((jumperMeleeDPS * cover) - defenderMeleeDPS) * (pawn.Position.DistanceTo(p.Position) / (float)Math.Sqrt(p.GetStatValue(StatDefOf.MoveSpeed))));
                        }
                    } else {
                        return p.GetStatValue(StatDefOf.MeleeDPS);
                    }
                    break;
                case 3:
                    return this.GetBestDestDef(intPsycasts, p, false);
                default:
                    break;
            }
            return -1f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 1:
                    Building bestTrap = null;
                    float bestTrapChance = 0f;
                    List<Pawn> collateral = new List<Pawn>();
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (p2 != p)
                        {
                            collateral.Add(p2);
                        }
                    }
                    foreach (Building_Trap b in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.skipRange, true).OfType<Building_Trap>().Distinct<Building_Trap>())
                    {
                        if (!this.needsLoS || GenSight.LineOfSight(b.Position, p.Position, p.Map))
                        {
                            float tsc = 0f;
                            float foeTsc = this.TrapSpringChance(b, p);
                            float foeDamageFactor = p.GetStatValue(StatDefOf.IncomingDamageFactor);
                            float allyTsc = 0f;
                            float allyDamageFactor = 0f;
                            foreach (Pawn p3 in collateral)
                            {
                                if (intPsycasts.foes.Contains(p3))
                                {
                                    foeTsc += this.TrapSpringChance(b, p3);
                                    foeDamageFactor += p3.GetStatValue(StatDefOf.IncomingDamageFactor);
                                } else if (intPsycasts.allies.Contains(p3)) {
                                    allyTsc += this.TrapSpringChance(b, p3);
                                    allyDamageFactor += p3.GetStatValue(StatDefOf.IncomingDamageFactor);
                                }
                            }
                            tsc = (foeTsc * foeDamageFactor) - (allyTsc * allyDamageFactor * this.allyMultiplier);
                            if (tsc > bestTrapChance)
                            {
                                bestTrapChance = tsc;
                                bestTrap = b;
                            }
                        }
                    }
                    if (bestTrap != null)
                    {
                        this.bestDestDmg = bestTrap.Position;
                        return bestTrapChance;
                    }
                    return 0f;
                case 2:
                    if (initialTarget)
                    {
                        float cover = Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)), 1f);
                        float allyNetDPS = 0f;
                        float foeNetDPS = p.GetStatValue(StatDefOf.MeleeDPS);
                        foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                        {
                            if (p2 != p)
                            {
                                if (intPsycasts.foes.Contains(p2))
                                {
                                    foeNetDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                                } else if (intPsycasts.allies.Contains(p2)) {
                                    allyNetDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                                }
                            }
                        }
                        Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.skipRange, false, p);
                        if (pawn != null)
                        {
                            this.bestDestDeb = pawn.Position;
                            return Math.Max(0f, (((pawnTargets.TryGetValue(pawn)+allyNetDPS) * cover) - foeNetDPS) * (pawn.Position.DistanceTo(p.Position) / (float)Math.Sqrt(pawn.GetStatValue(StatDefOf.MoveSpeed))));
                        }
                    } else {
                        return 1f / p.GetStatValue(StatDefOf.MeleeDPS);
                    }
                    break;
                case 3:
                    return this.GetBestDestDef(intPsycasts,p,true);
                default:
                    break;
            }
            return 0f;
        }
        public float GetBestDestDef(HediffComp_IntPsycasts intPsycasts, Pawn p, bool pIsFoe)
        {
            float netFoeMeleeDPS = p.GetStatValue(StatDefOf.MeleeDPS) * (pIsFoe ? 1f : -1f);
            bool anyNearbyAllies = false;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.Downed && !p2.IsBurning() && !p2.WorkTagIsDisabled(WorkTags.Violent))
                {
                    if (intPsycasts.allies.Contains(p2))
                    {
                        anyNearbyAllies = true;
                        netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                    } else if (intPsycasts.foes.Contains(p2)) {
                        netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                    }
                }
            }
            if (anyNearbyAllies)
            {
                List<Thing> foeTargetCache = new List<Thing>();
                foeTargetCache.AddRange(from a in p.Map.attackTargetsCache.GetPotentialTargetsFor(p) where !a.ThreatDisabled(p) select a.Thing);
                this.bestDestDef = CellFinderLoose.GetFallbackDest(p, foeTargetCache, this.skipRange, 2f, 2f, 20, (IntVec3 c) => c.IsValid && (!this.needsLoS || GenSight.LineOfSight(c, p.Position, intPsycasts.Pawn.Map)));
                return -netFoeMeleeDPS;
            }
            return 0f;
        }
        public float TrapSpringChance(Building_Trap bpt, Pawn p)
        {
            float num = 1f;
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }
            if (bpt.KnowsOfTrap(p))
            {
                if (p.Faction == null)
                {
                    if (p.IsAnimal)
                    {
                        num = 0.2f;
                        num *= bpt.def.building.trapPeacefulWildAnimalsSpringChanceFactor;
                    }
                    else
                    {
                        num = 0.3f;
                    }
                }
                else if (p.Faction == bpt.Faction)
                {
                    num = 0.005f;
                }
                else
                {
                    num = 0f;
                }
            }
            num *= bpt.GetStatValue(StatDefOf.TrapSpringChance, true, -1) * p.GetStatValue(StatDefOf.PawnTrapSpringChance, true, -1);
            return Mathf.Clamp01(num);
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase == 3 || situationCase == 5)
            {
                foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                {
                    if (p2.HostileTo(psycast.pawn))
                    {
                        return 0f;
                    }
                }
                return 1f;
            }
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDmg = IntVec3.Invalid;
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = (this.bestDestDmg.IsValid ? this.bestDestDmg : psycast.Caster.Position);
                return 2f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDeb = IntVec3.Invalid;
            int situation = intPsycasts.GetSituation();
            if (Rand.Chance(0.5f))
            {
                Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    psycast.ltiDest = (this.bestDestDeb.IsValid ? this.bestDestDeb : psycast.Caster.Position);
                    return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                }
            }
            else
            {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    psycast.ltiDest = (this.bestDestDeb.IsValid ? this.bestDestDeb : psycast.Caster.Position);
                    return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                }
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
                        if ((!this.needsLoS || GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map)) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                        {
                            this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                        }
                    }
                    psycast.lti = intPsycasts.Pawn;
                    psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                    return pathDistance * this.scoreFactor;
                }
            }
            else
            {
                if (Rand.Chance(0.5f))
                {
                    Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null && this.bestDestDef.IsValid)
                    {
                        psycast.lti = pawn;
                        psycast.ltiDest = this.bestDestDef;
                        return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                    }
                }
                else
                {
                    Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null)
                    {
                        psycast.lti = pawn;
                        psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                        return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestHeal = IntVec3.Invalid;
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null && this.bestDestHeal.IsValid)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.bestDestHeal;
                return 2f * pawnTargets.TryGetValue(pawn) * 5000f * this.scoreFactor / HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            int situation = intPsycasts.GetSituation();
            if (intPsycasts.Pawn.pather != null && intPsycasts.Pawn.pather.curPath != null && intPsycasts.Pawn.pather.curPath.Found)
            {
                int pathDistance = 0;
                for (int i = 0; i < intPsycasts.Pawn.pather.curPath.NodesLeftCount; i++)
                {
                    pathDistance++;
                    if ((!this.needsLoS || GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map)) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                    {
                        this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                    }
                }
                psycast.lti = intPsycasts.Pawn;
                psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                return pathDistance * this.scoreFactor;
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public IntVec3 bestDestDmg;
        public IntVec3 bestDestDeb;
        public IntVec3 bestDestDef;
        public IntVec3 bestDestHeal;
        public float skipRange;
        public bool needsLoS = true;
        public float scoreFactor = 1f;
    }
}
