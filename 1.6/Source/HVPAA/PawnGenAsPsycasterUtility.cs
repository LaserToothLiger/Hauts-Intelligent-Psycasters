using HautsFramework;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace HVPAA
{
    public static class PawnGenAsPsycasterUtility
    {
        //see mod setting, and also see FactionPsycasterRules.cs
        public static float PsycasterCommonality
        {
            get
            {
                return HVPAA_Mod.settings.psycasterCommonalityFactor / 100f;
            }
        }
        //gets the psycaster rule def for the specified faction. If it doesn't have one explicitly given to it in XML, this method instead returns the appropriate default FPRD (TribalAnima, PreIndustrial, Ultra, or Default)
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
            } else if (faction.techLevel == TechLevel.Ultra) {
                return HVPAADefOf.HVPAA_GenericUltra;
            }
            return HVPAADefOf.HVPAA_Default;
        }
        //==========RANDOM CASTER PROPERTIES=========
        //checks the chance this pawn should be a random caster according to the specified FPRD. If so, give them the appropriate psylinks, bonus psycasts, hediffs, items, equipment. Possibly also give them full psyfocus
        public static void FullRandCasterTreatment(Pawn pawn, float psysens, FactionPsycasterRuleDef fprd, PawnGenerationRequest request)
        {
            if (Rand.Chance(Math.Min(fprd.randCastersPerCapita * psysens * PawnGenAsPsycasterUtility.PsycasterCommonality, 0.75f)))
            {
                PawnGenAsPsycasterUtility.GiveRandPsylinkLevel(pawn, fprd.avgRandCasterLevel);
                PawnGenAsPsycasterUtility.GrantBonusPsycasts(pawn, fprd);
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
                if (Rand.Chance(0.35f))
                {
                    pawn.psychicEntropy?.RechargePsyfocus();
                }
            }
        }
        //add psylinks to the target pawn, using RandPsylinkLevel to determine exactly how many psylinks they should get. Non-adults can't gain a psylink level higher than 2 from this.
        public static void GiveRandPsylinkLevel(Pawn pawn, int avgRandCasterLevel)
        {
            int psyLevelOffset = PawnGenAsPsycasterUtility.RandPsylinkLevel(avgRandCasterLevel);
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
            } else if (psylinkLevelChance <= 0.6f) {
                if (avgRandCasterLevel <= 1)
                {
                    psylinkLevel = Rand.Chance(0.75f) ? 2 : 1;
                } else {
                    psylinkLevel = avgRandCasterLevel - 1;
                }
            } else if (psylinkLevelChance <= 0.8f) {
                psylinkLevel = avgRandCasterLevel + 1;
            } else if (psylinkLevelChance <= 0.99f) {
                psylinkLevel = Math.Max(1, (int)Math.Ceiling(Rand.Value * 6f));
            } else {
                psylinkLevel = Math.Max(1, (int)Math.Ceiling(Rand.Value * HediffDefOf.PsychicAmplifier.maxSeverity));
            }
            return (int)Math.Min(psylinkLevel, HediffDefOf.PsychicAmplifier.maxSeverity);
        }
        //adds the relevant FPRD's randCasterHediffs/randCasterItems/randCasterEquipment to the pawn. See FactionPsycasterRules.cs for lengthier explanation, or just read the code
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
        //don't be a xenotype that lowers psysens, but otherwise any xenotype you could normally have is fine
        public static XenotypeDef CleanPsycasterXenotype(XenotypeDef xenotype, PawnKindDef kind, Faction faction)
        {
            if (ModsConfig.BiotechActive)
            {
                if (!PawnGenAsPsycasterUtility.IsCleanPsycasterXenotype(xenotype))
                {
                    List<XenotypeDef> xdefs = PawnGenAsPsycasterUtility.AnyCleanPsycasterXenotype(PawnGenerator.XenotypesAvailableFor(kind, faction.def, faction));
                    if (xdefs.Count > 0)
                    {
                        xenotype = xdefs.RandomElement();
                    } else {
                        xenotype = XenotypeDefOf.Baseliner;
                    }
                }
            }
            return xenotype;
        }
        //remove all genes that lower psysens via stat offsets or factors
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
        //find whether the list of xenotypes a pawn could have has ANY options that don't lower psysens as a direct consequence of genes' stat offsets or factors
        public static List<XenotypeDef> AnyCleanPsycasterXenotype(Dictionary<XenotypeDef, float> xenotypesAvailableFor)
        {
            List<XenotypeDef> cleanXenotypes = new List<XenotypeDef>();
            foreach (XenotypeDef xenotype in xenotypesAvailableFor.Keys)
            {
                if (PawnGenAsPsycasterUtility.IsCleanPsycasterXenotype(xenotype))
                {
                    cleanXenotypes.Add(xenotype);
                }
            }
            return cleanXenotypes;
        }
        //same check, for a specific xenotype
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
        //remove all traits that lower psysens via stat offsets or factors. If this would result in the pawn having no traits, give them Psychically Hyper/sensitive
        public static void CleanPsycasterTraits(Pawn pawn)
        {
            if (pawn.story != null)
            {
                for (int i = pawn.story.traits.allTraits.Count - 1; i >= 0; i--)
                {
                    if (TraitModExtensionUtility.IsExciseTraitExempt(pawn.story.traits.allTraits[i].def))
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
                    if (!TraitModExtensionUtility.IsExciseTraitExempt(t.def, true))
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
        //ewisott, see FactionPsycasterRules.cs or FactionPsycasterRules.xml
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
                PawnGenAsPsycasterUtility.GrantBonusPsycastInner(psylink, i + 1, chance, 0);
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
                    PawnGenAsPsycasterUtility.GrantBonusPsycastInner(psylink, level, chance, increment);
                }
            }
        }
    }
}
