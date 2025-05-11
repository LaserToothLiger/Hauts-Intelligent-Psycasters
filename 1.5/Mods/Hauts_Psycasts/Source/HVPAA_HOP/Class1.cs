using AnimalBehaviours;
using HarmonyLib;
using HautsFramework;
using HVPAA;
using HautsPsycasts;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using VFECore;
using static RimWorld.RitualStage_InteractWithRole;
using static UnityEngine.GraphicsBuffer;
using System.Net.NetworkInformation;

namespace HVPAA_HOP
{
    [StaticConstructorOnStartup]
    public class HVPAA_HOP
    {
        private static readonly Type patchType = typeof(HVPAA_HOP);
        static HVPAA_HOP()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.HVPAA_hop");
            harmony.Patch(AccessTools.Method(typeof(HediffComp_LinkRevoker), nameof(HediffComp_LinkRevoker.AIShouldRecallOtherQualification)),
                          postfix: new HarmonyMethod(patchType, nameof(AIShouldRecallOtherQualificationPostfix)));
        }
        public static void AIShouldRecallOtherQualificationPostfix(HediffComp_LinkRevoker __instance, Hediff h, ref bool __result)
        {
            if (HVPAAUtility.CanPsyast(__instance.Pawn, 0) && HVPAAUtility.IsEnemy(__instance.Pawn, h.pawn))
            {
                __result = true;
                return;
            }
        }
    }
    //HOP lvl1
    public class UseCaseTags_Agonize : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.health.hediffSet.HasHediff(this.alsoCantHave);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.RaceProps.IsMechanoid)
            {
                return 1f / Math.Max(this.consciousnessMalus, this.consciousnessMalus + 0.30f - p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness));
            }
            else
            {
                float painFactor = 1f;
                foreach (Hediff h in p.health.hediffSet.hediffs)
                {
                    painFactor *= h.PainFactor;
                }
                if (ModsConfig.BiotechActive && p.genes != null)
                {
                    painFactor *= p.genes.PainFactor;
                }
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * ((painFactor * this.painOffset) + (2.5f * p.health.hediffSet.PainTotal / p.GetStatValue(StatDefOf.PainShockThreshold)));
            }
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
        public float consciousnessMalus;
        public float painOffset;
        public HediffDef alsoCantHave;
    }
    public class UseCaseTags_EnergyTransfer : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.Downed)
            {
                return true;
            }
            if (p.needs.AllNeeds.ContainsAny((Need n) => this.affectedNeeds.Keys.Contains(n.def)))
            {
                return false;
            }
            return true;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (useCase != 5 && p.ThreatDisabled(psycast.pawn)))
            {
                return true;
            }
            if (p.needs.AllNeeds.ContainsAny((Need n) => this.affectedNeeds.Keys.Contains(n.def)))
            {
                return false;
            }
            return true;
        }
        public float NetEnergies(Pawn pawn, bool donor)
        {
            float netEnergies = 0f;
            int netEnergyTypes = 0;
            foreach (NeedDef nd in this.affectedNeeds.Keys)
            {
                Need n = pawn.needs.TryGetNeed(nd);
                if (n != null)
                {
                    netEnergyTypes++;
                    netEnergies += donor?n.CurLevelPercentage*this.affectedNeeds.TryGetValue(nd):(float)Math.Pow(n.CurLevelPercentage,this.affectedNeeds.TryGetValue(nd));
                }
            }
            return netEnergies/Math.Max(1,netEnergyTypes);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float netEnergies = this.NetEnergies(p, true);
            netEnergies *= this.baseFractionTransferred;
            float worstAlliedEnergyInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map))
                {
                    float p2sMissingEnergy = (1f - this.NetEnergies(p2, false)) * (p2.RaceProps.Animal ? 0.1f :1f);
                    foreach (StatDef sd in this.relevantFallRateStats)
                    {
                        p2sMissingEnergy *= p2.GetStatValue(sd);
                    }
                    if (p2sMissingEnergy > worstAlliedEnergyInRange)
                    {
                        recipient = p2;
                        worstAlliedEnergyInRange = p2sMissingEnergy;
                    }
                }
            }
            if (worstAlliedEnergyInRange < (useCase == 5 ? this.missingEnergyThresholdForUtility : 0f) || recipient == null)
            {
                return -1f;
            }
            netEnergies *= p.BodySize / recipient.BodySize;
            netEnergies += worstAlliedEnergyInRange* (useCase == 5 ? 1f : 0.1f);
            this.targetPairs.Add(p,recipient);
            return Math.Max(0f, netEnergies);
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
        public override float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.targetPairs = new Dictionary<Pawn, Pawn>();
            return base.ApplicabilityScore(intPsycasts, psycast, niceToEvil);
        }
        public override Pawn FindEnemyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            if (useCase != 5)
            {
                return base.FindEnemyPawnTarget(intPsycasts, psycast, niceToEvil, useCase, out pawnTargets, range, initialTarget, nonCasterOrigin);
            }
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (intPsycasts.foes.Contains(p) || p.Downed || p.Faction == null)
                {
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        float pApplicability = this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
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
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            this.recipientPawn = this.targetPairs.TryGetValue(pawn);
            if (pawn != null && this.recipientPawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.recipientPawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public Dictionary<NeedDef,float> affectedNeeds;
        public List<StatDef> relevantFallRateStats;
        public Dictionary<Pawn, Pawn> targetPairs; 
        public float baseFractionTransferred;
        public float donorToRecipientRange;
        public float missingEnergyThresholdForUtility;
        public Pawn recipientPawn;
    }
    public class UseCaseTags_SinkholeSkip : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            if (this.avoidMakingTooMuchOfThing != null)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)this.Range(psycast), null, out IntVec3 spot);
                    if (spot.InBounds(psycast.pawn.Map) && GenSight.LineOfSight(psycast.pawn.Position, spot, psycast.pawn.Map) && !spot.Filled(psycast.pawn.Map) && spot.GetEdifice(psycast.pawn.Map) == null)
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
                        foreach (IntVec3 c in GenAdj.OccupiedRect(spot, this.avoidMakingTooMuchOfThing.defaultPlacingRot, this.avoidMakingTooMuchOfThing.Size).ExpandedBy(1))
                        {
                            List<Thing> list = psycast.pawn.Map.thingGrid.ThingsListAt(c);
                            for (int i = 0; i < list.Count; i++)
                            {
                                Thing thing2 = list[i];
                                if ((thing2 is Pawn p && intPsycasts.allies.Contains(p)) || (thing2.def.category == ThingCategory.Building && thing2.def.building.isTrap) || ((thing2.def.IsBlueprint || thing2.def.IsFrame) && thing2.def.entityDefToBuild is ThingDef && ((ThingDef)thing2.def.entityDefToBuild).building.isTrap))
                                {
                                    goNext = true;
                                }
                            }
                        }
                        if (goNext)
                        {
                            continue;
                        }
                        else
                        {
                            return spot;
                        }
                    }
                }
            }
            return IntVec3.Invalid;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count > 0 || Rand.Chance(this.spontaneousCastChance))
            {
                IntVec3 spot = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets, this.Range(psycast.ability));
                psycast.lti = spot;
                return 10f * Math.Min(10f, (intPsycasts.foes.Count + 1f));
            }
            return 0f;
        }
        public float spontaneousCastChance;
    }
    public class UseCaseTags_Stifle : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || p.InMentalState)
            {
                return true;
            }
            bool isGauConnected = false;
            if (ModsConfig.IdeologyActive && p.connections != null)
            {
                foreach (Thing t in p.connections.ConnectedThings)
                {
                    if (t.def == ThingDefOf.Plant_TreeGauranlen)
                    {
                        isGauConnected = true;
                        break;
                    }
                }
            }
            if (((p.HasPsylink || (ModsConfig.BiotechActive && p.mechanitor != null) || isGauConnected) && p.GetStatValue(StatDefOf.PsychicSensitivity) <= this.curPower))
            {
                return true;
            }
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(this.hediff);
            if (h != null && h.Severity > this.curPower * this.canReplaceCastsThisRelativelyWeak)
            {
                return true;
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || !p.HasPsylink || p.InMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
            {
                return true;
            }
            Hediff h = p.health.hediffSet.GetFirstHediffOfDef(this.hediff);
            if (h != null && h.Severity > this.curPower * this.canReplaceCastsThisRelativelyWeak)
            {
                return true;
            }
            return false;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float psyThoughts = 0f;
            if (p.needs != null && p.needs.mood != null)
            {
                List<Thought> list = new List<Thought>();
                p.needs.mood.thoughts.GetAllMoodThoughts(list);
                foreach (Thought t in list)
                {
                    if (t.def.effectMultiplyingStat != null && t.def.effectMultiplyingStat == StatDefOf.PsychicSensitivity)
                    {
                        psyThoughts -= t.MoodOffset();
                    }
                }
            }
            return Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), this.curPower) * psyThoughts * (p == psycast.pawn ? this.selfApplicabilityFactor : 1f);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetPsylinkLevel() * Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), this.curPower);
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.curPower = this.psysensPenaltyPerCasters * intPsycasts.Pawn.GetStatValue(StatDefOf.PsychicSensitivity);
            if (intPsycasts.GetSituation() != 1)
            {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    return pawnTargets.TryGetValue(pawn);
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.curPower = this.psysensPenaltyPerCasters * intPsycasts.Pawn.GetStatValue(StatDefOf.PsychicSensitivity);
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float psysensPenaltyPerCasters;
        public float canReplaceCastsThisRelativelyWeak;
        public float curPower;
        public float selfApplicabilityFactor;
        public HediffDef hediff;
    }
    public class UseCaseTags_Surestep : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.pather == null || p.pather.curPath == null || p.pather.nextCell == null || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float pathCost = 1f;
            if (!AnimalCollectionClass.floating_animals.Contains(p))
            {
                pathCost *= p.pather.nextCell.GetTerrain(p.Map) != null ? p.pather.nextCell.GetTerrain(p.Map).pathCost : 1f;
            }
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.MoveSpeed) * pathCost * ((useCase <= 4 && !p.WorkTagIsDisabled(WorkTags.Violent) && (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)) ? 2.5f : 1f) * (p == psycast.pawn && intPsycasts.GetSituation() == 3 ? 2.5f : 1f);
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
            return Rand.Chance(this.chanceToUtilityCast) ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
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
        public float chanceToUtilityCast;
    }
    public class UseCaseTags_WordOfWarning : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return this.flatApplicability;
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.IncomingDamageFactor) * (!p.WorkTagIsDisabled(WorkTags.Violent) ? 2f : 1f) * (!p.Awake() || p.Downed || p.Suspended ? 0.5f : 1f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float flatApplicability;
    }
    //HOP lvl2
    public class UseCaseTags_Carezone : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return (!p.Downed && !p.InBed()) || p.RaceProps.IsMechanoid || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            if (!this.TooMuchThingNearby(psycast, p.Position, this.aoe))
            {
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
            }
            return iNeedHealing > 0f ? iNeedHealing + 1f : 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) + this.aoe);
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
                        foreach (Pawn p2 in intPsycasts.allies)
                        {
                            if (p2.Position.DistanceTo(p.Position) <= this.aoe && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                            {
                                pTargetHits += pawnTargets.TryGetValue(p2);
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
                            foreach (Pawn p2 in intPsycasts.allies)
                            {
                                if (p2.Position.DistanceTo(randAoE1) <= this.aoe && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
                                    pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
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
    public class UseCaseTags_ParalysisLink : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.severityPerDay * p.GetStatValue(StatDefOf.PsychicSensitivity) * (1f + (p.GetStatValue(StatDefOf.ArmorRating_Blunt) + p.GetStatValue(StatDefOf.ArmorRating_Sharp) + (p.GetStatValue(StatDefOf.ArmorRating_Heat) / 2f))) / (HautsUtility.HitPointTotalFor(p) * Math.Max(0.01f, p.GetStatValue(StatDefOf.IncomingDamageFactor)));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            foreach (HediffDef h in this.dontUseIfHave)
            {
                if (intPsycasts.Pawn.health.hediffSet.HasHediff(h))
                {
                    return 0f;
                }
            }
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
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
        public float severityPerDay;
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
    }
    public class UseCaseTags_SkillTransfer : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.Downed || p.skills == null || !p.skills.skills.ContainsAny((SkillRecord sr) => !sr.TotallyDisabled);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.skills == null || !p.skills.skills.ContainsAny((SkillRecord sr) => !sr.TotallyDisabled);
        }
        public List<SkillDef> AllTransferrableSkills(Pawn pawn)
        {
            List<SkillDef> skillDefs = new List<SkillDef>();
            if (pawn.skills != null)
            {
                foreach (SkillRecord sr in pawn.skills.skills)
                {
                    if (!sr.TotallyDisabled)
                    {
                        skillDefs.Add(sr.def);
                    }
                }
            }
            return skillDefs;
        }
        public int BiggestSkillLevelDifference(Pawn donor, Pawn recipient, List<SkillDef> skillDefs)
        {
            int biggestDiff = 0;
            if (donor.skills != null && recipient.skills != null)
            {
                foreach (SkillRecord sr in recipient.skills.skills)
                {
                    if (!sr.TotallyDisabled && skillDefs.Contains(sr.def))
                    {
                        int skillDiff = donor.skills.GetSkill(sr.def).Level - sr.Level;
                        if (skillDiff > biggestDiff)
                        {
                            biggestDiff = skillDiff;
                        }
                    }
                }
            }
            return biggestDiff;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            List<SkillDef> skillDefs = this.AllTransferrableSkills(p);
            float biggestDiffInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map))
                {
                    int p2sBiggestDiff = this.BiggestSkillLevelDifference(p, p2, skillDefs);
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
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
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
                if (intPsycasts.foes.Contains(p) || p.Faction == null || (psycast.pawn.Faction != null && p.Faction.HostileTo(psycast.pawn.Faction)))
                {
                    if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        float pApplicability = this.PawnEnemyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
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
        public float baseFractionTransferred;
        public float donorToRecipientRange;
        public float maxXPtakenPerSkill;
        public Pawn recipientPawn;
        public Dictionary<Pawn, Pawn> targetPairs;
    }
    public class UseCaseTags_TetherSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || HVPAAUtility.SkipImmune(p, this.maxBodySize);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
            {
                foreach (Pawn p2 in intPsycasts.allies)
                {
                    if (p2.Position.DistanceTo(p.Position) <= 2f * p.GetStatValue(StatDefOf.MoveSpeed))
                    {
                        return 0f;
                    }
                }
                return p.GetStatValue(StatDefOf.MeleeDPS) / 2f;
            }
            else if (p.pather != null && p.pather.curPath != null && p.pather.LastPassableCellInPath.IsValid)
            {
                return CoverUtility.TotalSurroundingCoverScore(p.pather.LastPassableCellInPath, p.Map);
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
        public float maxBodySize;
    }
    public class UseCaseTags_WordOfConte : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.HasPsylink || p.psychicEntropy == null || (p != psycast.pawn && p.psychicEntropy.CurrentPsyfocus > 0.25f) || !p.psychicEntropy.IsCurrentlyMeditating || p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) * this.brainEfficiencyFactor <= 0.31f|| !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 1f / p.GetStatValue(StatDefOf.MeditationFocusGain);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float brainEfficiencyFactor;
    }
    //HOP lvl3
    public class UseCaseTags_Lightning : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            IntVec3 tryNewPosition = IntVec3.Invalid;
            float tryNewScore = 0f;
            int num = GenRadial.NumCellsInRadius(this.Range(psycast));
            for (int i = 0; i < num; i++)
            {
                tryNewPosition = psycast.pawn.Position + GenRadial.RadialPattern[i];
                if (tryNewPosition.IsValid && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0) && !psycast.pawn.Map.roofGrid.Roofed(tryNewPosition))
                {
                    tryNewScore = 0f;
                    HVPAAUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, 1.5f, ref tryNewScore);
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
            return IntVec3.Invalid;
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
    public class UseCaseTags_PsyphonLink : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.HasPsylink || Math.Min(0.25f, psycast.pawn.psychicEntropy.CurrentPsyfocus) >= p.psychicEntropy.CurrentPsyfocus;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsUtility.TotalPsycastLevel(p) + (p.GetPsylinkLevel() / 2f);
        }
        public override float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.Pawn.psychicEntropy.IsCurrentlyMeditating)
            {
                return 0f;
            }
            return base.ApplicabilityScore(intPsycasts, psycast, niceToEvil);
        }
        public override float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.psychicEntropy.CurrentPsyfocus < this.canDebuffBelowPsyfocusLvl ? base.PriorityScoreDebuff(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float highestPsyfocusCost = 0f;
            foreach (Ability a in psycast.pawn.abilities.abilities)
            {
                if (a.def.PsyfocusCost > highestPsyfocusCost)
                {
                    highestPsyfocusCost = a.def.PsyfocusCost;
                }
            }
            return psycast.pawn.psychicEntropy.CurrentPsyfocus < highestPsyfocusCost ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            foreach (HediffDef h in this.dontUseIfHave)
            {
                if (intPsycasts.Pawn.health.hediffSet.HasHediff(h))
                {
                    return 0f;
                }
            }
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
            float highestPsyfocusCost = 0f;
            foreach (Ability a in p.abilities.abilities)
            {
                if (a.def.PsyfocusCost > highestPsyfocusCost)
                {
                    highestPsyfocusCost = a.def.PsyfocusCost;
                }
            }
            if (intPsycasts.foes.Contains(p))
            {
                return highestPsyfocusCost * HautsUtility.TotalPsycastLevel(p) / 1.5f;
            }
            if (intPsycasts.Pawn.GetPsylinkLevel() < p.GetPsylinkLevel() && intPsycasts.GetSituation() == 1)
            {
                return 0f;
            }
            return 1f / (highestPsyfocusCost * p.GetPsylinkLevel());
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Dictionary<Pawn, float> pawnTargets = new Dictionary<Pawn, float>();
            foreach (Pawn p in intPsycasts.Pawn.Map.mapPawns.AllHumanlikeSpawned)
            {
                if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast.ability))
                {
                    if (GenSight.LineOfSight(intPsycasts.Pawn.Position, p.Position, p.Map) && psycast.ability.CanApplyPsycastTo(p) && !this.OtherAllyDisqualifiers(psycast.ability, p, 4))
                    {
                        if (this.avoidTargetsWithHediff != null && p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                        {
                            continue;
                        }
                        float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 4);
                        pawnTargets.Add(p, pApplicability);
                    }
                }
            }
            if (pawnTargets.Count > 0)
            {
                Pawn pawn = this.BestPawnFound(pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    return pawnTargets.TryGetValue(pawn);
                }
            }
            return 0f;
        }
        public override float Range(Psycast psycast)
        {
            return base.Range(psycast) / 1.5f;
        }
        public float canDebuffBelowPsyfocusLvl;
        public List<HediffDef> dontUseIfHave = new List<HediffDef>();
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
                return val*val;
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
                app = thingTargets.TryGetValue(item)/50;
            }
            return app;
        }
        public float chanceToUtilityCast;
        public List<ThingCategoryDef> allowedItemCategories;
        public int marketValueLimitItem;
        public int marketValueLimitStack;
    }
    public class UseCaseTags_Shield : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.IncomingDamageFactor);
            int foeShooters = 0;
            foreach (Thing t in this.hostileShooters)
            {
                if (t.Position.DistanceTo(p.Position) <= this.aoe)
                {
                    foeShooters++;
                }
            }
            app *= foeShooters;
            return app/10f;
        }
        public override Pawn FindAllyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            int choices = Math.Min(3, intPsycasts.allies.Count);
            bool fleeing = intPsycasts.GetSituation() == 3;
            bool consideredSelf = false;
            List<Pawn> allies = intPsycasts.allies;
            while (choices > 0)
            {
                choices--;
                Pawn p = !consideredSelf && fleeing ? psycast.pawn : allies.RandomElement();
                if (p != null)
                {
                    if (p == psycast.pawn)
                    {
                        consideredSelf = true;
                    }
                    if (allies.Contains(p))
                    {
                        allies.Remove(p);
                    }
                    if (p.Position.DistanceTo(origin) <= (range == -999 ? this.Range(psycast) : range))
                    {
                        if (GenSight.LineOfSight(origin, p.Position, p.Map) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget))
                        {
                            if (this.avoidTargetsWithHediff != null && p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                            {
                                continue;
                            }
                            float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
                            if (pApplicability > 0f)
                            {
                                pawnTargets.Add(p, pApplicability);
                            }
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
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count > 0 && Rand.Chance(this.spontaneousCastChance))
            {
                this.hostileShooters = new List<Thing>();
                foreach (Pawn p in intPsycasts.foes)
                {
                    if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.scanForShootersDistance && p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                    {
                        this.hostileShooters.Add(p);
                    }
                }
                foreach (Building b in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.scanForShootersDistance, true).OfType<Building>().Distinct<Building>())
                {
                    if (b.Faction != null)
                    {
                        if (b.def.building != null && b.def.building.IsTurret && intPsycasts.Pawn.Faction.HostileTo(b.Faction) && !b.Position.AnyGas(b.Map, GasType.BlindSmoke))
                        {
                            CompPowerTrader cpt = b.TryGetComp<CompPowerTrader>();
                            if (cpt == null || !cpt.PowerOn)
                            {
                                this.hostileShooters.Add(b);
                            }
                        }
                    }
                }
                if (this.hostileShooters.Count > 0)
                {
                    Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null)
                    {
                        psycast.lti = pawn;
                        return pawnTargets.TryGetValue(pawn);
                    }
                }
            }
            return 0f;
        }
        public List<Thing> hostileShooters;
        public float scanForShootersDistance;
        public float spontaneousCastChance;
    }
    public class UseCaseTags_Tremorzone : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.pather.Moving || p.GetStatValue(StatDefOf.StaggerDurationFactor) <= float.Epsilon || AnimalCollectionClass.floating_animals.Contains(p);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.pather.Moving || (this.tickPeriodicity / 60f) * p.GetStatValue(StatDefOf.MoveSpeed) >= 2f * this.aoe || p.GetStatValue(StatDefOf.StaggerDurationFactor) <= float.Epsilon || AnimalCollectionClass.floating_animals.Contains(p);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Min(this.speedMax, p.GetStatValue(StatDefOf.MoveSpeed)) * p.GetStatValue(StatDefOf.StaggerDurationFactor);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Min(this.speedMax, p.GetStatValue(StatDefOf.MoveSpeed)) * p.GetStatValue(StatDefOf.StaggerDurationFactor);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) + 8f);
            if (pawnTargets.Count > 0)
            {
                List<Pawn> topTargets = this.TopTargets(5, pawnTargets);
                IntVec3 finalPos = IntVec3.Invalid;
                float finalScore = 0f;
                if (topTargets.Count > 0)
                {
                    IntVec3 bestTargetPos;
                    foreach (Pawn p in topTargets)
                    {
                        bestTargetPos = IntVec3.Invalid;
                        float pTargetHits = pawnTargets.TryGetValue(p);
                        if (p.pather.curPath != null && p.pather.curPath.Found)
                        {
                            for (int i = p.pather.curPath.NodesLeftCount - 1; i > 0; i--)
                            {
                                IntVec3 pos = p.pather.curPath.Peek(i);
                                float distance = pos.DistanceTo(p.Position);
                                if (distance <= 1.25f * p.GetStatValue(StatDefOf.MoveSpeed))
                                {
                                    break;
                                }
                                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, pos, intPsycasts.Pawn.Map) && distance > p.GetStatValue(StatDefOf.MoveSpeed) / 3f && pos.InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                                {
                                    bestTargetPos = pos;
                                }
                            }
                        }
                        bool goNext = false;
                        if (bestTargetPos.IsValid)
                        {
                            List<IntVec3> radius = GenRadial.RadialCellsAround(bestTargetPos, this.aoe * 0.8f, true).ToList();
                            foreach (Thing t in GenRadial.RadialDistinctThingsAround(bestTargetPos, intPsycasts.Pawn.Map, this.aoe * 1.5f, true))
                            {
                                if (t.def == this.avoidMakingTooMuchOfThing)
                                {
                                    goNext = true;
                                    break;
                                }
                                else if (t is Pawn p2 && p2.pather.curPath != null && p2.pather.curPath.Found)
                                {
                                    if (intPsycasts.foes.Contains(p2))
                                    {
                                        if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2) && this.IntersectsRadius(radius, p2.pather.curPath.NodesReversed))
                                        {
                                            pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                        }
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2) && this.IntersectsRadius(radius, p2.pather.curPath.NodesReversed))
                                    {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                            }
                        }
                        if (goNext)
                        {
                            continue;
                        }
                        if (pTargetHits > finalScore)
                        {
                            finalPos = bestTargetPos;
                            finalScore = pTargetHits;
                        }
                    }
                    if (finalScore > 0f)
                    {
                        psycast.lti = finalPos;
                        return finalScore;
                    }
                }
            }
            return 0f;
        }
        public bool IntersectsRadius(List<IntVec3> radius, List<IntVec3> curPathNodes)
        {
            foreach (IntVec3 iv3 in curPathNodes)
            {
                if (radius.Contains(iv3))
                {
                    return true;
                }
            }
            return false;
        }
        public float speedMax;
        public int tickPeriodicity;
    }
    public class UseCaseTags_VaultSkip : UseCaseTags
    {
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase >= 5 && psycast.pawn.Faction != null && psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
            {
                return 1f;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            float netValue = -this.minNetValueToSteal;
            int itemCount = -this.minCountToSteal;
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.aoe * intPsycasts.Pawn.GetStatValue(HautsDefOf.Hauts_SkipcastRangeFactor), true))
            {
                if (t.def.EverHaulable)
                {
                    netValue += t.MarketValue * t.stackCount;
                    itemCount++;
                }
            }
            if (netValue > 0f || itemCount >= 0)
            {
                psycast.lti = new LocalTargetInfo(intPsycasts.Pawn);
                return netValue;
            }
            return 0f;
        }
        public float minNetValueToSteal;
        public int minCountToSteal;
    }
    //HOP lvl4
    public class UseCaseTags_BoosterLink : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || (useCase == 5 ? (!p.RaceProps.Humanlike || p.skills == null) : p.WorkTagIsDisabled(WorkTags.Violent)) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
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
                    app *= 10 * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
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
    public class UseCaseTags_DPC : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if ((Rand.Chance(this.spontaneousCastChance) && (intPsycasts.Pawn.Faction == null || (intPsycasts.Pawn.Map.ParentFaction != null && intPsycasts.Pawn.Faction == intPsycasts.Pawn.Map.ParentFaction))) || Rand.Chance(this.spontaneousCastChanceAway))
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, (int)this.aoe, null, out IntVec3 spot);
                    if (spot.IsValid && GenSight.LineOfSight(intPsycasts.Pawn.Position, spot, intPsycasts.Pawn.Map) && !spot.Standable(intPsycasts.Pawn.Map))
                    {
                        psycast.lti = spot;
                        return 2f;
                    }
                }
            }
            return 0f;
        }
        public float spontaneousCastChance;
        public float spontaneousCastChanceAway;
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
                    app = pet.Primary.MarketValue * p.GetStatValue(VFEDefOf.VEF_RangeAttackDamageFactor) * p.GetStatValue(StatDefOf.AimingDelayFactor) * p.GetStatValue(StatDefOf.RangedCooldownFactor) / (100f * p.GetStatValue(VFEDefOf.VEF_RangeAttackSpeedFactor));
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
                if (tryNewPosition.IsValid)
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
    //HOP lvl5
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
                if (tryNewPosition.IsValid)
                {
                    tryNewScore = -1f;
                    foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, aoe, true))
                    {
                        if (thing is Building b && !b.AllComps.NullOrEmpty())
                        {
                            bool hasEMPableComps = false;
                            if (b.TryGetComp<RimWorld.CompShield>() != null || b.TryGetComp<VFECore.CompShield>() != null || b.TryGetComp<CompProjectileInterceptor>() != null)
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
                        else if (thing is Pawn p && !p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff) && ((p.AmbientTemperature - (p.GetStatValue(StatDefOf.ComfyTemperatureMax) + this.maxComfyTempMod)) > 0f || ((p.GetStatValue(StatDefOf.ComfyTemperatureMin) + this.minComfyTempMod) - p.AmbientTemperature) > 0f || HautsUtility.ReactsToEMP(p)))
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
                    float chunkPower = HVPUtility.ChunkMeteorDamageMulti(t.def);
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
            if (p.Position != null && useCase == 4)
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
            } else if (useCase == 4) {
                Fire attachedFire = (Fire)p.GetAttachment(ThingDefOf.Fire);
                if (attachedFire != null)
                {
                    this.useToKillFires = true;
                    return p.GetStatValue(StatDefOf.Flammability) * attachedFire.CurrentSize();
                }
                return Math.Max(p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature, p.AmbientTemperature- p.GetStatValue(StatDefOf.ComfyTemperatureMax));
            } else if (useCase == 5) {
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
                    if (!fire.Position.Filled(psycast.pawn.Map))
                    {
                        float adjacentFires = 0f;
                        foreach (Thing thing in GenRadial.RadialDistinctThingsAround(fire.PositionHeld, psycast.pawn.Map, this.aoe, true))
                        {
                            if (thing is Fire)
                            {
                                adjacentFires += 1f;
                            } else if (thing.HasAttachment(ThingDefOf.Fire)) {
                                if (thing is Pawn p)
                                {
                                    if (HVPAAUtility.IsAlly(intPsycasts.niceToAnimals <= 0, psycast.pawn, p, niceToEvil))
                                    {
                                        if (this.OtherAllyDisqualifiers(psycast, p, useCase))
                                        {
                                            continue;
                                        }
                                    } else if (intPsycasts.foes.Contains(p)) {
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
                                    adjacentFires += 2f * thing.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, thing);
                                } else {
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
            } else if (useCase == 5 && intPsycasts.Pawn.Faction != null && (intPsycasts.Pawn.Faction == Faction.OfPlayerSilentFail || intPsycasts.Pawn.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Ally || (niceToEvil > 0 && intPsycasts.Pawn.Faction.RelationKindWith(Faction.OfPlayerSilentFail) == FactionRelationKind.Neutral))) {
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
                if (position.IsValid && caes != null && caes.Valid(new LocalTargetInfo(position),false))
                {
                    psycast.lti = position;
                    return 100f * positionTargets.TryGetValue(position);
                }
            } else {
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
                        } else if (t is Pawn p && intPsycasts.allies.Contains(p)) {
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
            return HautsUtility.DamageFactorFor(this.damageType, p) * (this.damageType.armorCategory != null ? 1f + p.GetStatValue(this.damageType.armorCategory.armorRatingStat) : 1f) / p.GetStatValue(StatDefOf.IncomingDamageFactor);
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
                    return t.MarketValue * HautsUtility.DamageFactorFor(this.damageType, t) / 4f;
                }
                else if (t.def.building.isTrap && t.def.building.ai_chillDestination)
                {
                    return t.MarketValue * HautsUtility.DamageFactorFor(this.damageType, t) / 2f;
                }
                else if (this.canTargetHB)
                {
                    return t.MarketValue * HautsUtility.DamageFactorFor(this.damageType, t) / 250f;
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.RaceProps.IsMechanoid || p.health.hediffSet.HasHediff(this.alsoAvoid) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.RaceProps.IsMechanoid || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float totalSevConditions = 0f;
            CompAbilityEffect_Sterilize caes = psycast.CompOfType<CompAbilityEffect_Sterilize>();
            if (caes != null)
            {
                List<Hediff> curables = HVPUtility.SterilizableHediffs(caes.Props, p);
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
    //HOP lvl6
    public class UseCaseTags_Evict : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return (Rand.Chance(chanceToCast) || !HVPAA_Mod.settings.powerLimiting) ? base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || (p.BodySize < this.bodySizeThreshold && p.MarketValue < 5000f) || this.psyfocusCostPerVictimSize.Evaluate(p.BodySize) > psycast.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Max(p.MarketValue / 2500f,p.BodySize);
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && t.HostileTo(psycast.pawn) && (this.psyfocusCostPerVictimSize.Evaluate(t.def.Size.x*t.def.Size.z) <= psycast.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f))
            {
                if (t.def.building.IsTurret && t.def.building.ai_combatDangerous && t.def.useHitPoints)
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        return 0f;
                    }
                    return t.HitPoints;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            float app = 0f;
            Thing turret = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Thing, float> thingTargets);
            if (turret != null)
            {
                psycast.lti = turret;
                app = thingTargets.TryGetValue(turret);
            }
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null && pawnTargets.TryGetValue(pawn) > app)
            {
                psycast.lti = pawn;
                app = pawnTargets.TryGetValue(pawn);
            }
            return app;
        }
        public float chanceToCast;
        public float bodySizeThreshold;
        public SimpleCurve psyfocusCostPerVictimSize;
    }
    public class UseCaseTags_HiveCall : UseCaseTags
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
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)range, null, out tryNewPosition);
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map) && psycast.verb.ValidateTarget(tryNewPosition, false))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid)
                {
                    tryNewScore = -this.minScoreToCast;
                    if (intPsycasts.Pawn.Faction != null)
                    {
                        if (intPsycasts.Pawn.Faction == Faction.OfInsects)
                        {
                            tryNewScore = Rand.Value * 30f;
                        }
                        else
                        {
                            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, aoe, true))
                            {
                                if (thing is Building b && b.Faction != null)
                                {
                                    if (intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                                    {
                                        tryNewScore += this.foeBuildingScore;
                                    }
                                    else if (niceToEvil > 0 || intPsycasts.Pawn.Faction == b.Faction || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                                    {
                                        tryNewScore -= this.allyBuildingScore;
                                    }
                                }
                                else if (thing is Pawn p)
                                {
                                    if (intPsycasts.allies.Contains(p))
                                    {
                                        tryNewScore -= this.allyPawnScore;
                                    }
                                    else if (intPsycasts.foes.Contains(p))
                                    {
                                        tryNewScore += this.foePawnScore;
                                    }
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
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            CompAbilityEffect_SpawnInfestation caesi = psycast.ability.CompOfType<CompAbilityEffect_SpawnInfestation>();
            if (caesi != null)
            {
                int hivesToSpawn = (int)Math.Ceiling(caesi.HivesToSpawn / 10f);
                IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<IntVec3, float> positionTargets, Math.Min(hivesToSpawn * 20f, this.Range(psycast.ability)));
                if (position.IsValid)
                {
                    psycast.lti = position;
                    return positionTargets.TryGetValue(position);
                }
            }
            return 0f;
        }
        public float minScoreToCast;
        public float allyBuildingScore;
        public float allyPawnScore;
        public float foeBuildingScore;
        public float foePawnScore;
    }
    public class UseCaseTags_RJSkip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float iNeedHealing = 0f;
            if (p.health.hediffSet.BleedRateTotal > 0.01f)
            {
                iNeedHealing = Math.Max(this.ticksToFatalBloodLossCutoff - HealthUtility.TicksUntilDeathDueToBloodLoss(p), 0f);
            }
            float injuryCount = -this.minInjurySeverity;
            float mbCount = -this.minMissingPartSeverity;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h is Hediff_Injury hi)
                {
                    if (hi.IsPermanent() && hi.Part == p.health.hediffSet.GetBrain())
                    {
                        iNeedHealing += this.bonusBrainInjurySeverity;
                    }
                    injuryCount += h.Severity;
                } else if (h is Hediff_MissingPart && h.Part != null) {
                    mbCount += h.Part.def.GetMaxHealth(p);
                }
            }
            return Math.Max(0f,injuryCount) + Math.Max(0f,mbCount) + iNeedHealing;
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
        public int ticksToFatalBloodLossCutoff;
        public float minInjurySeverity;
        public float minMissingPartSeverity;
        public float bonusBrainInjurySeverity;
    }
    public class UseCaseTags_Stuporzone : UseCaseTags
    {
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
            return Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) - this.consciousnessCurve.Evaluate(p.GetStatValue(StatDefOf.PsychicSensitivity)));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * Math.Max(0f, 1.25f * (p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness) - this.consciousnessCurve.Evaluate(p.GetStatValue(StatDefOf.PsychicSensitivity))));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) + 8f);
            if (pawnTargets.Count > 0)
            {
                List<Pawn> topTargets = this.TopTargets(5, pawnTargets);
                IntVec3 finalPos = IntVec3.Invalid;
                float finalScore = 0f;
                if (topTargets.Count > 0)
                {
                    IntVec3 bestTargetPos;
                    foreach (Pawn p in topTargets)
                    {
                        bestTargetPos = IntVec3.Invalid;
                        float pTargetHits = pawnTargets.TryGetValue(p);
                        if (p.pather.curPath != null && p.pather.curPath.Found)
                        {
                            for (int i = p.pather.curPath.NodesLeftCount - 1; i > 0; i--)
                            {
                                IntVec3 pos = p.pather.curPath.Peek(i);
                                float distance = pos.DistanceTo(p.Position);
                                if (distance <= 1.25f * p.GetStatValue(StatDefOf.MoveSpeed))
                                {
                                    break;
                                }
                                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, pos, intPsycasts.Pawn.Map) && distance > p.GetStatValue(StatDefOf.MoveSpeed) / 3f && pos.InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                                {
                                    bestTargetPos = pos;
                                }
                            }
                        }
                        else
                        {
                            bestTargetPos = p.Position;
                        }
                        bool goNext = false;
                        if (bestTargetPos.IsValid)
                        {
                            List<IntVec3> radius = GenRadial.RadialCellsAround(bestTargetPos, this.aoe * 0.8f, true).ToList();
                            foreach (Thing t in GenRadial.RadialDistinctThingsAround(bestTargetPos, intPsycasts.Pawn.Map, this.aoe * 1.5f, true))
                            {
                                if (t.def == this.avoidMakingTooMuchOfThing)
                                {
                                    goNext = true;
                                    break;
                                }
                                else if (t is Pawn p2 && p2.pather.curPath != null && p2.pather.curPath.Found)
                                {
                                    if (intPsycasts.foes.Contains(p2))
                                    {
                                        if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2) && this.IntersectsRadius(radius, p2.pather.curPath.NodesReversed))
                                        {
                                            pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                        }
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2) && this.IntersectsRadius(radius, p2.pather.curPath.NodesReversed))
                                    {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                            }
                        }
                        if (goNext)
                        {
                            continue;
                        }
                        if (pTargetHits > finalScore)
                        {
                            finalPos = bestTargetPos;
                            finalScore = pTargetHits;
                        }
                    }
                    if (finalScore > 0f)
                    {
                        psycast.lti = finalPos;
                        return finalScore;
                    }
                }
            }
            return 0f;
        }
        public bool IntersectsRadius(List<IntVec3> radius, List<IntVec3> curPathNodes)
        {
            foreach (IntVec3 iv3 in curPathNodes)
            {
                if (radius.Contains(iv3))
                {
                    return true;
                }
            }
            return false;
        }
        public SimpleCurve consciousnessCurve;
    }
    /* don't recall if I have a 'friendly pulse' AI UCT stashed somewhere else so I'm just commenting this one out in case I need it later
     public class UseCaseTags_ForesightPulse : UseCaseTags
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
            return p.GetStatValue(StatDefOf.IncomingDamageFactor);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.IncomingDamageFactor);
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
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
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
                                            pTargetHits -= this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                        }
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
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
    }*/
}
