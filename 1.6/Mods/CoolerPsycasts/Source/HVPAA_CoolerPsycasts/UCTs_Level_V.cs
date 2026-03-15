using CoolPsycasts;
using HVPAA;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using VEF;
using Verse;

namespace HVPAA_CoolerPsycasts
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
     * SpawnPsycastTrap_AIFriendly is for Embed. Makes NPCasters determine what psycast they want to put in the trap and automatically put it in there.
     * AI for Extend coming... whenever I come back to it. Most of it's done, except for the part where its range modifier integrates into the HIPAA-AI*/
    public class UseCaseTags_Echo : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 bestPosition = IntVec3.Invalid;
            List<Thing> allyShooters = new List<Thing>();
            List<Thing> foeShooters = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.scanRadius, true))
            {
                if (t.def.building != null && t.def.building.IsTurret && !t.Position.AnyGas(t.Map, GasType.BlindSmoke))
                {
                    CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                    if (cpt != null && !cpt.PowerOn)
                    {
                        continue;
                    }
                    if (t.HostileTo(psycast.pawn))
                    {
                        foeShooters.Add(t);
                    }
                    else if (HVPAA_DecisionMakingUtility.IsAlly(intPsycasts.niceToAnimals <= 0, psycast.pawn, t, niceToEvil))
                    {
                        allyShooters.Add(t);
                    }
                }
                else if (t is Pawn p)
                {
                    if (intPsycasts.allies.Contains(p))
                    {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            allyShooters.Add(t);
                        }
                    }
                    else if (intPsycasts.foes.Contains(p))
                    {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            foeShooters.Add(p);
                        }
                    }
                }
            }
            float innerScanRadius = Math.Min(this.Range(psycast), this.shooterScanRadius);
            List<Thing> allyShootersIteratable = allyShooters;
            for (int k = allyShootersIteratable.Count - 1; k >= 0; k--)
            {
                Thing t = allyShootersIteratable[k];
                if (t.Position.DistanceTo(psycast.pawn.Position) > innerScanRadius)
                {
                    allyShootersIteratable.Remove(t);
                }
            }
            if (allyShooters.Count > 0 && foeShooters.Count > 0 && foeShooters.Count - allyShooters.Count < 0)
            {
                for (int i = 10; i > 0; i--)
                {
                    if (allyShootersIteratable.NullOrEmpty())
                    {
                        break;
                    }
                    Thing ally = allyShootersIteratable.RandomElement();
                    allyShootersIteratable.Remove(ally);
                    if (ally is Pawn p && p.pather.MovingNow)
                    {
                        i++;
                        continue;
                    }
                    if (!positionTargets.ContainsKey(ally.Position))
                    {
                        bool nearbyEcho = false;
                        foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(ally.Position, psycast.pawn.Map, this.aoe, true))
                        {
                            if (t2.def == this.avoidMakingTooMuchOfThing)
                            {
                                nearbyEcho = true;
                                break;
                            }
                        }
                        if (!nearbyEcho)
                        {
                            bool nearbySkipshield = false;
                            foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(ally.Position, psycast.pawn.Map, this.otherThingAoE, true))
                            {
                                if (t2.def == this.otherThingToAvoidBeingNear)
                                {
                                    nearbySkipshield = true;
                                    break;
                                }
                            }
                            if (!nearbySkipshield)
                            {
                                float totalNearbyShooters = 1f;
                                foreach (Thing alli in allyShooters)
                                {
                                    if (alli.Position.DistanceTo(ally.Position) <= this.aoe)
                                    {
                                        totalNearbyShooters += 1f;
                                    }
                                }
                                positionTargets.Add(ally.Position, totalNearbyShooters);
                            }
                        }
                    }

                }
                if (positionTargets.Count > 0)
                {
                    bestPosition = positionTargets.Keys.RandomElement();
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
                return positionTargets.TryGetValue(position) * (intPsycasts.Pawn.equipment != null && intPsycasts.Pawn.equipment.Primary != null && intPsycasts.Pawn.equipment.Primary.def.IsRangedWeapon ? 1.5f : 1f);
            }
            return 0f;
        }
        public ThingDef otherThingToAvoidBeingNear;
        public float scanRadius;
        public float shooterScanRadius;
        public float otherThingAoE;
    }
    public class UseCaseTags_Embed : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            if (this.avoidMakingTooMuchOfThing != null)
            {
                for (int j = 0; j <= 100; j++)
                {
                    CellFinder.TryFindRandomCellNear(psycast.pawn.Position, psycast.pawn.Map, (int)this.Range(psycast), null, out IntVec3 spot);
                    if (spot.InBounds(psycast.pawn.Map) && GenSight.LineOfSight(psycast.pawn.Position, spot, psycast.pawn.Map) && !spot.Filled(psycast.pawn.Map) && spot.GetEdifice(psycast.pawn.Map) == null && (this.avoidMakingTooMuchOfThing.terrainAffordanceNeeded == null || spot.GetTerrain(psycast.pawn.Map).affordances.Contains(this.avoidMakingTooMuchOfThing.terrainAffordanceNeeded)))
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
                        if (psycast.pawn.Faction != null)
                        {
                            Faction ownFaction = psycast.pawn.Faction;
                            foreach (Building b in GenRadial.RadialDistinctThingsAround(spot, psycast.pawn.Map, this.aoe, true).OfType<Building>().Distinct<Building>())
                            {
                                Faction bFaction = b.Faction;
                                if (bFaction != null && (bFaction == ownFaction || bFaction.RelationKindWith(ownFaction) != FactionRelationKind.Hostile))
                                {
                                    goNext = true;
                                    break;
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
        public override float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            foreach (Ability a in psycast.pawn.abilities.abilities)
            {
                UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                if (uct != null && uct.trapPower > 0 && a.def.verbProperties.range > 0f && a.def.targetRequired && (a.def.PsyfocusCost + psycast.def.PsyfocusCost) < psycast.pawn.psychicEntropy.CurrentPsyfocus && !psycast.pawn.psychicEntropy.WouldOverflowEntropy(a.def.EntropyGain + psycast.def.PsyfocusCost))
                {
                    if (ModsConfig.OdysseyActive && psycast.pawn.MapHeld.Biome.inVacuum)
                    {
                        if (a.VerbProperties.Any((VerbProperties vp) => !vp.useableInVacuum))
                        {
                            continue;
                        }
                    }
                    return base.PriorityScoreUtility(psycast, situationCase, pacifist, niceToEvil, usableFoci);
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.foes.Count > 0 || Rand.Chance(this.spontaneousCastChance))
            {
                IntVec3 spot = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets, this.Range(psycast.ability));
                psycast.lti = spot;
                return 2f * Math.Min(10f, (intPsycasts.foes.Count + 1f));
            }
            return 0f;
        }
        public float spontaneousCastChance;
    }
    public class CompProperties_AbilitySpawnPsycastTrap_AIFriendly : CompProperties_AbilityEffect
    {
        public CompProperties_AbilitySpawnPsycastTrap_AIFriendly()
        {
            this.compClass = typeof(CompAbilityEffect_SpawnPsycastTrap_AIFriendly);
        }
        public ThingDef thingDef;
    }
    public class CompAbilityEffect_SpawnPsycastTrap_AIFriendly : CompAbilityEffect
    {
        public new CompProperties_AbilitySpawnPsycastTrap_AIFriendly Props
        {
            get
            {
                return (CompProperties_AbilitySpawnPsycastTrap_AIFriendly)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = this.parent.pawn;
            IEnumerable<Ability> allAbilitiesForReading = caster.abilities.AllAbilitiesForReading;
            if (allAbilitiesForReading.Count() == 0)
            {
                return;
            }
            Func<Ability, bool> func = delegate (Ability a)
            {
                if (a.def.IsPsycast && a.def.level <= caster.GetPsylinkLevel() && a.CanApplyOn(target) && a.def.verbProperties.range > 0f && a.def.targetRequired && a.FinalPsyfocusCost(target) < caster.psychicEntropy.CurrentPsyfocus && a.Id != this.parent.Id)
                {
                    if (!a.comps.Any((AbilityComp c) => c is CompAbilityEffect_WithDest))
                    {
                        return !caster.psychicEntropy.WouldOverflowEntropy(a.def.EntropyGain);
                    }
                }
                return false;
            };
            List<Ability> trapSettableAbilities = allAbilitiesForReading.Where(func).ToList<Ability>();
            if (this.parent.pawn.IsColonistPlayerControlled)
            {
                List<FloatMenuOption> options = new List<FloatMenuOption>();
                foreach (Ability ability in trapSettableAbilities)
                {
                    Ability toCast = ability;
                    FloatMenuOption option = new FloatMenuOption(ability.def.label, delegate
                    {
                        Thing thing = GenSpawn.Spawn(this.Props.thingDef, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                        thing.SetFaction(caster.Faction, null);
                        if (thing.TryGetComp<CompPsycastTrap>() != null)
                        {
                            thing.TryGetComp<CompPsycastTrap>().storedPsycast = toCast;
                        }
                    }, MenuOptionPriority.Default, null, null, 30f, null, null, true, 0);
                    options.Add(option);
                }
                FloatMenu menu = new FloatMenu(options);
                menu.vanishIfMouseDistant = false;
                Find.WindowStack.Add(menu);
            } else {
                Thing thing = GenSpawn.Spawn(this.Props.thingDef, target.Cell, this.parent.pawn.Map, WipeMode.Vanish);
                thing.SetFaction(caster.Faction, null);
                if (thing.TryGetComp<CompPsycastTrap>() != null)
                {
                    thing.TryGetComp<CompPsycastTrap>().storedPsycast = HVPAA_DecisionMakingUtility.StrongestTrapAbility(trapSettableAbilities, this.parent.pawn.Map, target.Cell);
                }
            }
        }
    }
    public class UseCaseTags_Execute : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (initialTarget)
            {
                return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
            }
            else
            {
                return p.Downed || p.WorkTagIsDisabled(WorkTags.Violent);
            }
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (initialTarget)
            {
                return p.MarketValue / 1000f;
            }
            if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
            return p.GetStatValue(StatDefOf.MeleeDPS);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.PsychicSensitivity) * p.CurrentEffectiveVerb.EffectiveRange * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
            return p.GetStatValue(StatDefOf.MeleeDPS);
        }
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            this.canHitColonist = Rand.Value <= this.chanceToCastColonist || !HVPAA_Mod.settings.powerLimiting;
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
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
                        foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                        {
                            if (!p2.HostileTo(p) && (p2.CurJob == null || p2.CurJob.targetA.Pawn == null || p2.CurJob.targetA.Pawn != p))
                            {
                                if (intPsycasts.foes.Contains(p2))
                                {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 2))
                                    {
                                        pTargetHits += this.PawnEnemyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2, false);
                                    }
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
                                    pTargetHits += this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2, false);
                                }
                            }
                        }
                        if (pTargetHits > bestTargetHits)
                        {
                            bestTarget = p;
                            bestTargetHits = pTargetHits;
                        }
                    }
                    psycast.lti = bestTarget;
                    return bestTargetHits;
                }
            }
            return 0f;
        }
        private bool canHitColonist;
        public float chanceToCastColonist;
    }
    /*public class UseCaseTags_Extend : UseCaseTags
    {
        public override float MetaApplicability(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, List<PotentialPsycast> psycasts, int situationCase, float niceToEvil)
        {
            Pawn pawn = psycast.ability.pawn;
            float entropyGain = psycast.ability.def.EntropyGain;
            float psyfocusCost = psycast.ability.def.PsyfocusCost;
            if (pawn.psychicEntropy != null)
            {
                float myRange = psycast.ability.verb.EffectiveRange;
                float bestScore = 0f;
                foreach (PotentialPsycast potPsy in psycasts)
                {
                    Psycast ability = potPsy.ability;
                    float aRange = ability.verb.EffectiveRange;
                    if (!ability.def.targetRequired || aRange <= 0f || aRange > myRange || ability.comps.Any((AbilityComp c) => c is CompAbilityEffect_WithDest))
                    {
                        continue;
                    }
                    if (pawn.psychicEntropy.WouldOverflowEntropy(ability.def.EntropyGain + entropyGain) || ability.def.PsyfocusCost + psyfocusCost > pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                    {
                        continue;
                    }
                    UseCaseTags uct = ability.def.GetModExtension<UseCaseTags>();
                    if (uct != null)
                    {
                        float score = potPsy.score * uct.ApplicabilityScore(intPsycasts, potPsy, niceToEvil);
                        float dist = potPsy.lti.Cell.DistanceTo(pawn.Position);
                        if (potPsy.lti.IsValid && dist <= myRange && dist > aRange && score > 0)
                        {
                            if (psycast.lti == null || score > bestScore)
                            {
                                psycast.lti = potPsy.lti;
                                bestScore = score;
                            }
                        }
                    }
                }
                return bestScore;
            }
            return 0f;
        }
        public float minCombatPsyfocusCost;
        public float minUtilityPsyfocusCost;
    }*/
    public class UseCaseTags_Jskip : UseCaseTags
    {
        public override bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            if (p.HostileTo(caster))
            {
                return true;
            }
            return false;
        }
        public override float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            if (t.def.building != null && t.def.building.IsTurret && t.def.building.ai_combatDangerous && !t.Position.AnyGas(t.Map, GasType.BlindSmoke) && t.HostileTo(psycast.pawn))
            {
                CompPowerTrader cpt = t.TryGetComp<CompPowerTrader>();
                if (cpt != null && !cpt.PowerOn)
                {
                    return 0f;
                }
                return 1f;
            }
            return 0f;
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            return psycast.pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff) ? 1f : base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = intPsycasts.Pawn;
            psycast.lti = pawn;
            int numFoes = 0;
            foreach (Pawn p in intPsycasts.foes)
            {
                if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe)
                {
                    numFoes++;
                }
            }
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.aoe, true))
            {
                if (GenSight.LineOfSight(intPsycasts.Pawn.Position, t.Position, t.Map))
                {
                    if (this.IsValidThing(intPsycasts.Pawn, t, niceToEvil, 2))
                    {
                        float tApplicability = this.ThingApplicability(psycast.ability, t, 2);
                        if (tApplicability > 0f)
                        {
                            numFoes++;
                        }
                    }
                }
            }
            if (numFoes > 0)
            {
                if (!intPsycasts.Pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                {
                    return 999999f;
                }
            }
            else if (intPsycasts.Pawn.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
            {
                return 999999f;
            }
            return 0f;
        }
    }
    public class UseCaseTags_OSkip : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float chance = Rand.Value;
            this.canHitHumanlike = chance <= this.chanceToCast || !HVPAA_Mod.settings.powerLimiting;
            this.canHitColonist = chance <= this.chanceToCastColonist || !HVPAA_Mod.settings.powerLimiting;
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || !p.RaceProps.Humanlike || !this.canHitHumanlike || (p.IsColonist && !this.canHitColonist);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float multi = 1f;
            foreach (Hediff h in p.health.hediffSet.hediffs)
            {
                if (h.def.spawnThingOnRemoved != null && h is Hediff_AddedPart && (h.Part == null || !organsList.Contains(h.Part.def.defName)))
                {
                    multi += this.bionicCountsFor;
                }
            }
            foreach (BodyPartRecord bpr in p.health.hediffSet.GetNotMissingParts(BodyPartHeight.Undefined, BodyPartDepth.Undefined))
            {
                if (organsList.Contains(bpr.def.defName))
                {
                    multi += this.intactOrganCountsFor;
                }
            }
            return multi;
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn) * 0.2f;
            }
            return 0f;
        }
        public float chanceToCast;
        public float chanceToCastColonist;
        public float intactOrganCountsFor;
        public float bionicCountsFor;
        private bool canHitHumanlike;
        private bool canHitColonist;
        List<string> organsList = new List<string> { "Heart", "Lung", "Lung", "Kidney", "Kidney", "Liver" };
    }
}
