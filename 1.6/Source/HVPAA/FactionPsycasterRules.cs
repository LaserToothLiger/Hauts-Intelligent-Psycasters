using System.Collections.Generic;
using Verse;

namespace HVPAA
{
    /*apply to FactionDefs to give them a matching FactionPsycasterRuleDef (governs the instantiation of random casters, spec casters, sellcasts, and titled casters for that faction).
     * If a faction lacks this DME, it falls back to one of the default FPRDs:
     * HVPAA_TribalAnima if the faction has Tribal backstories, or...
     * HVPAA_GenericPreIndustrial ...if the faction is below the Industrial tech level, or...
     * HVPAA_GenericUltra ...if the faction is Ultratech, or... HVPAA_Default*/
    public class SpecificPsycasterRules : DefModExtension
    {
        public SpecificPsycasterRules()
        {

        }
        public FactionPsycasterRuleDef fprd;
    }
    /*see FactionPsycasterRules.xml for comments that are next to actual, helpful examples.
     * bonusCastChance: should be no longer than six elements long, values should be between 0~1. From level 1-6 in ascending order, up-to-3x recursive chance for psycasters to be generated with another psycast of that level
     * ==RANDOM CASTERS==
     * randCastersPerCapita: percent chance any pawn generated for this faction (of at least 50% psysens) is given the benefits of being a random caster, as detailed in the following fields. Chance scales w/ psysens, caps at 75%.
     * avgRandCasterLevel: 40% chance for random casters to have this many psylinks added to them. It's not an actual standard distribution, see RandomCasterUtility.RandPsylinkLevel()
     * randCasterHediffs: random casters can have hediffs from this dictionary (float is the chance for each one), up to maxRandCasterHediffs. Such a hediff must be the addsHediff of some recipe that can apply to the pawn's def
     * randCasterItems: random casters can have extra items in their inventory from this dictionary. Again, float is chance, capped at maxRandCasterItems.
     * randCasterEquipment: random casters can have apparel or primary equipment (weapon) from tihs dictionary. Float chance, cap maxRandCasterEqupiment.
     *   Apparel or equipment generated in this way CAN replace existing items, with a handful of exceptions (see RandomCasterUtility.GiveRandCastEquipment)
     * ==SPEC CASTERS==
     * maxDomesticPower: limits how many "spec casters" (pawns of a kind def specifically set up to be a psycaster) can spawn on any settlement or other pawn-having world object owned by this faction. Non-settlements have 0.1x pts.
     * domesticSpecCasters: whitelist of pawn kinds that can be added to aforementioned world objects' pawn symbols.
     * specPointsPerRaidPoint: limits how many spec casters can spawn in a raid fielded by this faction. This value scales with the total threat points of the raid
     * specChanceInRaids: chance any raid CAN have spec casters at all. Unless you set it over 50%, caps at 50% even after accounting for the psycaster commonality mod setting.
     * raidSpecCasters: whitelist of pawn kinds that can be added to this faction's raids as spec casters.
     * offersSellcasts: required to be able to request a sellcast from a trader of, settlement of, or comms console interaction with this settlement
     * maxSellcastPsylinkLevel: ewisott*/
    public class FactionPsycasterRuleDef : Def
    {
        public override void ResolveReferences()
        {
            base.ResolveReferences();
        }
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
    /*DME can be applied to a raid strategy to prevent it from having spec casters added to it. (This is used by the Templar Strike Squad to prevent even more spec casters from showing up with them)
     * Can also be applied to PawnKindDefs you want to be able to spawn as spec casters (see above)
     * pointCost: interacts with the faction's FPRD's domesticSpecCasters and specPointsPerRaidPoint to determine the limit on spec casters that can show up in a site or raid. The higher this is, the fewer of these SCs can spawn
     * domesticChance: chance that this particular spec caster shows up whenever spec casters would be created. It's affected by psycaster commonality, but caps at 90% unless its base value exceeded that.
     *   It's possible for all spec caster types available to a faction to fail this chance, which will result in NO spec casters in that particular world object even if there otherwise "should" be some. This is intentional.
     * domesticCount: if domesticChance is passed, a number of this spec casters equal to a random value in this range is created. Scales with psycaster commonality, but still restricted by pointCost eating up maxDomesticPower.
     * raidChance|Count: as domesticChance|Count, but for raids.
     * bonusPsycasts: gain this many random psycasts of levels the pawn can cast at.
     * combatCastIntervalOverride: if positive and the spec caster is in combat, the spec caster's tick timer to its next cast attempt can't be longer than this value.
     * unwaveringLoyaltyChance: gee whiz i wonder what this does (cough cough, it's ewisott)*/
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
}
