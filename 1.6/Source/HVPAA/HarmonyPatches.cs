using HarmonyLib;
using RimWorld;
using RimWorld.BaseGen;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_IsColonistPlayerControlledPostfix)));
            harmony.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }),
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_GeneratePawnPostfix)));
            harmony.Patch(AccessTools.Method(typeof(SymbolResolver_PawnGroup), nameof(SymbolResolver_PawnGroup.Resolve)),
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_PawnGroup_ResolvePostfix)));
            harmony.Patch(AccessTools.Method(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns)),
                           postfix: new HarmonyMethod(patchType, nameof(HVPAA_PGMU_GeneratePawnsPostfix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.Notify_DamageTaken)),
                           prefix: new HarmonyMethod(patchType, nameof(HVPAA_Notify_DamageTakenPrefix)));
            harmony.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.GetGizmos)),
                          postfix: new HarmonyMethod(patchType, nameof(HVPAA_GetGizmosPostfix)));
            Log.Message("HVPAA_Initialize".Translate().CapitalizeFirst());
        }
        internal static object GetInstanceField(Type type, object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                | BindingFlags.Static;
            FieldInfo field = type.GetField(fieldName, bindFlags);
            return field.GetValue(instance);
        }
        //you can right-click on any pawn who wants to trade with you to try and hire a sellcast from the trader's faction
        public static IEnumerable<FloatMenuOption> HVPAA_GetOptionsForPostfix(IEnumerable<FloatMenuOption> __result, Pawn clickedPawn, FloatMenuContext context)
        {
            foreach (FloatMenuOption fmo in __result)
            {
                yield return fmo;
            }
            if (clickedPawn != null && clickedPawn.Faction != null && ((ITrader)clickedPawn).CanTradeNow && clickedPawn.GetLord().LordJob is LordJob_TradeWithColony && !clickedPawn.mindState.traderDismissed && context.FirstSelectedPawn.CanReach(clickedPawn, PathEndMode.OnCell, Danger.Deadly, false, false, TraverseMode.ByPawn) && !context.FirstSelectedPawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled && context.FirstSelectedPawn.CanTradeWith(clickedPawn.Faction, clickedPawn.TraderKind).Accepted)
            {
                FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(clickedPawn.Faction.def);
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
        //when you have a caravan at a settlement you could trade with, selecting it displays a button to hire a sellcast from the settlement's faction
        public static IEnumerable<Gizmo> HVPAA_GetCaravanGizmosPostfix(IEnumerable<Gizmo> __result, Settlement __instance, Caravan caravan)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }
            if (__instance.TraderKind != null && !__instance.Faction.def.permanentEnemy && __instance.Faction != Faction.OfPlayerSilentFail && __instance.Faction.RelationKindWith(Faction.OfPlayerSilentFail) != FactionRelationKind.Hostile && CaravanVisitUtility.SettlementVisitedNow(caravan) == __instance)
            {
                FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(__instance.Faction.def);
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
                    yield return SellcastUtility.HireSellcastCommand(caravan, closestPlayerMap, __instance.Faction, __instance.TraderKind);
                }
            }
        }
        //when you're jawing on the comms console with another faction, this lets you hire a sellcast from them
        public static void HVPAA_FactionDialogForPostfix(ref DiaNode __result, Pawn negotiator, Faction faction)
        {
            if (negotiator.Spawned && faction.leader != null && faction.PlayerRelationKind == FactionRelationKind.Ally)
            {
                FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(faction.def);
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
        //Self-directed trait prevents you from issuing direct orders to a pawn. Not from giving them schedules, restrictions, etc. though
        public static void HVPAA_IsColonistPlayerControlledPostfix(Pawn __instance, ref bool __result)
        {
            if (__result == true && SellcastUtility.IsSelfDirected(__instance))
            {
                __result = false;
            }
        }
        /*Ensures spec casters don't have psysens-reducing traits/xenotypes/genes, gives them their psylinks and bonus casts, and potentially gives them random caster stuff.
         * Also governs the assignation of random caster stuff to, well, random pawns. Random caster properties are handled on a per-faction basis (see FactionPsycasterRules.cs)*/
        public static void HVPAA_GeneratePawnPostfix(ref Pawn __result, PawnGenerationRequest request)
        {
            if (__result.RaceProps.Humanlike)
            {
                AddedSpecPsycasters asp = __result.kindDef.GetModExtension<AddedSpecPsycasters>();
                if (asp != null)
                {
                    PawnGenAsPsycasterUtility.CleanPsycasterTraits(__result);
                    if (ModsConfig.BiotechActive && __result.genes != null && __result.Faction != null)
                    {
                        __result.genes.SetXenotype(PawnGenAsPsycasterUtility.CleanPsycasterXenotype(__result.genes.Xenotype, __result.kindDef, __result.Faction));
                        PawnGenAsPsycasterUtility.CleanPsycasterCustomeGenes(__result);
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
                        PawnGenAsPsycasterUtility.GrantBonusPsycastInner(__result.GetMainPsylinkSource(), (int)Math.Ceiling(Rand.Value * __result.GetPsylinkLevel()), 1f, 3);
                        bonusPsycasts--;
                    }
                    if (__result.guest != null && Rand.Chance(asp.unwaveringLoyaltyChance))
                    {
                        __result.guest.Recruitable = false;
                    }
                    if (__result.Faction != null)
                    {
                        FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(__result.Faction.def);
                        if (fprd != null && Rand.Chance(fprd.randCastersPerCapita * __result.GetStatValue(StatDefOf.PsychicSensitivity)))
                        {
                            PawnGenAsPsycasterUtility.GiveRandPsylinkLevel(__result, fprd.avgRandCasterLevel);
                            PawnGenAsPsycasterUtility.GrantBonusPsycasts(__result, fprd);
                        }
                    }
                    return;
                }
                if (__result.Faction != null && !__result.Faction.IsPlayer)
                {
                    FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(__result.Faction.def);
                    if (fprd != null)
                    {
                        float psysens = Math.Min(2f, __result.GetStatValue(StatDefOf.PsychicSensitivity));
                        if (psysens >= 0.5f)
                        {
                            PawnGenAsPsycasterUtility.FullRandCasterTreatment(__result, psysens, fprd, request);
                        }
                    }
                }
            }
        }
        //When a map is being generated, and it belongs to a non-player faction, additional spec casters can be spawned according to the "domestic" properties of the faction's FPRD (see FactionPsycasterRules.cs)
        public static void HVPAA_PawnGroup_ResolvePostfix(ResolveParams rp)
        {
            Map m = BaseGen.globalSettings.map;
            if (rp.faction != null && m != null)
            {
                float multi = (m.Tile != null && Find.World.worldObjects.SettlementAt(m.Tile) != null) ? 1f : 0.1f;
                FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(rp.faction.def);
                if (fprd != null)
                {
                    IEnumerable<PawnKindDef> pkds = fprd.domesticSpecCasters.InRandomOrder();
                    int domesticPower = (int)(fprd.maxDomesticPower * PawnGenAsPsycasterUtility.PsycasterCommonality);
                    foreach (PawnKindDef pkd in pkds)
                    {
                        if (domesticPower > 0 && pkd.RaceProps.Humanlike)
                        {
                            AddedSpecPsycasters asp = pkd.GetModExtension<AddedSpecPsycasters>();
                            if (asp != null && Rand.Chance(asp.domesticChance>=0.9f? asp.domesticChance : Math.Min(asp.domesticChance * multi * (float)Math.Sqrt(PawnGenAsPsycasterUtility.PsycasterCommonality),0.9f)))
                            {
                                int toAdd = (int)(asp.domesticCount.RandomInRange * Math.Sqrt(PawnGenAsPsycasterUtility.PsycasterCommonality));
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
        //when a raid is generated, it can have additional spec casters added to it per the "raid" properties of the faction's FPRD (see FactionPsycasterRules.cs)
        public static IEnumerable<Pawn> HVPAA_PGMU_GeneratePawnsPostfix(IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
        {
            foreach (Pawn p in __result)
            {
                yield return p;
            }
            if (parms.faction != null /*&& parms.faction.def.humanlikeFaction*/ && parms.raidStrategy != null && !parms.raidStrategy.HasModExtension<AddedSpecPsycasters>())
            {
                FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(parms.faction.def);
                if (fprd != null && Rand.Chance(fprd.specChanceInRaids>=0.5f? fprd.specChanceInRaids : Math.Min(fprd.specChanceInRaids* (float)Math.Sqrt(PawnGenAsPsycasterUtility.PsycasterCommonality),0.5f)))
                {
                    IEnumerable<PawnKindDef> pkds = fprd.raidSpecCasters.InRandomOrder();
                    float specPoints = parms.points * fprd.specPointsPerRaidPoint * (float)Math.Sqrt(PawnGenAsPsycasterUtility.PsycasterCommonality);
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
                            if (asp != null && Rand.Chance(asp.raidChance*(Rand.Chance(0.5f)?1f:PawnGenAsPsycasterUtility.PsycasterCommonality)))
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
        //prevents NPCasters from having their ability-casting jobs interrupted by taking damage. Otherwise you can interrupt a Neuroquake-caster or similar by just lightly slapping them. !No bueno!
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
        /*Gizmos from hediffs don't show up if you don't control the character.
         * So, if you have a Self-directed sellcast/mendicant whom you can choose the sellcasts of... the button won't show up.
         * This patch fixes that specific exception.*/
        public static IEnumerable<Gizmo> HVPAA_GetGizmosPostfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
            {
                yield return gizmo;
            }
            if (Find.Selector.SingleSelectedThing == __instance && __instance.story != null && __instance.story.traits.HasTrait(HVPAADefOf.HVPAA_SellcastTrait))
            {
                Hediff h = __instance.health.hediffSet.GetFirstHediffOfDef(HVPAADefOf.HVPAA_ChooseMyCasts);
                if (h != null && h is Hediff_ChooseMyCasts hcmc)
                {
                    IEnumerable<Gizmo> gizmos = hcmc.GetGizmos();
                    if (gizmos != null)
                    {
                        foreach (Gizmo g in gizmos)
                        {
                            yield return g;
                        }
                    }
                }
            }
        }
    }
}
