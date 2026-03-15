using CoolPsycasts;
using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;
using Verse.AI.Group;

namespace HVPAA_CoolerPsycasts
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
     * SpawnWithHediffAI is for Illusion, endowing the created pawn with a Lord AI to escort its creator. Of course, this only triggers if the Illusion isn't of the player faction
     */
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
            return HautsMiscUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * HautsMiscUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && this.IsValidThing(psycast.pawn, t, niceToEvil, useCase))
            {
                float allyOrFoe = t.HostileTo(psycast.pawn) ? 1f : (HVPAA_DecisionMakingUtility.IsAlly(false, psycast.pawn, t, niceToEvil) ? -1f : 0f);
                if (t.def.building.IsTurret && t.def.building.ai_combatDangerous)
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        return 0f;
                    }
                    return allyOrFoe * t.MarketValue * HautsMiscUtility.DamageFactorFor(this.damageType, t) / 200f;
                }
                else if (this.canTargetHB)
                {
                    return allyOrFoe * t.MarketValue * HautsMiscUtility.DamageFactorFor(this.damageType, t) / 1000f;
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
            return Math.Min(numChunks, base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci));
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
            return 2f * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return this.appFactor * pawnTargets.TryGetValue(pawn) / 2f;
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
                                multi += p2.GetStatValue(StatDefOf.IncomingDamageFactor) * HautsMiscUtility.DamageFactorFor(this.damageDef, p2);
                            }
                            else if (intPsycasts.allies.Contains(p2))
                            {
                                multi -= this.allyMultiplier * p2.GetStatValue(StatDefOf.IncomingDamageFactor) * HautsMiscUtility.DamageFactorFor(this.damageDef, p2);
                            }
                        }
                    }
                }
            }
            return multi * p.GetStatValue(StatDefOf.PsychicSensitivity) / p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness);
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
}
