using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace HVPAA
{
    //it's like a TradeSession. except for hiring a sellcast from the faction of the trader
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
                    return PawnGenAsPsycasterUtility.GetPsycasterRules(SellcastHiringSession.trader.Faction.def);
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
    //it's like a TradeDeal. except you pay goodwill, and get a sellcast (complete with the temporary pawn quest that actually gives one to you)
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
        public bool TryExecute(out bool actuallyTraded, int durationDays, bool discount, int psylinkLevel, bool canChooseCasts, Map mapToDeliverTo)
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
                Pawn sellcast = SellcastUtility.GenerateSellcast(SellcastHiringSession.trader.Faction, SellcastHiringSession.FPRD, canChooseCasts);
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
                    }
                    else if (mapToDeliverTo != null)
                    {
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
    //it's like a Dialog_Trade, except not.
    [StaticConstructorOnStartup]
    public class Dialog_SellcastHiring : Window
    {
        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(1024f, (float)UI.screenHeight * 0.55f);
            }
        }
        private PlanetTile Tile
        {
            get
            {
                return TradeSession.playerNegotiator.Tile;
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
            bool isHighFantasy = ModCompatibilityUtility.IsHighFantasy();
            this.HVPAA_MenuLabel = isHighFantasy ? "HVPAA_SellcastLabelF".Translate() : "HVPAA_SellcastLabel".Translate();
            this.HVPAA_DurationLabel = "HVPAA_SellcastDurationLabel".Translate();
            this.HVPAA_DurationText = "HVPAA_SellcastDurationText".Translate();
            this.HVPAA_PsylinkLabel = isHighFantasy ? "HVPAA_SellcastPsylinkLabelF".Translate() : "HVPAA_SellcastPsylinkLabel".Translate();
            this.HVPAA_PsylinkText = isHighFantasy ? "HVPAA_SellcastPsylinkTextF".Translate() : "HVPAA_SellcastPsylinkText".Translate();
            this.maxPsylinkLevel = SellcastHiringSession.FPRD.maxSellcastPsylinkLevel;
            this.HVPAA_DiscountLabel = "HVPAA_SellcastDiscountLabel".Translate();
            this.HVPAA_DiscountText = "HVPAA_SellcastDiscountText".Translate();
            this.HVPAA_DiscountLabel2 = isHighFantasy ? "HVPAA_SellcastDiscountLabel2F".Translate() : "HVPAA_SellcastDiscountLabel2".Translate();
            this.HVPAA_DiscountText2 = isHighFantasy ? "HVPAA_SellcastDiscountText2F".Translate() : "HVPAA_SellcastDiscountText2".Translate();
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
            Widgets.Label(new Rect(rect.width * 0.15f, 5f, inRect.width * 0.85f, inRect.height / 2f), this.HVPAA_MenuLabel);
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
            listingStandard.CheckboxLabeled(this.HVPAA_DiscountLabel2, ref this.discount2, this.HVPAA_DiscountText2);
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
                    if (SellcastHiringSession.deal.TryExecute(out bool flag, this.durationDays, this.discount, this.psylinkLevel, !this.discount2, this.HVPAA_MapToGoTo))
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
            this.goodwillCost *= (int)(Math.Round(5f * this.durationDays * (this.discount ? 0.4f : 1f) * (this.discount2 ? 0.5f : 1f) / 6f));
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
        private string HVPAA_DiscountLabel2;
        private string HVPAA_DiscountText2;
        private int maxPsylinkLevel;
        private int goodwillCost;
        private int durationDays;
        private int psylinkLevel;
        private bool discount;
        private bool discount2;
        private string daysText;
        private string levelText;
        public static float lastGoodwillFlashTime = -100f;
        protected static readonly Vector2 AcceptButtonSize = new Vector2(160f, 40f);
    }
    //it's like JobDriver_TradeWithPawn, except it pops up the Dialog_SellcastHiring window instead
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
                    FactionPsycasterRuleDef fprd = PawnGenAsPsycasterUtility.GetPsycasterRules(this.TalkTo.Faction.def);
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
}
