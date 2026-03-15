using HautsFramework;
using HautsPsycasts;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA_HOP
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_FluxPulse : UseCaseTags
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
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map) && !tryNewPosition.Roofed(psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -1f;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, aoe, true))
                    {
                        if (thing is Building b && !b.AllComps.NullOrEmpty())
                        {
                            bool hasEMPableComps = false;
                            if (b.TryGetComp<RimWorld.CompShield>() != null || b.TryGetComp<VEF.Apparels.CompShield>() != null || b.TryGetComp<CompProjectileInterceptor>() != null)
                            {
                                hasEMPableComps = true;
                            }
                            else
                            {
                                CompStunnable cs = b.TryGetComp<CompStunnable>();
                                if (cs != null && cs.CanBeStunnedByDamage(DamageDefOf.EMP))
                                {
                                    hasEMPableComps = true;
                                }
                            }
                            if (hasEMPableComps)
                            {
                                if (intPsycasts.Pawn.HostileTo(b))
                                {
                                    tryNewScore += b.MarketValue / 1000f;
                                }
                                else if (niceToEvil > 0 || intPsycasts.Pawn.Faction == null || b.Faction == null || intPsycasts.Pawn.Faction == b.Faction || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                                {
                                    tryNewScore -= b.MarketValue * 1.5f / 1000f;
                                }
                            }
                        }
                        else if (thing is Pawn p && !p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff) && ((p.AmbientTemperature - (p.GetStatValue(StatDefOf.ComfyTemperatureMax) + this.maxComfyTempMod)) > 0f || ((p.GetStatValue(StatDefOf.ComfyTemperatureMin) + this.minComfyTempMod) - p.AmbientTemperature) > 0f || HautsMiscUtility.ReactsToEMP(p)))
                        {
                            if (intPsycasts.allies.Contains(p))
                            {
                                tryNewScore -= p.MarketValue / 1000f;
                            }
                            else if (intPsycasts.foes.Contains(p))
                            {
                                tryNewScore += p.MarketValue / 1000f;
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
        public float maxComfyTempMod;
        public float minComfyTempMod;
    }
    public class UseCaseTags_MeteoroidSkip : UseCaseTags
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
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.canTargetHB = HVPAA_Mod.settings.powerLimiting && Rand.Chance(this.chanceCanTargetHarmlessBuildings);
            this.canTargetPawns = Rand.Chance(this.chanceCanTargetPawns) || !HVPAA_Mod.settings.powerLimiting;
            List<Thing> firstChunkTargets = new List<Thing>();
            Dictionary<Thing, float> chunkTargets = new Dictionary<Thing, float>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.Range(psycast.ability), true))
            {
                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, t.Position, t.Map) && (t.HasThingCategory(ThingCategoryDefOf.Chunks) || t.HasThingCategory(ThingCategoryDefOf.StoneChunks)))
                {
                    firstChunkTargets.Add(t);
                }
            }
            for (int i = 5; i > 0; i--)
            {
                if (firstChunkTargets.Count > 0)
                {
                    Thing t = firstChunkTargets.RandomElement();
                    float chunkPower = Meteoroid.ChunkMeteorDamageMulti(t.def);
                    if (chunkPower > 0f)
                    {
                        chunkTargets.Add(t, chunkPower);
                    }
                    firstChunkTargets.Remove(t);
                }
            }
            List<Thing> highShields = intPsycasts.Pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
            psycast.lti = IntVec3.Invalid;
            float app = 0f;
            foreach (Thing t in chunkTargets.Keys)
            {
                Thing bestHit = null;
                float applic = 0f;
                foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(t.Position, intPsycasts.Pawn.Map, this.Range(psycast.ability), true))
                {
                    if (!GenSight.LineOfSight(t2.Position, t.Position, t2.Map))
                    {
                        continue;
                    }
                    if (t2.Position.GetRoof(t2.Map) == null || !t2.Position.GetRoof(t2.Map).isThickRoof)
                    {
                        bool canLandOn = true;
                        for (int i = 0; i < highShields.Count; i++)
                        {
                            CompProjectileInterceptor cpi = highShields[i].TryGetComp<CompProjectileInterceptor>();
                            if (cpi != null && cpi.Active && t2.Position.InHorDistOf(highShields[i].PositionHeld, cpi.Props.radius))
                            {
                                canLandOn = false;
                                break;
                            }
                        }
                        if (canLandOn)
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
                }
                if (app < applic)
                {
                    app = applic;
                    psycast.lti = t;
                    psycast.ltiDest = bestHit;
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
    }
    public class UseCaseTags_ThermoPinhole : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Spawned && useCase == 4)
            {
                if (this.IsOnFire(p))
                {
                    return false;
                }
                if (!p.Position.UsesOutdoorTemperature(p.Map))
                {
                    p.health.hediffSet.TryGetHediff(HediffDefOf.Hypothermia, out Hediff hypo);
                    if (hypo != null && hypo.Severity >= 0.04f)
                    {
                        return false;
                    }
                    p.health.hediffSet.TryGetHediff(HediffDefOf.Heatstroke, out Hediff heat);
                    if (heat != null && heat.Severity >= 0.04f)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (useCase == 3)
            {
                return p.MarketValue;
            }
            else if (useCase == 4)
            {
                Fire attachedFire = (Fire)p.GetAttachment(ThingDefOf.Fire);
                if (attachedFire != null)
                {
                    this.useToKillFires = true;
                    return p.GetStatValue(StatDefOf.Flammability) * attachedFire.CurrentSize();
                }
                return Math.Max(p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature, p.AmbientTemperature - p.GetStatValue(StatDefOf.ComfyTemperatureMax));
            }
            else if (useCase == 5)
            {
                return 1f;
            }
            return 1f;
        }
        public bool IsOnFire(Pawn p)
        {
            Fire attachedFire = (Fire)p.GetAttachment(ThingDefOf.Fire);
            if (attachedFire != null)
            {
                this.useToKillFires = true;
                return p.GetStatValue(StatDefOf.Flammability) > 0;
            }
            return false;
        }
        public override bool TooMuchThingAdditionalCheck(Thing thing, Psycast psycast)
        {
            return WanderUtility.InSameRoom(psycast.pawn.Position, thing.Position, thing.Map);
        }
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            if (useCase == 4)
            {
                foreach (Fire fire in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.Range(psycast), true).OfType<Fire>().Distinct<Fire>())
                {
                    if (!fire.Position.Filled(psycast.pawn.Map) && !possibleTargets.ContainsKey(fire.PositionHeld))
                    {
                        float adjacentFires = 0f;
                        foreach (Thing thing in GenRadial.RadialDistinctThingsAround(fire.PositionHeld, psycast.pawn.Map, this.aoe, true))
                        {
                            if (thing is Fire)
                            {
                                adjacentFires += 1f;
                            }
                            else if (thing.HasAttachment(ThingDefOf.Fire))
                            {
                                if (thing is Pawn p)
                                {
                                    if (HVPAA_DecisionMakingUtility.IsAlly(intPsycasts.niceToAnimals <= 0, psycast.pawn, p, niceToEvil))
                                    {
                                        if (this.OtherAllyDisqualifiers(psycast, p, useCase))
                                        {
                                            continue;
                                        }
                                    }
                                    else if (intPsycasts.foes.Contains(p))
                                    {
                                        adjacentFires -= niceToEvil > 0 ? 1f : 4f;
                                    }
                                }
                                CompExplosive cexp = thing.TryGetComp<CompExplosive>();
                                if (cexp != null && cexp.Props.startWickOnDamageTaken != null && cexp.Props.startWickOnDamageTaken.Contains(DamageDefOf.Flame))
                                {
                                    adjacentFires += cexp.Props.damageAmountBase < 0f ? cexp.Props.explosiveDamageType.defaultDamage : cexp.Props.damageAmountBase;
                                }
                                if (thing.Faction != null && psycast.pawn.Faction != null && (thing.Faction == psycast.pawn.Faction || (niceToEvil > 0f && thing.Faction.RelationKindWith(psycast.pawn.Faction) == FactionRelationKind.Ally)))
                                {
                                    adjacentFires += 2f * thing.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, thing);
                                }
                                else
                                {
                                    adjacentFires += 1f;
                                }
                            }
                            if (!fire.PositionHeld.UsesOutdoorTemperature(psycast.pawn.Map))
                            {
                                adjacentFires *= 5f;
                            }
                        }
                        if (adjacentFires > 0 && !possibleTargets.ContainsKey(fire.PositionHeld))
                        {
                            possibleTargets.Add(fire.PositionHeld, adjacentFires * this.scoreFactor);
                        }
                    }
                }
                if (!possibleTargets.NullOrEmpty())
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
                        if (kvp.Value >= highestValue / (Math.Max(1f, highestValue - 1f)))
                        {
                            positionTargets.Add(kvp.Key, kvp.Value);
                        }
                    }
                    if (positionTargets != null && positionTargets.Count > 0)
                    {
                        return positionTargets.RandomElement().Key;
                    }
                }
            }
            else if (useCase == 5 && intPsycasts.Pawn.Faction != null && (intPsycasts.Pawn.Faction == Faction.OfPlayerSilentFail || intPsycasts.Pawn.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Ally || (niceToEvil > 0 && intPsycasts.Pawn.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Neutral)))
            {
                Map map = intPsycasts.Pawn.Map;
                Dictionary<Plant, float> glowPoints = new Dictionary<Plant, float>();
                List<Plant> plants = GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, map, this.Range(psycast), true).OfType<Plant>().Distinct<Plant>().ToList();
                if (!plants.NullOrEmpty())
                {
                    for (int i = 0; i < 5; i++)
                    {
                        Plant p = plants.RandomElement();
                        if (!p.def.plant.cavePlant && !glowPoints.ContainsKey(p))
                        {
                            Zone zone = map.zoneManager.ZoneAt(p.Position);
                            if (zone != null && zone is Zone_Growing && map.roofGrid.Roofed(p.Position))
                            {
                                float glow = p.def.plant.growMinGlow - map.glowGrid.GroundGlowAt(p.Position);
                                foreach (Plant p2 in GenRadial.RadialDistinctThingsAround(p.Position, map, this.aoe, true).OfType<Plant>().Distinct<Plant>().ToList())
                                {
                                    if (p2.def.plant.cavePlant)
                                    {
                                        glow = 0f;
                                        break;
                                    }
                                    if (map.roofGrid.Roofed(p2.Position) && map.zoneManager.ZoneAt(p2.Position) != null)
                                    {
                                        glow += p2.def.plant.growMinGlow - map.glowGrid.GroundGlowAt(p2.Position);
                                    }
                                }
                                if (glow > 0f && !positionTargets.ContainsKey(p.Position))
                                {
                                    glowPoints.Add(p, glow);
                                    positionTargets.Add(p.Position, glow);
                                }
                            }
                        }
                    }
                    if (glowPoints.Count > 0)
                    {
                        KeyValuePair<Plant, float> toPick = glowPoints.First();
                        foreach (KeyValuePair<Plant, float> kvp in glowPoints)
                        {
                            if (kvp.Value > toPick.Value)
                            {
                                toPick = kvp;
                            }
                        }
                        return toPick.Key.Position;
                    }
                }
            }
            return IntVec3.Invalid;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.useToKillFires = false;
            foreach (Pawn p in intPsycasts.allies)
            {
                if (this.IsOnFire(p))
                {
                    this.useToKillFires = true;
                    break;
                }
            }
            CompAbilityEffect_Spawn caes = psycast.ability.CompOfType<CompAbilityEffect_Spawn>();
            if (this.useToKillFires)
            {
                IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<IntVec3, float> positionTargets);
                if (position.IsValid && caes != null && caes.Valid(new LocalTargetInfo(position), false))
                {
                    psycast.lti = position;
                    return 100f * positionTargets.TryGetValue(position);
                }
            }
            else
            {
                Room room = intPsycasts.Pawn.Position.GetRoom(intPsycasts.Pawn.Map);
                if (room != null)
                {
                    int solarPinholes = 0;
                    List<IntVec3> solarCells = new List<IntVec3>();
                    float coldTemps = 0f;
                    foreach (Thing t in room.ContainedAndAdjacentThings)
                    {
                        if (t.def == this.avoidMakingTooMuchOfThing)
                        {
                            solarPinholes++;
                            solarCells.Add(t.Position);
                            if (solarPinholes >= this.thingLimit)
                            {
                                return 0f;
                            }
                        }
                        else if (t is Pawn p && intPsycasts.allies.Contains(p))
                        {
                            if (!this.OtherAllyDisqualifiers(psycast.ability, p, 4))
                            {
                                coldTemps += this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 4);
                            }
                        }
                    }
                    if (caes != null)
                    {
                        if (coldTemps > 0f)
                        {
                            IntVec3 bestCell = IntVec3.Invalid;
                            float darkness = 200f;
                            foreach (IntVec3 cell in room.Cells)
                            {
                                if (caes.Valid(new LocalTargetInfo(cell), false) && !solarCells.Contains(cell) && cell.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast.ability) && GenSight.LineOfSight(intPsycasts.Pawn.Position, cell, intPsycasts.Pawn.Map))
                                {
                                    float light = intPsycasts.Pawn.Map.glowGrid.GroundGlowAt(cell, false, false);
                                    if (light < darkness)
                                    {
                                        darkness = light;
                                        bestCell = cell;
                                    }
                                }
                            }
                            if (bestCell.IsValid)
                            {
                                psycast.lti = bestCell;
                                return coldTemps;
                            }
                        }
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return 100f * positionTargets.TryGetValue(position);
            }
            return 0f;
        }
        public bool useToKillFires = false;
        public float scoreFactor = 0.01f;
        public FloatRange stableTempRange;
    }
    public class UseCaseTags_Reave : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.CurJob != null && (situationCase > 2 || (psycast.pawn.CurJob.jobGiver != null && psycast.pawn.CurJob.jobGiver is JobGiver_AISapper)))
            {
                this.wallBlast = true;
                return 1f;
            }
            this.wallBlast = false;
            return Rand.Chance(0.5f) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.GetStatValue(StatDefOf.IncomingDamageFactor) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsMiscUtility.DamageFactorFor(this.damageType, p) * (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f) / p.GetStatValue(StatDefOf.IncomingDamageFactor);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && t.HostileTo(psycast.pawn))
            {
                if (t.def.building.IsTurret && t.def.building.ai_combatDangerous)
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        return 0f;
                    }
                    return t.MarketValue * HautsMiscUtility.DamageFactorFor(this.damageType, t) / 4f;
                }
                else if (t.def.building.isTrap && t.def.building.ai_chillDestination)
                {
                    return t.MarketValue * HautsMiscUtility.DamageFactorFor(this.damageType, t) / 2f;
                }
                else if (this.canTargetHB)
                {
                    return t.MarketValue * HautsMiscUtility.DamageFactorFor(this.damageType, t) / 250f;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.canTargetHB = HVPAA_Mod.settings.powerLimiting && Rand.Chance(this.chanceCanTargetHarmlessBuildings);
            if (this.wallBlast)
            {
                if (intPsycasts.Pawn.CurJobDef == JobDefOf.Mine || intPsycasts.Pawn.CurJobDef == JobDefOf.AttackStatic)
                {
                    Thing thing = intPsycasts.Pawn.CurJob.targetA.Thing;
                    if (thing != null && thing.def.useHitPoints)
                    {
                        psycast.lti = thing;
                        return 10f;
                    }
                }
            }
            float app = 0f;
            Thing turret = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Thing, float> thingTargets);
            if (turret != null)
            {
                psycast.lti = turret;
                app = thingTargets.TryGetValue(turret);
            }
            if (Rand.Chance(this.chanceCanTargetPawns) || !HVPAA_Mod.settings.powerLimiting)
            {
                Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null && pawnTargets.TryGetValue(pawn) > app)
                {
                    psycast.lti = pawn;
                    app = pawnTargets.TryGetValue(pawn);
                }
            }
            return app;
        }
        public DamageDef damageType;
        public bool wallBlast = false;
        public bool canTargetHB = false;
        public float chanceCanTargetPawns;
        public float chanceCanTargetHarmlessBuildings;
    }
    public class UseCaseTags_WordOfSafety : UseCaseTags
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.RaceProps.IsFlesh || p.health.hediffSet.HasHediff(this.alsoAvoid) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= (this.Range(psycast) + this.aoe))
            {
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
                            iNeedHealing += Math.Max(0f, h.Severity + 4f * h.BleedRate);
                        }
                    }
                }
            }
            if (iNeedHealing > 0f)
            {
                iNeedHealing += 1f;
            }
            return iNeedHealing * Math.Max(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f) * (!p.WorkTagIsDisabled(WorkTags.Violent) ? 2f : 1f) * (p.Downed ? 2f : 1f) * (p.InBed() ? 0.1f : 1f) * p.MarketValue / 1000f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public HediffDef alsoAvoid;
    }
    public class UseCaseTags_WordOfSterility : UseCaseTags
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.RaceProps.IsFlesh || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float totalSevConditions = 0f;
            CompAbilityEffect_Sterilize caes = psycast.CompOfType<CompAbilityEffect_Sterilize>();
            if (caes != null)
            {
                List<Hediff> curables = CompAbilityEffect_Sterilize.SterilizableHediffs(caes.Props, p);
                foreach (Hediff h in curables)
                {
                    if (h.IsCurrentlyLifeThreatening || h.def == HediffDefOf.DrugOverdose)
                    {
                        totalSevConditions += Math.Max(0f, h.Severity - this.minSeverityToBeCured);
                    }
                }
            }
            return totalSevConditions * p.MarketValue / 1000f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minSeverityToBeCured;
    }
}
