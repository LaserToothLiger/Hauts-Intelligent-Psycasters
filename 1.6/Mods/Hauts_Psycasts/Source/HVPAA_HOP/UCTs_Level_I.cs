using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF.AnimalBehaviours;
using Verse;
using Verse.AI;

namespace HVPAA_HOP
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Agonize : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.health.hediffSet.HasHediff(this.alsoCantHave);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (!p.RaceProps.IsFlesh)
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
                    netEnergies += donor ? n.CurLevelPercentage * this.affectedNeeds.TryGetValue(nd) : (float)Math.Pow(n.CurLevelPercentage, this.affectedNeeds.TryGetValue(nd));
                }
            }
            return netEnergies / Math.Max(1, netEnergyTypes);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float netEnergies = this.NetEnergies(p, true);
            if (netEnergies <= this.donorIsUnusableBelow)
            {
                return 0f;
            }
            netEnergies *= this.baseFractionTransferred;
            float worstAlliedEnergyInRange = 0f;
            Pawn recipient = null;
            foreach (Pawn p2 in intPsycasts.allies)
            {
                if (p2.PositionHeld.DistanceTo(psycast.pawn.PositionHeld) <= this.donorToRecipientRange && !this.OtherAllyDisqualifiers(psycast, p2, useCase, false) && GenSight.LineOfSight(p.PositionHeld, p2.PositionHeld, p.Map))
                {
                    float p2sMissingEnergy = (1f - this.NetEnergies(p2, false)) * (p2.RaceProps.Animal ? 0.1f : 1f);
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
            netEnergies += worstAlliedEnergyInRange * (useCase == 5 ? 1f : 0.1f);
            this.targetPairs.Add(p, recipient);
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
        public Dictionary<NeedDef, float> affectedNeeds;
        public List<StatDef> relevantFallRateStats;
        public Dictionary<Pawn, Pawn> targetPairs;
        public float baseFractionTransferred;
        public float donorIsUnusableBelow;
        public float donorToRecipientRange;
        public float missingEnergyThresholdForUtility;
        public Pawn recipientPawn;
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
            return p.Downed || p.pather == null || p.pather.curPath == null || !p.pather.nextCell.IsValid || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float pathCost = 1f;
            if (!StaticCollectionsClass.floating_animals.Contains(p))
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
            return p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (!p.RaceProps.Humanlike && p.MarketValue <= this.nonHumanMarketValueCutoff) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.GetStatValue(StatDefOf.IncomingDamageFactor) * (!p.WorkTagIsDisabled(WorkTags.Violent) ? 2f : 1f) * (!p.Awake() || p.Downed ? 0.5f : 1f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float flatApplicability;
        public float nonHumanMarketValueCutoff;
    }
}
