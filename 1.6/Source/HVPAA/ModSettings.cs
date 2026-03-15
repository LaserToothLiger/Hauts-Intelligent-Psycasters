using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HVPAA
{
    /*psycastAttemptInterval: how many ticks in between each NPCaster's psycast attempts. Certain spec casters ignore this setting, and taking damage sets the timer to 240 ticks if it wasn't already shorter.
     *   is half duration if inviisble, and quarter duration if in combat
     * maxChoicesPerAttempt: only this many of the highest-rated psycasts survive the initial priority estimation process, to then get their applicability evaluated in more detail. Lower is more performant, higher results in
     *   more interesting and complex casting from NPCasters
     * showNPCasterLevel: governs whether the yellow "Level __" text is visible next to each pawn with the HVPAA-AI casting hediff
     * mostDangerousNotifs: sends a red letter when certain psycasts are used in anti-player contexts
     * powerLimiting: makes the AI of various unfair psycasts worse in some way. They'll be allowed to choose worse targets (or not have guidance to choose better ones), or something to that effect. Exact effect depends per psycast
     * nicerPsycasters: gives every psycaster +1 niceness, which results in more use of Defense and Healing psycasts, as well as in most NPCasters treating neutral pawns as allies
     * psycasterCommonalityFactor: the higher you make this, the more random and spec casters you see. 100 = 100%
     * non|hostileUsabilities: psycasts' defNames are the keys, whether or not they're considerable for casting by the HIPAA-AI of pawns non|hostile to the player are the values*/
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
