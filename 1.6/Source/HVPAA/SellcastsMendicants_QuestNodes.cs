using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HVPAA
{
    /*Mendicants are basically Sellcasts that don't cost goodwill and have a random set of options selected. They come from a faction you've never heard of (but is of either the Civil Outlander or Gentle Tribe kind).
     * So, randomized whether they're Self-directed or not, randomized whether you can choose their psycasts or not, random psylink level, random amount of days they stay with you (daysRange)
     * psycaster generation rules of the pawn's faction are overriden by whatever factionPsycasterRules is*/
    public class QuestNode_GenerateRandomSellcastProperties : QuestNode
    {
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            List<FactionRelation> list2 = new List<FactionRelation>();
            FactionDef fd = Rand.Chance(0.5f) ? FactionDefOf.OutlanderCivil : FactionDefOf.TribeCivil;
            foreach (Faction faction4 in Find.FactionManager.AllFactionsListForReading)
            {
                if (!faction4.def.PermanentlyHostileTo(fd))
                {
                    list2.Add(new FactionRelation
                    {
                        other = faction4,
                        kind = FactionRelationKind.Neutral
                    });
                }
            }
            FactionGeneratorParms factionGeneratorParms = new FactionGeneratorParms(fd, default(IdeoGenerationParms), true);
            if (ModsConfig.IdeologyActive)
            {
                factionGeneratorParms.ideoGenerationParms = new IdeoGenerationParms(factionGeneratorParms.factionDef, false, DefDatabase<PreceptDef>.AllDefs.Where((PreceptDef p) => p.proselytizes || p.approvesOfCharity).ToList<PreceptDef>(), null, null, false, false, false, false, "", null, null, false, "", false);
            }
            Faction faction = FactionGenerator.NewGeneratedFactionWithRelations(factionGeneratorParms, list2);
            faction.temporary = true;
            Find.FactionManager.Add(faction);
            bool choosable = Rand.Chance(0.5f);
            Pawn sellcast = SellcastUtility.GenerateSellcast(faction, this.factionPsycasterRuleset, choosable);
            bool discounted = Rand.Chance(0.5f);
            if (discounted && sellcast.story != null)
            {
                sellcast.story.traits.GainTrait(new Trait(HVPAADefOf.HVPAA_SellcastTrait));
            }
            PawnGenAsPsycasterUtility.GiveRandPsylinkLevel(sellcast, this.factionPsycasterRuleset.avgRandCasterLevel);
            if (sellcast.psychicEntropy != null)
            {
                sellcast.psychicEntropy.RechargePsyfocus();
            }
            slate.Set<Pawn>(this.storeAs.GetValue(slate), sellcast, false);
            slate.Set<Pawn>("sellcast", sellcast, false);
            slate.Set<int>("sellcastLevel", sellcast.GetPsylinkLevel(), false);
            slate.Set<int>("days", this.daysRange.RandomInRange, false);
            int psycastCount = 0;
            if (sellcast.abilities != null)
            {
                foreach (Ability a in sellcast.abilities.abilities)
                {
                    if (a is Psycast)
                    {
                        psycastCount++;
                    }
                }
            }
            slate.Set<int>("psycastCount", psycastCount, false);
            slate.Set<int>("discounted", discounted ? 1 : 0, false);
            slate.Set<int>("choosable", choosable ? 1 : 0, false);
            slate.Set<int>("isHighFantasy", ModCompatibilityUtility.IsHighFantasy() ? 1 : 0, false);
            List<Pawn> pawns = new List<Pawn> { sellcast };
            Map map = QuestGen_Get.GetMap(false, null, false);
            QuestGen.quest.SetFaction(pawns, Faction.OfPlayer, null);
            QuestGen.quest.PawnsArrive(pawns, null, map.Parent, null, false, null, null, null, null, null, false, false, false);
        }
        protected override bool TestRunInt(Slate slate)
        {
            Map map = QuestGen_Get.GetMap(false, null, false);
            if (map == null)
            {
                return false;
            }
            if (!FactionDefOf.OutlanderRefugee.allowedArrivalTemperatureRange.Includes(map.mapTemperature.OutdoorTemp))
            {
                return false;
            }
            return true;
        }
        public FactionPsycasterRuleDef factionPsycasterRuleset;
        [NoTranslate]
        public SlateRef<string> storeAs;
        public IntRange daysRange;
    }
    //sets the duration at which the sellcast or mendicant stays in your colony
    public class QuestNode_Sellcast_Etc : QuestNode
    {
        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            quest.Delay(this.days.GetValue(slate) * 60000, delegate
            {
                Faction faction3 = this.faction.GetValue(slate);
                Action action6 = null;
                Action action7 = delegate
                {
                    quest.Letter(LetterDefOf.PositiveEvent, null, null, null, null, false, QuestPart.SignalListenMode.OngoingOnly, null, false, "[lodgersLeavingLetterText]", null, "[lodgersLeavingLetterLabel]", null, null);
                };
                quest.SignalPassWithFaction(faction3, action6, action7, null, null);
                QuestPart_SkipOutOnCleanup qpsooc = new QuestPart_SkipOutOnCleanup();
                qpsooc.inSignal = slate.Get<string>("inSignal", null, false);
                qpsooc.pawns.AddRange(pawns.GetValue(slate));
                qpsooc.sendStandardLetter = false;
                qpsooc.leaveOnCleanup = true;
                qpsooc.inSignalRemovePawn = null;
                quest.AddPart(qpsooc);
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
    //makes the sellcast or mendicant "teleport out" (despawn) at the end of the quest
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
                SellcastUtility.SkipOutPawn(this.pawns, this.sendStandardLetter, this.quest);
            }
        }
        public override void Cleanup()
        {
            base.Cleanup();
            if (this.leaveOnCleanup)
            {
                SellcastUtility.SkipOutPawn(this.pawns, this.sendStandardLetter, this.quest);
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
    //if a sellcast dies, lose goodwill
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
}
