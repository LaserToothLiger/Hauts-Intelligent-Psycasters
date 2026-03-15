using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.Sound;

namespace HVPAA
{
    public static class SellcastUtility
    {
        //checks if pawn has the Self-directed trait
        public static bool IsSelfDirected(Pawn pawn)
        {
            if (pawn.story != null && pawn.story.traits.HasTrait(HVPAADefOf.HVPAA_SellcastTrait))
            {
                return true;
            }
            return false;
        }
        /*generates a Sellcast for the specified faction. FPRD can be specified separately from the faction's usual FPRD, which is how Mendicants have their own unique FPRD despite either nominally being Outlanders or Tribals.
         * canChoosePsycasts: removes all the pawn's psycasts and instead gives them the hediff that lets players choose their psycasts*/
        public static Pawn GenerateSellcast(Faction faction, FactionPsycasterRuleDef fprd, bool canChoosePsycasts)
        {
            if (!faction.def.pawnGroupMakers.NullOrEmpty())
            {
                Dictionary<PawnKindDef, float> pawnOptions = new Dictionary<PawnKindDef, float>();
                //get all pawnkinds in the faction's PGMs, and put them in a Dictionary such that their weights are all adjusted for this new pool
                foreach (PawnGroupMaker pgm in faction.def.pawnGroupMakers)
                {
                    float sumWeight = 0f;
                    foreach (PawnGenOption pgo in pgm.options)
                    {
                        SellcastUtility.SellcastPawnGroupMakerSumWeight(pgo, ref sumWeight);
                    }
                    foreach (PawnGenOption pgo in pgm.guards)
                    {
                        SellcastUtility.SellcastPawnGroupMakerSumWeight(pgo, ref sumWeight);
                    }
                    foreach (PawnGenOption pgo in pgm.traders)
                    {
                        SellcastUtility.SellcastPawnGroupMakerSumWeight(pgo, ref sumWeight);
                    }
                    foreach (PawnGenOption pgo in pgm.options)
                    {
                        SellcastUtility.SellcastAddPotentialPawnKind(faction, pgo, sumWeight, ref pawnOptions);
                    }
                    foreach (PawnGenOption pgo in pgm.guards)
                    {
                        SellcastUtility.SellcastAddPotentialPawnKind(faction, pgo, sumWeight, ref pawnOptions);
                    }
                    foreach (PawnGenOption pgo in pgm.traders)
                    {
                        SellcastUtility.SellcastAddPotentialPawnKind(faction, pgo, sumWeight, ref pawnOptions);
                    }
                }
                //get random pawnkind from the pool, clean xenotype/genes/traits as per PawnGenAsPsycasterUtility, grant 100% psyfocus, give ChooseMyPsycasts if necessary, and treat as random psycaster
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
                            if (PawnGenAsPsycasterUtility.IsCleanPsycasterXenotype(kvp.Key))
                            {
                                cleanXenotypesAvailableFor.Add(kvp.Key, kvp.Value);
                            }
                        }
                        cleanXenotypesAvailableFor.Keys.TryRandomElementByWeight((XenotypeDef x) => Math.Max(cleanXenotypesAvailableFor.TryGetValue(x), 0f), out xenotype);
                    }
                    PawnGenerationContext pawnGenerationContext = PawnGenerationContext.NonPlayer;
                    PawnGenerationRequest request = new PawnGenerationRequest(pkd, faction, pawnGenerationContext, -1, false, false, false, true, true, 1f, false, true, false, true, true, false, false, false, false, 0f, 0f, null, 1f, null, null, null, null, null, null, null, null, null, null, null, null, false, false, false, false, null, null, xenotype, null, null, 0f, DevelopmentalStage.Adult, null, null, null, false, false, false, -1, 0, false);
                    Pawn pawn = PawnGenerator.GeneratePawn(request);
                    PawnGenAsPsycasterUtility.CleanPsycasterCustomeGenes(pawn);
                    PawnGenAsPsycasterUtility.CleanPsycasterTraits(pawn);
                    if (fprd == null && faction != null)
                    {
                        fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(faction.def);
                    }
                    PawnGenAsPsycasterUtility.GrantBonusPsycasts(pawn, fprd);
                    pawn.psychicEntropy?.RechargePsyfocus();
                    if (canChoosePsycasts)
                    {
                        Hediff hediff = HediffMaker.MakeHediff(HVPAADefOf.HVPAA_ChooseMyCasts, pawn);
                        pawn.health.AddHediff(hediff);
                    }
                    if (pawn.guest != null)
                    {
                        pawn.guest.Recruitable = false;
                    }
                    if (fprd.randCasterHediffs != null)
                    {
                        PawnGenAsPsycasterUtility.GiveRandCastHediffs(pawn, fprd.randCasterHediffs, fprd.maxRandCasterHediffs);
                    }
                    if (fprd.randCasterItems != null)
                    {
                        PawnGenAsPsycasterUtility.GiveRandCastItems(pawn, fprd.randCasterItems, fprd.maxRandCasterItems);
                    }
                    if (fprd.randCasterEquipment != null)
                    {
                        PawnGenAsPsycasterUtility.GiveRandCastEquipment(pawn, fprd.randCasterEquipment, fprd.maxRandCasterEquipment, request);
                    }
                    return pawn;
                }
            }
            Log.Error("HVPAA_NoGoodSellcast".Translate(faction.Name));
            return null;
        }
        //tools for pooling all pawngenoptions from all pawngroupmakers of the given faction into one pool
        public static void SellcastPawnGroupMakerSumWeight(PawnGenOption pgo, ref float sumWeight)
        {
            if (pgo.kind.RaceProps.Humanlike)
            {
                sumWeight += pgo.selectionWeight;
            }
        }
        public static void SellcastAddPotentialPawnKind(Faction faction, PawnGenOption pgo, float sumWeight, ref Dictionary<PawnKindDef, float> pawnOptions)
        {
            if (pgo.kind.RaceProps.Humanlike)
            {
                if (ModsConfig.BiotechActive)
                {
                    foreach (KeyValuePair<XenotypeDef, float> kvp in PawnGenerator.XenotypesAvailableFor(pgo.kind, faction.def, faction))
                    {
                        if (PawnGenAsPsycasterUtility.IsCleanPsycasterXenotype(kvp.Key))
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
        //when a caravan is at a settlement, a button appears to buy sellcast contracts from it
        public static Command HireSellcastCommand(Caravan caravan, Map mapToDeliverTo, Faction faction = null, TraderKindDef trader = null)
        {
            Pawn bestNegotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan, faction, trader);
            Command_Action command_Action = new Command_Action();
            command_Action.defaultLabel = "HVPAA_FloatMenuSellcast".Translate();
            command_Action.defaultDesc = ModCompatibilityUtility.IsHighFantasy() ? "HVPAA_SellcastLabelF".Translate() : "HVPAA_SellcastLabel".Translate();
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
                } else {
                    command_Action.Disable("CommandTradeFailNoNegotiator".Translate());
                }
            }
            if (bestNegotiator != null && bestNegotiator.skills.GetSkill(SkillDefOf.Social).TotallyDisabled)
            {
                command_Action.Disable("CommandTradeFailSocialDisabled".Translate());
            }
            return command_Action;
        }
        //when a sellcast or mendicant's timer is up, you get notified via letter, they leave your faction, the related quest ends, and they despawn
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
                SellcastUtility.SkipOutPawnInner(pawn);
            }
            if (!quest.Historical)
            {
                quest.End(QuestEndOutcome.Success, false);
                foreach (Pawn p in pawns)
                {
                    p.Destroy();
                }
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
}
