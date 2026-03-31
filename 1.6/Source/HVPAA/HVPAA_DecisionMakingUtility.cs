using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

namespace HVPAA
{
    public static class HVPAA_DecisionMakingUtility
    {
        public static bool CanPsycast(Pawn pawn, int situation)
        {
            if (pawn.Spawned && !pawn.IsColonistPlayerControlled && (!pawn.IsPrisoner || !pawn.guest.PrisonerIsSecure) && !pawn.DeadOrDowned && !pawn.Suspended && !pawn.DevelopmentalStage.Baby() && !pawn.InBed() && (!pawn.InMentalState || pawn.MentalStateDef.HasModExtension<PsycastPermissiveMentalState>()) && pawn.HasPsylink && HautsMiscUtility.IsntCastingAbility(pawn) && !pawn.stances.stunner.Stunned)
            {
                if (pawn.CurJob != null && pawn.CurJobDef.HasModExtension<LimitsHVPAACasting>())
                {
                    return false;
                }
                Pawn_PsychicEntropyTracker ppet = pawn.psychicEntropy;
                if (ppet != null && ppet.PsychicSensitivity > 0f)
                {
                    return true;
                }
            }
            return false;
        }
        public static void SetAlliesAndAdversaries(Pawn caster, List<Pawn> allies, List<Pawn> foes, float niceToAnimals, float niceToEvil)
        {
            foreach (Pawn p in (List<Pawn>)caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (HVPAA_DecisionMakingUtility.IsEnemy(caster, p))
                {
                    foes.Add(p);
                }
                else if (HVPAA_DecisionMakingUtility.IsAlly(niceToAnimals <= 0, caster, p, niceToEvil))
                {
                    allies.Add(p);
                }
            }
        }
        public static bool IsEnemy(Pawn caster, Pawn p)
        {
            return caster.HostileTo(p) && p.IsCombatant() && !p.IsPsychologicallyInvisible();
        }
        public static bool IsAlly(bool canUseAnimalRightsViolations, Pawn caster, Thing p, float niceToEvil)
        {
            if (p is Pawn pawn)
            {
                if (pawn.RaceProps.Animal && (pawn.Faction == null || !p.HostileTo(pawn)) && !canUseAnimalRightsViolations)
                {
                    return !pawn.IsPsychologicallyInvisible();
                }
                else
                {
                    if (caster.Faction == null || p.Faction == null)
                    {
                        return false;
                    }
                    if (caster.Faction != p.Faction)
                    {
                        return !pawn.IsPsychologicallyInvisible() && !caster.HostileTo(p) && (caster.Faction.RelationKindWith(p.Faction) == FactionRelationKind.Ally || (niceToEvil > 0f && caster.Faction.RelationKindWith(p.Faction) == FactionRelationKind.Neutral));
                    }
                    else
                    {
                        return !caster.HostileTo(p);
                    }
                }
            }
            if (caster.Faction == null || p.Faction == null)
            {
                return false;
            }
            if (caster.Faction != p.Faction)
            {
                return !caster.HostileTo(p) && (caster.Faction.RelationKindWith(p.Faction) == FactionRelationKind.Ally || (niceToEvil > 0f && caster.Faction.RelationKindWith(p.Faction) == FactionRelationKind.Neutral));
            }
            else
            {
                return !caster.HostileTo(p);
            }
        }
        public static bool MovesFasterInLight(Pawn p)
        {
            MethodInfo NoDarkVision = typeof(StatPart_Glow).GetMethod("ActiveFor", BindingFlags.NonPublic | BindingFlags.Instance);
            StatPart_Glow spg = null;
            foreach (StatPart sp in StatDefOf.MoveSpeed.parts)
            {
                if (sp is StatPart_Glow spg2)
                {
                    spg = spg2;
                    break;
                }
            }
            return (bool)NoDarkVision.Invoke(spg, new object[] { p });
        }
        public static bool DebilitatedByLight(Pawn p, bool melee, bool ranged)
        {
            if (ModsConfig.IdeologyActive)
            {
                if (melee)
                {
                    if ((p.equipment != null && (p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)) && (p.GetStatValue(StatDefOf.MeleeDodgeChanceIndoorsLitOffset) < 0f || p.GetStatValue(StatDefOf.MeleeDodgeChanceOutdoorsLitOffset) < 0f || p.GetStatValue(StatDefOf.MeleeHitChanceIndoorsLitOffset) < 0f || p.GetStatValue(StatDefOf.MeleeHitChanceOutdoorsLitOffset) < 0f))
                    {
                        return true;
                    }
                }
                if (ranged)
                {
                    if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon && (p.GetStatValue(StatDefOf.ShootingAccuracyIndoorsLitOffset) < 0f || p.GetStatValue(StatDefOf.ShootingAccuracyOutdoorsLitOffset) < 0f))
                    {
                        return true;
                    }
                }
            }
            if (ModsConfig.AnomalyActive && p.health.hediffSet.HasHediff(HediffDefOf.LightExposure))
            {
                return true;
            }
            return false;
        }
        public static float ExpectedBeckonTime(Pawn target, Pawn caster)
        {
            return target.Position.DistanceTo(caster.Position) / target.GetStatValue(StatDefOf.MoveSpeed);
        }
        public static float BerserkApplicability(HediffComp_IntPsycasts castComp, Pawn p, Psycast psycast, float niceToEvil, bool zeroIfClosestIsAlly = true, bool ignoreAnimals = false)
        {
            Pawn closestPawn = null;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, p.GetStatValue(StatDefOf.MoveSpeed) * 3f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2 != p && (closestPawn == null || p.Position.DistanceTo(p2.Position) <= p.Position.DistanceTo(closestPawn.Position)))
                {
                    closestPawn = p2;
                }
            }
            if (closestPawn != null && (!ignoreAnimals || !closestPawn.IsAnimal) && castComp.allies.Contains(closestPawn))
            {
                return zeroIfClosestIsAlly ? 0f : -p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.MeleeDPS) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? 2.2f : 1f);
            }
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.MeleeDPS) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? 2.2f : 1f);
        }
        public static float ChaosSkipApplicability(Pawn p, Psycast psycast)
        {
            float meleeThreat = 0f;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2.HostileTo(p))
                {
                    meleeThreat -= p2.GetStatValue(StatDefOf.MeleeDPS);
                }
            }
            if (meleeThreat < 0f)
            {
                meleeThreat += p.GetStatValue(StatDefOf.MeleeDPS);
            }
            return (0.65f + (0.3f * Rand.Value)) * p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)), 1f) * Math.Max(0.5f, meleeThreat);
        }
        public static float LightningApplicability(UseCaseTags uct, HediffComp_IntPsycasts intPsycasts, Psycast psycast, IntVec3 tryNewPosition, float niceToEvil, float aoe, ref float tryNewScore)
        {
            Faction f = intPsycasts.Pawn.Faction;
            if (f != null)
            {
                Map map = intPsycasts.Pawn.Map;
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, map, aoe, true))
                {
                    if (thing is Plant plant)
                    {
                        Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
                        if (zone != null && zone is Zone_Growing && f != Faction.OfPlayerSilentFail && f.HostileTo(Faction.OfPlayerSilentFail))
                        {
                            tryNewScore += plant.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, plant) * plant.MarketValue / 500f;
                        }
                    } else if (thing is Building b && b.Faction != null) {
                        if (f != b.Faction && f.HostileTo(b.Faction))
                        {
                            tryNewScore += HVPAA_DecisionMakingUtility.LightningBuildingScore(b);
                        } else if (niceToEvil > 0 || f == b.Faction || f.RelationKindWith(b.Faction) == FactionRelationKind.Ally) {
                            tryNewScore -= HVPAA_DecisionMakingUtility.LightningBuildingScore(b);
                        }
                    } else if (thing is Pawn p) {
                        if (intPsycasts.allies.Contains(p) && !uct.OtherAllyDisqualifiers(psycast, p, 1))
                        {
                            tryNewScore -= p.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, p) * 1.5f;
                        } else if (intPsycasts.foes.Contains(p) && !uct.OtherEnemyDisqualifiers(psycast, p, 1)) {
                            tryNewScore += p.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, p);
                        }
                    }
                }
            }
            return tryNewScore;
        }
        public static float LightningBuildingScore(Building b)
        {
            float scoreMulti = 1f;
            if (b.def.building != null && b.def.building.IsTurret)
            {
                CompPowerTrader cpt = b.TryGetComp<CompPowerTrader>();
                if (cpt == null || !cpt.PowerOn)
                {
                    scoreMulti = 2f;
                }
            }
            return scoreMulti * b.GetStatValue(StatDefOf.Flammability) * HautsMiscUtility.DamageFactorFor(DamageDefOf.Flame, b) * b.MarketValue / 200f;
        }
        public static bool SkipImmune(Pawn p, float maxBodySize)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.kindDef.skipResistant || p.BodySize > maxBodySize || p.kindDef.isBoss;
        }
        public static bool IsPlantInHostileFactionGrowZone(Plant plant, Faction f)
        {
            Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
            if (zone != null && zone is Zone_Growing && f.HostileTo(Faction.OfPlayerSilentFail))
            {
                return true;
            }
            return false;
        }
        public static Ability StrongestTrapAbility(List<Ability> trapAbilities, Map map, IntVec3 targetPos, bool weightedRandom = true)
        {
            if (weightedRandom)
            {
                Dictionary<Ability, int> dai = new Dictionary<Ability, int>();
                foreach (Ability a in trapAbilities)
                {
                    UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                    if (uct != null && uct.trapPower > 0 && (uct.trapPlacementWorker == null || uct.Worker.IsGoodSpot(targetPos, map)))
                    {
                        dai.Add(a, uct.trapPower);
                    }
                }
                if (!dai.NullOrEmpty())
                {
                    return dai.Keys.RandomElementByWeight((Ability ab) => dai.TryGetValue(ab));
                }
            }
            else
            {
                Ability strongestAbility = null;
                int strongestPower = 0;
                foreach (Ability a in trapAbilities)
                {
                    UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                    if (uct != null && uct.trapPower > strongestPower && (uct.trapPlacementWorker == null || uct.Worker.IsGoodSpot(targetPos, map)))
                    {
                        strongestAbility = a;
                    }
                }
                if (strongestAbility != null)
                {
                    return strongestAbility;
                }
            }
            return trapAbilities.RandomElement();
        }
    }
}
