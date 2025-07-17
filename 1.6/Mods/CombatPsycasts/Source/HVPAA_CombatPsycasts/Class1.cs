using CombatPsycasts.Comps;
using CombatPsycasts.Verbs;
using HarmonyLib;
using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Verse;
using Verse.AI;

namespace HVPAA_CombatPsycasts
{
    [StaticConstructorOnStartup]
    public class HVPAA_CombatPsycasts
    {
        private static readonly Type patchType = typeof(HVPAA_CombatPsycasts);
        static HVPAA_CombatPsycasts()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.HVPAA.combatpsycasts");
            harmony.Patch(AccessTools.Method(typeof(CompAbilityEffect_PsychicSustainedShoot), nameof(CompAbilityEffect_PsychicSustainedShoot.ShouldContinueFiring)),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_ShouldContinueFiringPrefix)));
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static bool HVPAA_ShouldContinueFiringPrefix(ref bool __result, CompAbilityEffect_PsychicSustainedShoot __instance)
        {
            if (__instance.parent.pawn.drafter == null)
            {
                __result = __instance.parent.CanCast && (bool)__instance.GetType().GetField("shootCanReach", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance) && (bool)__instance.GetType().GetMethod("ThingIsStillStanding", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(__instance, new object[] { });
                return false;
            }
            return true;
        }
    }
    public class UseCaseTags_SlugPellet : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.psychicEntropy.WouldOverflowEntropy((psycast.def.EntropyGain*2f)+1f))
            {
                return 0f;
            }
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
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.GetStatValue(StatDefOf.IncomingDamageFactor) < this.incomingDamageResistCutoff || HautsUtility.DamageFactorFor(damageType, p) < this.specificDamageResistCutoff;
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
                    return 1f;
                } else if (t.def.building.isTrap && t.def.building.ai_chillDestination) {
                    return 0.25f;
                } else if (this.canTargetHB) {
                    return t.MarketValue / 2500f;
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
                app = Rand.Value * thingTargets.TryGetValue(turret) * this.baseApplicability;
            }
            if (Rand.Chance(this.chanceCanTargetPawns) || !HVPAA_Mod.settings.powerLimiting)
            {
                Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null && pawnTargets.TryGetValue(pawn) > app)
                {
                    psycast.lti = pawn;
                    app = Rand.Value * this.baseApplicability;
                }
            }
            return app;
        }
        public DamageDef damageType;
        public bool wallBlast = false;
        public bool canTargetHB = false;
        public float baseApplicability;
        public float chanceCanTargetPawns = 1f;
        public float chanceCanTargetHarmlessBuildings;
        public float incomingDamageResistCutoff;
        public float specificDamageResistCutoff;
    }
    public class UseCaseTags_Grenada : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            int num = GenRadial.NumCellsInRadius(this.Range(psycast));
            bool canTargetBuildings = Rand.Chance(this.chanceCanTargetBuildings);
            for (int i = 0; i < num; i++)
            {
                tryNewPosition = psycast.pawn.Position + GenRadial.RadialPattern[i];
                if (tryNewPosition.IsValid && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0))
                {
                    tryNewScore = 0f;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, this.aoe, true))
                    {
                        if (thing is Building b && b.Faction != null)
                        {
                            if (intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                            {
                                tryNewScore += this.ThingApplicability(psycast, b, niceToEvil, 1);
                            }
                            else if (niceToEvil > 0 || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                            {
                                tryNewScore -= this.ThingApplicability(psycast, b, niceToEvil, 1);
                            }
                        }
                        else if (thing is Pawn p)
                        {
                            if (intPsycasts.allies.Contains(p) && !this.OtherAllyDisqualifiers(psycast, p, 1))
                            {
                                tryNewScore -= this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, 1);
                            }
                            else if (intPsycasts.foes.Contains(p) && !this.OtherEnemyDisqualifiers(psycast, p, 1))
                            {
                                tryNewScore += this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, 1);
                            }
                        }
                    }
                    possibleTargets.Add(tryNewPosition, tryNewScore);
                }
            }
            if (possibleTargets != null && possibleTargets.Count > 0)
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
                    if (kvp.Value > highestValue / (Math.Max(1f, highestValue - 1f)))
                    {
                        positionTargets.Add(kvp.Key, kvp.Value);
                    }
                }
                if (positionTargets != null && positionTargets.Count > 0)
                {
                    return positionTargets.RandomElement().Key;
                }
            }
            return IntVec3.Invalid;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            return HautsUtility.DamageFactorFor(this.damageType, t) * t.GetStatValue(StatDefOf.IncomingDamageFactor);
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
        public DamageDef damageType;
        public float chanceCanTargetBuildings;
    }
    public class UseCaseTags_DeathAttack : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            Pawn pawn = psycast.pawn;
            if (pawn.apparel != null)
            {
                List<Apparel> wornApparel = pawn.apparel.WornApparel;
                for (int i = 0; i < wornApparel.Count; i++)
                {
                    if (wornApparel[i].TryGetComp<CompShield>() != null)
                    {
                        return 0f;
                    }
                }
            }
            return (Rand.Chance(chanceToCast) || !HVPAA_Mod.settings.powerLimiting) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.MarketValue < this.minMarketValue;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return Rand.Value * pawn.MarketValue / 2500f;
            }
            return 0f;
        }
        public float chanceToCast;
        public float minMarketValue;
    }
}
