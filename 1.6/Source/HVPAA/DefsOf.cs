using RimWorld;
using Verse;

namespace HVPAA
{
    [DefOf]
    public static class HVPAADefOf
    {
        static HVPAADefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(HVPAADefOf));
        }
        public static HediffDef HVPAA_AI;
        public static HediffDef HVPAA_ChooseMyCasts;
        public static HistoryEventDef HVPAA_HiredSellcaster;
        public static HistoryEventDef HVPAA_SellcasterCaptured;
        public static JobDef HVPAA_FollowRally;
        public static JobDef HVPAA_BuySellcast;
        public static QuestScriptDef HVPAA_HiredSellcasterQuest;
        public static QuestScriptDef HVPAA_MendicantCaster;
        public static TraitDef HVPAA_SellcastTrait;

        public static FactionPsycasterRuleDef HVPAA_Default;
        public static FactionPsycasterRuleDef HVPAA_TribalAnima;
        public static FactionPsycasterRuleDef HVPAA_GenericPreIndustrial;
        public static FactionPsycasterRuleDef HVPAA_GenericUltra;
    }
}
