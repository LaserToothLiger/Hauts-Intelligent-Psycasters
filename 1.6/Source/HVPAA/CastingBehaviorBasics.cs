using HautsFramework;
using RimWorld;
using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HVPAA
{
    /*this comp, added to a psylink, periodically adds the HVPAA_AI hediff to a pawn. THAT hediff instantiates the NPC casting behavior of this mod
     * Psylinks don't directly handle that because, especially if you are running a lot of mods, you will eventually find some incredibly specific (and typically difficult to replicate) scenario
     * in which the casting algorithm hits an exception of some kind, which results in the hediff being removed. We don't want psylinks to be removed! Thus, the expendable, regenerating separate hediff.*/
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
            if (HVPAA_DecisionMakingUtility.CanPsycast(this.Pawn, 0))
            {
                if (!this.Pawn.health.hediffSet.HasHediff(HVPAADefOf.HVPAA_AI))
                {
                    this.Pawn.health.AddHediff(HVPAADefOf.HVPAA_AI);
                }
            }
        }
    }
    /*handles the actual NPC psycasting behavior, pretty much as described by the manual*/
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
            if (HautsMiscUtility.IsntCastingAbility(this.Pawn))
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
                    this.highestPriorityPsycasts = new List<PotentialPsycast>();
                    this.highestPriorityPsycasts = this.ThreePriorityPsycasts(situationCase);
                    //Log.Message("highest-priority psycasts as follows: ");
                    /*foreach (PotentialPsycast pp in highestPriorityPsycasts)
                    {
                        Log.Message(pp.ability.def.label + ">> " + pp.score);
                    }*/
                    //figure out the 'applicability' of each psycast to the current situation
                    bool immediatelyPsycastAgain = false;
                    if (this.highestPriorityPsycasts.Count > 0)
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
                                    float metapplicability = uct.MetaApplicability(this, psyToCast, this.highestPriorityPsycasts, situationCase, this.niceToEvil);
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
                            foreach (PotentialPsycast potPsy in this.highestPriorityPsycasts)
                            {
                                UseCaseTags uct = potPsy.ability.def.GetModExtension<UseCaseTags>();
                                if (uct != null)
                                {
                                    potPsy.score *= uct.ApplicabilityScore(this, potPsy, this.niceToEvil);
                                }
                            }
                            //get the psycast with the highest priority * applicability
                            if (this.highestPriorityPsycasts.Count > 0)
                            {
                                PotentialPsycast psyToCast = null;
                                foreach (PotentialPsycast potPsy in this.highestPriorityPsycasts)
                                {
                                    if (potPsy.score > 0f)
                                    {
                                        if (psyToCast == null)
                                        {
                                            psyToCast = potPsy;
                                        }
                                        else if (psyToCast.score < potPsy.score || (psyToCast.score == potPsy.score && Rand.Chance(0.5f)))
                                        {
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
                    this.highestPriorityPsycasts.Clear();
                    //Log.Error("time to next cast: " + (this.timer / 60));
                }
            }
        }
        //visual effect tells you what psycast is about to be cast
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
                    if (ModCompatibilityUtility.IsHighFantasy())
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
        //on being hurt, timer to next psycast attempt shortens to 4s if longer than that
        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (HautsMiscUtility.IsntCastingAbility(this.Pawn) && Rand.Chance(0.5f))
            {
                this.timer = Math.Min(240, this.timer);
            }
        }
        public bool CanPsycast()
        {
            return HVPAA_DecisionMakingUtility.CanPsycast(this.Pawn, this.GetSituation());
        }
        //handles redetermination of a pawn's relevant psychological profile for determining which casts to use. Traits and memes alter these parameters
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
                                aaa.mapsCovered = new Dictionary<Map, MapAlliesAndAdversaries>();
                            }
                            if (aaa.mapsCovered.TryGetValue(this.Pawn.Map, out MapAlliesAndAdversaries maaa))
                            {
                                this.allies = maaa.allies;
                                this.foes = maaa.foes;
                            } else {
                                MapAlliesAndAdversaries maaa2 = new MapAlliesAndAdversaries();
                                HVPAA_DecisionMakingUtility.SetAlliesAndAdversaries(this.Pawn, maaa2.allies, maaa2.foes, this.niceToAnimals, this.niceToEvil);
                                aaa.mapsCovered.Add(this.Pawn.Map, maaa2);
                                this.allies = maaa2.allies;
                                this.foes = maaa2.foes;
                            }
                        }
                    }
                } else {
                    HVPAA_DecisionMakingUtility.SetAlliesAndAdversaries(this.Pawn, this.allies, this.foes, this.niceToAnimals, this.niceToEvil);
                }
                //HVPAA_DecisionMakingUtility.SetAlliesAndAdversaries(this.Pawn, this.allies, this.foes, this.niceToAnimals, this.niceToEvil);
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
        //some psycasts (they only exist in mods) should be cast before a desired cast
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
        //grabs 1-5 (mod setting dependent) psycasts to utilize
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
                            float priority = level * uct.PriorityScoreHealing(psycast, situationCase, this.pacifist, this.niceToEvil, this.mfds) * (situationCase == 2 ? this.PriorityRandomizationFactor : 1f);
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
        public bool CanUseThisPsycast(UseCaseTags uct, Psycast a)
        {
            if (uct.light && !this.lightUser)
            {
                return false;
            }
            if (uct.animalRightsViolation && this.niceToAnimals > 0)
            {
                return false;
            }
            if (uct.antiDisease && !this.cureUser)
            {
                return false;
            }
            if (uct.painkiller && !this.painkiller)
            {
                return false;
            }
            if (uct.antiScar && !this.scarHealer)
            {
                return false;
            }
            return true;
        }
        public void TryAddToPriorityPsycasts(ref List<PotentialPsycast> priorityPsycasts, UseCaseTags uct, Psycast a, float priority, int useCase)
        {
            float initPriority = priority;
            //darkness adherents do not cast light-making psycasts
            if (!uct.usableWhileFleeing && this.GetSituation() == 3)
            {
                return;
            }
            if (!this.CanUseThisPsycast(uct, a))
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
                } else {
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
        public List<PotentialPsycast> highestPriorityPsycasts;
    }
    /*PotentialPsycasts are, as the name indicates, psycasts in consideration for being cast.
     * ability: the psycast
     * useCase: every psycast has one or more useCases (damage, defense, debuff, healing, or utility). A psycast can give rise to one PotentialPsycast per useCaseTag it has
     * score: each PotentialPsycast gets a score based on the psycast's UseCaseTags DME's evaluation of the relevant useCase
     * lti, ltiDest, gti: saved info about the target(s) the psycast would be cast on, if it were to be selected to cast. ltiDest is a second target location for abilities that specify a target and destination e.g. Skip
     * immediatelyPsycastAgain: if this psycast were to be cast, sets the next psycast attempt to 10 ticks later*/
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
    /*apply to traits or memes. A pawn with this trait/who believes in this meme takes these properties into account when selecting what of its psycasts it picks and how favorably it evaluates them
     * niceOrEvil: a net positive niceOrEvil means a caster tends to select... actually, fuck it, go read my document. I didn't write that just for y'all to ignore it. And go read my xpath for examples. It has some comments.*/
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
    //tag a mental state def or job def with this to make psycasters able to cast during it. id = 3 for mental states/jobs equivalent to fleeing (see Job_Patches.xml or MentalState_Patches.xml), or 4 otherwise
    public class PsycastPermissiveMentalState : DefModExtension
    {
        public PsycastPermissiveMentalState()
        {

        }
        public int id;
    }
    /*tag a jobdef with this to make psycasters incapable of interrupting that job with a psycast attempt
     * or tag a hediffdef with this to make psycasters incapable of doing further psycast attempts while that hediff exists and the neural heat meter is at >="percent" percentage*/
    public class LimitsHVPAACasting : DefModExtension
    {
        public LimitsHVPAACasting()
        {

        }
        public float percent;
    }
}
