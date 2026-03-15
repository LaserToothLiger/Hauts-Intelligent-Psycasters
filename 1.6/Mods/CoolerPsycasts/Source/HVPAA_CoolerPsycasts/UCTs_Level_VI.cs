using HVPAA;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA_CoolerPsycasts
{
    //see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
    public class UseCaseTags_Duplicate : UseCaseTags
    {
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn caster = intPsycasts.Pawn;
            if (caster.WorkTagIsDisabled(WorkTags.Violent))
            {
                return 0f;
            }
            Pawn pawn = intPsycasts.foes.Where((Pawn p) => p.Position.DistanceTo(caster.Position) <= this.Range(psycast.ability) && GenSight.LineOfSight(caster.Position, p.Position, p.Map)).RandomElement();
            if (pawn != null)
            {
                psycast.lti = pawn.Position;
                return this.flatApplicability;
            }
            return 0f;
        }
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return Rand.Chance(this.chanceToUtilityCast) ? base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci) : 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            psycast.lti = intPsycasts.Pawn.Position;
            return this.flatApplicability;
        }
        public float flatApplicability;
        public float chanceToUtilityCast;
    }
    public class UseCaseTags_Enslave : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.guest == null || p.Faction == psycast.pawn.Faction || p.InMentalState || p.RaceProps.intelligence != Intelligence.Humanlike || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (p.MarketValue - this.minMarketValue) / 1000f;
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return ((niceToEvil > this.minEvil && HVPAA_Mod.settings.powerLimiting) || psycast.pawn.Faction == null) ? 0f : base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
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
        public float minMarketValue;
        public float minEvil;
    }
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
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
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
                                }
                                else if (intPsycasts.foes.Contains(pawn))
                                {
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
}
