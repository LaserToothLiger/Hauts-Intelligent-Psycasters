using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace HVPAA
{
    /*see comments in Psycasts_Patch_Royalty.xml, as well as comments in UCT_0Basic.xml
     * Farskip receives totally new and different functionality when done by an NPCaster. HVPAAFarskip replaces regular Farskip comp, mostly working identically, except that if it's a non-player caster it just despawns all victims.*/
    public class UseCaseTags_Berserk : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.InAggroMentalState;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAA_DecisionMakingUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn) * Math.Max((intPsycasts.foes.Count - intPsycasts.allies.Count) / 10f, 1f);
            }
            return 0f;
        }
    }
    public class UseCaseTags_Flashstorm : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.Downed && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.Downed && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan;
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
                    if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map) && !tryNewPosition.Roofed(psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -15f;
                    HVPAA_DecisionMakingUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, this.aoe, ref tryNewScore);
                    positionTargets.Add(tryNewPosition, tryNewScore);
                }
            }
            IntVec3 bestPosition = IntVec3.Invalid;
            if (positionTargets.Count > 0)
            {
                float value = -1f;
                foreach (KeyValuePair<IntVec3, float> kvp in positionTargets)
                {
                    if (!bestPosition.IsValid || kvp.Value >= value)
                    {
                        bestPosition = kvp.Key;
                        value = kvp.Value;
                    }
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
        public float ignoreAllPawnsFasterThan;
    }
    public class UseCaseTags_Farskip : UseCaseTags
    {
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (intPsycasts.Pawn.Faction == null)
            {
                return 0f;
            }
            WorldObject anyFactionSite = null;
            foreach (WorldObject wo in Find.WorldObjects.AllWorldObjects)
            {
                if (wo.Faction != null && wo.Faction == intPsycasts.Pawn.Faction)
                {
                    anyFactionSite = wo;
                    continue;
                }
            }
            if (anyFactionSite == null)
            {
                return 0f;
            }
            psycast.gti = anyFactionSite;
            float multi = 1f;
            int situation = intPsycasts.GetSituation();
            foreach (Pawn p2 in intPsycasts.Pawn.Map.mapPawns.PawnsInFaction(intPsycasts.Pawn.Faction))
            {
                if (this.ShouldRally(psycast.ability, p2, situation))
                {
                    multi += p2.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
                }
            }
            if (situation == 3 || situation == 6)
            {
                return multi * (niceToEvil > 0f ? 1f : 5f);
            }
            else if (situation == 5)
            {
                return multi * ((intPsycasts.Pawn.carryTracker != null && intPsycasts.Pawn.carryTracker.CarriedThing != null) ? intPsycasts.Pawn.carryTracker.CarriedThing.MarketValue : 0f);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return this.ApplicabilityScoreDefense(intPsycasts, psycast, niceToEvil);
        }
        public override bool ShouldRally(Psycast psycast, Pawn p, int situation)
        {
            return p != psycast.pawn && p.Faction != null && psycast.pawn.Faction != null && p.Faction == psycast.pawn.Faction && (situation != 3 || (p.InMentalState && p.MentalStateDef == MentalStateDefOf.PanicFlee) || (p.CurJob != null && p.CurJobDef == JobDefOf.Flee)) && (p.Position.DistanceTo(psycast.pawn.Position) - this.rallyRadius) / p.GetStatValue(StatDefOf.MoveSpeed) <= psycast.def.verbProperties.warmupTime;
        }
    }
    public class CompProperties_AbilityHVPAAFarskip : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityHVPAAFarskip()
        {
            this.compClass = typeof(CompAbilityEffect_FarskipHVPAA);
        }
        public IntRange stunTicks;
    }
    public class CompAbilityEffect_FarskipHVPAA : CompAbilityEffect
    {
        private new CompProperties_AbilityHVPAAFarskip Props
        {
            get
            {
                return (CompProperties_AbilityHVPAAFarskip)this.props;
            }
        }
        public override void Apply(GlobalTargetInfo target)
        {
            Caravan caravan = this.parent.pawn.GetCaravan();
            Map targetMap = ((target.WorldObject is MapParent mapParent) ? mapParent.Map : null);
            IntVec3 targetCell = IntVec3.Invalid;
            List<Pawn> list = this.PawnsToSkip().ToList<Pawn>();
            if (this.parent.pawn.Spawned)
            {
                foreach (Pawn pawn in list)
                {
                    this.parent.AddEffecterToMaintain(EffecterDefOf.Skip_Entry.Spawn(pawn, pawn.Map, 1f), pawn.Position, 60, null);
                }
                SoundDefOf.Psycast_Skip_Pulse.PlayOneShot(new TargetInfo(target.Cell, this.parent.pawn.Map, false));
            }
            if (targetMap == null && !this.parent.pawn.IsColonist && !this.parent.pawn.IsSlaveOfColony)
            {
                foreach (Pawn pawn3 in list)
                {
                    this.ExitPawn(pawn3);
                }
                return;
            }
            if (this.ShouldEnterMap(target))
            {
                Pawn pawn2 = this.AlliedPawnOnMap(targetMap);
                if (pawn2 != null)
                {
                    targetCell = pawn2.Position;
                }
                else
                {
                    targetCell = this.parent.pawn.Position;
                }
            }
            if (targetCell.IsValid)
            {
                foreach (Pawn pawn3 in list)
                {
                    this.ExitPawn(pawn3);
                    IntVec3 targetCell2 = targetCell;
                    Map targetMap2 = targetMap;
                    int num = 4;
                    Predicate<IntVec3> predicate = (IntVec3 cell) => cell != targetCell && cell.GetRoom(targetMap) == targetCell.GetRoom(targetMap);
                    CellFinder.TryFindRandomSpawnCellForPawnNear(targetCell2, targetMap2, out IntVec3 intVec, num, predicate);
                    GenSpawn.Spawn(pawn3, intVec, targetMap, WipeMode.Vanish);
                    if (pawn3.drafter != null && pawn3.IsColonistPlayerControlled && !pawn3.Downed)
                    {
                        pawn3.drafter.Drafted = true;
                    }
                    pawn3.stances.stunner.StunFor(this.Props.stunTicks.RandomInRange, this.parent.pawn, false, true, false);
                    pawn3.Notify_Teleported(true, true);
                    CompAbilityEffect_Teleport.SendSkipUsedSignal(pawn3, this.parent.pawn);
                    if (pawn3.IsPrisoner)
                    {
                        pawn3.guest.WaitInsteadOfEscapingForDefaultTicks();
                    }
                    this.parent.AddEffecterToMaintain(EffecterDefOf.Skip_ExitNoDelay.Spawn(pawn3, pawn3.Map, 1f), pawn3.Position, 60, targetMap);
                    SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(intVec, pawn3.Map, false));
                    if ((pawn3.IsColonist || pawn3.RaceProps.packAnimal || pawn3.IsColonyMech) && pawn3.Map.IsPlayerHome)
                    {
                        pawn3.inventory.UnloadEverything = true;
                    }
                }
                if (caravan != null)
                {
                    caravan.Destroy();
                    return;
                }
            } else {
                if (target.WorldObject is Caravan caravan2 && caravan2.Faction == this.parent.pawn.Faction)
                {
                    if (caravan != null)
                    {
                        caravan.pawns.TryTransferAllToContainer(caravan2.pawns, true);
                        caravan2.Notify_Merged(new List<Caravan> { caravan });
                        caravan.Destroy();
                        return;
                    }
                    using (List<Pawn>.Enumerator enumerator = list.GetEnumerator())
                    {
                        while (enumerator.MoveNext())
                        {
                            Pawn pawn4 = enumerator.Current;
                            caravan2.AddPawn(pawn4, true);
                            pawn4.ExitMap(false, Rot4.Invalid);
                            AbilityUtility.DoClamor(pawn4.Position, (float)this.Props.clamorRadius, this.parent.pawn, this.Props.clamorType);
                        }
                        return;
                    }
                }
                if (caravan != null)
                {
                    caravan.Tile = target.Tile;
                    caravan.pather.StopDead();
                    return;
                }
                CaravanMaker.MakeCaravan(list, this.parent.pawn.Faction, target.Tile, false);
                foreach (Pawn pawn5 in list)
                {
                    pawn5.ExitMap(false, Rot4.Invalid);
                }
            }
        }
        public void ExitPawn(Pawn pawn)
        {
            if (pawn.Spawned)
            {
                pawn.teleporting = true;
                pawn.ExitMap(false, Rot4.Invalid);
                AbilityUtility.DoClamor(pawn.Position, (float)this.Props.clamorRadius, this.parent.pawn, this.Props.clamorType);
                pawn.teleporting = false;
            }
        }
        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            yield return new PreCastAction
            {
                action = delegate (LocalTargetInfo t, LocalTargetInfo d)
                {
                    foreach (Pawn pawn in this.PawnsToSkip())
                    {
                        FleckCreationData dataAttachedOverlay = FleckMaker.GetDataAttachedOverlay(pawn, FleckDefOf.PsycastSkipFlashEntry, new Vector3(-0.5f, 0f, -0.5f), 1f, -1f);
                        dataAttachedOverlay.link.detachAfterTicks = 5;
                        pawn.Map.flecks.CreateFleck(dataAttachedOverlay);
                    }
                },
                ticksAwayFromCast = 5
            };
            yield break;
        }
        private IEnumerable<Pawn> PawnsToSkip()
        {
            Caravan caravan = this.parent.pawn.GetCaravan();
            if (caravan != null)
            {
                foreach (Pawn pawn in caravan.pawns)
                {
                    yield return pawn;
                }
            }
            else
            {
                Faction faction = this.parent.pawn.Faction;
                bool homeMap = faction == Faction.OfPlayerSilentFail && this.parent.pawn.Map.IsPlayerHome;
                bool sentLodgerMessage = false;
                using (IEnumerator<Thing> enumerator2 = GenRadial.RadialDistinctThingsAround(this.parent.pawn.Position, this.parent.pawn.Map, this.parent.def.EffectRadius, true).GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        if (enumerator2.Current is Pawn pawn2)
                        {
                            if (pawn2.IsQuestLodger())
                            {
                                if (!sentLodgerMessage)
                                {
                                    Messages.Message("MessageLodgersCantFarskip".Translate(), pawn2, MessageTypeDefOf.NegativeEvent, false);
                                }
                                sentLodgerMessage = true;
                            }
                            else if (!pawn2.Dead && this.parent.pawn.Faction != null)
                            {
                                if ((pawn2.Faction != null && pawn2.Faction == faction && (!pawn2.IsAnimal || !homeMap)) || (pawn2.IsPrisoner && pawn2.guest.HostFaction == faction))
                                {
                                    yield return pawn2;
                                }
                            }
                        }
                    }
                }
            }
            yield break;
        }
        private Pawn AlliedPawnOnMap(Map targetMap)
        {
            return targetMap.mapPawns.AllPawnsSpawned.FirstOrDefault((Pawn p) => !p.NonHumanlikeOrWildMan() && this.parent.pawn.Faction != null && p.HomeFaction == this.parent.pawn.Faction && !this.PawnsToSkip().Contains(p));
        }
        private bool ShouldEnterMap(GlobalTargetInfo target)
        {
            if (target.WorldObject is Caravan caravan && caravan.Faction == this.parent.pawn.Faction)
            {
                return false;
            }
            return target.WorldObject is MapParent mapParent && mapParent.HasMap && (this.AlliedPawnOnMap(mapParent.Map) != null || mapParent.Map == this.parent.pawn.Map);
        }
        private bool ShouldJoinCaravan(GlobalTargetInfo target)
        {
            return target.WorldObject is Caravan caravan && caravan.Faction == this.parent.pawn.Faction;
        }
        public override bool Valid(GlobalTargetInfo target, bool throwMessages = false)
        {
            Caravan caravan = this.parent.pawn.GetCaravan();
            if (caravan != null && caravan.ImmobilizedByMass)
            {
                return false;
            }
            if (target.WorldObject != null && this.parent.pawn.Faction != null && !this.parent.pawn.Faction.IsPlayer && target.WorldObject.Faction != null && target.WorldObject.Faction == this.parent.pawn.Faction)
            {
                if (target.WorldObject is MapParent mapParent)
                {
                    if (!mapParent.HasMap)
                    {
                        return true;
                    }
                    else if (mapParent.Map == this.parent.pawn.Map)
                    {
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
            Caravan caravan2 = target.WorldObject as Caravan;
            return (caravan == null || caravan != caravan2) && (this.ShouldEnterMap(target) || this.ShouldJoinCaravan(target)) && base.Valid(target, throwMessages);
        }
        public override bool CanApplyOn(GlobalTargetInfo target)
        {
            MapParent mapParent;
            return ((mapParent = target.WorldObject as MapParent) == null || mapParent.Map == null || this.AlliedPawnOnMap(mapParent.Map) != null) && base.CanApplyOn(target);
        }
        public override Window ConfirmationDialog(GlobalTargetInfo target, Action confirmAction)
        {
            if (this.parent.pawn.Faction != null && this.parent.pawn.Faction.IsPlayer)
            {
                Pawn pawn = this.PawnsToSkip().FirstOrDefault((Pawn p) => p.IsQuestLodger());
                if (pawn != null)
                {
                    return Dialog_MessageBox.CreateConfirmation("FarskipConfirmTeleportingLodger".Translate(pawn.Named("PAWN")), confirmAction, false, null, WindowLayer.Dialog);
                }
            }
            return null;
        }
        public override string WorldMapExtraLabel(GlobalTargetInfo target)
        {
            Caravan caravan = this.parent.pawn.GetCaravan();
            if (caravan != null && caravan.ImmobilizedByMass)
            {
                return "CaravanImmobilizedByMass".Translate();
            }
            if (!this.Valid(target, false))
            {
                return "AbilityNeedAllyToSkip".Translate();
            }
            if (this.ShouldJoinCaravan(target))
            {
                return "AbilitySkipToJoinCaravan".Translate();
            }
            return "AbilitySkipToRandomAlly".Translate();
        }
    }
    public class UseCaseTags_Invisibility : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.IsPsychologicallyInvisible() || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(p.MarketValue / 1000f, 1f) * Math.Max(p.GetPsylinkLevel() / 2f, 1f) * Math.Max(p.Map.attackTargetsCache.GetPotentialTargetsFor(p).Count, 1f) * (p.WorkTagIsDisabled(WorkTags.Violent) ? (niceToEvil >= 0 ? niceToEvil : 0.5f) : 1f);
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
    }
    public class UseCaseTags_WordOfInspiration : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 10f * this.PawnAllyApplicability(intPsycasts, psycast.ability, pawn, niceToEvil, 4);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Faction != Faction.OfPlayerSilentFail || p.mindState.inspirationHandler == null || p.Inspired || p.Downed)
            {
                return true;
            }
            foreach (Hediff hediff in p.health.hediffSet.hediffs)
            {
                if (hediff.CurStage != null && hediff.CurStage.blocksInspirations)
                {
                    return true;
                }
            }
            return false;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
}
