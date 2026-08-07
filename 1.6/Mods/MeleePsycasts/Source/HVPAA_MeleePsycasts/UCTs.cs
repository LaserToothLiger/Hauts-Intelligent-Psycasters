using HautsFramework;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA_MeleePsycasts
{
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
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !HautsMiscUtility.ReactsToEMP(p);
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
            return HautsMiscUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (1f + Math.Max(0f, this.damageType.armorCategory != null ? p.GetStatValue(this.damageType.armorCategory.armorRatingStat) - this.armorPen : 0f));
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawnTargets.Count > 0)
            {
                return this.FindPulseTarget(intPsycasts, psycast, niceToEvil, pawnTargets,1);
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
            return HautsMiscUtility.DamageFactorFor(this.damageType, p) * p.GetStatValue(StatDefOf.IncomingDamageFactor) / (1f + Math.Max(0f, this.damageType.armorCategory != null ? p.GetStatValue(this.damageType.armorCategory.armorRatingStat) - this.armorPen : 0f));
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
