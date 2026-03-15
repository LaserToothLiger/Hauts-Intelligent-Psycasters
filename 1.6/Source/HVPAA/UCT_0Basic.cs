using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace HVPAA
{
    //apply derivatives of this to a psycast's def. Contains extensible methods for determining the applicability of a psycast in a given situation, as well as for finding an ideal target
    public class UseCaseTags : DefModExtension
    {
        public UseCaseTags()
        {

        }
        /*before actually running any complex calculations of how to use one's own psycasts, the psycaster first uses a quick "Priority evaluation" to rank their psycasts' usefulness to the situation at hand.
         * Only the 1~5 (exact number determined in mod settings) highest-ranked psycasts proceed to the complex calculations, saving us a lot of performance without (usually) noticeably dumbing down the caster.
         * There are six situationCases (1 = in combat, 2 = out of combat, 3 = fleeing, 4 = other mental state, 5 = fleeing with cargo, 6 = otherwise exiting map).
         * Broadly speaking, there are five different "types" of psycast usages: Damage, Defense, Debuff, Healing, and Utility. Their importance relative to each other is dependent on the situationCase
         * and on the caster's personality (obviously, a pacifist won't use Damage psycasts, and a pawn who has a negative niceToEvil rating will favor psycasts that put others down e.g. Damage or Debuff
         * over those that help e.g. Defense or Healing).
         * This priority evaluation is handled by the following PriorityScore__ methods. A single psycast tagged with multiple usage types can be evaluated multiple times, hence the different such method per usage type.
         * A psycast might have a different priority score depending on other factors specific to that psycast. In such a case, you would override these methods.*/
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
                    } else if (niceToEvil < 0f) {
                        return 2f;
                    } else {
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
                    } else if (niceToEvil < 0f) {
                        return 1.7f;
                    } else {
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
                    } else if (niceToEvil < 0f) {
                        return 3f;
                    } else {
                        return 5f;
                    }
                case 6:
                    if (niceToEvil > 0f)
                    {
                        return 10f;
                    } else if (niceToEvil < 0f) {
                        return 3f;
                    } else {
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
        /*rarely, a psycast might be cast BECAUSE it empowers a future psycast(s). These should be tagged "meta" (the secret sixth usage type). They are handled outside the normal priority/applicability algorithm.
         * At time of writing, the examples are:
         * Flare (Hauts' Offbeat Psycasts) - the psycast cast after it costs less neural heat, a portion of its psyfocus cost is refunded, and if it's a skip psycast it has extra range. Generally useful to invoke before expensive casts.
         * Chunk Skip (but only if you are running Cooler Psycasts) - the Chunk Rain psycast can do heavy damage, but it only works if there are chunks adjacent to the caster. Use Chunk Skip on self first to grab ammo.*/
        public virtual float MetaApplicability(HediffComp_IntPsycasts intPsycasts, PotentialPsycast psycast, List<PotentialPsycast> psycasts, int situationCase, float niceToEvil)
        {
            return 0f;
        }
        /*the psycasts that survived the Priority Evaluation stage now go through "Applicability evaluation". Override the five ApplicabilityScore__ methods with bespoke calculations
         * which must set both a numerical scoring AND the PotentialPsycast's ideal target (as LocalTargetInfo, or rarely possibly as GlobalTargetInfo).
         * Whichever PotentialPsycast has the highest score (subject to a slight randomization) gets cast on its desired target.
         * Higher-level psycasts' PPs are scored higher, because it is rarer for NPCasters to have them, and they're typically more expensive than lower-level psycasts and so therefore less likely to be cast as time goes on.*/
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
        /*evaluates all pawns in intPsycasts' foes list and comes up with the one that would be best to target (if any).
         * If this isn't the kind of scan you want to do, you can override it.
         * Also provides an output dictionary of all the other candidates in case that's useful.*/
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
        //ditto, for allies list
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
        //ditto, for all Things in range
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
        //unless you override this, it returns nothing. This is intended for psycasts that target locations instead of anything in particular
        public virtual IntVec3 FindBestPositionTarget(HediffComp_IntPsycasts intPsycasts, Psycast psycast, float niceToEvil, int useCase, out Dictionary<IntVec3, float> positionTargets, float range = -999)
        {
            positionTargets = new Dictionary<IntVec3, float>();
            return IntVec3.Invalid;
        }
        //returns a pawn from the given dictionary, weighted by its corresponding value. Used by FindEnemy|AllyPawnTarget
        public virtual Pawn BestPawnFound(Dictionary<Pawn, float> pawnTargets)
        {
            pawnTargets.Keys.TryRandomElementByWeight((Pawn p) => Math.Max(pawnTargets.TryGetValue(p), 0f), out Pawn pawn);
            return pawn;
        }
        //ditto for FindBestThingTarget
        public virtual Thing BestThingFound(Dictionary<Thing, float> thingTargets)
        {
            thingTargets.Keys.TryRandomElementByWeight((Thing t) => Math.Max(thingTargets.TryGetValue(t), 0f), out Thing thing);
            return thing;
        }
        //FindBestThingTarget skips over Things that don't pass this check
        public virtual bool IsValidThing(Pawn caster, Thing p, float niceToEvil, int useCase)
        {
            return true;
        }
        //skip this Pawn if it passes this check. Used by FindEnemy|AllyPawnTarget and some derivatives thereof
        public virtual bool OtherEnemyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return false;
        }
        public virtual bool OtherAllyDisqualifiers(Psycast psycast, Pawn p, int useCase, bool initialTarget = true)
        {
            return false;
        }
        //get the score that this psycast would have if cast on this target
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
        //in some cases, it behooves you to grab the top maxNumber pawns returned from a FindEnemy|AllyPawnTarget dictionary, and run a separate set of evaluations on them. Pulse psycasts and other AoEs tend to use this
        public virtual List<Pawn> TopTargets(int maxNumber, Dictionary<Pawn, float> pawnTargets)
        {
            List<Pawn> topTargets = new List<Pawn>();
            foreach (Pawn p in pawnTargets.Keys)
            {
                if (topTargets.Count <= 5)
                {
                    topTargets.Add(p);
                } else {
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
        /*if there's at least thingLimit worth of avoidMakingTooMuchOfThing in range of the target position, it's a bad position.
         * Typically used by psycasts that spawn objects e.g. Solar Pinhole to prevent their oversaturation of an area long after they've ceased to have any effect.
         * For a Thing to count against the limit, it must pass TooMuchThingAdditionalCheck.*/
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
        //psycasts with >0 rallyRadius cause allies in that range to consider whether they should rally to the caster. This is what handles that consideration. See Farskip and Neuroquake for examples
        public virtual bool ShouldRally(Psycast psycast, Pawn p, int situation)
        {
            return false;
        }
        /*determines the scanning range for this psycast. In most instances you wouldn't specify a rangeOffset or range Multiplier, nor would you override this; it would just be the psycast's actual range.
         * However, for psycasts that have melee range e.g. Words, you would obviously want to scan beyond melee range. In such a case, this is typically overriden to scale with the pawn's moving capacity,
         * effectively turning Range into "anyone you could likely reach in a given timespan, assuming no terrain or obstacle issues".*/
        public virtual float Range(Psycast psycast)
        {
            return (psycast.def.verbProperties.AdjustedRange(psycast.verb, psycast.pawn) + this.rangeOffset) * this.rangeMultiplier;
        }
        //for use with Cooler Psycasts' Embed Psycast. Some locations are poor choices to embed some psycasts - this handles that.
        public TrapPlacementWorker Worker
        {
            get
            {
                if (this.workerInt == null)
                {
                    this.workerInt = (TrapPlacementWorker)Activator.CreateInstance(this.trapPlacementWorker);
                }
                return this.workerInt;
            }
        }
        //go see examples in Psycasts_Patch_Royalty.xml, which has tons of annotations already
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
        public int trapPower;
        public Type trapPlacementWorker;
        public TrapPlacementWorker workerInt;
    }
}
