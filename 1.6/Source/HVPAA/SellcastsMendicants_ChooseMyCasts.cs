using RimWorld;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace HVPAA
{
    //if you got a sellcast or mendicant that you can select the psycasts of, this is what handles it, button and all.
    [StaticConstructorOnStartup]
    public class Hediff_ChooseMyCasts : Hediff
    {
        public override void PostMake()
        {
            base.PostMake();
            this.buttonTooltip = "HVPAA_ChooseMyCastsText".Translate();
        }
        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (this.choices != null)
            {
                foreach (int i in this.choices)
                {
                    if (i > 0)
                    {
                        Command_Action cmdRecall = new Command_Action
                        {
                            defaultLabel = "HVPAA_ChooseMyCastsLabel".Translate(i),
                            defaultDesc = this.buttonTooltip,
                            icon = Hediff_ChooseMyCasts.uiIcon,
                            action = delegate ()
                            {
                                this.OpenWindow(i);
                            }
                        };
                        yield return cmdRecall;
                    }
                }
            }
            yield break;
        }
        public void OpenWindow(int level)
        {
            ChooseMyCastWindow window = new ChooseMyCastWindow(this.pawn, level, this);
            Find.WindowStack.Add(window);
        }
        public override void PostTickInterval(int delta)
        {
            base.PostTickInterval(delta);
            if (!this.triggered)
            {
                if (this.pawn.abilities != null && !this.pawn.abilities.abilities.NullOrEmpty())
                {
                    List<AbilityDef> psycasts = new List<AbilityDef>();
                    foreach (Ability a in this.pawn.abilities.abilities)
                    {
                        if (a is Psycast p)
                        {
                            psycasts.Add(a.def);
                            this.choices.Add(a.def.level);
                        }
                    }
                    foreach (AbilityDef ad in psycasts)
                    {
                        if (this.pawn.abilities.GetAbility(ad) != null)
                        {
                            this.pawn.abilities.RemoveAbility(ad);
                        }
                    }
                }
                this.triggered = true;
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<bool>(ref this.triggered, "triggered", false, false);
            Scribe_Collections.Look<int>(ref this.choices, "choices", LookMode.Value, Array.Empty<object>());
            Scribe_Values.Look<string>(ref this.buttonTooltip, "buttonTooltip", "HVPAA_ChooseMyCastsText".Translate(), false);
        }
        public bool triggered;
        public List<int> choices = new List<int>();
        public static readonly Texture2D uiIcon = ContentFinder<Texture2D>.Get("Things/Mote/PsycastSkipFlash", true);
        string buttonTooltip;
    }
    public class ChooseMyCastWindow : Window
    {
        public override void PreOpen()
        {
            base.PreOpen();
            this.grantableAbilities.Clear();
            if (this.pawn.abilities == null)
            {
                return;
            }
            foreach (AbilityDef a in DefDatabase<AbilityDef>.AllDefsListForReading)
            {
                if (a.IsPsycast && a.level == this.level && this.pawn.abilities.GetAbility(a) == null)
                {
                    this.grantableAbilities.Add(a);
                }
            }
            this.grantableAbilities.SortBy((AbilityDef a) => a.label);
        }
        public ChooseMyCastWindow(Pawn pawn, int level, Hediff_ChooseMyCasts hcmc)
        {
            this.pawn = pawn;
            this.forcePause = true;
            this.level = level;
            this.hcmc = hcmc;
        }
        private float Height
        {
            get
            {
                return CharacterCardUtility.PawnCardSize(this.pawn).y + Window.CloseButSize.y + 4f + this.Margin * 2f;
            }
        }
        public override Vector2 InitialSize
        {
            get
            {
                return new Vector2(500f, this.Height);
            }
        }
        public override void DoWindowContents(Rect inRect)
        {
            inRect.yMax -= 4f + Window.CloseButSize.y;
            Text.Font = GameFont.Small;
            Rect viewRect = new Rect(inRect.x, inRect.y, inRect.width * 0.7f, this.scrollHeight);
            Widgets.BeginScrollView(inRect, ref this.scrollPosition, viewRect, true);
            float num = 0f;
            Widgets.Label(0f, ref num, viewRect.width, "HVPAA_ChooseMyCastWindowLabel".Translate().CapitalizeFirst().Formatted(this.pawn.Named("PAWN")).AdjustedFor(this.pawn, "PAWN", true).Resolve(), default(TipSignal));
            num += 14f;
            Listing_Standard listing_Standard = new Listing_Standard();
            Rect rect = new Rect(0f, num, inRect.width - 30f, 99999f);
            listing_Standard.Begin(rect);
            foreach (AbilityDef a in this.grantableAbilities)
            {
                bool flag = this.chosenAbility == a;
                bool flag2 = flag;
                listing_Standard.CheckboxLabeled(a.label, ref flag, a.description);
                if (flag != flag2)
                {
                    if (flag)
                    {
                        this.chosenAbility = a;
                    }
                }
            }
            listing_Standard.End();
            num += listing_Standard.CurHeight + 10f + 4f;
            if (Event.current.type == EventType.Layout)
            {
                this.scrollHeight = Mathf.Max(num, inRect.height);
            }
            Widgets.EndScrollView();
            Rect rect2 = new Rect(0f, inRect.yMax + 4f, inRect.width, Window.CloseButSize.y);
            AcceptanceReport acceptanceReport = this.CanClose();
            if (!acceptanceReport.Accepted)
            {
                TextAnchor anchor = Text.Anchor;
                GameFont font = Text.Font;
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                Rect rect3 = rect;
                rect3.xMax = rect2.xMin - 4f;
                Widgets.Label(rect3, acceptanceReport.Reason.Colorize(ColoredText.WarningColor));
                Text.Font = font;
                Text.Anchor = anchor;
            }
            if (Widgets.ButtonText(rect2, "OK".Translate(), true, true, true, null))
            {
                if (acceptanceReport.Accepted)
                {
                    if (this.pawn.abilities != null)
                    {
                        this.pawn.abilities.GainAbility(this.chosenAbility);
                    }
                    if (this.hcmc != null && !this.hcmc.choices.NullOrEmpty() && this.hcmc.choices.Contains(this.level))
                    {
                        this.hcmc.choices.Remove(this.level);
                    }
                    this.Close(true);
                }
                else
                {
                    Messages.Message(acceptanceReport.Reason, null, MessageTypeDefOf.RejectInput, false);
                }
            }
        }
        private AcceptanceReport CanClose()
        {
            if (this.chosenAbility == null)
            {
                return "HVPAA_ChooseMyCastWindowLabel".Translate().CapitalizeFirst().Formatted(this.pawn.Named("PAWN")).AdjustedFor(this.pawn, "PAWN", true).Resolve();
            }
            return AcceptanceReport.WasAccepted;
        }
        private Pawn pawn;
        private AbilityDef chosenAbility = null;
        private float scrollHeight;
        private Vector2 scrollPosition;
        private List<AbilityDef> grantableAbilities = new List<AbilityDef>();
        private int level;
        private Hediff_ChooseMyCasts hcmc;
    }
}
