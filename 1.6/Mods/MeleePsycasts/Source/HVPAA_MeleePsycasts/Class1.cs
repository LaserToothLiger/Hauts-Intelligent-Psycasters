using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using static UnityEngine.GraphicsBuffer;

namespace HVPAA_MeleePsycasts
{
    [StaticConstructorOnStartup]
    public class HVPAA_MeleePsycasts
    {

    }
    //level 3
    public class UseCaseTags_EMPBlow : UseCaseTags
    {
        public override float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (this.mustBeMelee && (psycast.pawn.equipment == null || psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsMeleeWeapon))
            {
                return 0f;
            }
            return base.PriorityScoreDebuff(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !HautsUtility.ReactsToEMP(p);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn)/300f;
            }
            return 0f;
        }
        public bool mustBeMelee = true;
    }
    //level 4
    public class UseCaseTags_SpinCut : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.equipment == null || psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsMeleeWeapon)
            {
                return 0f;
            }
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
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
            return HautsUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (1f + Math.Max(0f, this.damageType.armorCategory != null ? p.GetStatValue(this.damageType.armorCategory.armorRatingStat) - this.armorPen : 0f));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
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
                                } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
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
                                    } else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2)) {
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
        public DamageDef damageType;
        public float armorPen;
    }
    //level 6
    public class UseCaseTags_KO : UseCaseTags
    {
        public override float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (this.mustBeMelee && (psycast.pawn.equipment == null || psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsMeleeWeapon))
            {
                return 0f;
            }
            if (HVPAA_Mod.settings.powerLimiting && !Rand.Chance(this.chance))
            {
                return 0f;
            }
            return base.PriorityScoreDebuff(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Max(p.health.capacities.GetLevel(PawnCapacityDefOf.Consciousness), p.health.capacities.GetLevel(PawnCapacityDefOf.Moving));
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 5f;
            }
            return 0f;
        }
        public bool mustBeMelee = true;
        public float chance = 1f;
    }
    //multiple levels
    public class UseCaseTags_Slice : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (this.mustBeMelee &&(psycast.pawn.equipment == null || psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsMeleeWeapon))
            {
                return 0f;
            }
            if (HVPAA_Mod.settings.powerLimiting && !Rand.Chance(this.chance))
            {
                return 0f;
            }
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (HVPAA_Mod.settings.powerLimiting && p.MarketValue < this.minMarketValue)
            {
                return true;
            }
            bool anyValidSpot = !this.moveAdjacentToTarget;
            if (this.moveAdjacentToTarget)
            {
                List<IntVec3> iv3s = new List<IntVec3>
                {
                    p.Position + IntVec3.North,
                    p.Position + IntVec3.South,
                    p.Position + IntVec3.East,
                    p.Position + IntVec3.West
                };
                foreach (IntVec3 iv3 in iv3s)
                {
                    if (iv3.IsValid && iv3.InBounds(p.Map) && !iv3.Impassable(p.Map))
                    {
                        anyValidSpot = true;
                        break;
                    }
                }
            }
            if (this.targetedBodyPart != null)
            {
                BodyPartRecord bodyPartRecord = ((p != null) ? p.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined, null, null).FirstOrDefault((BodyPartRecord x) => x.def == this.targetedBodyPart) : null);
                if (bodyPartRecord == null)
                {
                    return true;
                }
            }
            return !anyValidSpot || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HautsUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (1f + Math.Max(0f, this.damageType.armorCategory != null ? p.GetStatValue(this.damageType.armorCategory.armorRatingStat) - this.armorPen : 0f));
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
        public float armorPen;
        public bool moveAdjacentToTarget;
        public BodyPartDef targetedBodyPart;
        public float chance = 1f;
        public float minMarketValue;
        public bool mustBeMelee = true;
    }
}
