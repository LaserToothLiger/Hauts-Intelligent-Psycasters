using VEF.AnimalBehaviours;
using HarmonyLib;
using HautsFramework;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Noise;
using Verse.Sound;
using VEF;
using static HarmonyLib.Code;
using static RimWorld.RitualStage_InteractWithRole;
using static System.Collections.Specialized.BitVector32;
using static UnityEngine.GraphicsBuffer;
using System.Runtime.CompilerServices;

namespace HVPAA
{
    [StaticConstructorOnStartup]
    public class HVPAA
    {
        private static readonly Type patchType = typeof(HVPAA);
        static HVPAA()
        {
            Harmony harmony = new Harmony(id: "rimworld.hautarche.hvpaa.main");
            harmony.Patch(AccessTools.Method(typeof(FloatMenuOptionProvider_Trade), nameof(FloatMenuOptionProvider_Trade.GetOptionsFor), new[] { typeof(Pawn), typeof(FloatMenuContext)}),
                          postfix: new HarmonyMethod(patchType, nameof(HVPAA_GetOptionsForPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Settlement), nameof(Settlement.GetCaravanGizmos)),
                          postfix: new HarmonyMethod(patchType, nameof(HVPAA_GetCaravanGizmosPostfix)));
            harmony.Patch(AccessTools.Method(typeof(FactionDialogMaker), nameof(FactionDialogMaker.FactionDialogFor)),
                          postfix: new HarmonyMethod(patchType, nameof(HVPAA_FactionDialogForPostfix)));
            harmony.Patch(AccessTools.Property(typeof(Pawn), nameof(Pawn.IsColonistPlayerControlled)).GetGetMethod(),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_IsColonistPlayerControlledPrefix)));
            harmony.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }),
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_GeneratePawnPostfix)));
            harmony.Patch(AccessTools.Method(typeof(SymbolResolver_PawnGroup), nameof(SymbolResolver_PawnGroup.Resolve)),
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_PawnGroup_ResolvePostfix)));
            harmony.Patch(AccessTools.Method(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns)),
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_PGMU_GeneratePawnsPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.Notify_DamageTaken)),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_Notify_DamageTakenPrefix)));
            Log.Message("HVPAA_Initialize".Translate().CapitalizeFirst());
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        public static IEnumerable<FloatMenuOption> HVPAA_GetOptionsForPostfix(IEnumerable<FloatMenuOption> __result, Pawn clickedPawn, FloatMenuContext context)
        {
            foreach (FloatMenuOption fmo in __result)
            {
                yield return fmo;
            }
            if (clickedPawn != null && clickedPawn.Faction != null && ((ITrader)clickedPawn).CanTradeNow && context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn) && !context.FirstSelectedPawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled && !clickedPawn.mindState.traderDismissed && context.FirstSelectedPawn.CanTradeWith(clickedPawn.Faction, clickedPawn.TraderKind).Accepted)
            {
                FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(clickedPawn.Faction.def);
                if (fprd != null && fprd.offersSellcasts && Find.AnyPlayerHomeMap != null)
                {
                    Action action = delegate
                    {
                        SellcastHiringSession.SetupWith(clickedPawn, context.FirstSelectedPawn);
                        Job job32 = JobMaker.MakeJob(HVPAADefOf.HVPAA_BuySellcast, clickedPawn);
                        job32.playerForced = true;
                        context.FirstSelectedPawn.jobs.TryTakeOrderedJob(job32, new JobTag?(JobTag.Misc), false);
                        PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.InteractingWithTraders, KnowledgeAmount.Total);
                    };
                    string text = "";
                    if (clickedPawn.Faction != null)
                    {
                        text = " (" + clickedPawn.Faction.Name + ")";
                    }
                    yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption("HVPAA_FloatMenuSellcast".Translate() + text, action, MenuOptionPriority.InitiateSocial, null, clickedPawn, 0f, null, null, true, 0), context.FirstSelectedPawn, clickedPawn, "ReservedBy", null);
                }
            }
        }
        public static IEnumerable<Gizmo> HVPAA_GetCaravanGizmosPostfix(IEnumerable<Gizmo> __result, Settlement __instance, Caravan caravan)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }
            if (__instance.TraderKind != null && !__instance.Faction.def.permanentEnemy && __instance.Faction != Faction.OfPlayerSilentFail && __instance.Faction.RelationKindWith(Faction.OfPlayerSilentFail) != FactionRelationKind.Hostile && CaravanVisitUtility.SettlementVisitedNow(caravan) == __instance)
            {
                FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(__instance.Faction.def);
                if (fprd != null && fprd.offersSellcasts && Find.AnyPlayerHomeMap != null)
                {
                    Map closestPlayerMap = Find.RandomPlayerHomeMap;
                    foreach (Map m in Find.Maps)
                    {
                        if (m != closestPlayerMap && m.IsPlayerHome && Find.WorldGrid.TraversalDistanceBetween(m.Parent.Tile,__instance.Tile,true) < Find.WorldGrid.TraversalDistanceBetween(closestPlayerMap.Parent.Tile, __instance.Tile,true))
                        {
                            closestPlayerMap = m;
                        }
                    }
                    yield return HVPAAUtility.HireSellcastCommand(caravan, closestPlayerMap, __instance.Faction, __instance.TraderKind);
                }
            }
        }
        public static void HVPAA_FactionDialogForPostfix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (negotiator.Spawned && faction.leader != null && faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(faction.def);
                if (fprd != null && fprd.offersSellcasts)
                {
                    if (faction.def.baseTraderKinds != null)
                    {
                        List<TraderKindDef> baseTraderKinds = faction.def.baseTraderKinds;
                        if (!baseTraderKinds.NullOrEmpty<TraderKindDef>())
                        {
                            TraderKindDef highestTkd = null;
                            foreach (TraderKindDef tkd in baseTraderKinds)
                            {
                                if (tkd.TitleRequiredToTrade != null && (highestTkd == null || highestTkd.TitleRequiredToTrade == null || tkd.TitleRequiredToTrade.seniority > highestTkd.TitleRequiredToTrade.seniority))
                                {
                                    highestTkd = tkd;
                                }
                            }
                            if (highestTkd != null && highestTkd.TitleRequiredToTrade != null)
                            {
                                if (!negotiator.CanTradeWith(faction, highestTkd))
                                {
                                    TaggedString taggedStringX = "HVPAA_FloatMenuSellcast".Translate();
                                    DiaOption diaOptionX = new DiaOption(taggedStringX);
                                    diaOptionX.action = delegate
                                    {
                                        if (faction.leader.trader == null)
                                        {
                                            faction.leader.trader = new Pawn_TraderTracker(faction.leader);
                                        }
                                        faction.leader.mindState.wantsToTradeWithColony = true;
                                        faction.leader.trader.traderKind = faction.def.baseTraderKinds.RandomElement();
                                        Find.WindowStack.Add(new Dialog_SellcastHiring(negotiator, faction.leader, negotiator.Map));
                                    };
                                    diaOptionX.Disable("MessageNeedRoyalTitleToCallWithShip".Translate(highestTkd.TitleRequiredToTrade));
                                    __result.options.Add(diaOptionX);
                                    return;
                                }
                            }
                        }
                    }
                    TaggedString taggedString = "HVPAA_FloatMenuSellcast".Translate();
                    DiaOption diaOption = new DiaOption(taggedString);
                    diaOption.action = delegate
                    {
                        if (faction.leader.trader == null)
                        {
                            faction.leader.trader = new Pawn_TraderTracker(faction.leader);
                        }
                        faction.leader.mindState.wantsToTradeWithColony = true;
                        faction.leader.trader.traderKind = faction.def.baseTraderKinds.RandomElement();
                        Find.WindowStack.Add(new Dialog_SellcastHiring(negotiator, faction.leader, negotiator.Map));
                    };
                    if (negotiator.skills != null && negotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
                    {
                        diaOption.Disable("WorkTypeDisablesOption".Translate(SkillDefOf.Social.label));
                    }
                    __result.options.Add(diaOption);
                }
            }
        }
        public static bool HVPAA_IsColonistPlayerControlledPrefix(Pawn __instance, ref bool __result)
        {
            if (HVPAAUtility.IsSellcastDiscounted(__instance))
            {
                __result = false;
                return false;
            }
            return true;
        }
        public static void HVPAA_GeneratePawnPostfix(ref Pawn __result, PawnGenerationRequest request)
        {
            if (__result.RaceProps.Humanlike)
            {
                AddedSpecPsycasters asp = __result.kindDef.GetModExtension<AddedSpecPsycasters>();
                if (asp != null)
                {
                    HVPAAUtility.CleanPsycasterTraits(__result);
                    if (ModsConfig.BiotechActive && __result.genes != null && __result.Faction != null)
                    {
                        __result.genes.SetXenotype(HVPAAUtility.CleanPsycasterXenotype(__result.genes.Xenotype, __result.kindDef, __result.Faction));
                        HVPAAUtility.CleanPsycasterCustomeGenes(__result);
                    }
                    int psylinksToAdd = asp.minPsylinkLevel.RandomInRange - __result.GetPsylinkLevel();
                    while (psylinksToAdd > 0)
                    {
                        __result.ChangePsylinkLevel(1, false);
                        psylinksToAdd--;
                    }
                    int bonusPsycasts = asp.bonusPsycasts;
                    while (bonusPsycasts > 0)
                    {
                        HVPAAUtility.GrantBonusPsycastInner(__result.GetMainPsylinkSource(), (int)Math.Ceiling(Rand.Value * __result.GetPsylinkLevel()), 1f, 3);
                        bonusPsycasts--;
                    }
                    if (__result.guest != null && Rand.Chance(asp.unwaveringLoyaltyChance))
                    {
                        __result.guest.Recruitable = false;
                    }
                    if (__result.Faction != null)
                    {
                        FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(__result.Faction.def);
                        if (fprd != null && Rand.Chance(fprd.randCastersPerCapita * __result.GetStatValue(StatDefOf.PsychicSensitivity)))
                        {
                            HVPAAUtility.GiveRandPsylinkLevel(__result, fprd.avgRandCasterLevel);
                            HVPAAUtility.GrantBonusPsycasts(__result, fprd);
                        }
                    }
                    return;
                }
                if (__result.Faction != null && !__result.Faction.IsPlayer)
                {
                    FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(__result.Faction.def);
                    if (fprd != null)
                    {
                        float psysens = Math.Min(2f, __result.GetStatValue(StatDefOf.PsychicSensitivity));
                        if (psysens >= 0.5f)
                        {
                            HVPAAUtility.FullRandCasterTreatment(__result, psysens, fprd, request);
                        }
                    }
                }
            }
        }
        public static void HVPAA_PawnGroup_ResolvePostfix(ResolveParams rp)
        {
            if (rp.faction != null && BaseGen.globalSettings.map != null)
            {
                float multi = Find.World.worldObjects.SettlementAt(BaseGen.globalSettings.map.Tile) != null ? 1f : 0.1f;
                FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(rp.faction.def);
                if (fprd != null)
                {
                    IEnumerable<PawnKindDef> pkds = fprd.domesticSpecCasters.InRandomOrder();
                    int domesticPower = (int)(fprd.maxDomesticPower * HVPAAUtility.PsycasterCommonality);
                    foreach (PawnKindDef pkd in pkds)
                    {
                        if (domesticPower > 0 && pkd.RaceProps.Humanlike)
                        {
                            AddedSpecPsycasters asp = pkd.GetModExtension<AddedSpecPsycasters>();
                            if (asp != null && Rand.Chance(asp.domesticChance>=0.9f? asp.domesticChance : Math.Min(asp.domesticChance * multi * (float)Math.Sqrt(HVPAAUtility.PsycasterCommonality),0.9f)))
                            {
                                int toAdd = (int)(asp.domesticCount.RandomInRange * Math.Sqrt(HVPAAUtility.PsycasterCommonality));
                                while (toAdd > 0 && asp.pointCost <= domesticPower)
                                {
                                    Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pkd, rp.faction, PawnGenerationContext.NonPlayer, rp.pawnGroupMakerParams.tile, false, false, false, true, true, 1f, false, true, true, true, true, rp.pawnGroupMakerParams.inhabitants, false, false, false, pkd.biocodeWeaponChance, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, rp.pawnGroupMakerParams.ideo != null ? rp.pawnGroupMakerParams.ideo : rp.faction.ideos.GetRandomIdeoForNewPawn(), false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                                    ResolveParams resolveParams = rp;
                                    resolveParams.singlePawnToSpawn = pawn;
                                    BaseGen.symbolStack.Push("pawn", resolveParams, null);
                                    domesticPower -= asp.pointCost;
                                    toAdd -= 1;
                                }
                            }
                        }
                    }
                }
            }
        }
        public static IEnumerable<Pawn> HVPAA_PGMU_GeneratePawnsPostfix(IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
        {
            foreach (Pawn p in __result)
            {
                yield return p;
            }
            if (parms.faction != null /*&& parms.faction.def.humanlikeFaction*/ && parms.raidStrategy != null && !parms.raidStrategy.HasModExtension<AddedSpecPsycasters>())
            {
                FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(parms.faction.def);
                if (fprd != null && Rand.Chance(fprd.specChanceInRaids>=0.5f? fprd.specChanceInRaids : Math.Min(fprd.specChanceInRaids* (float)Math.Sqrt(HVPAAUtility.PsycasterCommonality),0.5f)))
                {
                    IEnumerable<PawnKindDef> pkds = fprd.raidSpecCasters.InRandomOrder();
                    float specPoints = parms.points * fprd.specPointsPerRaidPoint * (float)Math.Sqrt(HVPAAUtility.PsycasterCommonality);
                    foreach (PawnKindDef pkd in pkds)
                    {
                        if (specPoints > 0 && pkd.RaceProps.Humanlike)
                        {
                            bool skip = false;
                            foreach (Pawn p in __result)
                            {
                                if (p.kindDef == pkd)
                                {
                                    skip = true;
                                    break;
                                }
                            }
                            if (skip)
                            {
                                continue;
                            }
                            AddedSpecPsycasters asp = pkd.GetModExtension<AddedSpecPsycasters>();
                            if (asp != null && Rand.Chance(asp.raidChance*(Rand.Chance(0.5f)?1f:HVPAAUtility.PsycasterCommonality)))
                            {
                                int toAdd = asp.raidCount.RandomInRange;
                                while (toAdd > 0 && asp.pointCost <= specPoints)
                                {
                                    Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(pkd, parms.faction, PawnGenerationContext.NonPlayer, parms.tile, false, false, false, true, true, 1f, false, true, true, true, true, parms.inhabitants, false, false, false, pkd.biocodeWeaponChance, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, parms.ideo != null ? parms.ideo : parms.faction.ideos.GetRandomIdeoForNewPawn(), false, false, false, false, null, null, null, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false));
                                    specPoints -= asp.pointCost;
                                    toAdd -= 1;
                                    yield return pawn;
                                }
                            }
                        }
                    }
                }
            }
        }
        public static bool HVPAA_Notify_DamageTakenPrefix(Pawn_JobTracker __instance)
        {
            if (__instance.curJob != null && __instance.curJob.ability != null)
            {
                Pawn pawn = GetInstanceField(typeof(Pawn_JobTracker), __instance, "pawn") as Pawn;
                if (pawn.health.hediffSet.HasHediff(HVPAADefOf.HVPAA_AI))
                {
                    return false;
                }
            }
            return true;
        }
    }
    [DefOf]
    public static class HVPAADefOf
    {
        static HVPAADefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVPAADefOf));
        }
        public static HediffDef HVPAA_AI;
        public static HistoryEventDef HVPAA_HiredSellcaster;
        public static HistoryEventDef HVPAA_SellcasterCaptured;
        public static JobDef HVPAA_FollowRally;
        public static JobDef HVPAA_BuySellcast;
        public static QuestScriptDef HVPAA_HiredSellcasterQuest;
        public static TraitDef HVPAA_SellcastTrait;

        public static FactionPsycasterRuleDef HVPAA_Default;
        public static FactionPsycasterRuleDef HVPAA_TribalAnima;
        public static FactionPsycasterRuleDef HVPAA_GenericPreIndustrial;
        public static FactionPsycasterRuleDef HVPAA_GenericUltra;
    }
    //HVPAA algorithm foundation
    public class HediffCompProperties_InitiateHIPAAifAI : HediffCompProperties
    {
        public HediffCompProperties_InitiateHIPAAifAI()
        {
            this.compClass = typeof(HediffComp_InitiateHIPAAifAI);
        }
    }
    public class HediffComp_InitiateHIPAAifAI : HediffComp
    {
        public HediffCompProperties_IntPsycasts Props
        {
            get
            {
                return (HediffCompProperties_IntPsycasts)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(250, delta))
            {
                this.InstantiateAIifNeeded();
            }
        }
        public void InstantiateAIifNeeded()
        {
            if (HVPAAUtility.CanPsyast(this.Pawn, 0))
            {
                if (!this.Pawn.health.hediffSet.HasHediff(HVPAADefOf.HVPAA_AI))
                {
                    this.Pawn.health.AddHediff(HVPAADefOf.HVPAA_AI);
                }
            }
        }
    }
    public class HautsFactionCompProperties_AlliesAndAdversaries : HautsFactionCompProperties
    {
        public HautsFactionCompProperties_AlliesAndAdversaries()
        {
            this.compClass = typeof(HautsFactionComp_AlliesAndAdversaries);
        }
    }
    public class HautsFactionComp_AlliesAndAdversaries : HautsFactionComp
    {
        public HautsFactionCompProperties_AlliesAndAdversaries Props
        {
            get
            {
                return (HautsFactionCompProperties_AlliesAndAdversaries)this.props;
            }
        }
        public override void CompPostTick()
        {
            base.CompPostTick();
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                this.mapsCovered.Clear();
            }
        }
        public Dictionary<Map,MapAlliesAndAdversaries> mapsCovered = new Dictionary<Map, MapAlliesAndAdversaries>();
    }
    public class MapAlliesAndAdversaries
    {
        public MapAlliesAndAdversaries ()
        {
            this.allies = new List<Pawn>();
            this.foes = new List<Pawn>();
        }
        public List<Pawn> allies = new List<Pawn>();
        public List<Pawn> foes = new List<Pawn>();
    }
    public class HediffCompProperties_IntPsycasts : HediffCompProperties_MoteConditional
    {
        public HediffCompProperties_IntPsycasts()
        {
            this.compClass = typeof(HediffComp_IntPsycasts);
        }
    }
    public class HediffComp_IntPsycasts : HediffComp_MoteConditional
    {
        public new HediffCompProperties_IntPsycasts Props
        {
            get
            {
                return (HediffCompProperties_IntPsycasts)this.props;
            }
        }
        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            this.timer = 1;
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (!this.Pawn.Spawned)
            {
                this.continuousTimeSpawned = 0;
                return;
            }
            this.continuousTimeSpawned = Math.Min(this.continuousTimeSpawned + 1, 60000);
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (HautsUtility.IsntCastingAbility(this.Pawn))
            {
                if (this.timer > 0)
                {
                    this.timer -= delta;
                } else if (this.CanPsycast()) {
                    //set the situation case here: 1 = in combat, 2 = out of combat, 3 = fleeing, 4 = other mental state, 5 = fleeing with cargo
                    int situationCase = this.GetSituation();
                    //Log.Error(this.Pawn.Name.ToStringShort + "can cast, situation " + situationCase);
                    //redetermines the niceness or evilness, pacifism, meditation focus types, etc. of the caster
                    this.ResetAllParameters();
                    //Log.Warning("Parameters as follows: " + this.continuousTimeSpawned + " cts\n" + this.niceToEvil + " niceness\n" + this.niceToAnimals + " animal affinity\n" + this.pacifist + " pacifist\n" + this.fireUser + " fireuser\n" + this.pyro + " pyro\n" + this.lightUser + " lightuser\n" + this.cureUser + " cureuser\n" + this.painkiller + " painkiller\n" + this.scarHealer + " scarhealer\n");
                    /*foreach (MeditationFocusDef mfd in this.mfds)
                    {
                        Log.Warning("med focus type " + mfd.label);
                    }*/
                    //give me up to 5 psycasts to try out. A psycast can enter this list multiple times if it has multiple use cases
                    List<PotentialPsycast> highestPriorityPsycasts = this.ThreePriorityPsycasts(situationCase);
                    //Log.Message("highest-priority psycasts as follows: ");
                    /*foreach (PotentialPsycast pp in highestPriorityPsycasts)
                    {
                        Log.Message(pp.ability.def.label + ">> " + pp.score);
                    }*/
                    //figure out the 'applicability' of each psycast to the current situation
                    bool immediatelyPsycastAgain = false;
                    if (highestPriorityPsycasts.Count > 0)
                    {
                        for (int i = this.allies.Count - 1; i >= 0; i--)
                        {
                            if (this.allies[i] == null || !this.allies[i].Spawned)
                            {
                                this.allies.RemoveAt(i);
                            }
                        }
                        for (int i = this.foes.Count - 1; i >= 0; i--)
                        {
                            if (this.foes[i] == null || !this.foes[i].Spawned)
                            {
                                this.foes.RemoveAt(i);
                            }
                        }
                        List<Psycast> metas = this.MetaCasts();
                        bool metaWasCast = false;
                        if (!metas.NullOrEmpty())
                        {
                            Psycast meta = metas.RandomElement();
                            if ((!this.pacifist || !meta.def.casterMustBeCapableOfViolence) && meta.CanCast && (meta.def.EntropyGain * this.Pawn.GetStatValue(StatDefOf.PsychicEntropyGain)) + this.Pawn.psychicEntropy.EntropyValue <= this.percentEntropyLimit * this.Pawn.psychicEntropy.MaxEntropy && meta.def.PsyfocusCost <= this.Pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                            {
                                UseCaseTags uct = meta.def.GetModExtension<UseCaseTags>();
                                if (uct != null)
                                {
                                    PotentialPsycast psyToCast = new PotentialPsycast(meta, -1f, 0, uct.immediatelyPsycastAgain);
                                    float metapplicability = uct.MetaApplicability(this, psyToCast, highestPriorityPsycasts, situationCase, this.niceToEvil);
                                    if (metapplicability > 0)
                                    {
                                        if (!this.Pawn.Awake())
                                        {
                                            this.Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
                                        }
                                        immediatelyPsycastAgain = uct.immediatelyPsycastAgain;
                                        if (psyToCast.lti.IsValid)
                                        {
                                            this.Pawn.jobs.StartJob(psyToCast.ability.GetJob(psyToCast.lti, psyToCast.ltiDest != null ? psyToCast.ltiDest : null), JobCondition.InterruptForced);
                                            this.PostCast(psyToCast, situationCase);
                                            metaWasCast = true;
                                        } else if (psyToCast.gti.IsValid) {
                                            Job job = JobMaker.MakeJob(psyToCast.ability.def.jobDef ?? JobDefOf.CastAbilityOnWorldTile);
                                            job.verbToUse = psyToCast.ability.verb;
                                            job.globalTarget = psyToCast.gti;
                                            job.ability = psyToCast.ability;
                                            this.Pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                                            this.PostCast(psyToCast, situationCase);
                                            metaWasCast = true;
                                        }
                                    }
                                }
                            }
                        }
                        if (!metaWasCast)
                        {
                            foreach (PotentialPsycast potPsy in highestPriorityPsycasts)
                            {
                                UseCaseTags uct = potPsy.ability.def.GetModExtension<UseCaseTags>();
                                if (uct != null)
                                {
                                    potPsy.score *= uct.ApplicabilityScore(this, potPsy, this.niceToEvil);
                                }
                            }
                            //get the psycast with the highest priority * applicability
                            if (highestPriorityPsycasts.Count > 0)
                            {
                                PotentialPsycast psyToCast = null;
                                foreach (PotentialPsycast potPsy in highestPriorityPsycasts)
                                {
                                    if (potPsy.score > 0f)
                                    {
                                        if (psyToCast == null)
                                        {
                                            psyToCast = potPsy;
                                        } else if (psyToCast.score < potPsy.score || (psyToCast.score == potPsy.score && Rand.Chance(0.5f))) {
                                            psyToCast = potPsy;
                                        }
                                    }
                                }
                                //now cast that sucka
                                if (psyToCast != null)
                                {
                                    if (!this.Pawn.Awake())
                                    {
                                        this.Pawn.jobs.EndCurrentJob(JobCondition.InterruptForced, true, true);
                                    }
                                    immediatelyPsycastAgain = psyToCast.immediatelyPsycastAgain;
                                    if (psyToCast.lti.IsValid)
                                    {
                                        this.Pawn.jobs.StartJob(psyToCast.ability.GetJob(psyToCast.lti, psyToCast.ltiDest != null ? psyToCast.ltiDest : null), JobCondition.InterruptForced);
                                        this.PostCast(psyToCast, situationCase);
                                    } else if (psyToCast.gti.IsValid) {
                                        Job job = JobMaker.MakeJob(psyToCast.ability.def.jobDef ?? JobDefOf.CastAbilityOnWorldTile);
                                        job.verbToUse = psyToCast.ability.verb;
                                        job.globalTarget = psyToCast.gti;
                                        job.ability = psyToCast.ability;
                                        job.playerForced = true;
                                        this.Pawn.jobs.StartJob(job, JobCondition.InterruptForced);
                                        job.playerForced = true;
                                        this.PostCast(psyToCast, situationCase);
                                    }
                                }
                            }
                        }
                    }
                    //reset the timer
                    if (immediatelyPsycastAgain)
                    {
                        this.timer = 10;
                    } else {
                        this.timer = (int)HVPAA_Mod.settings.psycastAttemptInterval;
                        if (situationCase == 1)
                        {
                            this.timer /= 4;
                            AddedSpecPsycasters asp = this.Pawn.kindDef.GetModExtension<AddedSpecPsycasters>();
                            if (asp != null && asp.combatCastIntervalOverride > 0)
                            {
                                this.timer = Math.Min(asp.combatCastIntervalOverride, this.timer);
                            }
                        }
                        if (this.Pawn.IsPsychologicallyInvisible())
                        {
                            this.timer /= 2;
                        }
                    }
                    //Log.Error("time to next cast: " + (this.timer / 60));
                }
            }
        }
        public void DoBubble(Psycast psycast)
        {
            MoteMaker.MakeInteractionBubble(this.Pawn, null, ThingDefOf.Mote_Speech, psycast.def.uiIcon);
        }
        public void PostCast(PotentialPsycast psyToCast, int situationCase)
        {
            this.DoBubble(psyToCast.ability);
            UseCaseTags uct = psyToCast.ability.def.GetModExtension<UseCaseTags>();
            if (uct != null)
            {
                //some psycasts might be better balanced in non-player hands by a longer cast time, achieved by adding a stun
                if (uct.additionalCastingTicks > 0 && HVPAA_Mod.settings.powerLimiting)
                {
                    this.Pawn.stances.stunner.StunFor(uct.additionalCastingTicks, this.Pawn, false, false, false);
                }
                //some psycasts might be important enough to warrant sending a letter to the player
                if (uct.sendLetter && HVPAA_Mod.settings.mostDangerousNotifs && (!uct.letterOnlyIfPlayerTarget || (psyToCast.lti.Thing != null && psyToCast.lti.Pawn != null && PawnUtility.ShouldSendNotificationAbout(psyToCast.lti.Pawn))))
                {
                    TaggedString label;
                    TaggedString message;
                    if (HautsUtility.IsHighFantasy())
                    {
                        label = uct.letterLabelF.Translate();
                        message = uct.letterTextF.Translate();
                    } else {
                        label = uct.letterLabel.Translate();
                        message = uct.letterText.Translate();
                    }
                    LookTargets toLook = new LookTargets(this.Pawn);
                    ChoiceLetter letter = LetterMaker.MakeLetter(label, message, uct.letterDef, toLook, null, null, null);
                    Find.LetterStack.ReceiveLetter(letter, null);
                }
                //some psycasts (e.g. Farskip, Neuroquake) "rally" nearby allies, so they can be in/out of the AoE
                if (uct.rallyRadius > 0f)
                {
                    foreach (Pawn p2 in this.allies)
                    {
                        if (uct.ShouldRally(psyToCast.ability, p2, situationCase))
                        {
                            Job rallyJob = new Job(HVPAADefOf.HVPAA_FollowRally, this.Pawn);
                            p2.jobs.StartJob(rallyJob, JobCondition.InterruptForced);
                            JobDriver_FollowRally jdfr = (JobDriver_FollowRally)p2.jobs.curDriver;
                            if (jdfr != null)
                            {
                                jdfr.maxRallyTicks = uct.maxRallyTicks;
                            }
                        }
                    }
                }
            }
        }
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (HautsUtility.IsntCastingAbility(this.Pawn) && Rand.Chance(0.5f))
            {
                this.timer = Math.Min(240, this.timer);
            }
        }
        public bool CanPsycast()
        {
            return HVPAAUtility.CanPsyast(this.Pawn, this.GetSituation());
        }
        public void ResetAllParameters()
        {
            this.fireUser = true;
            this.lightUser = true;
            this.cureUser = true;
            this.painkiller = true;
            this.scarHealer = true;
            this.niceToEvil = HVPAA_Mod.settings.nicerPsycasters ? 1f : 0f;
            this.niceToAnimals = 0f;
            this.pyro = false;
            this.pacifist = this.Pawn.WorkTagIsDisabled(WorkTags.Violent);
            if (this.Pawn.story != null)
            {
                foreach (Trait t in this.Pawn.story.traits.TraitsSorted)
                {
                    CasterPersonality castper = t.def.GetModExtension<CasterPersonality>();
                    if (castper != null)
                    {
                        this.niceToEvil += castper.niceOrEvil;
                        this.niceToAnimals += castper.niceToAnimals;
                        if (castper.pacifist)
                        {
                            this.pacifist = true;
                        }
                        if (castper.proDisease)
                        {
                            this.cureUser = false;
                        }
                        if (castper.proPain)
                        {
                            this.painkiller = false;
                        }
                        if (castper.proScar)
                        {
                            this.scarHealer = false;
                        }
                        if (castper.yesFlames)
                        {
                            this.pyro = true;
                        }
                        if (castper.noFlames)
                        {
                            this.fireUser = false;
                        }
                        if (castper.noLight)
                        {
                            this.lightUser = false;
                        }
                    }
                }
            }
            if (ModsConfig.IdeologyActive && this.Pawn.ideo != null && this.Pawn.ideo.Ideo != null && this.Pawn.ideo.Ideo.memes.Count > 0)
            {
                foreach (MemeDef m in this.Pawn.ideo.Ideo.memes)
                {
                    CasterPersonality castPer = m.GetModExtension<CasterPersonality>();
                    if (castPer != null)
                    {
                        this.niceToEvil += castPer.niceOrEvil;
                        this.niceToAnimals += castPer.niceToAnimals;
                        if (castPer.pacifist)
                        {
                            this.pacifist = true;
                        }
                        if (castPer.proDisease)
                        {
                            this.cureUser = false;
                        }
                        if (castPer.proScar)
                        {
                            this.scarHealer = false;
                        }
                        if (castPer.proPain)
                        {
                            this.painkiller = false;
                        }
                        if (castPer.yesFlames)
                        {
                            this.pyro = true;
                        }
                        if (castPer.noFlames)
                        {
                            this.fireUser = false;
                        }
                        if (castPer.noLight)
                        {
                            this.lightUser = false;
                        }
                    }
                }
            }
            this.mfds.Clear();
            foreach (MeditationFocusDef mfd in MeditationUtility.FocusTypesAvailableForPawn(this.Pawn))
            {
                this.mfds.Add(mfd);
            }
            this.allies.Clear();
            this.foes.Clear();
            if (this.Pawn.Spawned)
            {
                if (this.Pawn.Faction != null)
                {
                    WorldComponent_HautsFactionComps WCFC = (WorldComponent_HautsFactionComps)Find.World.GetComponent(typeof(WorldComponent_HautsFactionComps));
                    Hauts_FactionCompHolder fch = WCFC.FindCompsFor(this.Pawn.Faction);
                    if (fch != null)
                    {
                        HautsFactionComp_AlliesAndAdversaries aaa = fch.TryGetComp<HautsFactionComp_AlliesAndAdversaries>();
                        if (aaa != null)
                        {
                            if (aaa.mapsCovered == null)
                            {
                                aaa.mapsCovered = new Dictionary<Map,MapAlliesAndAdversaries>();
                            }
                            if (aaa.mapsCovered.TryGetValue(this.Pawn.Map, out MapAlliesAndAdversaries maaa))
                            {
                                this.allies = maaa.allies;
                                this.foes = maaa.foes;
                            } else {
                                MapAlliesAndAdversaries maaa2 = new MapAlliesAndAdversaries();
                                HVPAAUtility.SetAlliesAndAdversaries(this.Pawn,maaa2.allies,maaa2.foes,this.niceToAnimals,this.niceToEvil);
                                aaa.mapsCovered.Add(this.Pawn.Map, maaa2);
                                this.allies = maaa2.allies;
                                this.foes = maaa2.foes;
                            }
                        }
                    }
                } else {
                    HVPAAUtility.SetAlliesAndAdversaries(this.Pawn, this.allies, this.foes, this.niceToAnimals, this.niceToEvil);
                }
                //HVPAAUtility.SetAlliesAndAdversaries(this.Pawn, this.allies, this.foes, this.niceToAnimals, this.niceToEvil);
            }
            float limit = 1f;
            foreach (Hediff h in this.Pawn.health.hediffSet.hediffs)
            {
                LimitsHVPAACasting lhc = h.def.GetModExtension<LimitsHVPAACasting>();
                if (lhc != null && lhc.percent < limit)
                {
                    limit = lhc.percent;
                }
            }
            this.percentEntropyLimit = limit;
            if (ModsConfig.BiotechActive && this.Pawn.genes != null && this.Pawn.genes.HasActiveGene(GeneDefOf.FireTerror))
            {
                this.fireUser = false;
            }
            if (!this.fireUser)
            {
                this.pyro = false;
            }
        }
        public List<Psycast> MetaCasts()
        {
            List<Psycast> metas = new List<Psycast>();
            foreach (Ability a in this.Pawn.abilities.abilities)
            {
                UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                if (uct != null && uct.meta && a is Psycast p)
                {
                    metas.Add(p);
                }
            }
            return metas;
        }
        public List<PotentialPsycast> ThreePriorityPsycasts(int situationCase)
        {
            List<PotentialPsycast> priorityPsycasts = new List<PotentialPsycast>();
            foreach (Ability a in this.Pawn.abilities.abilities)
            {
                if (a is Psycast psycast && (!this.pacifist || !a.def.casterMustBeCapableOfViolence) && psycast.CanCast && psycast.def.level <= this.Pawn.psychicEntropy.MaxAbilityLevel && (psycast.def.EntropyGain * this.Pawn.GetStatValue(StatDefOf.PsychicEntropyGain)) + this.Pawn.psychicEntropy.EntropyValue <= this.percentEntropyLimit * this.Pawn.psychicEntropy.MaxEntropy && psycast.def.PsyfocusCost <= this.Pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
                {
                    UseCaseTags uct = a.def.GetModExtension<UseCaseTags>();
                    if (uct != null)
                    {
                        bool hostile = (this.Pawn.Faction != null && this.Pawn.Faction.HostileTo(Faction.OfPlayer)) || this.Pawn.InAggroMentalState;
                        if (HVPAA_Mod.settings.hostileUsabilities.ContainsKey(a.def.defName))
                        {
                            HVPAA_Mod.settings.hostileUsabilities.TryGetValue(a.def.defName, out bool canUseHostile);
                            if (!canUseHostile)
                            {
                                if (hostile)
                                {
                                    continue;
                                }
                            }
                        } else {
                            HVPAA_Mod.settings.hostileUsabilities.Add(a.def.defName, true);
                        }
                        if (HVPAA_Mod.settings.nonhostileUsabilities.ContainsKey(a.def.defName))
                        {
                            HVPAA_Mod.settings.nonhostileUsabilities.TryGetValue(a.def.defName, out bool canUseNonhostile);
                            if (!canUseNonhostile)
                            {
                                if (!hostile)
                                {
                                    continue;
                                }
                            }
                        } else {
                            HVPAA_Mod.settings.nonhostileUsabilities.Add(a.def.defName, true);
                        }
                        if (uct.disabledAtPercentEntropy > 0f && psycast.pawn.psychicEntropy.EntropyRelativeValue >= uct.disabledAtPercentEntropy)
                        {
                            continue;
                        }
                        float level = (float)Math.Sqrt(a.def.level);
                        if (uct.damage)
                        {
                            float priority = level * uct.PriorityScoreDamage(psycast, situationCase, this.pacifist, this.niceToEvil, this.mfds) * this.PriorityRandomizationFactor;
                            this.TryAddToPriorityPsycasts(ref priorityPsycasts, uct, psycast, priority, 1);
                        }
                        if (uct.defense)
                        {
                            float priority = level * uct.PriorityScoreDefense(psycast, situationCase, this.pacifist, this.niceToEvil, this.mfds) * this.PriorityRandomizationFactor;
                            this.TryAddToPriorityPsycasts(ref priorityPsycasts, uct, psycast, priority, 2);
                        }
                        if (uct.debuff)
                        {
                            float priority = level * uct.PriorityScoreDebuff(psycast, situationCase, this.pacifist, this.niceToEvil, this.mfds) * this.PriorityRandomizationFactor;
                            this.TryAddToPriorityPsycasts(ref priorityPsycasts, uct, psycast, priority, 3);
                        }
                        if (uct.healing)
                        {
                            float priority = level * uct.PriorityScoreHealing(psycast, situationCase, this.pacifist, this.niceToEvil, this.mfds) * (situationCase == 2 ? this.PriorityRandomizationFactor: 1f);
                            this.TryAddToPriorityPsycasts(ref priorityPsycasts, uct, psycast, priority, 4);
                        }
                        if (uct.utility)
                        {
                            float priority = level * uct.PriorityScoreUtility(psycast, situationCase, this.pacifist, this.niceToEvil, this.mfds) * (situationCase == 2 ? this.PriorityRandomizationFactor : 1f);
                            this.TryAddToPriorityPsycasts(ref priorityPsycasts, uct, psycast, priority, 5);
                        }
                    }
                }
            }
            return priorityPsycasts;
        }
        public float PriorityRandomizationFactor
        {
            get
            {
                return ((Rand.Value * 0.4f) + 0.8f);
            }
        }
        public void TryAddToPriorityPsycasts(ref List<PotentialPsycast> priorityPsycasts, UseCaseTags uct, Psycast a, float priority, int useCase)
        {
            float initPriority = priority;
            //darkness adherents do not cast light-making psycasts
            if (!uct.usableWhileFleeing && this.GetSituation() == 3)
            {
                return;
            }
            if (uct.light && !this.lightUser)
            {
                return;
            }
            if (uct.animalRightsViolation && this.niceToAnimals > 0)
            {
                return;
            }
            if (uct.antiDisease && !this.cureUser)
            {
                return;
            }
            if (uct.painkiller && !this.painkiller)
            {
                return;
            }
            if (uct.antiScar && !this.scarHealer)
            {
                return;
            }
            if (!this.Pawn.Awake() && !uct.usableWhileAsleep)
            {
                return;
            }
            if (uct.mfds != null)
            {
                //pyrophobes like sanguophages will not cast flame psycasts
                if (uct.mfds.Contains(DefDatabase<MeditationFocusDef>.GetNamed("Flame")) && !this.fireUser)
                {
                    return;
                }
                else if (this.mfds != null)
                {
                    //otherwise, certain psycasts appeal more to psycasters with certain meditation focus types. Pyromaniacs LOVE shooting fire and Morbids desire the disturbing and gory
                    foreach (MeditationFocusDef mfd in uct.mfds)
                    {
                        if (this.mfds.Contains(mfd))
                        {
                            priority += initPriority;
                        }
                    }
                }
            }
            if (uct.antiFlame && (this.Pawn.WorkTagIsDisabled(WorkTags.Firefighting) || this.pyro))
            {
                return;
            }
            if (priority > 0f)
            {
                if (priorityPsycasts.Count <= HVPAA_Mod.settings.maxChoicesPerAttempt)
                {
                    priorityPsycasts.Add(new PotentialPsycast(a, priority, useCase, uct.immediatelyPsycastAgain));
                }
                else
                {
                    PotentialPsycast lowestPriority = priorityPsycasts.First();
                    foreach (PotentialPsycast potentialPsycast in priorityPsycasts)
                    {
                        if (potentialPsycast.score < lowestPriority.score)
                        {
                            lowestPriority = potentialPsycast;
                        }
                    }
                    if (priority > lowestPriority.score)
                    {
                        priorityPsycasts.Remove(lowestPriority);
                        priorityPsycasts.Add(new PotentialPsycast(a, priority, useCase, uct.immediatelyPsycastAgain));
                    }
                }
            }
        }
        //situation cases: 1 = in combat, 2 = out of combat, 3 = fleeing, 4 = other mental state, 5 = fleeing with cargo, 6 = otherwise exiting map
        public int GetSituation()
        {
            if (this.Pawn.mindState != null)
            {
                if (this.Pawn.InMentalState)
                {
                    PsycastPermissiveMentalState ppms = this.Pawn.MentalStateDef.GetModExtension<PsycastPermissiveMentalState>();
                    if (ppms != null)
                    {
                        return ppms.id;
                    }
                }
                if (this.Pawn.IsFighting() || (this.Pawn.CurJob != null && this.Pawn.CurJob.jobGiver != null && (this.Pawn.CurJob.jobGiver is JobGiver_AIFightEnemy)) || Find.TickManager.TicksGame < this.Pawn.mindState.lastHarmTick + 400 || Find.TickManager.TicksGame < this.Pawn.mindState.lastEngageTargetTick + 400 || Find.TickManager.TicksGame < this.Pawn.mindState.lastSelfTendTick + 400)
                {
                    return 1;
                }
                if (this.Pawn.CurJob != null)
                {
                    PsycastPermissiveMentalState ppms = this.Pawn.CurJobDef.GetModExtension<PsycastPermissiveMentalState>();
                    if (ppms != null)
                    {
                        return ppms.id;
                    }
                }
                this.Pawn.TryGetLord(out Lord lord);
                if (lord != null && lord.CurLordToil != null)
                {
                    if (lord.CurLordToil is LordToil_DoOpportunisticTaskOrCover || lord.CurLordToil is LordToil_StealCover || lord.CurLordToil is LordToil_KidnapCover)
                    {
                        return 5;
                    }
                    if (lord.CurLordToil is LordToil_ExitMap || lord.CurLordToil is LordToil_ExitMapAndEscortCarriers || lord.CurLordToil is LordToil_ExitMapAndDefendSelf)
                    {
                        return 6;
                    }
                }
            }
            return 2;
        }
        public int SetNewTimer()
        {
            return Math.Max(60, (int)(Rand.Value * 420));
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<int>(ref this.timer, "timer", this.SetNewTimer(), false);
            Scribe_Values.Look<int>(ref this.continuousTimeSpawned, "continuousTimeSpawned", 0, false);
            Scribe_Values.Look<float>(ref this.niceToEvil, "niceToEvil", 0f, false);
            Scribe_Values.Look<bool>(ref this.pacifist, "pacifist", false, false);
        }
        public int timer;
        public int continuousTimeSpawned;
        public float niceToEvil;
        public float niceToAnimals;
        public bool pacifist;
        public bool fireUser;
        public bool pyro;
        public bool lightUser;
        public bool cureUser;
        public bool painkiller;
        public bool scarHealer;
        public float percentEntropyLimit = 1f;
        public List<MeditationFocusDef> mfds = new List<MeditationFocusDef>();
        public List<Pawn> allies = new List<Pawn>();
        public List<Pawn> foes = new List<Pawn>();
    }
    public class PotentialPsycast
    {
        public PotentialPsycast()
        {

        }
        public Pawn Caster
        {
            get
            {
                return this.ability.pawn;
            }
        }
        public PotentialPsycast(Psycast ability, float score, int useCase, bool immediatelyPsycastAgain)
        {
            this.ability = ability;
            this.score = score;
            this.useCase = useCase;
            this.immediatelyPsycastAgain = immediatelyPsycastAgain;
        }
        public Psycast ability;
        public float score;
        public int useCase; //damage 1, defense 2, debuff 3, healing 4, utility 5
        public LocalTargetInfo lti = IntVec3.Invalid;
        public LocalTargetInfo ltiDest = IntVec3.Invalid;
        public GlobalTargetInfo gti = new GlobalTargetInfo(IntVec3.Invalid, null, false);
        public bool immediatelyPsycastAgain;
    }
    public class MoteNPCasterText : MoteConditionalText
    {
        public override string TextString
        {
            get
            {
                if (HVPAA_Mod.settings.showNPCasterLevel && this.link1.Target != null && this.link1.Target.Thing != null && this.link1.Target.Thing is Pawn p && !p.IsColonistPlayerControlled)
                {
                    return "HVPAA_NPC_mote".Translate(p.GetPsylinkLevel(), this.TotalPsycasts(p));
                }
                return " ";
            }
        }
        public int TotalPsycasts(Pawn p)
        {
            int totalPsycastPower = 0;
            if (p.abilities != null)
            {
                for (int i = 0; i < p.abilities.abilities.Count; i++)
                {
                    if (p.abilities.abilities[i].def.IsPsycast)
                    {
                        totalPsycastPower++;
                    }
                }
            }
            return totalPsycastPower;
        }
    }
    //DefModExtensions
    public class UseCaseTags : DefModExtension
    {
        public UseCaseTags()
        {

        }
        public virtual float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (pacifist)
            {
                return 0f;
            }
            switch (situationCase)
            {
                case 1:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
                        return 1.7f;
                    }
                case 2:
                    return 0f;
                case 6:
                    return 0f;
                default:
                    return niceToEvil < 0f ? (-niceToEvil / 10f) : 0f;
            }
        }
        public virtual float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            switch (situationCase)
            {
                case 1:
                    return niceToEvil >= 0f ? 2f : 1.5f;
                case 2:
                    return 0f;
                case 6:
                    return 0f;
                default:
                    return 1f;
            }
        }
        public virtual float PriorityScoreDebuff(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            switch (situationCase)
            {
                case 1:
                    if (niceToEvil > 0f)
                    {
                        return 1.25f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 1.7f;
                    }
                    else
                    {
                        return 1.5f;
                    }
                case 3:
                    if (niceToEvil >= 0f)
                    {
                        return 0.1f;
                    }
                    return 0.5f;
                case 5:
                    if (niceToEvil >= 0f)
                    {
                        return 0.1f;
                    }
                    return 0.5f;
                default:
                    return 0f;
            }
        }
        public virtual float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            switch (situationCase)
            {
                case 1:
                    return 1f;
                case 2:
                    if (niceToEvil > 0f)
                    {
                        return 10f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 3f;
                    }
                    else
                    {
                        return 5f;
                    }
                case 6:
                    if (niceToEvil > 0f)
                    {
                        return 10f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 3f;
                    }
                    else
                    {
                        return 5f;
                    }
                default:
                    return niceToEvil >= 0f ? 0.7f : 0f;
            }
        }
        public virtual float PriorityScoreUtility(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            switch (situationCase)
            {
                case 2:
                    return 1f;
                case 6:
                    return 1f;
                default:
                    return 0f;
            }
        }
        public virtual float MetaApplicability(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, List<PotentialPsycast> psycasts, int situationCase, float niceToEvil)
        {
            return 0f;
        }
        public virtual float ApplicabilityScore(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (psycast.ability.def.casterMustBeCapableOfViolence && psycast.Caster.WorkTagIsDisabled(WorkTags.Violent))
            {
                return 0f;
            }
            switch (psycast.useCase)
            {
                case 1:
                    return this.ApplicabilityScoreDamage(intPsycasts, psycast, niceToEvil);
                case 2:
                    return this.ApplicabilityScoreDefense(intPsycasts, psycast, niceToEvil);
                case 3:
                    return this.ApplicabilityScoreDebuff(intPsycasts, psycast, niceToEvil);
                case 4:
                    return this.ApplicabilityScoreHealing(intPsycasts, psycast, niceToEvil);
                case 5:
                    return this.ApplicabilityScoreUtility(intPsycasts, psycast, niceToEvil);
                default:
                    return 1f;
            }
        }
        public virtual float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return 0f;
        }
        public virtual float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return 0f;
        }
        public virtual float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return 0f;
        }
        public virtual float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return 0f;
        }
        public virtual float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return 0f;
        }
        public virtual Pawn FindEnemyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in intPsycasts.foes)
            {
                if (p.Position.DistanceTo(origin) <= (range == -999 ? this.Range(psycast) : range))
                {
                    if ((!this.requiresLoS || GenSight.LineOfSight(origin, p.Position, p.Map)) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherEnemyDisqualifiers(psycast, p, useCase, initialTarget))
                    {
                        if (this.avoidTargetsWithHediff != null && p.health.hediffSet.HasHediff(this.avoidTargetsWithHediff))
                        {
                            continue;
                        }
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
        public virtual Pawn FindAllyPawnTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn, float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in intPsycasts.allies)
            {
                if (p.Position.DistanceTo(origin) <= (range == -999 ? this.Range(psycast) : range))
                {
                    if ((!this.requiresLoS || GenSight.LineOfSight(origin, p.Position, p.Map)) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget))
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
            if (pawnTargets.Count > 0)
            {
                return this.BestPawnFound(pawnTargets);
            }
            return null;
        }
        public virtual Thing FindBestThingTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Thing, float> thingTargets, float range = -999)
        {
            thingTargets = new Dictionary<Thing, float>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, (range == -999 ? this.Range(psycast) : range), true))
            {
                if ((!this.requiresLoS || GenSight.LineOfSight(psycast.pawn.Position, t.Position, t.Map)) && psycast.CanApplyPsycastTo(t))
                {
                    if (this.IsValidThing(psycast.pawn, t, niceToEvil, useCase))
                    {
                        float tApplicability = this.ThingApplicability(psycast, t, useCase);
                        if (tApplicability > 0f)
                        {
                            thingTargets.Add(t, tApplicability);
                        }
                    }
                }
            }
            if (thingTargets.Count > 0)
            {
                return this.BestThingFound(thingTargets);
            }
            return null;
        }
        public virtual IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            return IntVec3.Invalid;
        }
        public virtual bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            return true;
        }
        public virtual Pawn BestPawnFound(Dictionary<Pawn, float> pawnTargets)
        {
            pawnTargets.Keys.TryRandomElementByWeight((Pawn p) => Math.Max(pawnTargets.TryGetValue(p), 0f), out Pawn pawn);
            return pawn;
        }
        public virtual Thing BestThingFound(Dictionary<Thing, float> thingTargets)
        {
            thingTargets.Keys.TryRandomElementByWeight((Thing t) => Math.Max(thingTargets.TryGetValue(t), 0f), out Thing thing);
            return thing;
        }
        public virtual bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return false;
        }
        public virtual bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return false;
        }
        public virtual float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 1f;
        }
        public virtual float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return 1f;
        }
        public virtual float ThingApplicability(Psycast psycast, Thing t, float niceToEvil, int useCase = 1)
        {
            return 1f;
        }
        public virtual List<Pawn> TopTargets(int maxNumber, Dictionary<Pawn, float> pawnTargets)
        {
            List<Pawn> topTargets = new List<Pawn>();
            foreach (Pawn p in pawnTargets.Keys)
            {
                if (topTargets.Count <= 5)
                {
                    topTargets.Add(p);
                }
                else
                {
                    Pawn lowestApp = topTargets.First();
                    foreach (Pawn apps in topTargets)
                    {
                        if (pawnTargets.TryGetValue(apps) < pawnTargets.TryGetValue(lowestApp))
                        {
                            lowestApp = apps;
                        }
                    }
                    if (pawnTargets.TryGetValue(p) > pawnTargets.TryGetValue(lowestApp))
                    {
                        topTargets.Remove(lowestApp);
                        topTargets.Add(p);
                    }
                }
            }
            return topTargets;
        }
        public virtual bool TooMuchThingNearby(Psycast psycast, IntVec3 position, float range)
        {
            if (this.avoidMakingTooMuchOfThing != null)
            {
                int thingCount = 0;
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(position, psycast.pawn.Map, range, true))
                {
                    if (thing.def == this.avoidMakingTooMuchOfThing && this.TooMuchThingAdditionalCheck(thing, psycast))
                    {
                        thingCount++;
                        if (thingCount >= this.thingLimit)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
        public virtual bool TooMuchThingAdditionalCheck(Thing thing, Psycast psycast)
        {
            return true;
        }
        public virtual bool ShouldRally(Psycast psycast, Pawn p, int situation)
        {
            return false;
        }
        public virtual float Range(Psycast psycast)
        {
            return (psycast.def.verbProperties.AdjustedRange(psycast.verb, psycast.pawn) + this.rangeOffset) * this.rangeMultiplier;
        }
        public bool damage;
        public bool defense;
        public bool debuff;
        public bool healing;
        public bool utility;
        public bool meta;
        public bool usableWhileFleeing;
        public bool usableWhileAsleep;
        public bool light;
        public bool antiFlame;
        public bool animalRightsViolation;
        public bool antiDisease;
        public bool antiScar;
        public bool painkiller;
        public float rangeOffset;
        public float rangeMultiplier = 1f;
        public float aoe = 1f;
        public float disabledAtPercentEntropy = -1f;
        public bool requiresLoS = true;
        public HediffDef avoidTargetsWithHediff;
        public ThingDef avoidMakingTooMuchOfThing;
        public int thingLimit = 1;
        public List<MeditationFocusDef> mfds;
        public bool immediatelyPsycastAgain;
        public bool sendLetter;
        public LetterDef letterDef;
        public string letterLabel;
        public string letterText;
        public string letterLabelF;
        public string letterTextF;
        public bool letterOnlyIfPlayerTarget;
        public float rallyRadius = -1f;
        public int maxRallyTicks;
        public int additionalCastingTicks;
        public float allyMultiplier;
    }
    public class CasterPersonality : DefModExtension
    {
        public CasterPersonality()
        {

        }
        public float niceOrEvil;
        public float niceToAnimals;
        public bool pacifist;
        public bool noFlames;
        public bool yesFlames;
        public bool noLight;
        public bool proDisease;
        public bool proScar;
        public bool proPain;
    }
    public class PsycastPermissiveMentalState : DefModExtension
    {
        public PsycastPermissiveMentalState()
        {

        }
        public int id;
    }
    public class LimitsHVPAACasting : DefModExtension
    {
        public LimitsHVPAACasting()
        {

        }
        public float percent;
    }
    public class SensitizeScalar : DefModExtension
    {
        public SensitizeScalar()
        {

        }
    }
    //faction stuff
    public class FactionPsycasterRuleDef : Def
    {
        public override void ResolveReferences()
        {
            base.ResolveReferences();
        }
        public bool respectsTitleMinPsylevel = true;
        public List<float> bonusCastChance = new List<float>();
        public float randCastersPerCapita = 0.0025f;
        public int avgRandCasterLevel = 2;
        public Dictionary<HediffDef, float> randCasterHediffs;
        public int maxRandCasterHediffs = 1;
        public Dictionary<ThingDef, float> randCasterItems;
        public int maxRandCasterItems = 1;
        public Dictionary<ThingDef, float> randCasterEquipment;
        public int maxRandCasterEquipment = 1;
        //spec casters
        public int maxDomesticPower;
        public List<PawnKindDef> domesticSpecCasters;
        public float specPointsPerRaidPoint;
        public float specChanceInRaids;
        public List<PawnKindDef> raidSpecCasters;
        //sellcasts
        public bool offersSellcasts;
        public int maxSellcastPsylinkLevel = 6;
    }
    public class SpecificPsycasterRules : DefModExtension
    {
        public SpecificPsycasterRules()
        {

        }
        public FactionPsycasterRuleDef fprd;
    }
    public class AddedSpecPsycasters : DefModExtension
    {
        public AddedSpecPsycasters()
        {

        }
        public int pointCost;
        public float domesticChance;
        public IntRange domesticCount;
        public float raidChance;
        public IntRange raidCount;
        public IntRange minPsylinkLevel = new IntRange(1, 1);
        public int bonusPsycasts;
        public int combatCastIntervalOverride;
        public float unwaveringLoyaltyChance = 0.98f;
    }
    //sellcasts
    public static class SellcastHiringSession
    {
        public static bool Active
        {
            get
            {
                return SellcastHiringSession.trader != null;
            }
        }
        public static int Goodwill
        {
            get
            {
                if (SellcastHiringSession.trader != null && SellcastHiringSession.trader.Faction != null)
                {
                    return Faction.OfPlayerSilentFail.GoodwillWith(SellcastHiringSession.trader.Faction);
                }
                return 0;
            }
        }
        public static FactionPsycasterRuleDef FPRD
        {
            get
            {
                if (SellcastHiringSession.trader != null && SellcastHiringSession.trader.Faction != null)
                {
                    return HVPAAUtility.GetPsycasterRules(SellcastHiringSession.trader.Faction.def);
                }
                return null;
            }
        }
        public static void SetupWith(ITrader newTrader, Pawn newPlayerNegotiator)
        {
            if (!newTrader.CanTradeNow)
            {
                Log.Warning("Called SetupWith with a trader not willing to trade now.");
            }
            SellcastHiringSession.trader = newTrader;
            SellcastHiringSession.playerNegotiator = newPlayerNegotiator;
            SellcastHiringSession.deal = new SellcastHiringDeal();
            if (SellcastHiringSession.deal.cannotSellReasons.Count > 0)
            {
                Messages.Message("MessageCannotSellItemsReason".Translate() + SellcastHiringSession.deal.cannotSellReasons.ToCommaList(true, false).CapitalizeFirst(), MessageTypeDefOf.NegativeEvent, false);
            }
        }
        public static void Close()
        {
            SellcastHiringSession.trader = null;
        }
        public static ITrader trader;
        public static Pawn playerNegotiator;
        public static SellcastHiringDeal deal;
    }
    public class SellcastHiringDeal
    {
        public SellcastHiringDeal()
        {
            this.Reset();
        }
        public void Reset()
        {

        }
        public void UpdateGoodwill()
        {
            if (this.goodwillCost == 0)
            {
                return;
            }
            Faction.OfPlayer.TryAffectGoodwillWith(SellcastHiringSession.trader.Faction, -this.goodwillCost, false, true, HVPAADefOf.HVPAA_HiredSellcaster);
        }
        public bool TryExecute(out bool actuallyTraded, int durationDays, bool discount, int psylinkLevel, Map mapToDeliverTo)
        {
            if (SellcastHiringSession.Goodwill - this.goodwillCost < 0)
            {
                Find.WindowStack.WindowOfType<Dialog_SellcastHiring>().FlashGoodwill();
                SoundDefOf.ClickReject.PlayOneShotOnCamera(null);
                Messages.Message("MessageColonyCannotAfford".Translate(), MessageTypeDefOf.RejectInput, false);
                actuallyTraded = false;
                return false;
            }
            this.UpdateGoodwill();
            actuallyTraded = false;
            actuallyTraded = false;
            if (SellcastHiringSession.trader.Faction != null)
            {
                SellcastHiringSession.trader.Faction.Notify_PlayerTraded(0f, SellcastHiringSession.playerNegotiator);
                Pawn sellcast = HVPAAUtility.GenerateSellcast(SellcastHiringSession.trader.Faction, SellcastHiringSession.FPRD);
                if (sellcast != null)
                {
                    if (sellcast.psychicEntropy != null)
                    {
                        sellcast.psychicEntropy.RechargePsyfocus();
                    }
                    int psylinkDiff = psylinkLevel - sellcast.GetPsylinkLevel();
                    IIncidentTarget iit = Find.AnyPlayerHomeMap;
                    for (int i = 0; i < psylinkDiff; i++)
                    {
                        sellcast.ChangePsylinkLevel(1, false);
                    }
                    if (SellcastHiringSession.playerNegotiator.SpawnedOrAnyParentSpawned)
                    {
                        IntVec3 iv3 = SellcastHiringSession.playerNegotiator.PositionHeld;
                        if (!iv3.IsValid)
                        {
                            iv3 = CellFinder.RandomCell(SellcastHiringSession.playerNegotiator.MapHeld);
                        }
                        GenSpawn.Spawn(sellcast, iv3, SellcastHiringSession.playerNegotiator.MapHeld, WipeMode.VanishOrMoveAside);
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(iv3.ToVector3Shifted(), SellcastHiringSession.playerNegotiator.MapHeld, FleckDefOf.PsycastSkipInnerExit, 1f);
                        dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        SellcastHiringSession.playerNegotiator.MapHeld.flecks.CreateFleck(dataStatic);
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(iv3.ToVector3Shifted(), SellcastHiringSession.playerNegotiator.MapHeld, FleckDefOf.PsycastSkipOuterRingExit, 1f);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        SellcastHiringSession.playerNegotiator.MapHeld.flecks.CreateFleck(dataStatic2);
                        SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(SellcastHiringSession.playerNegotiator.PositionHeld, SellcastHiringSession.playerNegotiator.MapHeld, false));
                        iit = SellcastHiringSession.playerNegotiator.MapHeld;
                    } else if (mapToDeliverTo != null) {
                        CellFinder.TryFindRandomEdgeCellWith((IntVec3 c) => c.IsValid && c.InBounds(mapToDeliverTo) && c.WalkableBy(mapToDeliverTo, sellcast), mapToDeliverTo, 0f, out IntVec3 iv3);
                        if (mapToDeliverTo.mapPawns.AnyColonistSpawned)
                        {
                            iv3 = mapToDeliverTo.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer).RandomElement().PositionHeld;
                        }
                        if (!iv3.IsValid)
                        {
                            iv3 = CellFinder.RandomCell(mapToDeliverTo);
                        }
                        GenSpawn.Spawn(sellcast, iv3, mapToDeliverTo, WipeMode.VanishOrMoveAside);
                        FleckCreationData dataStatic = FleckMaker.GetDataStatic(iv3.ToVector3Shifted(), mapToDeliverTo, FleckDefOf.PsycastSkipInnerExit, 1f);
                        dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        mapToDeliverTo.flecks.CreateFleck(dataStatic);
                        FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(iv3.ToVector3Shifted(), mapToDeliverTo, FleckDefOf.PsycastSkipOuterRingExit, 1f);
                        dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                        dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                        mapToDeliverTo.flecks.CreateFleck(dataStatic2);
                        SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(iv3, mapToDeliverTo, false));
                        iit = mapToDeliverTo;
                    }
                    Messages.Message("HVPAA_SellcastConfirm".Translate(sellcast.Name.ToStringShort, durationDays), null, MessageTypeDefOf.PositiveEvent, true);
                    this.MakeQuest(HVPAADefOf.HVPAA_HiredSellcasterQuest, sellcast, iit, durationDays, discount);
                }
            }
            this.Reset();
            Pawn pawn = TradeSession.trader as Pawn;
            if (pawn != null)
            {
                TaleRecorder.RecordTale(TaleDefOf.TradedWith, new object[]
                {
                    TradeSession.playerNegotiator,
                    pawn
                });
            }
            if (actuallyTraded)
            {
                TradeSession.playerNegotiator.mindState.inspirationHandler.EndInspiration(InspirationDefOf.Inspired_Trade);
            }
            return true;
        }
        private void MakeQuest(QuestScriptDef questScript, Pawn sellcaster, IIncidentTarget target, int durationDays, bool discount)
        {
            Slate slate = new Slate();
            slate.Set<Pawn>("sellcast", sellcaster, false);
            slate.Set<int>("discounted", discount ? 1 : 0, false);
            if (discount && sellcaster.story != null)
            {
                sellcaster.story.traits.GainTrait(new Trait(HVPAADefOf.HVPAA_SellcastTrait));
            }
            if (durationDays < 1)
            {
                durationDays = 1;
            }
            slate.Set<int>("days", durationDays, false);
            slate.Set<Faction>("sellcastFaction", sellcaster.Faction, false);
            if (!questScript.CanRun(slate, target))
            {
                return;
            }
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questScript, slate);
            if (sellcaster.guest != null)
            {
                sellcaster.guest.SetGuestStatus(null, GuestStatus.Guest);
            }
            sellcaster.SetFaction(Faction.OfPlayerSilentFail);
            if (sellcaster.guest != null)
            {
                sellcaster.guest.Notify_PawnRecruited();
            }
            Find.LetterStack.ReceiveLetter(quest.name, quest.description, LetterDefOf.PositiveEvent, null, null, quest, null, null, 0, true);
        }
        public List<string> cannotSellReasons = new List<string>();
        public int goodwillCost;
    }

    [StaticConstructorOnStartup]
    public class Dialog_SellcastHiring : Window
    {
        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(1024f, (float)UI.screenHeight*0.55f);
            }
        }
        private PlanetTile Tile
        {
            get
            {
                return TradeSession.playerNegotiator.Tile;
            }
        }
        private BiomeDef Biome
        {
            get
            {
                return Find.WorldGrid[this.Tile].PrimaryBiome;
            }
        }
        public Dialog_SellcastHiring(Pawn playerNegotiator, ITrader trader, Map mapToDeliverTo)
            : base(null)
        {
            SellcastHiringSession.SetupWith(trader, playerNegotiator);
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
            this.soundAppear = SoundDefOf.CommsWindow_Open;
            this.soundClose = SoundDefOf.CommsWindow_Close;
            this.HVPAA_MapToGoTo = mapToDeliverTo;
            if (trader is PassingShip)
            {
                this.soundAmbient = SoundDefOf.RadioComms_Ambience;
            }
            this.commonSearchWidgetOffset.x = this.commonSearchWidgetOffset.x + 18f;
            this.commonSearchWidgetOffset.y = this.commonSearchWidgetOffset.y - 18f;
        }
        public override void PreOpen()
        {
            base.PreOpen();
            this.HVPAA_MenuLabel = HautsUtility.IsHighFantasy() ? "HVPAA_SellcastLabelF".Translate() : "HVPAA_SellcastLabel".Translate();
            this.HVPAA_DurationLabel = "HVPAA_SellcastDurationLabel".Translate();
            this.HVPAA_DurationText = "HVPAA_SellcastDurationText".Translate();
            this.HVPAA_PsylinkLabel = HautsUtility.IsHighFantasy() ? "HVPAA_SellcastPsylinkLabelF".Translate() : "HVPAA_SellcastPsylinkLabel".Translate();
            this.HVPAA_PsylinkText = HautsUtility.IsHighFantasy() ? "HVPAA_SellcastPsylinkTextF".Translate() : "HVPAA_SellcastPsylinkText".Translate();
            this.maxPsylinkLevel = SellcastHiringSession.FPRD.maxSellcastPsylinkLevel;
            this.HVPAA_DiscountLabel = "HVPAA_SellcastDiscountLabel".Translate();
            this.HVPAA_DiscountText = "HVPAA_SellcastDiscountText".Translate();
            this.durationDays = 6;
            this.psylinkLevel = 1;
        }
        public override void PostOpen()
        {
            base.PostOpen();
            this.RecalcCosts();
        }
        public override void DoWindowContents(Rect inRect)
        {
            Widgets.BeginGroup(inRect);
            inRect = inRect.AtZero();
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;
            float num2 = inRect.width - 590f;
            Rect rect = new Rect(num2, 40f, inRect.width - num2, 58f);
            Widgets.Label(new Rect(rect.width*0.15f, 5f, inRect.width *0.85f, inRect.height / 2f), this.HVPAA_MenuLabel);
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(rect.width * 0.15f, 45f, rect.width / 2f, rect.height / 2f), "HVPAA_GoodwillCost".Translate(this.goodwillCost.ToStringCached(), SellcastHiringSession.Goodwill.ToStringCached()));
            Widgets.EndGroup();
            float num = 90f;
            this.daysText = this.durationDays.ToString();
            int origR = this.durationDays;
            Rect rectSlider = new Rect(inRect.width * 0.1f, num, inRect.width * 0.8f, 32);
            this.durationDays = (int)Widgets.HorizontalSlider(rectSlider, this.durationDays, 6f, 60f, true, this.HVPAA_DurationLabel, "6 days", "60 days", 6f);
            TooltipHandler.TipRegion(rectSlider.LeftPart(0.4f), this.HVPAA_DurationText);
            if (origR != this.durationDays)
            {
                this.daysText = this.durationDays.ToString() + " days";
                this.RecalcCosts();
            }
            num += 32f;
            string origStringR = this.daysText;
            this.daysText = Widgets.TextField(new Rect(inRect.width * 0.1f, num, 50, 32), this.daysText);
            if (!this.daysText.Equals(origStringR))
            {
                this.ParseInput(this.daysText, this.durationDays, out this.durationDays);
            }
            num += 50f;
            this.levelText = this.psylinkLevel.ToString();
            int origR2 = this.psylinkLevel;
            Rect rectSlider2 = new Rect(inRect.width * 0.1f, num, inRect.width * 0.8f, 32);
            this.psylinkLevel = (int)Widgets.HorizontalSlider(rectSlider2, this.psylinkLevel, 1f, this.maxPsylinkLevel, true, this.HVPAA_PsylinkLabel, "1", this.maxPsylinkLevel.ToString(), 1f);
            TooltipHandler.TipRegion(rectSlider2.LeftPart(1f), this.HVPAA_PsylinkText);
            if (origR2 != this.psylinkLevel)
            {
                this.levelText = "Level" + this.psylinkLevel.ToString();
                this.RecalcCosts();
            }
            num += 32f;
            string origStringR2 = this.levelText;
            this.levelText = Widgets.TextField(new Rect(inRect.width * 0.1f, num, 50, 32), this.levelText);
            if (!this.levelText.Equals(origStringR))
            {
                this.ParseInput(this.levelText, this.psylinkLevel, out this.psylinkLevel);
            }
            num += 40;
            Rect inRect2 = new Rect(inRect.width * 0.1f, num, inRect.width * 0.8f, num + 60);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect2);
            listingStandard.CheckboxLabeled(this.HVPAA_DiscountLabel, ref this.discount, this.HVPAA_DiscountText);
            listingStandard.End();
            GUI.color = Color.gray;
            Widgets.DrawLineHorizontal(0f, inRect.height - 61f, inRect.width);
            GUI.color = Color.white;

            Text.Font = GameFont.Small;
            Rect rect7 = new Rect(inRect.width / 2f - Dialog_SellcastHiring.AcceptButtonSize.x / 2f, inRect.height - 55f, Dialog_SellcastHiring.AcceptButtonSize.x, Dialog_SellcastHiring.AcceptButtonSize.y);
            if (Widgets.ButtonText(rect7, "AcceptButton".Translate(), true, true, true, null))
            {
                Action action = delegate
                {
                    if (SellcastHiringSession.deal.TryExecute(out bool flag, this.durationDays, this.discount, this.psylinkLevel, this.HVPAA_MapToGoTo))
                    {
                        if (flag)
                        {
                            SoundDefOf.ExecuteTrade.PlayOneShotOnCamera(null);
                            this.Close(false);
                            return;
                        }
                        this.Close(true);
                    }
                };
                action();
                Event.current.Use();
            }
            if (Widgets.ButtonText(new Rect(rect7.xMax + 10f, rect7.y, Dialog_SellcastHiring.AcceptButtonSize.x, Dialog_SellcastHiring.AcceptButtonSize.y), "CancelButton".Translate(), true, true, true, null))
            {
                this.Close(true);
                Event.current.Use();
            }
            Widgets.EndGroup();
        }
        public override void Close(bool doCloseSound = true)
        {
            DragSliderManager.ForceStop();
            base.Close(doCloseSound);
            Pawn pawn = TradeSession.trader as Pawn;
            if (pawn != null && pawn.mindState.hasQuest)
            {
                TradeUtility.ReceiveQuestFromTrader(pawn, TradeSession.playerNegotiator);
            }
        }
        private void ParseInput(string buffer, float origValue, out int newValue)
        {
            if (!int.TryParse(buffer, out newValue))
                newValue = (int)origValue;
            if (newValue < 0)
                newValue = (int)origValue;
            this.RecalcCosts();
        }
        private void RecalcCosts()
        {
            this.goodwillCost = this.psylinkLevel;
            this.goodwillCost *= (int)(Math.Ceiling(5f * this.durationDays * (this.discount ? 0.4f : 1f) / 6f));
            SellcastHiringSession.deal.goodwillCost = this.goodwillCost;
        }
        public void FlashGoodwill()
        {
            Dialog_SellcastHiring.lastGoodwillFlashTime = Time.time;
        }
        public override bool CausesMessageBackground()
        {
            return true;
        }
        private Map HVPAA_MapToGoTo;
        private string HVPAA_MenuLabel;
        private string HVPAA_DurationLabel;
        private string HVPAA_DurationText;
        private string HVPAA_PsylinkLabel;
        private string HVPAA_PsylinkText;
        private string HVPAA_DiscountLabel;
        private string HVPAA_DiscountText;
        private int maxPsylinkLevel;
        private int goodwillCost;
        private int durationDays;
        private int psylinkLevel;
        private bool discount;
        private string daysText;
        private string levelText;
        public static float lastGoodwillFlashTime = -100f;
        protected static readonly Vector2 AcceptButtonSize = new Vector2(160f, 40f);
    }
    public class QuestNode_Sellcast_Etc : QuestNode
    {
        protected override void RunInt()
        {
            QuestGen.quest.Delay(this.days.GetValue(QuestGen.slate) * 60000, delegate
            {
                Faction faction3 = this.faction.GetValue(QuestGen.slate);
                Action action6 = null;
                Action action7 = delegate
                {
                    QuestGen.quest.Letter(LetterDefOf.PositiveEvent, null, null, null, null, false, QuestPart.SignalListenMode.OngoingOnly, null, false, "[lodgersLeavingLetterText]", null, "[lodgersLeavingLetterLabel]", null, null);
                };
                QuestGen.quest.SignalPassWithFaction(faction3, action6, action7, null, null);
                HVPAAUtility.SkipOutPawnPart(QuestGen.quest,pawns.GetValue(QuestGen.slate), null, false, false, null, true);
            }, null, null, null, false, null, null, false, "GuestsDepartsIn".Translate(), "GuestsDepartsOn".Translate(), "QuestDelay", false, QuestPart.SignalListenMode.OngoingOnly, false);
        }
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }
        public SlateRef<int> days;
        public SlateRef<Faction> faction;
        public SlateRef<IEnumerable<Pawn>> pawns;
    }
    public class QuestNode_SkipOutOnCleanup : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            IEnumerable<Pawn> value = this.pawns.GetValue(slate);
            if (value.EnumerableNullOrEmpty<Pawn>())
            {
                return;
            }
            QuestPart_SkipOutOnCleanup qpsooc = new QuestPart_SkipOutOnCleanup();
            qpsooc.pawns.AddRange(value);
            qpsooc.sendStandardLetter = this.sendStandardLetter.GetValue(slate) ?? qpsooc.sendStandardLetter;
            qpsooc.leaveOnCleanup = true;
            qpsooc.inSignalRemovePawn = this.inSignalRemovePawn.GetValue(slate);
            QuestGen.quest.AddPart(qpsooc);
        }
        public SlateRef<IEnumerable<Pawn>> pawns;
        public SlateRef<bool?> sendStandardLetter;
        [NoTranslate]
        public SlateRef<string> inSignalRemovePawn;
    }
    public class QuestPart_SkipOutOnCleanup : QuestPart
    {
        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (GlobalTargetInfo globalTargetInfo in base.QuestLookTargets)
                {
                    yield return globalTargetInfo;
                }
                foreach (Pawn pawn in PawnsArriveQuestPartUtility.GetQuestLookTargets(this.pawns))
                {
                    yield return pawn;
                }
                yield break;
            }
        }
        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            Pawn pawn;
            if (signal.tag == this.inSignalRemovePawn && signal.args.TryGetArg<Pawn>("SUBJECT", out pawn) && this.pawns.Contains(pawn))
            {
                this.pawns.Remove(pawn);
            }
            if (signal.tag == this.inSignal)
            {
                HVPAAUtility.SkipOutPawn(this.pawns, this.sendStandardLetter, this.quest);
            }
        }
        public override void Cleanup()
        {
            base.Cleanup();
            if (this.leaveOnCleanup)
            {
                HVPAAUtility.SkipOutPawn(this.pawns, this.sendStandardLetter, this.quest);
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<string>(ref this.inSignal, "inSignal", null, false);
            Scribe_Collections.Look<Pawn>(ref this.pawns, "pawns", LookMode.Reference, Array.Empty<object>());
            Scribe_Values.Look<bool>(ref this.sendStandardLetter, "sendStandardLetter", true, false);
            Scribe_Values.Look<bool>(ref this.leaveOnCleanup, "leaveOnCleanup", false, false);
            Scribe_Values.Look<string>(ref this.inSignalRemovePawn, "inSignalRemovePawn", null, false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                this.pawns.RemoveAll((Pawn x) => x == null);
            }
        }
        public override void AssignDebugData()
        {
            base.AssignDebugData();
            this.inSignal = "DebugSignal" + Rand.Int;
            if (Find.AnyPlayerHomeMap != null)
            {
                Map randomPlayerHomeMap = Find.RandomPlayerHomeMap;
                if (randomPlayerHomeMap.mapPawns.FreeColonistsCount != 0)
                {
                    this.pawns.Add(randomPlayerHomeMap.mapPawns.FreeColonists.First<Pawn>());
                }
            }
        }
        public override void ReplacePawnReferences(Pawn replace, Pawn with)
        {
            this.pawns.Replace(replace, with);
        }
        public string inSignal;
        public List<Pawn> pawns = new List<Pawn>();
        public bool sendStandardLetter = true;
        public bool leaveOnCleanup = true;
        public string inSignalRemovePawn;
    }
    public class QuestNode_EndSellcast : QuestNode
    {
        protected override bool TestRunInt(Slate slate)
        {
            return true;
        }
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            int value = this.goodwillChangeAmount.GetValue(slate);
            Faction value2 = this.goodwillChangeFactionOf.GetValue(slate);
            if (value != 0 && value2 != null)
            {
                QuestPart_FactionGoodwillChange questPart_FactionGoodwillChange = new QuestPart_FactionGoodwillChange();
                questPart_FactionGoodwillChange.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(this.inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal", null, false);
                questPart_FactionGoodwillChange.faction = value2;
                questPart_FactionGoodwillChange.change = value;
                questPart_FactionGoodwillChange.historyEvent = this.goodwillChangeReason.GetValue(slate);
                slate.Set<string>("goodwillPenalty", Mathf.Abs(value).ToString(), false);
                QuestGen.quest.AddPart(questPart_FactionGoodwillChange);
            }
            QuestPart_QuestEnd questPart_QuestEnd = new QuestPart_QuestEnd();
            questPart_QuestEnd.inSignal = QuestGenUtility.HardcodedSignalWithQuestID(this.inSignal.GetValue(slate)) ?? QuestGen.slate.Get<string>("inSignal", null, false);
            questPart_QuestEnd.outcome = new QuestEndOutcome?(this.outcome.GetValue(slate));
            questPart_QuestEnd.signalListenMode = this.signalListenMode.GetValue(slate) ?? QuestPart.SignalListenMode.OngoingOnly;
            questPart_QuestEnd.sendLetter = this.sendStandardLetter.GetValue(slate) ?? false;
            QuestGen.quest.AddPart(questPart_QuestEnd);
        }
        [NoTranslate]
        public SlateRef<string> inSignal;
        public SlateRef<QuestEndOutcome> outcome;
        public SlateRef<QuestPart.SignalListenMode?> signalListenMode;
        public SlateRef<bool?> sendStandardLetter;
        public SlateRef<int> goodwillChangeAmount;
        public SlateRef<Faction> goodwillChangeFactionOf;
        public SlateRef<HistoryEventDef> goodwillChangeReason;
    }
    //bespoke DMEs for specific psycasts, starting w vanilla level 1 psycasts
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
            if (HVPAAUtility.DebilitatedByLight(p, true, true))
            {
                return false;
            }
            return false;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Spawned)
            {
                if (HVPAAUtility.MovesFasterInLight(p) && !p.Downed)
                {
                    if (p.pather.Moving)
                    {
                        if (useCase == 3)
                        {
                            return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f || HVPAAUtility.DebilitatedByLight(p, true, false);
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
            } else if (useCase == 4) {
                return p.GetStatValue(StatDefOf.ComfyTemperatureMin) - p.AmbientTemperature;
            } else if (useCase == 5) {
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
                                    else if (p2.pather.Moving && HVPAAUtility.MovesFasterInLight(p2))
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
                    } else if (laborers.Count > 0) {
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
    //vanilla lvl2
    public class UseCaseTags_BlindingPulse : UseCaseTags
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
            return p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) * p.GetStatValue(StatDefOf.PsychicSensitivity) * this.sightReduction * (p.CurJob != null && p.CurJob.verbToUse != null ? 2f : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) * p.GetStatValue(StatDefOf.PsychicSensitivity) * this.sightReduction * (p.CurJob != null && p.CurJob.verbToUse != null ? 2f : 1f);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
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
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
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
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
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
        public float sightReduction;
    }
    public class UseCaseTags_NHD : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Faction == null || p.Faction != psycast.pawn.Faction || p.MarketValue > 1000f || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.HasPsylink;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return Math.Max(1000f - p.MarketValue, 0f);
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (!HVPAAUtility.IsSellcastDiscounted(intPsycasts.Pawn) && (psycast.Caster.psychicEntropy.EntropyRelativeValue >= 0.9f || (psycast.Caster.psychicEntropy.EntropyRelativeValue >= 0.8f && Rand.Chance(0.25f))))
            {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    return 1000f;
                }
            }
            return 0f;
        }
    }
    public class CompProperties_AbilityPlayerOnlyTargetColonist : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityPlayerOnlyTargetColonist()
        {
            this.compClass = typeof(CompAbilityEffect_PlayerOnlyTargetColonist);
        }
    }
    public class CompAbilityEffect_PlayerOnlyTargetColonist : CompAbilityEffect
    {
        public new CompProperties_AbilityPlayerOnlyTargetColonist Props
        {
            get
            {
                return (CompProperties_AbilityPlayerOnlyTargetColonist)this.props;
            }
        }
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            return pawn != null && this.parent.pawn.Faction != null && pawn.Faction != null && this.parent.pawn.Faction == pawn.Faction;
        }
    }
    public class UseCaseTags_Waterskip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.Downed && !p.stances.stunner.Stunned && p.GetStatValue(StatDefOf.MoveSpeed) >= 1f;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float fireSize = 0f;
            Fire attachedFire = (Fire)p.GetAttachment(ThingDefOf.Fire);
            if (attachedFire != null)
            {
                fireSize += attachedFire.CurrentSize();
            }
            return p.GetStatValue(StatDefOf.Flammability) * fireSize;
        }
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            Dictionary<IntVec3, float> possibleTargets = new Dictionary<IntVec3, float>();
            Faction f = psycast.pawn.Faction;
            foreach (Fire fire in GenRadial.RadialDistinctThingsAround(intPsycasts.Pawn.Position, intPsycasts.Pawn.Map, this.Range(psycast), true).OfType<Fire>().Distinct<Fire>())
            {
                IntVec3 pos = fire.Position;
                if (pos.Filled(psycast.pawn.Map) || !GenSight.LineOfSight(psycast.pawn.Position, pos, psycast.pawn.Map, true, null, 0, 0))
                {
                    bool goNext = true;
                    for (int i = 0; i < 8; i++)
                    {
                        IntVec3 intVec = pos + GenRadial.RadialPattern[i];
                        if (!intVec.Filled(psycast.pawn.Map) && GenSight.LineOfSight(psycast.pawn.Position, intVec, psycast.pawn.Map, true, null, 0, 0))
                        {
                            pos = intVec;
                            goNext = false;
                            break;
                        }
                    }
                    if (goNext)
                    {
                        continue;
                    }
                }
                if (!possibleTargets.ContainsKey(pos))
                {
                    int numFires = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        IntVec3 intVec = pos + GenRadial.RadialPattern[i];
                        List<Thing> ctl = intVec.GetThingList(psycast.pawn.Map);
                        if (ctl != null)
                        {
                            foreach (Thing t in ctl)
                            {
                                if (t is Fire || t.IsBurning())
                                {
                                    numFires++;
                                }
                                if ((t is Pawn p && intPsycasts.foes.Contains(p)) || (f != null && ((t.Faction != null && f.HostileTo(t.Faction)) || (t is Plant p2 && HVPAAUtility.IsPlantInHostileFactionGrowZone(p2, f)))))
                                {
                                    numFires--;
                                }
                            }
                        }
                    }
                    possibleTargets.Add(pos,numFires);
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
            return IntVec3.Invalid;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return 100f * positionTargets.TryGetValue(position);
            }
            return 0f;
        }
    }
    public class UseCaseTags_WordOfJoy : UseCaseTags
    {
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return 10f * pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.needs.mood == null || p.needs.mood.CurLevel >= p.mindState.mentalBreaker.BreakThresholdMajor || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || PawnCapacityUtility.CalculatePartEfficiency(p.health.hediffSet, p.health.hediffSet.GetBrain(), false, null) < (0.31f - this.brainEfficiencyOffset) || p.InMentalState || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max((p.mindState.mentalBreaker.BreakThresholdMajor - p.needs.mood.CurLevel), 0f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float brainEfficiencyOffset;
    }
    //vanilla lvl3
    public class UseCaseTags_Beckon : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (p.CurJob != null && p.CurJobDef == JobDefOf.GotoMindControlled) || p.Position.DistanceTo(psycast.pawn.Position) < 2f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * HVPAAUtility.ExpectedBeckonTime(p, psycast.pawn) * ((psycast.pawn.equipment != null && (psycast.pawn.equipment.Primary == null || !psycast.pawn.equipment.Primary.def.IsRangedWeapon)) ? 2f : 1f) * ((p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon) ? (1f + CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)) : 1f) / 1000f;
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
    }
    public class UseCaseTags_ChaosSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return HVPAAUtility.SkipImmune(p, this.maxBodySize) || p.stances.stunner.Stunned || p.Downed;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAAUtility.ChaosSkipApplicability(p, psycast);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = psycast.ability.CompOfType<CompAbilityEffect_WithDest>().GetDestination(psycast.lti);
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
    }
    public class UseCaseTags_VertigoPulse   : UseCaseTags
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
            return 4f * p.GetStatValue(StatDefOf.PsychicSensitivity) * (p.CurJob != null && p.CurJob.verbToUse != null ? 1.25f : 1f) * (!p.RaceProps.IsFlesh ? 0.4f : 1f) * (this.Digesting(p) ? 100f : 1f);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.GetStatValue(StatDefOf.PsychicSensitivity) * (p.CurJob != null && p.CurJob.verbToUse != null ? 1.25f : 1f) * (!p.RaceProps.IsFlesh ? 0.4f : 1f) * (this.Digesting(p) ? 2.5f : 1f);
        }
        public bool Digesting(Pawn p)
        {
            if (ModsConfig.AnomalyActive)
            {
                CompDevourer cd;
                if (p.TryGetComp(out cd) && cd.Digesting)
                {
                    return true;
                }
            }
            return false;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
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
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
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
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
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
    }
    public class UseCaseTags_WordOfLove : UseCaseTags
    {
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.GetWorstRelation(pawn, out int worstRelation);
                return this.PawnAllyApplicability(intPsycasts, psycast.ability, pawn, niceToEvil, 4);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.relations == null || !p.RaceProps.Humanlike || p.IsMutant || p.ageTracker.AgeBiologicalYearsFloat < this.minAge || (p == psycast.pawn && Rand.Chance(0.9f)) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || SocialCardUtility.PawnsForSocialInfo(p).Count == 0 || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false)))
            {
                return true;
            }
            return false;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            this.GetWorstRelation(p, out int worstRelation);
            return p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(-1f * worstRelation, 0f);
        }
        public Pawn GetWorstRelation(Pawn p, out int worstRelation)
        {
            Pawn pawn = p;
            worstRelation = 0;
            foreach (Pawn other in SocialCardUtility.PawnsForSocialInfo(p))
            {
                if (other.Spawned && other.Map == p.Map && p.relations.OpinionOf(other) < worstRelation && RelationsUtility.PawnsKnowEachOther(p,other) && (!ModsConfig.AnomalyActive || !other.IsMutant))
                {
                    if (other.ageTracker.AgeBiologicalYearsFloat < this.minAge || (this.mustMatchOrientation && !RelationsUtility.AttractedToGender(p, other.gender)))
                    {
                        continue;
                    }
                    worstRelation = p.relations.OpinionOf(other);
                    pawn = other;
                }
            }
            worstRelation += p.IsPrisoner ? this.prisonerRelationOffset : 0;
            return pawn == p ? null : pawn;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public float minAge;
        public bool mustMatchOrientation;
        public int prisonerRelationOffset;
    }
    //vanilla lvl4
    public class UseCaseTags_Focus : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || (useCase == 5 ? (!p.RaceProps.Humanlike || p.skills == null) : p.WorkTagIsDisabled(WorkTags.Violent)) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.PsychicSensitivity) * Math.Max(0f, Math.Max(0f, (p.health.capacities.GetLevel(PawnCapacityDefOf.Sight) - this.sightCutoff)) + Math.Max(0f, p.health.capacities.GetLevel(PawnCapacityDefOf.Moving) - this.movingCutoff));
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
                    app *= 10 * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
                }
            }
            return app * (p == psycast.pawn && intPsycasts.GetSituation() == 3 ? 2.5f : 1f);
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
        public float sightCutoff;
        public float movingCutoff;
        public float chanceToUtilityCast;
        public int minUtilitySkillLevel;
    }
    public class UseCaseTags_Smokepop : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.IsBurning() || (!p.Downed && p.GetStatValue(StatDefOf.MoveSpeed) >= 1f);
        }
        public override bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            if (p.HostileTo(caster) && useCase == 2)
            {
                return true;
            }
            else if (p.Faction != null && caster.Faction != null && (p.Faction == caster.Faction || (niceToEvil > 0 && p.Faction.RelationKindWith(caster.Faction) == FactionRelationKind.Ally)) && useCase == 3)
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
                return t.MarketValue / 200f;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Thing turret = this.FindBestThingTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Thing, float> thingTargets);
            if (turret != null && turret.SpawnedOrAnyParentSpawned)
            {
                psycast.lti = turret.PositionHeld;
                float netMarketValue = turret.MarketValue / 50f;
                foreach (Thing t in thingTargets.Keys)
                {
                    if (t != turret)
                    {
                        netMarketValue += t.MarketValue / 200f;
                    }
                }
                return netMarketValue;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (psycast.Caster.Position.AnyGas(psycast.Caster.Map, GasType.BlindSmoke))
            {
                return 0f;
            }
            int numShooters = (int)this.ApplicabilityScoreDefense(intPsycasts, psycast, niceToEvil);
            foreach (Pawn p in (List<Pawn>)psycast.Caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                {
                    if (p.Position.DistanceTo(p.Position) <= this.aoe && intPsycasts.allies.Contains(p))
                    {
                        return 0f;
                    }
                    if (p.Position.DistanceTo(p.Position) <= this.Range(psycast.ability) && !p.Position.AnyGas(p.Map, GasType.BlindSmoke))
                    {
                        if (intPsycasts.foes.Contains(p) && GenSight.LineOfSight(psycast.Caster.Position, p.Position, p.Map))
                        {
                            numShooters++;
                        }
                    }
                }
            }
            psycast.lti = psycast.Caster.pather.nextCell.IsValid ? psycast.Caster.pather.nextCell : psycast.Caster.Position;
            return 2f * numShooters;
        }
        public override float Range(Psycast psycast)
        {
            return this.rangeOffset;
        }
    }
    public class UseCaseTags_WordOfSerenity : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase != 1)
            {
                return 0f;
            }
            return base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public Pawn FindMentallyBrokenTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<Pawn,float> pawnTargets, float range = -999, bool initialTarget = true, Thing nonCasterOrigin = null)
        {
            pawnTargets = new Dictionary<Pawn, float>();
            IntVec3 origin = nonCasterOrigin != null ? nonCasterOrigin.PositionHeld : psycast.pawn.Position;
            foreach (Pawn p in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.Range(psycast), true).OfType<Pawn>().Distinct<Pawn>())
            {
                if ((!this.requiresLoS || GenSight.LineOfSight(origin, p.Position, p.Map)) && (!initialTarget || psycast.CanApplyPsycastTo(p)) && !this.OtherAllyDisqualifiers(psycast, p, useCase, initialTarget))
                {
                    float pApplicability = this.PawnAllyApplicability(intPsycasts, psycast, p, niceToEvil, useCase, initialTarget);
                    if (pApplicability > 0f)
                    {
                        CompAbilityEffect_StopMentalState sms = psycast.CompOfType<CompAbilityEffect_StopMentalState>();
                        if (sms != null && sms.PsyfocusCostForTarget(p) <= psycast.pawn.psychicEntropy.CurrentPsyfocus + 0.0005f)
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
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindMentallyBrokenTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets, this.Range(psycast.ability) / 4f);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            Pawn pawn = this.FindMentallyBrokenTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                return pawnTargets.TryGetValue(pawn);
            }
            return 0f;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.InMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || (!p.RaceProps.Humanlike && p.MarketValue < 500) || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            CompAbilityEffect_StopMentalState sms = psycast.CompOfType<CompAbilityEffect_StopMentalState>();
            float smsMulti = 1f;
            if (sms != null)
            {
                switch (sms.TargetMentalBreakIntensity(p))
                {
                    case MentalBreakIntensity.Extreme:
                        smsMulti = useCase == 5 ? 100 : 15;
                        break;
                    case MentalBreakIntensity.Major:
                        smsMulti = useCase == 5 ? 20 : 5;
                        break;
                    default:
                        smsMulti = useCase == 5 ? 2 : 1;
                        break;
                }
            }
            int multi;
            switch (p.MentalStateDef.category)
            {
                case MentalStateCategory.Aggro:
                    multi = 20;
                    break;
                case MentalStateCategory.Malicious:
                    multi = useCase == 5 ? 2 : 0;
                    break;
                default:
                    multi = useCase == 5 ? 1 : 0;
                    break;
            }
            return multi * smsMulti;
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
    }
    public class UseCaseTags_Skip : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (HVPAAUtility.SkipImmune(p, this.maxBodySize))
            {
                return true;
            }
            if (initialTarget)
            {
                switch (useCase)
                {
                    case 2:
                        foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                        {
                            if (p2.HostileTo(p))
                            {
                                return true;
                            }
                        }
                        break;
                    case 3:
                        if (!this.RangedP(p))
                        {
                            return true;
                        }
                        break;
                    default:
                        break;
                }
            }
            if (useCase != 4)
            {
                if (p.Downed)
                {
                    return true;
                }
                if (!initialTarget)
                {
                    if (!this.RangedP(p) || p.WorkTagIsDisabled(WorkTags.Violent))
                    {
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 2:
                    if (!this.RangedP(p))
                    {
                        return true;
                    }
                    break;
                default:
                    break;
            }
            return HVPAAUtility.SkipImmune(p, this.maxBodySize);
        }
        public bool RangedP(Pawn p)
        {
            return p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 2:
                    if (initialTarget)
                    {
                        float cover = 1f;
                        Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.skipRange, false, p);
                        if (pawn != null)
                        {
                            cover = Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(pawn.Position, pawn.Map)), 1f);
                            this.bestDestDeb = pawn.Position;
                            return Math.Max(0f, ((p.GetStatValue(StatDefOf.MeleeDPS) * cover) - pawn.GetStatValue(StatDefOf.MeleeDPS)) * (pawn.Position.DistanceTo(p.Position) / (float)Math.Sqrt(p.GetStatValue(StatDefOf.MoveSpeed))));
                        }
                    } else {
                        return p.GetStatValue(StatDefOf.MeleeDPS);
                    }
                    break;
                case 3:
                    float netFoeMeleeDPS = -p.GetStatValue(StatDefOf.MeleeDPS);
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.WorkTagIsDisabled(WorkTags.Violent) && !p2.Downed && !p2.IsBurning())
                        {
                            if (p2.HostileTo(p))
                            {
                                netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                            } else if (intPsycasts.allies.Contains(p2)) {
                                netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                            }
                        }
                    }
                    this.bestDestDef = psycast.pawn.PositionHeld;
                    if (netFoeMeleeDPS > 0f)
                    {
                        List<Thing> foeTargetCache = new List<Thing>();
                        foeTargetCache.AddRange(from a in p.Map.attackTargetsCache.GetPotentialTargetsFor(p) where !a.ThreatDisabled(p) select a.Thing);
                        this.bestDestDef = CellFinderLoose.GetFallbackDest(p, foeTargetCache, this.skipRange, 2f, 2f, 20, (IntVec3 c) => c.IsValid && (!this.requiresLoS || GenSight.LineOfSight(c, p.Position, intPsycasts.Pawn.Map)));
                    }
                    return Math.Max(0f, netFoeMeleeDPS);
                case 4:
                    if (p.Downed && HealthUtility.TicksUntilDeathDueToBloodLoss(p) <= 10000)
                    {
                        Pawn bestDoctorInRange = null;
                        float bestDoctorLevel = -1f;
                        foreach (Pawn p2 in intPsycasts.allies)
                        {
                            if (p2.CurJobDef == JobDefOf.TendPatient && p2.CurJob.targetA.Pawn != null && p2.CurJob.targetA.Pawn == p)
                            {
                                return 0f;
                            }
                            if (p2.Downed || p2.IsPlayerControlled || p2.HasPsylink || p.Position.DistanceTo(p2.Position) > this.skipRange)
                            {
                                continue;
                            }
                            float doctorLevel = -1f;
                            if (!p2.WorkTagIsDisabled(WorkTags.Caring) || (p2.RaceProps.mechEnabledWorkTypes != null && p2.RaceProps.mechEnabledWorkTypes.Contains(WorkTypeDefOf.Doctor)))
                            {
                                doctorLevel = p2.GetStatValue(StatDefOf.MedicalTendQuality) * p2.GetStatValue(StatDefOf.MedicalTendSpeed);
                            }
                            if (doctorLevel > bestDoctorLevel || (bestDoctorInRange != null && doctorLevel == bestDoctorLevel && p.Position.DistanceTo(p2.Position) <= p.Position.DistanceTo(bestDoctorInRange.Position)))
                            {
                                bestDoctorInRange = p2;
                                bestDoctorLevel = doctorLevel;
                            }
                        }
                        if (bestDoctorInRange != null)
                        {
                            this.bestDestHeal = bestDoctorInRange.Position;
                            CompAbilityEffect_GiveToDoctor gtd = psycast.CompOfType<CompAbilityEffect_GiveToDoctor>();
                            if (gtd != null)
                            {
                                gtd.doctor = bestDoctorInRange;
                            }
                            return bestDoctorLevel;
                        }
                    }
                    break;
                default:
                    break;
            }
            return -1f;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            switch (useCase)
            {
                case 1:
                    Building bestTrap = null;
                    float bestTrapChance = 0f;
                    foreach (Building b in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.skipRange, true).OfType<Building>().Distinct<Building>())
                    {
                        if (b is Building_Trap bpt && (!this.requiresLoS || GenSight.LineOfSight(b.Position, p.Position, p.Map)))
                        {
                            float tsc = this.TrapSpringChance(bpt, p);
                            if (tsc > bestTrapChance)
                            {
                                bestTrapChance = tsc;
                                bestTrap = bpt;
                            }
                        }
                    }
                    if (bestTrap != null)
                    {
                        this.bestDestDmg = bestTrap.Position;
                        return bestTrapChance;
                    }
                    return 0f;
                case 2:
                    if (initialTarget)
                    {
                        float cover = Math.Max(1 + (1.5f * CoverUtility.TotalSurroundingCoverScore(p.Position, p.Map)), 1f);
                        Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets, this.skipRange, false, p);
                        if (pawn != null)
                        {
                            this.bestDestDeb = pawn.Position;
                            return Math.Max(0f, ((pawnTargets.TryGetValue(pawn) * cover) - p.GetStatValue(StatDefOf.MeleeDPS)) * (pawn.Position.DistanceTo(p.Position) / (float)Math.Sqrt(pawn.GetStatValue(StatDefOf.MoveSpeed))));
                        }
                    } else {
                        return 1f / p.GetStatValue(StatDefOf.MeleeDPS);
                    }
                    break;
                case 3:
                    float netFoeMeleeDPS = p.GetStatValue(StatDefOf.MeleeDPS);
                    bool anyNearbyAllies = false;
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.Downed && !p2.IsBurning() && !p2.WorkTagIsDisabled(WorkTags.Violent))
                        {
                            if (intPsycasts.allies.Contains(p2))
                            {
                                anyNearbyAllies = true;
                                netFoeMeleeDPS -= p2.GetStatValue(StatDefOf.MeleeDPS);
                            } else if (intPsycasts.foes.Contains(p2)) {
                                netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                            }
                        }
                    }
                    if (anyNearbyAllies)
                    {
                        List<Thing> foeTargetCache = new List<Thing>();
                        foeTargetCache.AddRange(from a in p.Map.attackTargetsCache.GetPotentialTargetsFor(p) where !a.ThreatDisabled(p) select a.Thing);
                        this.bestDestDef = CellFinderLoose.GetFallbackDest(p, foeTargetCache, this.skipRange, 2f, 2f, 20, (IntVec3 c) => c.IsValid && (!this.requiresLoS || GenSight.LineOfSight(c, p.Position, intPsycasts.Pawn.Map)));
                        return netFoeMeleeDPS;
                    }
                    break;
                default:
                    break;
            }
            return 0f;
        }
        public float TrapSpringChance(Building_Trap bpt, Pawn p)
        {
            float num = 1f;
            if (p.kindDef.immuneToTraps)
            {
                return 0f;
            }
            if (bpt.KnowsOfTrap(p))
            {
                if (p.Faction == null)
                {
                    if (p.IsAnimal)
                    {
                        num = 0.2f;
                        num *= bpt.def.building.trapPeacefulWildAnimalsSpringChanceFactor;
                    }
                    else
                    {
                        num = 0.3f;
                    }
                }
                else if (p.Faction == bpt.Faction)
                {
                    num = 0.005f;
                }
                else
                {
                    num = 0f;
                }
            }
            num *= bpt.GetStatValue(StatDefOf.TrapSpringChance, true, -1) * p.GetStatValue(StatDefOf.PawnTrapSpringChance, true, -1);
            return Mathf.Clamp01(num);
        }
        public override float PriorityScoreDefense(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase == 3 || situationCase == 5)
            {
                return 1f;
            }
            return base.PriorityScoreDefense(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDmg = IntVec3.Invalid;
            Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 1, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null)
            {
                psycast.lti = pawn;
                psycast.ltiDest = (this.bestDestDmg.IsValid ? this.bestDestDmg : psycast.Caster.Position);
                return 2f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
            }
            return 0f;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDeb = IntVec3.Invalid;
            int situation = intPsycasts.GetSituation();
            if (Rand.Chance(0.5f))
            {
                Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    psycast.ltiDest = (this.bestDestDeb.IsValid ? this.bestDestDeb : psycast.Caster.Position);
                    return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                }
            } else {
                Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
                if (pawn != null)
                {
                    psycast.lti = pawn;
                    psycast.ltiDest = (this.bestDestDeb.IsValid ? this.bestDestDeb : psycast.Caster.Position);
                    return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreDefense(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestDef = IntVec3.Invalid;
            int situation = intPsycasts.GetSituation();
            if (situation == 3 || situation == 5)
            {
                if (intPsycasts.Pawn.pather != null && intPsycasts.Pawn.pather.curPath != null && intPsycasts.Pawn.pather.curPath.Found)
                {
                    int pathDistance = 0;
                    for (int i = 1; i < intPsycasts.Pawn.pather.curPath.NodesLeftCount - 1; i++)
                    {
                        pathDistance++;
                        if ((!this.requiresLoS || GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map)) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                        {
                            this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                        }
                    }
                    psycast.lti = intPsycasts.Pawn;
                    psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                    return pathDistance * this.scoreFactor;
                }
            } else {
                if (Rand.Chance(0.5f))
                {
                    Pawn pawn = this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null && this.bestDestDef.IsValid)
                    {
                        psycast.lti = pawn;
                        psycast.ltiDest = this.bestDestDef;
                        return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                    }
                } else {
                    Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
                    if (pawn != null)
                    {
                        psycast.lti = pawn;
                        psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                        return 3f * pawnTargets.TryGetValue(pawn) * this.scoreFactor;
                    }
                }
            }
            return 0f;
        }
        public override float ApplicabilityScoreHealing(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.bestDestHeal = IntVec3.Invalid;
            Pawn pawn = this.FindAllyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 4, out Dictionary<Pawn, float> pawnTargets);
            if (pawn != null && this.bestDestHeal.IsValid)
            {
                psycast.lti = pawn;
                psycast.ltiDest = this.bestDestHeal;
                return 2f * pawnTargets.TryGetValue(pawn) * 5000f * this.scoreFactor / HealthUtility.TicksUntilDeathDueToBloodLoss(pawn);
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            int situation = intPsycasts.GetSituation();
            if (situation != 2 && intPsycasts.Pawn.pather != null && intPsycasts.Pawn.pather.curPath != null && intPsycasts.Pawn.pather.curPath.Found)
            {
                int pathDistance = 0;
                for (int i = 0; i < intPsycasts.Pawn.pather.curPath.NodesLeftCount; i++)
                {
                    pathDistance++;
                    if ((!this.requiresLoS || GenSight.LineOfSight(intPsycasts.Pawn.Position, intPsycasts.Pawn.pather.curPath.Peek(i), intPsycasts.Pawn.Map)) && intPsycasts.Pawn.pather.curPath.Peek(i).InHorDistOf(intPsycasts.Pawn.Position, this.Range(psycast.ability)))
                    {
                        this.bestDestDef = intPsycasts.Pawn.pather.curPath.Peek(i);
                    }
                }
                psycast.lti = intPsycasts.Pawn;
                psycast.ltiDest = (this.bestDestDef.IsValid ? this.bestDestDef : psycast.Caster.Position);
                return pathDistance * this.scoreFactor;
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public IntVec3 bestDestDmg;
        public IntVec3 bestDestDeb;
        public IntVec3 bestDestDef;
        public IntVec3 bestDestHeal;
        public float skipRange;
        public float scoreFactor = 1f;
    }
    public class CompProperties_AbilityGiveToDoctor : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityGiveToDoctor()
        {
            this.compClass = typeof(CompAbilityEffect_GiveToDoctor);
        }
    }
    public class CompAbilityEffect_GiveToDoctor : CompAbilityEffect
    {
        public new CompProperties_AbilityGiveToDoctor Props
        {
            get
            {
                return (CompProperties_AbilityGiveToDoctor)this.props;
            }
        }
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (target.Pawn != null && doctor != null && !doctor.DeadOrDowned && doctor.CurJobDef != JobDefOf.TendPatient && !doctor.IsPlayerControlled && !doctor.HasPsylink)
            {
                doctor.jobs.StartJob(JobMaker.MakeJob(JobDefOf.TendPatient, target.Pawn, HealthAIUtility.FindBestMedicine(doctor, target.Pawn, true)), JobCondition.InterruptForced);
                doctor = null;
            }
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look<Pawn>(ref this.doctor, "doctor", false);
        }
        public Pawn doctor;
    }
    //vanilla lvl5
    public class UseCaseTags_Berserk : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.stances.stunner.Stunned || p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.InAggroMentalState;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAAUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil);
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
                    HVPAAUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, this.aoe, ref tryNewScore);
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
                } else {
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
    //vanilla lvl6
    public class UseCaseTags_BerserkPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.InAggroMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.InAggroMentalState || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAAUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil, false);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * HVPAAUtility.BerserkApplicability(intPsycasts, p, psycast, niceToEvil, false);
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
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
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
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
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                            }
                            if (pTargetHits > bestTargetHits)
                            {
                                bestTargetPos = randAoE1;
                                bestTargetHits = pTargetHits;
                                psycast.lti = bestTargetPos;
                                return bestTargetHits * Math.Max((intPsycasts.foes.Count - intPsycasts.allies.Count) / 15f, 1f);
                            }
                        }
                        psycast.lti = bestTarget;
                        return bestTargetHits * Math.Max((intPsycasts.foes.Count - intPsycasts.allies.Count) / 15f, 1f);
                    }
                }
            }
            return 0f;
        }
    }
    public class UseCaseTags_MassChaosSkip : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return HVPAAUtility.SkipImmune(p, this.maxBodySize) || p.stances.stunner.Stunned || p.Downed;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return HVPAAUtility.SkipImmune(p, this.maxBodySize);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAAUtility.ChaosSkipApplicability(p, psycast);
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return HVPAAUtility.ChaosSkipApplicability(p, psycast);
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 2, out Dictionary<Pawn, float> pawnTargets);
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
                        float pTargetHits = this.scoreOffset;
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
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
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
                            float pTargetHits = this.scoreOffset;
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
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
                                        pTargetHits -= this.PawnAllyApplicability(intPsycasts, psycast.ability, p2, niceToEvil, 2);
                                    }
                                }
                            }
                            if (pTargetHits > bestTargetHits)
                            {
                                bestTargetPos = randAoE1;
                                bestTargetHits = pTargetHits;
                                psycast.lti = bestTargetPos;
                                psycast.ltiDest = psycast.ability.CompOfType<CompAbilityEffect_WithDest>().GetDestination(psycast.lti);
                                return bestTargetHits;
                            }
                        }
                        psycast.lti = bestTarget;
                        psycast.ltiDest = psycast.ability.CompOfType<CompAbilityEffect_WithDest>().GetDestination(psycast.lti);
                        return bestTargetHits;
                    }
                }
            }
            return 0f;
        }
        public float maxBodySize = 3.5f;
        public float scoreOffset;
    }
    public class UseCaseTags_MHPulse : UseCaseTags
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
                    if (tryNewPosition.IsValid && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = -10f;
                    foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.aoe, true).OfType<Pawn>().Distinct<Pawn>())
                    {
                        if (!p2.AnimalOrWildMan() || (p2.InMentalState && (p2.MentalStateDef == MentalStateDefOf.Manhunter || p2.MentalStateDef == MentalStateDefOf.ManhunterPermanent)) || p2.Downed || p2.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon)
                        {
                            continue;
                        }
                        if (intPsycasts.allies.Contains(p2))
                        {
                            tryNewScore -= p2.MarketValue / 500f;
                        }
                        tryNewScore += HVPAAUtility.BerserkApplicability(intPsycasts, p2, psycast, niceToEvil, false, true);
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
    public class UseCaseTags_Neuroquake : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (psycast.pawn.Faction == null || !psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail) || (situationCase != 1 && situationCase != 3))
            {
                return 0f;
            }
            if (pacifist)
            {
                return 0f;
            }
            switch (situationCase)
            {
                case 1:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
                        return 1.7f;
                    }
                case 3:
                    if (niceToEvil > 0f)
                    {
                        return 1f;
                    }
                    else if (niceToEvil < 0f)
                    {
                        return 2f;
                    }
                    else
                    {
                        return 1.7f;
                    }
                default:
                    return 0f;
            }
        }
        public override float ApplicabilityScoreDamage(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            if (Rand.Chance(this.chancePerEvil * (-niceToEvil - this.minEvil)) && (intPsycasts.continuousTimeSpawned > 5000 || !HVPAA_Mod.settings.powerLimiting))
            {
                psycast.lti = new LocalTargetInfo(intPsycasts.Pawn);
                float score = 1f;
                int situation = intPsycasts.GetSituation();
                foreach (Pawn p in intPsycasts.allies)
                {
                    if (p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe && !p.kindDef.isBoss)
                    {
                        if (this.ShouldRally(psycast.ability, p, situation))
                        {
                            score += p.MarketValue / (niceToEvil > 0f ? 250f : 1000f);
                        }
                        else
                        {
                            score -= (p.MarketValue / (niceToEvil > 0f ? 250f : 1000f));
                            score += HVPAAUtility.BerserkApplicability(intPsycasts, p, psycast.ability, niceToEvil, false);
                        }
                    }
                }
                foreach (Pawn p in intPsycasts.Pawn.Map.mapPawns.AllPawnsSpawned)
                {
                    if (!intPsycasts.allies.Contains(p) && p.Position.DistanceTo(intPsycasts.Pawn.Position) <= this.aoe && !p.kindDef.isBoss)
                    {
                        score += HVPAAUtility.BerserkApplicability(intPsycasts, p, psycast.ability, niceToEvil, false);
                    }
                }
                return score;
            }
            return 0f;
        }
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            return intPsycasts.GetSituation() != 3 ? 0f : this.ApplicabilityScoreDamage(intPsycasts, psycast, niceToEvil);
        }
        public override bool ShouldRally(Psycast psycast, Pawn p, int situation)
        {
            return p != psycast.pawn && (p.Position.DistanceTo(psycast.pawn.Position) - this.rallyRadius) / p.GetStatValue(StatDefOf.MoveSpeed) <= psycast.def.verbProperties.warmupTime;
        }
        public float minEvil;
        public float chancePerEvil;
    }
    public class UseCaseTags_Skipshield : UseCaseTags
    {
        public override IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            IntVec3 bestPosition = IntVec3.Invalid;
            List<Pawn> allyMelee = new List<Pawn>();
            List<Thing> allyShooters = new List<Thing>();
            List<Pawn> foeMelee = new List<Pawn>();
            List<Thing> foeShooters = new List<Thing>();
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(psycast.pawn.Position, psycast.pawn.Map, this.aoe, true))
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
                    else if (HVPAAUtility.IsAlly(intPsycasts.niceToAnimals <= 0, psycast.pawn, t, niceToEvil))
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
                        else
                        {
                            allyMelee.Add(p);
                        }
                    }
                    else if (intPsycasts.foes.Contains(p))
                    {
                        if (p.equipment != null && p.equipment.Primary != null && p.equipment.Primary.def.IsRangedWeapon)
                        {
                            foeShooters.Add(p);
                        }
                        else
                        {
                            foeMelee.Add(p);
                        }
                    }
                }
                int outgunned = Math.Max(foeShooters.Count - allyShooters.Count, allyMelee.Count);
                if (allyMelee.Count > 0 && foeShooters.Count > 0 && outgunned > 0)
                {
                    foreach (Pawn p in allyMelee)
                    {
                        Thing foe = foeShooters.RandomElement();
                        float percent = (Rand.Value + Rand.Value) / 2f;
                        int x = Math.Min(p.Position.x, foe.Position.x) + (int)(Rand.Value * Math.Abs(p.Position.x - foe.Position.x));
                        int z = Math.Min(p.Position.z, foe.Position.z) + (int)(Rand.Value * Math.Abs(p.Position.z - foe.Position.z));
                        IntVec3 randPosBetween = new IntVec3(x, p.Position.y, z);
                        if (randPosBetween.IsValid && !positionTargets.ContainsKey(randPosBetween) && GenSight.LineOfSight(psycast.pawn.Position, randPosBetween, psycast.pawn.Map) && randPosBetween.DistanceTo(intPsycasts.Pawn.Position) <= this.Range(psycast) && !positionTargets.Keys.Contains(randPosBetween))
                        {
                            bool nearbySkipshield = false;
                            foreach (Thing t2 in GenRadial.RadialDistinctThingsAround(randPosBetween, psycast.pawn.Map, 3f, true))
                            {
                                if (t2.def == this.avoidMakingTooMuchOfThing)
                                {
                                    nearbySkipshield = true;
                                    break;
                                }
                            }
                            if (!nearbySkipshield)
                            {
                                positionTargets.Add(randPosBetween, outgunned);
                            }
                        }
                    }
                    if (positionTargets.Count > 0)
                    {
                        bestPosition = positionTargets.Keys.RandomElement();
                    }
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
                return positionTargets.TryGetValue(position) * (intPsycasts.Pawn.equipment != null && intPsycasts.Pawn.equipment.Primary != null && intPsycasts.Pawn.equipment.Primary.def.IsRangedWeapon ? 1f : 1.5f);
            }
            return 0f;
        }
    }
    //used by multiple other mods
    public class UseCaseTags_ArcticPinhole : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Map.glowGrid.GroundGlowAt(p.Position, false, false) > 0.3f)
            {
                return true;
            }
            if (HVPAAUtility.DebilitatedByLight(p, true, true))
            {
                return false;
            }
            return false;
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Spawned)
            {
                if (HVPAAUtility.MovesFasterInLight(p) && !p.Downed)
                {
                    if (p.pather.Moving)
                    {
                        if (useCase == 3)
                        {
                            return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f || HVPAAUtility.DebilitatedByLight(p, true, false);
                        }
                    }
                    if (useCase == 5)
                    {
                        return p.Map.glowGrid.GroundGlowAt(p.Position, false, false) >= 0.3f;
                    }
                }
                if (useCase == 4 && !p.Position.UsesOutdoorTemperature(p.Map))
                {
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
                return p.AmbientTemperature - p.GetStatValue(StatDefOf.ComfyTemperatureMax);
            } else if (useCase == 5) {
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
                                } else if (intPsycasts.foes.Contains(p2)) {
                                    if (!this.OtherEnemyDisqualifiers(psycast.ability, p2, 3))
                                    {
                                        pTargetHits++;
                                    }
                                    else if (p2.pather.Moving && HVPAAUtility.MovesFasterInLight(p2))
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
                float hotTemps = 0f;
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
                            hotTemps += this.PawnAllyApplicability(intPsycasts, psycast.ability, p, niceToEvil, 4);
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
                    if (hotTemps > 0f)
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
                            return hotTemps * this.scoreFactor;
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
    public class UseCaseTags_BloodStaunch : UseCaseTags
    {
        public override float PriorityScoreHealing(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            if (situationCase == 1)
            {
                return 0f;
            }
            return base.PriorityScoreHealing(psycast, situationCase, pacifist, niceToEvil, usableFoci);
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
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return !p.RaceProps.IsFlesh || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !p.Map.reachability.CanReach(psycast.pawn.Position, p.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false));
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return (this.scalesOffPsysens ? Math.Min(p.GetStatValue(StatDefOf.PsychicSensitivity), 2f):1f) * Math.Max(this.ticksToFatalBloodLossCutoff - HealthUtility.TicksUntilDeathDueToBloodLoss(p), 0f);
        }
        public override float Range(Psycast psycast)
        {
            return this.aoe * psycast.pawn.health.capacities.GetLevel(PawnCapacityDefOf.Moving);
        }
        public int ticksToFatalBloodLossCutoff;
        public bool scalesOffPsysens;
    }
    public class UseCaseTags_Dart : UseCaseTags
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
                    if (tryNewPosition.IsValid && !tryNewPosition.Filled(psycast.pawn.Map) && (!tryNewPosition.Roofed(psycast.pawn.Map) || !tryNewPosition.GetRoof(psycast.pawn.Map).isThickRoof) && !positionTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map))
                    {
                        break;
                    }
                }
                if (tryNewPosition.IsValid && !positionTargets.ContainsKey(tryNewPosition))
                {
                    tryNewScore = 1f;
                    foreach (IntVec3 iv3 in GenRadial.RadialCellsAround(tryNewPosition, 0f, this.aoe))
                    {
                        if (iv3.InBounds(psycast.pawn.Map) && GenSight.LineOfSightToEdges(tryNewPosition, iv3, psycast.pawn.Map, true, null))
                        {
                            List<Thing> things = iv3.GetThingList(psycast.pawn.Map);
                            foreach (Thing thing in things)
                            {
                                if (thing == psycast.pawn)
                                {
                                    tryNewScore = 0f;
                                    break;
                                } else if (psycast.pawn.Faction != null) {
                                    if (thing is Building && (thing.Faction == null || !psycast.pawn.Faction.HostileTo(thing.Faction)))
                                    {
                                        tryNewScore = 0f;
                                        break;
                                    } else if (thing is Plant) {
                                        Zone zone = thing.Map.zoneManager.ZoneAt(thing.Position);
                                        if (zone != null && zone is Zone_Growing && !psycast.pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
                                        {
                                            tryNewScore = 0f;
                                            break;
                                        }
                                    }
                                } else if (thing is Pawn p && !psycast.pawn.HostileTo(p)) {
                                    tryNewScore = 0f;
                                }
                            }
                            if (tryNewScore == 0f)
                            {
                                break;
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
        public override float ApplicabilityScoreUtility(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            IntVec3 position = this.FindBestPositionTarget(intPsycasts, psycast.ability, niceToEvil, 5, out Dictionary<IntVec3, float> positionTargets);
            if (position.IsValid)
            {
                psycast.lti = position;
                return positionTargets.TryGetValue(position);
            }
            return 0f;
        }
        public int minFertilizableCells;
    }
    public class UseCaseTags_DurabilityBuff : UseCaseTags
    {
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            float app = p.GetStatValue(StatDefOf.IncomingDamageFactor);
            float netFoeMeleeDPS = 0f;
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (!p2.Downed && !p2.IsBurning() && intPsycasts.foes.Contains(p2) && !p2.WorkTagIsDisabled(WorkTags.Violent))
                {
                    netFoeMeleeDPS += p2.GetStatValue(StatDefOf.MeleeDPS);
                }
            }
            if (netFoeMeleeDPS > 0f)
            {
                return app * netFoeMeleeDPS;
            }
            return p.GetStatValue(StatDefOf.IncomingDamageFactor) * (p.pather.Moving ? p.GetStatValue(StatDefOf.MoveSpeed) / 4f : 1f);
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
    public class UseCaseTags_EMPPulse : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.stances.stunner.Stunned || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !HautsUtility.ReactsToEMP(p);
        }
        public override bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.stances.stunner.Stunned || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || !HautsUtility.ReactsToEMP(p);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue;
        }
        public override float PawnAllyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return this.allyMultiplier * p.MarketValue;
        }
        public override float ApplicabilityScoreDebuff(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, float niceToEvil)
        {
            this.FindEnemyPawnTarget(intPsycasts, psycast.ability, niceToEvil, 3, out Dictionary<Pawn, float> pawnTargets);
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
                                }
                                else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                {
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
                                    }
                                    else if (intPsycasts.allies.Contains(p2) && !this.OtherAllyDisqualifiers(psycast.ability, p2, 2))
                                    {
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
                        return bestTargetHits / 300f;
                    }
                }
            }
            return 0f;
        }
    }
    public class UseCaseTags_Entomb : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            if (p.Downed || p.WorkTagIsDisabled(WorkTags.Violent))
            {
                return true;
            }
            foreach (Pawn p2 in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, 1.42f, true).OfType<Pawn>().Distinct<Pawn>())
            {
                if (p2.HostileTo(p))
                {
                    return true;
                }
            }
            return false;
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            CompAbilityEffect_Wallraise caew = psycast.CompOfType<CompAbilityEffect_Wallraise>();
            if (caew != null && !caew.Valid(new LocalTargetInfo(p.Position), false))
            {
                return 0f;
            }
            if (p.equipment == null || p.equipment.Primary == null || !p.equipment.Primary.def.IsRangedWeapon)
            {
                return p.GetStatValue(StatDefOf.MeleeDPS);
            }
            else
            {
                return 10 * p.GetStatValue(StatDefOf.ShootingAccuracyPawn) * p.GetStatValue(VEFDefOf.VEF_RangeAttackDamageFactor) / (p.GetStatValue(StatDefOf.RangedCooldownFactor) * p.equipment.Primary.GetStatValue(StatDefOf.RangedWeapon_Cooldown));
            }
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
    }
    public class UseCaseTags_FIYAH : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.pather.MovingNow && p.GetStatValue(StatDefOf.MoveSpeed) > this.ignoreAllPawnsFasterThan;
        }
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
                if (tryNewPosition.IsValid && !possibleTargets.ContainsKey(tryNewPosition) && GenSight.LineOfSight(psycast.pawn.Position, tryNewPosition, psycast.pawn.Map, true, null, 0, 0))
                {
                    tryNewScore = 0f;
                    HVPAAUtility.LightningApplicability(this, intPsycasts, psycast, tryNewPosition, niceToEvil, this.aoe, ref tryNewScore);
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
        public float ignoreAllPawnsFasterThan;
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
                        } else {
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
    public class UseCaseTags_XavierAttack : UseCaseTags
    {
        public override float PriorityScoreDamage(Psycast psycast, int situationCase, bool pacifist, float niceToEvil, List<MeditationFocusDef> usableFoci)
        {
            float chance = Rand.Value;
            this.canHitHumanlike = chance <= this.chanceToCastHumanlike || !HVPAA_Mod.settings.powerLimiting;
            this.canHitColonist = chance <= this.chanceToCastColonist || !HVPAA_Mod.settings.powerLimiting;
            return base.PriorityScoreDamage(psycast, situationCase, pacifist, niceToEvil, usableFoci);
        }
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.MarketValue < 1000f || (p.RaceProps.Humanlike && !this.canHitHumanlike) || (p.IsColonist && !this.canHitColonist);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
        {
            return p.MarketValue / 500f;
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
        public float chanceToCast;
        public float chanceToCastHumanlike;
        public float chanceToCastColonist;
        private bool canHitHumanlike;
        private bool canHitColonist;
    }
    //this wizard war is so fucked
    public class UseCaseTags_CBT : UseCaseTags
    {
        public override bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return p.Downed || p.gender != Gender.Male || this.excludeRaces.Contains(p.def) || p.GetStatValue(StatDefOf.PsychicSensitivity) <= float.Epsilon || p.health.hediffSet.HasHediff(this.alsoCantHave);
        }
        public override float PawnEnemyApplicability(HediffComp_IntPsycasts intPsycasts, Psycast psycast, Pawn p, float niceToEvil, int useCase = 1, bool initialTarget = true)
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
            return p.health.capacities.GetLevel(PawnCapacityDefOf.Moving)*((painFactor * this.painOffset) + (2.5f * p.health.hediffSet.PainTotal / p.GetStatValue(StatDefOf.PainShockThreshold)));
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
        public float painOffset;
        public HediffDef alsoCantHave;
        public List<ThingDef> excludeRaces;
    }
    //jobs
    public class JobDriver_FollowRally : JobDriver
    {
        private Pawn Followee
        {
            get
            {
                return (Pawn)this.job.GetTarget(TargetIndex.A).Thing;
            }
        }
        private bool CurrentlyWalkingToFollowee
        {
            get
            {
                return this.pawn.pather.Moving && this.pawn.pather.Destination == this.Followee;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }
        public override void Notify_Starting()
        {
            base.Notify_Starting();
            this.job.followRadius = 3f;
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            Toil toil = ToilMaker.MakeToil("MakeNewToils");
            toil.tickAction = delegate
            {
                Pawn followee = this.Followee;
                float followRadius = this.job.followRadius;
                if (!this.pawn.pather.Moving || this.pawn.IsHashIntervalTick(30))
                {
                    bool flag = false;
                    if (this.CurrentlyWalkingToFollowee)
                    {
                        if (JobDriver_FollowRally.NearFollowee(this.pawn, followee, followRadius))
                        {
                            flag = true;
                        }
                    }
                    else
                    {
                        float num = followRadius * 1.2f;
                        if (JobDriver_FollowRally.NearFollowee(this.pawn, followee, num))
                        {
                            flag = true;
                        }
                        else
                        {
                            if (!this.pawn.CanReach(followee, PathEndMode.Touch, Danger.Deadly, false, false, TraverseMode.ByPawn))
                            {
                                base.EndJobWith(JobCondition.Incompletable);
                                return;
                            }
                            this.pawn.pather.StartPath(followee, PathEndMode.Touch);
                            this.locomotionUrgencySameAs = null;
                        }
                    }
                    if (Find.TickManager.TicksGame - this.startTick > this.maxRallyTicks)
                    {
                        base.EndJobWith(JobCondition.Succeeded);
                        return;
                    }
                    if (flag)
                    {
                        if (JobDriver_FollowRally.NearDestinationOrNotMoving(this.pawn, followee, followRadius))
                        {
                            this.pawn.pather.StartPath(followee, PathEndMode.Touch);
                            return;
                        }
                        IntVec3 lastPassableCellInPath = followee.pather.LastPassableCellInPath;
                        if (!this.pawn.pather.Moving || this.pawn.pather.Destination.HasThing || !this.pawn.pather.Destination.Cell.InHorDistOf(lastPassableCellInPath, followRadius))
                        {
                            IntVec3 intVec = CellFinder.RandomClosewalkCellNear(lastPassableCellInPath, base.Map, Mathf.FloorToInt(followRadius), null);
                            if (intVec.IsValid && this.pawn.CanReach(intVec, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
                            {
                                this.pawn.pather.StartPath(intVec, PathEndMode.OnCell);
                                this.locomotionUrgencySameAs = followee;
                                return;
                            }
                            return;
                        }
                    }
                }
            };
            toil.defaultCompleteMode = ToilCompleteMode.Never;
            yield return toil;
            yield break;
        }
        public override bool IsContinuation(Job j)
        {
            return this.job.GetTarget(TargetIndex.A) == j.GetTarget(TargetIndex.A);
        }
        public static bool FarEnoughAndPossibleToStartJob(Pawn follower, Pawn followee, float radius)
        {
            if (radius <= 0f)
            {
                string text = "Checking follow job with radius <= 0. pawn=" + follower.ToStringSafe<Pawn>();
                if (follower.mindState != null && follower.mindState.duty != null)
                {
                    text = text + " duty=" + follower.mindState.duty.def;
                }
                Log.ErrorOnce(text, follower.thingIDNumber ^ 843254009);
                return false;
            }
            if (!follower.CanReach(followee, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn))
            {
                return false;
            }
            float num = radius * 1.2f;
            return !JobDriver_FollowRally.NearFollowee(follower, followee, num) || (!JobDriver_FollowRally.NearDestinationOrNotMoving(follower, followee, num) && follower.CanReach(followee.pather.LastPassableCellInPath, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn));
        }
        private static bool NearFollowee(Pawn follower, Pawn followee, float radius)
        {
            return follower.Position.AdjacentTo8WayOrInside(followee.Position) || (follower.Position.InHorDistOf(followee.Position, radius));
        }
        private static bool NearDestinationOrNotMoving(Pawn follower, Pawn followee, float radius)
        {
            if (!followee.pather.Moving)
            {
                return true;
            }
            IntVec3 lastPassableCellInPath = followee.pather.LastPassableCellInPath;
            return !lastPassableCellInPath.IsValid || follower.Position.AdjacentTo8WayOrInside(lastPassableCellInPath) || follower.Position.InHorDistOf(lastPassableCellInPath, radius);
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref maxRallyTicks, "maxRallyTicks", 750);
        }
        private const TargetIndex FolloweeInd = TargetIndex.A;
        private const int CheckPathIntervalTicks = 30;
        public int maxRallyTicks = 750;
    }
    public class JobDriver_TalkToSellcast : JobDriver
    {
        private Pawn TalkTo
        {
            get
            {
                return (Pawn)base.TargetThingA;
            }
        }
        private bool CanBeBought
        {
            get
            {
                if (this.TalkTo.Faction != null)
                {
                    FactionPsycasterRuleDef fprd = HVPAAUtility.GetPsycasterRules(this.TalkTo.Faction.def);
                    if (fprd != null)
                    {
                        return fprd.offersSellcasts;
                    }
                }
                return false;
            }
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return this.pawn.Reserve(this.TalkTo, this.job, 1, -1, null, errorOnFailed, false);
        }
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch, false).FailOn(() => !this.CanBeBought);
            Toil trade = ToilMaker.MakeToil("MakeNewToils");
            trade.initAction = delegate
            {
                Pawn actor = trade.actor;
                if (this.CanBeBought)
                {
                    Find.WindowStack.Add(new Dialog_SellcastHiring(actor, this.TalkTo, this.pawn.Map));
                }
            };
            yield return trade;
            yield break;
        }
    }
    //spec caster incidents
    public class StrikeSquaddie : DefModExtension
    {
        public StrikeSquaddie()
        {

        }
    }
    public class RaidStrategyWorker_TemplarStrikeSquad : RaidStrategyWorker_WithRequiredPawnKinds
    {
        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            IntVec3 intVec = (parms.spawnCenter.IsValid ? parms.spawnCenter : pawns[0].PositionHeld);
            if (parms.attackTargets != null && parms.attackTargets.Count > 0)
            {
                return new LordJob_AssaultThings(parms.faction, parms.attackTargets, 1f, false);
            }
            if (parms.faction.HostileTo(Faction.OfPlayer))
            {
                Faction faction = parms.faction;
                bool canTimeoutOrFlee = parms.canTimeoutOrFlee;
                return new LordJob_AssaultColony(faction, parms.canKidnap, canTimeoutOrFlee, false, false, parms.canSteal, false, false);
            }
            IntVec3 intVec2;
            RCellFinder.TryFindRandomSpotJustOutsideColony(intVec, map, out intVec2);
            return new LordJob_AssistColony(parms.faction, intVec2);
        }
        protected override bool MatchesRequiredPawnKind(PawnKindDef kind)
        {
            return kind.HasModExtension<StrikeSquaddie>();
        }

        protected override int MinRequiredPawnsForPoints(float pointsTotal, Faction faction = null)
        {
            return 1;
        }
    }
    //utilities
    public class HVPAAUtility
    {
        //psycasting decision-making
        public static bool CanPsyast(Pawn pawn, int situation)
        {
            if (pawn.Spawned && !pawn.IsColonistPlayerControlled && (!pawn.IsPrisoner || !pawn.guest.PrisonerIsSecure) && !pawn.DeadOrDowned && !pawn.Suspended && !pawn.DevelopmentalStage.Baby() && !pawn.InBed() && (!pawn.InMentalState || pawn.MentalStateDef.HasModExtension<PsycastPermissiveMentalState>()) && pawn.HasPsylink && HautsUtility.IsntCastingAbility(pawn) && !pawn.stances.stunner.Stunned)
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
                if (HVPAAUtility.IsEnemy(caster, p))
                {
                    foes.Add(p);
                }
                else if (HVPAAUtility.IsAlly(niceToAnimals <= 0, caster, p, niceToEvil))
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
            if (intPsycasts.Pawn.Faction != null)
            {
                foreach (Thing thing in GenRadial.RadialDistinctThingsAround(tryNewPosition, intPsycasts.Pawn.Map, aoe, true))
                {
                    if (thing is Plant plant)
                    {
                        Zone zone = plant.Map.zoneManager.ZoneAt(plant.Position);
                        if (zone != null && zone is Zone_Growing && intPsycasts.Pawn.Faction != null && intPsycasts.Pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
                        {
                            tryNewScore += plant.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, plant) * plant.MarketValue / 500f;
                        }
                    }
                    else if (thing is Building b && b.Faction != null)
                    {
                        if (intPsycasts.Pawn.Faction.HostileTo(b.Faction))
                        {
                            tryNewScore += HVPAAUtility.LightningBuildingScore(b);
                        }
                        else if (niceToEvil > 0 || intPsycasts.Pawn.Faction == b.Faction || intPsycasts.Pawn.Faction.RelationKindWith(b.Faction) == FactionRelationKind.Ally)
                        {
                            tryNewScore -= HVPAAUtility.LightningBuildingScore(b);
                        }
                    }
                    else if (thing is Pawn p)
                    {
                        if (intPsycasts.allies.Contains(p) && !uct.OtherAllyDisqualifiers(psycast, p, 1))
                        {
                            tryNewScore -= p.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, p) * 1.5f;
                        }
                        else if (intPsycasts.foes.Contains(p) && !uct.OtherEnemyDisqualifiers(psycast, p, 1))
                        {
                            tryNewScore += p.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, p);
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
            return scoreMulti * b.GetStatValue(StatDefOf.Flammability) * HautsUtility.DamageFactorFor(DamageDefOf.Flame, b) * b.MarketValue / 200f;
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
        //psycaster spawning
        public static float PsycasterCommonality
        {
            get
            {
                return HVPAA_Mod.settings.psycasterCommonalityFactor / 100f;
            }
        }
        public static FactionPsycasterRuleDef GetPsycasterRules(FactionDef faction)
        {
            if (!faction.humanlikeFaction)
            {
                return null;
            }
            SpecificPsycasterRules spr = faction.GetModExtension<SpecificPsycasterRules>();
            if (spr != null && spr.fprd != null)
            {
                return spr.fprd;
            }
            if (faction.backstoryFilters != null)
            {
                foreach (BackstoryCategoryFilter bcf in faction.backstoryFilters)
                {
                    if (bcf.categories != null && bcf.categories.Contains("Tribal"))
                    {
                        return HVPAADefOf.HVPAA_TribalAnima;
                    }
                }
            }
            if (faction.techLevel < TechLevel.Industrial)
            {
                return HVPAADefOf.HVPAA_GenericPreIndustrial;
            }
            else if (faction.techLevel == TechLevel.Ultra)
            {
                return HVPAADefOf.HVPAA_GenericUltra;
            }
            return HVPAADefOf.HVPAA_Default;
        }
        public static void FullRandCasterTreatment(Pawn pawn, float psysens, FactionPsycasterRuleDef fprd, PawnGenerationRequest request)
        {
            if (Rand.Chance(Math.Min(fprd.randCastersPerCapita * psysens * HVPAAUtility.PsycasterCommonality,0.75f)))
            {
                HVPAAUtility.GiveRandPsylinkLevel(pawn, fprd.avgRandCasterLevel);
                HVPAAUtility.GrantBonusPsycasts(pawn, fprd);
                if (fprd.randCasterHediffs != null)
                {
                    HVPAAUtility.GiveRandCastHediffs(pawn, fprd.randCasterHediffs, fprd.maxRandCasterHediffs);
                }
                if (fprd.randCasterItems != null)
                {
                    HVPAAUtility.GiveRandCastItems(pawn, fprd.randCasterItems, fprd.maxRandCasterItems);
                }
                if (fprd.randCasterEquipment != null)
                {
                    HVPAAUtility.GiveRandCastEquipment(pawn, fprd.randCasterEquipment, fprd.maxRandCasterEquipment, request);
                }
                if (Rand.Chance(0.35f))
                {
                    pawn.psychicEntropy?.RechargePsyfocus();
                }
            }
        }
        public static void GiveRandPsylinkLevel(Pawn pawn, int avgRandCasterLevel)
        {
            int psyLevelOffset = HVPAAUtility.RandPsylinkLevel(avgRandCasterLevel);
            if (pawn.DevelopmentalStage.Juvenile())
            {
                psyLevelOffset = Math.Min(psyLevelOffset, 2 - pawn.GetPsylinkLevel());
            }
            for (int i = 0; i < psyLevelOffset; i++)
            {
                pawn.ChangePsylinkLevel(1, false);
            }
        }
        public static int RandPsylinkLevel(int avgRandCasterLevel)
        {
            float psylinkLevelChance = Rand.Value;
            int psylinkLevel;
            if (psylinkLevelChance <= 0.4f)
            {
                psylinkLevel = avgRandCasterLevel;
            }
            else if (psylinkLevelChance <= 0.6f)
            {
                if (avgRandCasterLevel <= 1)
                {
                    psylinkLevel = Rand.Chance(0.75f) ? 2 : 1;
                }
                else
                {
                    psylinkLevel = avgRandCasterLevel - 1;
                }
            }
            else if (psylinkLevelChance <= 0.8f)
            {
                psylinkLevel = avgRandCasterLevel + 1;
            }
            else
            {
                psylinkLevel = Math.Max(1, (int)Math.Ceiling(Rand.Value * HediffDefOf.PsychicAmplifier.maxSeverity));
            }
            return psylinkLevel;
        }
        public static void GiveRandCastHediffs(Pawn pawn, Dictionary<HediffDef, float> randCastHediffs, int maxRandCasterHediffs)
        {
            IEnumerable<KeyValuePair<HediffDef, float>> rchs = randCastHediffs.InRandomOrder();
            int rchsAdded = 0;
            foreach (KeyValuePair<HediffDef, float> kvp in rchs)
            {
                if (rchsAdded < maxRandCasterHediffs && Rand.Chance(kvp.Value))
                {
                    IEnumerable<RecipeDef> enumerable = DefDatabase<RecipeDef>.AllDefs.Where((RecipeDef x) => x.addsHediff == kvp.Key && pawn.def.AllRecipes.Contains(x));
                    if (enumerable.Any<RecipeDef>())
                    {
                        RecipeDef recipeDef = enumerable.RandomElement<RecipeDef>();
                        if (!recipeDef.targetsBodyPart)
                        {
                            recipeDef.Worker.ApplyOnPawn(pawn, null, null, new List<Thing>(), null);
                            rchsAdded++;
                        } else if (Faction.OfPlayerSilentFail != null && recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).Any<BodyPartRecord>()) {
                            recipeDef.Worker.ApplyOnPawn(pawn, recipeDef.Worker.GetPartsToApplyOn(pawn, recipeDef).RandomElement<BodyPartRecord>(), null, new List<Thing>(), null);
                            rchsAdded++;
                        }
                    }
                }
            }
        }
        public static void GiveRandCastItems(Pawn pawn, Dictionary<ThingDef, float> randCasterItems, int maxRandCasterItems)
        {
            if (pawn.inventory != null)
            {
                IEnumerable<KeyValuePair<ThingDef, float>> rchs = randCasterItems.InRandomOrder();
                int rchsAdded = 0;
                foreach (KeyValuePair<ThingDef, float> kvp in rchs)
                {
                    if (rchsAdded < maxRandCasterItems && Rand.Chance(kvp.Value))
                    {
                        ThingDefCount tdc = new ThingDefCount(kvp.Key, Math.Min(kvp.Key.stackLimit, (int)(Math.Ceiling(Rand.Value * 3f))));
                        Thing thing = StartingPawnUtility.GenerateStartingPossession(tdc);
                        pawn.inventory.innerContainer.TryAdd(thing, true);
                        rchsAdded++;
                    }
                }
            }
        }
        public static void GiveRandCastEquipment(Pawn pawn, Dictionary<ThingDef, float> randCasterEquipment, int maxRandCasterEquipment, PawnGenerationRequest request)
        {
            if (pawn.apparel != null && pawn.equipment != null)
            {
                IEnumerable<KeyValuePair<ThingDef, float>> rchs = randCasterEquipment.InRandomOrder();
                int rchsAdded = 0;
                foreach (KeyValuePair<ThingDef, float> kvp in rchs)
                {
                    if (rchsAdded < maxRandCasterEquipment && Rand.Chance(kvp.Value))
                    {
                        if (typeof(Apparel).IsAssignableFrom(kvp.Key.thingClass) && pawn.apparel != null)
                        {
                            List<Apparel> alreadyWorn = new List<Apparel>();
                            bool hasAlready = false;
                            foreach (Apparel a in pawn.apparel.WornApparel)
                            {
                                if (kvp.Key == a.def)
                                {
                                    hasAlready = true;
                                    break;
                                }
                                if (!ApparelUtility.CanWearTogether(kvp.Key, a.def, pawn.RaceProps.body))
                                {
                                    alreadyWorn.Add(a);
                                }
                            }
                            if (hasAlready)
                            {
                                continue;
                            }
                            bool canReplace = true;
                            float temperature = 21f;
                            float toxicity = 0f;
                            int num = request.Tile;
                            if (num == -1)
                            {
                                Map anyPlayerHomeMap = Find.AnyPlayerHomeMap;
                                if (anyPlayerHomeMap != null)
                                {
                                    num = anyPlayerHomeMap.Tile;
                                }
                            }
                            if (num != -1)
                            {
                                temperature = GenTemperature.AverageTemperatureAtTileForTwelfth(num, GenLocalDate.Twelfth(num));
                                toxicity = Mathf.Clamp01(Find.WorldGrid[num].pollution);
                            }
                            ThingStuffPair tsm = new ThingStuffPair(kvp.Key, GenStuff.RandomStuffFor(kvp.Key));
                            foreach (Apparel a in alreadyWorn)
                            {
                                if (!pawn.WorkTagIsDisabled(WorkTags.Violent) && (a.def.thingCategories.Contains(ThingCategoryDefOf.ApparelArmor) || a.def.thingCategories.Contains(ThingCategoryDefOf.ArmorHeadgear)))
                                {
                                    if (a.GetStatValue(StatDefOf.ArmorRating_Blunt) + a.GetStatValue(StatDefOf.ArmorRating_Sharp) + (0.4f * a.GetStatValue(StatDefOf.ArmorRating_Heat)) > 1.25f)
                                    {
                                        canReplace = false;
                                        break;
                                    }
                                }
                                if (pawn.GetStatValue(StatDefOf.ComfyTemperatureMax) - a.GetStatValue(StatDefOf.Insulation_Heat) + tsm.InsulationHeat < temperature || temperature < pawn.GetStatValue(StatDefOf.ComfyTemperatureMin) - a.GetStatValue(StatDefOf.Insulation_Cold) + tsm.InsulationCold)
                                {
                                    canReplace = false;
                                    break;
                                }
                                if (!pawn.kindDef.apparelIgnorePollution && toxicity > Math.Max(pawn.GetStatValue(StatDefOf.ToxicEnvironmentResistance), pawn.GetStatValue(StatDefOf.ToxicResistance)) - Math.Max(a.GetStatValue(StatDefOf.ToxicResistance), a.GetStatValue(StatDefOf.ToxicEnvironmentResistance) + tsm.ToxicEnvironmentResistance))
                                {
                                    canReplace = false;
                                    break;
                                }
                            }
                            if (canReplace)
                            {
                                for (int i = pawn.apparel.WornApparel.Count - 1; i >= 0; i--)
                                {
                                    if (alreadyWorn.Contains(pawn.apparel.WornApparel[i]))
                                    {
                                        pawn.apparel.Remove(pawn.apparel.WornApparel[i]);
                                    }
                                }
                                Apparel ap = (Apparel)ThingMaker.MakeThing(tsm.thing, tsm.stuff);
                                PawnGenerator.PostProcessGeneratedGear(ap, pawn);
                                if (ApparelUtility.HasPartsToWear(pawn, ap.def))
                                {
                                    pawn.apparel.Wear(ap, false, false);
                                }
                                rchsAdded++;
                            }
                        } else if (kvp.Key.equipmentType == EquipmentType.Primary && pawn.equipment != null) {
                            if (pawn.equipment.Primary != null && !randCasterEquipment.ContainsKey(pawn.equipment.Primary.def))
                            {
                                pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
                            }
                            if (pawn.equipment.Primary == null)
                            {
                                ThingDefCount tdc = new ThingDefCount(kvp.Key, 1);
                                Thing thing = StartingPawnUtility.GenerateStartingPossession(tdc);
                                PawnGenerator.PostProcessGeneratedGear(thing, pawn);
                                pawn.equipment.AddEquipment((ThingWithComps)thing);
                                rchsAdded++;
                            }
                        }
                    }
                }
            }
        }
        public static XenotypeDef CleanPsycasterXenotype(XenotypeDef xenotype, PawnKindDef kind, Faction faction)
        {
            if (ModsConfig.BiotechActive)
            {
                if (!HVPAAUtility.IsCleanPsycasterXenotype(xenotype))
                {
                    List<XenotypeDef> xdefs = HVPAAUtility.AnyCleanPsycasterXenotype(PawnGenerator.XenotypesAvailableFor(kind, faction.def, faction));
                    if (xdefs.Count > 0)
                    {
                        xenotype = xdefs.RandomElement();
                    }
                    else
                    {
                        xenotype = XenotypeDefOf.Baseliner;
                    }
                }
            }
            return xenotype;
        }
        public static void CleanPsycasterCustomeGenes(Pawn pawn)
        {
            if (ModsConfig.BiotechActive && pawn.genes != null && pawn.genes.Xenotype == XenotypeDefOf.Baseliner)
            {
                for (int i = pawn.genes.GenesListForReading.Count - 1; i >= 0; i--)
                {
                    bool remove = false;
                    if (pawn.genes.GenesListForReading[i].def.statOffsets != null)
                    {
                        foreach (StatModifier sm in pawn.genes.GenesListForReading[i].def.statOffsets)
                        {
                            if (sm.stat == StatDefOf.PsychicSensitivity && sm.value < 0f)
                            {
                                remove = true;
                                break;
                            }
                        }
                    }
                    if (!remove && pawn.genes.GenesListForReading[i].def.statFactors != null)
                    {
                        foreach (StatModifier sm in pawn.genes.GenesListForReading[i].def.statFactors)
                        {
                            if (sm.stat == StatDefOf.PsychicSensitivity && sm.value < 0.25f)
                            {
                                remove = true;
                                break;
                            }
                        }
                    }
                    if (remove)
                    {
                        pawn.genes.RemoveGene(pawn.genes.GenesListForReading[i]);
                    }
                }
            }
        }
        public static List<XenotypeDef> AnyCleanPsycasterXenotype(Dictionary<XenotypeDef, float> xenotypesAvailableFor)
        {
            List<XenotypeDef> cleanXenotypes = new List<XenotypeDef>();
            foreach (XenotypeDef xenotype in xenotypesAvailableFor.Keys)
            {
                if (HVPAAUtility.IsCleanPsycasterXenotype(xenotype))
                {
                    cleanXenotypes.Add(xenotype);
                }
            }
            return cleanXenotypes;
        }
        public static bool IsCleanPsycasterXenotype(XenotypeDef xenotype)
        {
            bool remove = false;
            foreach (GeneDef g in xenotype.AllGenes)
            {
                if (g.statOffsets != null)
                {
                    foreach (StatModifier sm in g.statOffsets)
                    {
                        if (sm.stat == StatDefOf.PsychicSensitivity && sm.value < 0f)
                        {
                            remove = true;
                            break;
                        }
                    }
                }
                if (!remove && g.statFactors != null)
                {
                    foreach (StatModifier sm in g.statFactors)
                    {
                        if (sm.stat == StatDefOf.PsychicSensitivity && sm.value < 0.25f)
                        {
                            remove = true;
                            break;
                        }
                    }
                }
            }
            return !remove;
        }
        public static void CleanPsycasterTraits(Pawn pawn)
        {
            if (pawn.story != null)
            {
                for (int i = pawn.story.traits.allTraits.Count - 1; i >= 0; i--)
                {
                    if (HautsUtility.IsExciseTraitExempt(pawn.story.traits.allTraits[i].def))
                    {
                        continue;
                    }
                    bool remove = false;
                    TraitDegreeData t = pawn.story.traits.allTraits[i].def.DataAtDegree(pawn.story.traits.allTraits[i].Degree);
                    if (t.statOffsets != null)
                    {
                        foreach (StatModifier sm in t.statOffsets)
                        {
                            if (sm.stat == StatDefOf.PsychicSensitivity && sm.value < 0f)
                            {
                                remove = true;
                                break;
                            }
                        }
                    }
                    if (!remove && t.statFactors != null)
                    {
                        foreach (StatModifier sm in t.statFactors)
                        {
                            if (sm.stat == StatDefOf.PsychicSensitivity && sm.value < 1f)
                            {
                                remove = true;
                                break;
                            }
                        }
                    }
                    if (remove)
                    {
                        pawn.story.traits.RemoveTrait(pawn.story.traits.allTraits[i]);
                    }
                }
                bool anyTraits = false;
                foreach (Trait t in pawn.story.traits.allTraits)
                {
                    if (!HautsUtility.IsExciseTraitExempt(t.def, true))
                    {
                        anyTraits = true;
                    }
                }
                if (!anyTraits)
                {
                    pawn.story.traits.GainTrait(new Trait(DefDatabase<TraitDef>.GetNamed("PsychicSensitivity"), Rand.Chance(0.5f) ? 2 : 1, true));
                }
            }
        }
        public static void GrantBonusPsycasts(Pawn pawn, FactionPsycasterRuleDef fprd)
        {
            int psylevel = pawn.GetPsylinkLevel();
            Hediff_Psylink psylink = pawn.GetMainPsylinkSource();
            for (int i = 0; i < psylevel; i++)
            {
                float chance = 0.1f;
                if (fprd.bonusCastChance != null && fprd.bonusCastChance.Count > i)
                {
                    chance = fprd.bonusCastChance[i];
                }
                HVPAAUtility.GrantBonusPsycastInner(psylink, i + 1, chance, 0);
            }
        }
        public static void GrantBonusPsycastInner(Hediff_Psylink psylink, int level, float chance, int increment)
        {
            if (Rand.Chance(chance))
            {
                psylink.TryGiveAbilityOfLevel(level, false);
                List<AbilityDef> unknownPsycasts = DefDatabase<AbilityDef>.AllDefs.Where((AbilityDef a) => a.IsPsycast && a.level == level && psylink.pawn.abilities.GetAbility(a) == null).ToList<AbilityDef>();
                if (unknownPsycasts.Count > 0)
                {
                    psylink.pawn.abilities.GainAbility(unknownPsycasts.RandomElement());
                }
                increment++;
                if (increment < 3)
                {
                    HVPAAUtility.GrantBonusPsycastInner(psylink, level, chance, increment);
                }
            }
        }
        //sellcast-specific stuff
        public static bool IsSellcastDiscounted(Pawn pawn)
        {
            if (pawn.story != null && pawn.story.traits.HasTrait(HVPAADefOf.HVPAA_SellcastTrait))
            {
                return true;
            }
            return false;
        }
        public static Pawn GenerateSellcast(Faction faction, FactionPsycasterRuleDef fprd, List<AbilityDef> guaranteedPsycasts = null)
        {
            if (!faction.def.pawnGroupMakers.NullOrEmpty())
            {
                Dictionary<PawnKindDef, float> pawnOptions = new Dictionary<PawnKindDef, float>();
                foreach (PawnGroupMaker pgm in faction.def.pawnGroupMakers)
                {
                    float sumWeight = 0f;
                    foreach (PawnGenOption pgo in pgm.options)
                    {
                        HVPAAUtility.SellcastPawnGroupMakerSumWeight(faction, pgo,ref sumWeight);
                    }
                    foreach (PawnGenOption pgo in pgm.guards)
                    {
                        HVPAAUtility.SellcastPawnGroupMakerSumWeight(faction, pgo, ref sumWeight);
                    }
                    foreach (PawnGenOption pgo in pgm.traders)
                    {
                        HVPAAUtility.SellcastPawnGroupMakerSumWeight(faction, pgo, ref sumWeight);
                    }
                    foreach (PawnGenOption pgo in pgm.options)
                    {
                        HVPAAUtility.SellcastAddPotentialPawnKind(faction, pgo, sumWeight, ref pawnOptions);
                    }
                    foreach (PawnGenOption pgo in pgm.guards)
                    {
                        HVPAAUtility.SellcastAddPotentialPawnKind(faction, pgo, sumWeight, ref pawnOptions);
                    }
                    foreach (PawnGenOption pgo in pgm.traders)
                    {
                        HVPAAUtility.SellcastAddPotentialPawnKind(faction, pgo, sumWeight, ref pawnOptions);
                    }
                }
                if (!pawnOptions.NullOrEmpty())
                {
                    pawnOptions.Keys.TryRandomElementByWeight((PawnKindDef p) => Math.Max(pawnOptions.TryGetValue(p), 0f), out PawnKindDef pkd);
                    if (pkd == null)
                    {
                        pkd = PawnKindDefOf.Colonist;
                    }
                    Dictionary<XenotypeDef, float> cleanXenotypesAvailableFor = new Dictionary<XenotypeDef, float>();
                    XenotypeDef xenotype = XenotypeDefOf.Baseliner;
                    if (ModsConfig.BiotechActive)
                    {
                        foreach (KeyValuePair<XenotypeDef, float> kvp in PawnGenerator.XenotypesAvailableFor(pkd, faction.def, faction))
                        {
                            if (HVPAAUtility.IsCleanPsycasterXenotype(kvp.Key))
                            {
                                cleanXenotypesAvailableFor.Add(kvp.Key, kvp.Value);
                            }
                        }
                        cleanXenotypesAvailableFor.Keys.TryRandomElementByWeight((XenotypeDef x) => Math.Max(cleanXenotypesAvailableFor.TryGetValue(x), 0f), out xenotype);
                    }
                    PawnGenerationContext pawnGenerationContext = PawnGenerationContext.NonPlayer;
                    PawnGenerationRequest request = new PawnGenerationRequest(pkd, faction, pawnGenerationContext, -1, false, false, false, true, true, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false, false, null, null, xenotype, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false);
                    Pawn pawn = PawnGenerator.GeneratePawn(request);
                    HVPAAUtility.CleanPsycasterCustomeGenes(pawn);
                    HVPAAUtility.CleanPsycasterTraits(pawn);
                    HVPAAUtility.GrantBonusPsycasts(pawn, fprd);
                    pawn.psychicEntropy?.RechargePsyfocus();
                    if (pawn.guest != null)
                    {
                        pawn.guest.Recruitable = false;
                    }
                    if (fprd.randCasterHediffs != null)
                    {
                        HVPAAUtility.GiveRandCastHediffs(pawn, fprd.randCasterHediffs, fprd.maxRandCasterHediffs);
                    }
                    if (fprd.randCasterItems != null)
                    {
                        HVPAAUtility.GiveRandCastItems(pawn, fprd.randCasterItems, fprd.maxRandCasterItems);
                    }
                    if (fprd.randCasterEquipment != null)
                    {
                        HVPAAUtility.GiveRandCastEquipment(pawn, fprd.randCasterEquipment, fprd.maxRandCasterEquipment, request);
                    }
                    return pawn;
                }
            }
            Log.Error("HVPAA_NoGoodSellcast".Translate(faction.Name));
            return null;
        }
        public static void SellcastPawnGroupMakerSumWeight(Faction faction, PawnGenOption pgo, ref float sumWeight)
        {
            if (pgo.kind.RaceProps.Humanlike)
            {
                sumWeight += pgo.selectionWeight;
            }
        }
        public static void SellcastAddPotentialPawnKind(Faction faction, PawnGenOption pgo, float sumWeight, ref Dictionary<PawnKindDef,float> pawnOptions)
        {
            if (pgo.kind.RaceProps.Humanlike)
            {
                if (ModsConfig.BiotechActive)
                {
                    foreach (KeyValuePair<XenotypeDef, float> kvp in PawnGenerator.XenotypesAvailableFor(pgo.kind, faction.def, faction))
                    {
                        if (HVPAAUtility.IsCleanPsycasterXenotype(kvp.Key))
                        {
                            float weight = pgo.selectionWeight / (sumWeight * (pgo.kind.titleRequired != null ? (pgo.kind.titleRequired.seniority / 2f) : 1f));
                            if (!pawnOptions.ContainsKey(pgo.kind))
                            {
                                pawnOptions.Add(pgo.kind, weight);
                            } else {
                                pawnOptions[pgo.kind] += weight;
                            }
                            break;
                        }
                    }
                } else {
                    float weight = pgo.selectionWeight / (sumWeight * (pgo.kind.titleRequired != null ? (pgo.kind.titleRequired.seniority / 2f) : 1f));
                    if (!pawnOptions.ContainsKey(pgo.kind))
                    {
                        pawnOptions.Add(pgo.kind, weight);
                    } else {
                        pawnOptions[pgo.kind] += weight;
                    }
                }
            }
        }
        public static Command HireSellcastCommand(Caravan caravan, Map mapToDeliverTo, Faction faction = null, TraderKindDef trader = null)
        {
            Pawn bestNegotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, faction, trader);
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "HVPAA_FloatMenuSellcast".Translate();
            command_Action.defaultDesc = HautsUtility.IsHighFantasy() ? "HVPAA_SellcastLabelF".Translate() : "HVPAA_SellcastLabel".Translate();
            command_Action.icon = HVPAATextures.HireSellcastCommandTex;
            command_Action.action = delegate
            {
                Settlement settlement = CaravanVisitUtility.SettlementVisitedNow(caravan);
                if (settlement != null && settlement.CanTradeNow)
                {
                    Find.WindowStack.Add(new Dialog_SellcastHiring(bestNegotiator, settlement, mapToDeliverTo));
                }
            };
            if (bestNegotiator == null)
            {
                if (trader != null && trader.permitRequiredForTrading != null && !caravan.PawnsListForReading.Any((Pawn p) => p.royalty != null && p.royalty.HasPermit(trader.permitRequiredForTrading, faction)))
                {
                    command_Action.Disable("CommandTradeFailNeedPermit".Translate(trader.permitRequiredForTrading.LabelCap));
                }
                else
                {
                    command_Action.Disable("CommandTradeFailNoNegotiator".Translate());
                }
            }
            if (bestNegotiator != null && bestNegotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
            {
                command_Action.Disable("CommandTradeFailSocialDisabled".Translate());
            }
            return command_Action;
        }
        public static void SkipOutPawnPart(Quest quest, IEnumerable<Pawn> pawns, string inSignal = null, bool sendStandardLetter = true, bool leaveOnCleanup = true, string inSignalRemovePawn = null, bool wakeUp = false)
        {
            QuestPart_SkipOutOnCleanup qpsooc = new QuestPart_SkipOutOnCleanup();
            qpsooc.inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal", null, false);
            qpsooc.pawns.AddRange(pawns);
            qpsooc.sendStandardLetter = sendStandardLetter;
            qpsooc.leaveOnCleanup = true;
            qpsooc.inSignalRemovePawn = inSignalRemovePawn;
            quest.AddPart(qpsooc);
        }
        public static void SkipOutPawn(IEnumerable<Pawn> pawns, bool sendLetter, Quest quest)
        {
            List<Pawn> list = pawns.Where((Pawn x) => x.Spawned || x.IsCaravanMember()).ToList<Pawn>();
            if (sendLetter && list.Any<Pawn>())
            {
                string text = GenLabel.BestGroupLabel(list, false, out Pawn pawn);
                string text2 = GenLabel.BestGroupLabel(list, true, out pawn);
                if (pawns.Any((Pawn x) => x.Faction == Faction.OfPlayer || x.HostFaction == Faction.OfPlayer))
                {
                    if (pawn != null)
                    {
                        Find.LetterStack.ReceiveLetter("LetterLabelPawnLeaving".Translate(text), "LetterPawnLeaving".Translate(text2), LetterDefOf.NeutralEvent, null, null, quest, null, null, 0, true);
                    } else {
                        Find.LetterStack.ReceiveLetter("LetterLabelPawnsLeaving".Translate(text), "LetterPawnsLeaving".Translate(text2), LetterDefOf.NeutralEvent, null, null, quest, null, null, 0, true);
                    }
                } else if (pawn != null) {
                    Messages.Message("MessagePawnLeaving".Translate(text2), null, MessageTypeDefOf.NeutralEvent, true);
                } else {
                    Messages.Message("MessagePawnsLeaving".Translate(text2), null, MessageTypeDefOf.NeutralEvent, true);
                }
            }
            foreach (Pawn pawn in pawns)
            {
                LeaveQuestPartUtility.MakePawnLeave(pawn, quest);
                HVPAAUtility.SkipOutPawnInner(pawn);
            }
        }
        public static void SkipOutPawnInner(Pawn pawn)
        {
            if (pawn.Spawned)
            {
                Map map = pawn.Map;
                FleckCreationData dataStatic = FleckMaker.GetDataStatic(pawn.Position.ToVector3Shifted(), map, FleckDefOf.PsycastSkipInnerExit, 1f);
                dataStatic.rotationRate = (float)Rand.Range(-30, 30);
                dataStatic.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                map.flecks.CreateFleck(dataStatic);
                FleckCreationData dataStatic2 = FleckMaker.GetDataStatic(pawn.Position.ToVector3Shifted(), map, FleckDefOf.PsycastSkipOuterRingExit, 1f);
                dataStatic2.rotationRate = (float)Rand.Range(-30, 30);
                dataStatic2.rotation = (float)(90 * Rand.RangeInclusive(0, 3));
                map.flecks.CreateFleck(dataStatic2);
                SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(pawn.Position, map, false));
                pawn.teleporting = true;
                pawn.ExitMap(false, Rot4.Invalid);
            }
        }
    }
    [StaticConstructorOnStartup]
    public class HVPAATextures
    {
        public static readonly Texture2D HireSellcastCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/Trade", true);
    }
    //mod settings
    public class HVPAA_Settings : ModSettings
    {
        public float psycastAttemptInterval = 500f;
        public float maxChoicesPerAttempt = 3f;
        public bool showNPCasterLevel = true;
        public bool mostDangerousNotifs = true;
        public bool powerLimiting = true;
        public bool nicerPsycasters = false;
        public float psycasterCommonalityFactor = 100f;
        public Dictionary<string, bool> hostileUsabilities = new Dictionary<string, bool>();
        public Dictionary<string, bool> nonhostileUsabilities = new Dictionary<string, bool>();
        public List<string> castablePsycasts = new List<string>();
        public List<bool> hostileUseRecord = new List<bool>();
        public List<bool> nonhostileUseRecord = new List<bool>();
        public override void ExposeData()
        {
            Scribe_Values.Look(ref psycastAttemptInterval, "psycastAttemptInterval", 500f);
            Scribe_Values.Look(ref maxChoicesPerAttempt, "maxChoicesPerAttempt", 3f);
            Scribe_Values.Look(ref showNPCasterLevel, "showNPCasterLevel", true);
            Scribe_Values.Look(ref showNPCasterLevel, "showNPCasterLevel", true);
            Scribe_Values.Look(ref powerLimiting, "powerLimiting", true);
            Scribe_Values.Look(ref psycasterCommonalityFactor, "psycasterCommonalityFactor", 100f);
            Scribe_Collections.Look<string, bool>(ref this.hostileUsabilities, "hostileUsabilities", LookMode.Value, LookMode.Value, ref this.castablePsycasts, ref this.hostileUseRecord, true, false, false);
            Scribe_Collections.Look<string, bool>(ref this.nonhostileUsabilities, "nonhostileUsabilities", LookMode.Value, LookMode.Value, ref this.castablePsycasts, ref this.nonhostileUseRecord, true, false, false);
            base.ExposeData();
        }
    }
    public class HVPAA_Mod : Mod
    {
        public HVPAA_Mod(ModContentPack content) : base(content)
        {
            HVPAA_Mod.settings = GetSettings<HVPAA_Settings>();
        }
        public override void WriteSettings()
        {
            //intention is to get rid of any keys that correspond to defunct abilitydefs e.g. if you stopped running a mod that contained psycasts, they shouldn't show up in the list anymore
            base.WriteSettings();
            List<string> toKeep = new List<string>();
            foreach (AbilityDef ab in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                UseCaseTags uct = ab.GetModExtension<UseCaseTags>();
                if (uct != null)
                {
                    toKeep.Add(ab.defName);
                }
            }
            for (int i = settings.hostileUsabilities.Count - 1; i >= 0; i--)
            {
                string toTest = settings.hostileUsabilities.Keys.ToArray()[i];
                if (!toKeep.Contains(toTest))
                {
                    settings.hostileUsabilities.Remove(toTest);
                }
            }
            for (int i = settings.nonhostileUsabilities.Count - 1; i >= 0; i--)
            {
                string toTest = settings.nonhostileUsabilities.Keys.ToArray()[i];
                if (!toKeep.Contains(toTest))
                {
                    settings.nonhostileUsabilities.Remove(toTest);
                }
            }
        }
        public override void DoSettingsWindowContents(Rect inRect)
        {
            //frequency at which NPC psycasters try psycasting
            float x = inRect.xMin, y = inRect.yMin + 25, halfWidth = inRect.width * 0.5f;
            psycastAttemptInterval = ((int)settings.psycastAttemptInterval).ToString();
            float origR = settings.psycastAttemptInterval;
            Rect attemptFrequencyRect = new Rect(x + 10, y, halfWidth - 15, 32);
            settings.psycastAttemptInterval = Widgets.HorizontalSlider(attemptFrequencyRect, settings.psycastAttemptInterval, 500f, 7500f, true, "HVPAA_SettingPAI".Translate(), "0.2h", "3h", 1f);
            TooltipHandler.TipRegion(attemptFrequencyRect.LeftPart(1f), "HVPAA_TooltipAttemptInterval".Translate());
            if (origR != settings.psycastAttemptInterval)
            {
                psycastAttemptInterval = (settings.psycastAttemptInterval / 2500f).ToString() + " ticks";
            }
            y += 32;
            string origStringR = psycastAttemptInterval;
            psycastAttemptInterval = Widgets.TextField(new Rect(x + 10, y, 50, 32), psycastAttemptInterval);
            if (!psycastAttemptInterval.Equals(origStringR))
            {
                this.ParseInput(psycastAttemptInterval, settings.psycastAttemptInterval, out settings.psycastAttemptInterval);
            }
            y -= 32;
            //number of psycasts that can be considered per attempt
            maxChoicesPerAttempt = ((int)settings.maxChoicesPerAttempt).ToString();
            float origL = settings.maxChoicesPerAttempt;
            Rect choiceCapRect = new Rect(x + 5 + halfWidth, y, halfWidth - 15, 32);
            settings.maxChoicesPerAttempt = Widgets.HorizontalSlider(choiceCapRect, settings.maxChoicesPerAttempt, 1f, 5f, true, "HVPAA_SettingMCPL".Translate(), "1", "5", 1f);
            TooltipHandler.TipRegion(choiceCapRect.LeftPart(1f), "HVPAA_TooltipChoices".Translate());
            if (origL != settings.maxChoicesPerAttempt)
            {
                maxChoicesPerAttempt = ((int)settings.maxChoicesPerAttempt).ToString();
            }
            y += 32;
            string origStringL = maxChoicesPerAttempt;
            maxChoicesPerAttempt = Widgets.TextField(new Rect(x + 5 + halfWidth, y, 50, 32), maxChoicesPerAttempt);
            if (!maxChoicesPerAttempt.Equals(origStringL))
            {
                this.ParseInput(maxChoicesPerAttempt, settings.maxChoicesPerAttempt, out settings.maxChoicesPerAttempt);
            }
            y += 32;
            //psycaster commonality
            psycasterCommonFactor = ((int)settings.psycasterCommonalityFactor).ToString();
            float origPCF = settings.psycasterCommonalityFactor;
            Rect pcfRect = new Rect(x + 10, y, halfWidth - 15, 32);
            settings.psycasterCommonalityFactor = Widgets.HorizontalSlider(pcfRect, settings.psycasterCommonalityFactor, 100f, 1000f, true, "HVPAA_SettingPCF".Translate(), "100%", "1000%", 1f);
            TooltipHandler.TipRegion(pcfRect.LeftPart(1f), "HVPAA_TooltipPCF".Translate());
            if (origPCF != settings.psycasterCommonalityFactor)
            {
                psycasterCommonFactor = ((int)settings.psycasterCommonalityFactor).ToString();
            }
            y += 32;
            string origStringPCF = psycasterCommonFactor;
            psycasterCommonFactor = Widgets.TextField(new Rect(x + 10, y, 50, 32), psycasterCommonFactor);
            if (!psycasterCommonFactor.Equals(origStringPCF))
            {
                this.ParseInput(psycasterCommonFactor, settings.psycasterCommonalityFactor, out settings.psycasterCommonalityFactor);
            }
            y += 40;
            Rect inRect2 = new Rect(x, y, inRect.width, inRect.height);
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect2);
            //toggle display of NPC psycaster levels
            listingStandard.CheckboxLabeled("HVPAA_SettingShow".Translate(), ref settings.showNPCasterLevel, "HVPAA_TooltipShow".Translate());
            //toggle notifications when an NPC uses a super dangerous psycast
            listingStandard.CheckboxLabeled("HVPAA_SettingNotifs".Translate(), ref settings.mostDangerousNotifs, "HVPAA_TooltipNotifs".Translate());
            //toggle artificial stupidity
            listingStandard.CheckboxLabeled("HVPAA_SettingPowerLimits".Translate(), ref settings.powerLimiting, "HVPAA_TooltipPowerLimits".Translate());
            //toggle whether psycasters should be "nice" by default
            listingStandard.CheckboxLabeled("HVPAA_SettingNicerPsycasters".Translate(), ref settings.nicerPsycasters, "HVPAA_TooltipNicerPsycasters".Translate());
            listingStandard.Gap(10f);
            listingStandard.Label("HVPAA_UsabilityHeader".Translate());
            listingStandard.End();
            base.DoSettingsWindowContents(inRect);
            if (settings.hostileUsabilities == null)
            {
                settings.hostileUsabilities = new Dictionary<string, bool>();
            }
            if (settings.nonhostileUsabilities == null)
            {
                settings.nonhostileUsabilities = new Dictionary<string, bool>();
            }
            foreach (AbilityDef ab in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                UseCaseTags uct = ab.GetModExtension<UseCaseTags>();
                if (uct != null)
                {
                    if (!settings.hostileUsabilities.ContainsKey(ab.defName))
                    {
                        settings.hostileUsabilities.Add(ab.defName, true);
                    }
                    if (!settings.nonhostileUsabilities.ContainsKey(ab.defName))
                    {
                        settings.nonhostileUsabilities.Add(ab.defName, true);
                    }
                }
            }
            List<string> list = (from abs in settings.hostileUsabilities.Keys.ToList<string>()
                                 orderby abs descending
                                 select abs).ToList<string>();
            List<string> list2 = (from abs in settings.nonhostileUsabilities.Keys.ToList<string>()
                                  orderby abs descending
                                  select abs).ToList<string>();
            Rect rect = new Rect(inRect.xMin, 320f, inRect.width, inRect.height * 0.5f);
            Rect rect2 = new Rect(0f, 0f, (inRect.width - 30f) * 0.4f, (float)(list.Count * 24));
            Rect rect3 = new Rect((inRect.width - 30f) * 0.4f, 0f, (inRect.width - 30f) * 0.41f, (float)(list.Count * 24));
            Widgets.BeginScrollView(rect, ref this.scrollPosition, rect2, true);
            Listing_Standard listing_Standard2 = new Listing_Standard();
            listing_Standard2.Begin(rect2);
            for (int i = list.Count - 1; i >= 0; i--)
            {
                bool flag = settings.hostileUsabilities[list[i]];
                listing_Standard2.CheckboxLabeled("HVPAA_HPrefix".Translate() + ": " + list[i], ref flag, null, 0f, 1f);
                settings.hostileUsabilities[list[i]] = flag;
            }
            listing_Standard2.End();
            Listing_Standard listing_Standard3 = new Listing_Standard();
            listing_Standard3.Begin(rect3);
            for (int i = list2.Count - 1; i >= 0; i--)
            {
                bool flag = settings.nonhostileUsabilities[list2[i]];
                listing_Standard3.CheckboxLabeled("HVPAA_NHPrefix".Translate() + ": " + list2[i], ref flag, null, 0f, 1f);
                settings.nonhostileUsabilities[list2[i]] = flag;
            }
            listing_Standard3.End();
            Widgets.EndScrollView();
        }
        private void ParseInput(string buffer, float origValue, out float newValue)
        {
            if (!float.TryParse(buffer, out newValue))
                newValue = origValue;
            if (newValue < 0)
                newValue = origValue;
        }
        public override string SettingsCategory()
        {
            return "Hauts' Intelligent Psycasting: Allies and Adversaries";
        }
        public static HVPAA_Settings settings;
        public string psycastAttemptInterval, maxChoicesPerAttempt, sellcastCostMultiplier, psycasterCommonFactor;
        public Vector2 scrollPosition = Vector2.zero;
    }
}
