using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace HVPAA
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Burden : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.MoveSpeed) <= 1.5f || p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) <= this.imposedMovingCap || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.pather.Moving;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.MoveSpeed) * Math.Max(p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.imposedMovingCap, 0f);
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
        public float imposedMovingCap;
    }
    public class UseCaseTags_Painblock : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PainShockThreshold) <= 0.05f || (p.health.InPainShock ? useCase != 4 : p.Downed) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max((p.health.hediffSet.PainTotal * 2.5f) - p.GetStatValue(StatDefOf.PainShockThreshold), 0f) * (p == intPsycasts.Pawn ? 1.5f : 1f);
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
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.GetSituation() != 1)
            {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    return this.healingMulti * pawnTargets.TryGetValue(pawn);
                }
            }
            return 0f;
        }
        public float healingMulti;
    }
    public class UseCaseTags_Stun : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.MarketValue / 1000f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 2f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
    }
    public class UseCaseTags_SolarPinhole : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Map.glowGrid.GroundGlowAt(p.Position, false, false) > 0.3f)
            {
                return true;
            }
            if (HVPAA_DecisionMakingUtility.DebilitatedByLight(p, true, true))
            {
                return false;
            }
            return false;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Spawned)
            {
                if (HVPAA_DecisionMakingUtility.MovesFasterInLight(p) && !p.Downed)
                {
                    if (p.pather.Moving)
                    {
                        if (useCase == 3)
                        {
                            return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f || HVPAA_DecisionMakingUtility.DebilitatedByLight(p, true, false);
                        }
                    }
                    if (useCase == 5)
                    {
                        return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f;
                    }
                }
                if (useCase == 4 && !p.Position.UsesOutdoorTemperature(p.Map))
                {
                    p.health.hediffSet.TryGetHediff(HediffDefOf.Hypothermia, out Hediff hypo);
                    if (hypo != null && hypo.Severity >= 0.04f)
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
                return p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature;
            }
            else if (useCase == 5)
            {
                return 1f;
            }
            return 1f;
        }
        public override bool TooMuchThingAdditionalCheck(Thing thing, Psycast psycast)
        {
            return WanderUtility.InSameRoom(psycast.pawn.Position, thing.Position, thing.Map);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                Dictionary<Pawn, float> pawnTargetsNonNegative = new Dictionary<Pawn, float>();
                foreach (KeyValuePair<Pawn, float> kvp in pawnTargets)
                {
                    pawnTargetsNonNegative.Add(kvp.Key, Math.Max(kvp.Value, 0f));
                }
                List<Pawn> topTargets = this.TopTargets(5, pawnTargetsNonNegative);
                if (topTargets.Count > 0)
                {
                    Pawn bestTarget = topTargets.First();
                    int bestTargetHits = 0;
                    foreach (Pawn p in topTargets)
                    {
                        int pTargetHits = 0;
                        foreach (Pawn p2 in (List<Pawn>)p.Map.mapPawns.AllPawnsSpawned)
                        {
                            if (p2.Position.DistanceTo(p.Position) <= this.aoe && GenSight.LineOfSight(p.Position, p2.Position, p.Map))
                            {
                                if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 3))
                                {
                                    pTargetHits++;
                                }
                                else if (intPsycasts.foes.Contains(p2))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 3))
                                    {
                                        pTargetHits++;
                                    }
                                    else if (p2.pather.Moving && HVPAA_DecisionMakingUtility.MovesFasterInLight(p2))
                                    {
                                        pTargetHits--;
                                    }
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
                        psycast.lti = bestTarget.Position;
                        return ((Rand.Value * 0.4f) + 0.8f) * pawnTargets.Count * this.scoreFactor;
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Room room = intPsycasts.Pawn.Position.GetRoom(intPsycasts.Pawn.Map);
            if (room != null)
            {
                int solarPinholes = 0;
                List<IntVec3> solarCells = new List<IntVec3>();
                float coldTemps = 0f;
                List<Pawn> laborers = new List<Pawn>();
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
                        if (!this.OtherAllyDisqualifiers(psycast.ability, p, 5) && p.jobs.curDriver != null && p.jobs.curDriver.ActiveSkill != null && this.light)
                        {
                            laborers.Add(p);
                        }
                    }
                }
                CompAbilityEffect_Spawn caes = psycast.ability.CompOfType<CompAbilityEffect_Spawn>();
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
                            return coldTemps * this.scoreFactor;
                        }
                    }
                    else if (laborers.Count > 0)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            IntVec3 cell = laborers.RandomElement().Position;
                            if (caes.Valid(new LocalTargetInfo(cell), false) && !solarCells.Contains(cell) && cell.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast.ability) && GenSight.LineOfSight(intPsycasts.Pawn.Position, cell, intPsycasts.Pawn.Map))
                            {
                                psycast.lti = cell;
                                return laborers.Count * this.scoreFactor;
                            }
                        }
                    }
                }
            }
            return 0f;
        }
        public float scoreFactor = 1f;
    }
    public class UseCaseTags_WordOfTrust : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (psycast.Caster.Faction != null && (psycast.Caster.Faction == Faction.OfPlayerSilentFail || psycast.Caster.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Ally || (niceToEvil > 0f && psycast.Caster.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Neutral)))
            {
                Pawn mostResistantPrisoner = null;
                foreach (Pawn p in (List<Pawn>)psycast.Caster.Map.mapPawns.AllPawnsSpawned)
                {
                    if (p.guest != null && p.guest.resistance > float.Epsilon && psycast.ability.CanApplyOn((LocalTargetInfo)p) && psycast.Caster.Position.DistanceTo(p.Position) <= this.Range(psycast.ability) && p.Map.reachability.CanReach(psycast.Caster.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)))
                    {
                        if (mostResistantPrisoner == null || mostResistantPrisoner.guest.resistance < p.guest.resistance)
                        {
                            mostResistantPrisoner = p;
                        }
                    }
                }
                if (mostResistantPrisoner != null)
                {
                    psycast.lti = mostResistantPrisoner;
                    return mostResistantPrisoner.guest.resistance;
                }
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
}
