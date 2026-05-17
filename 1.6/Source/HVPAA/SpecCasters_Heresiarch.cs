using RimWorld;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace HVPAA
{
    /*Arch-heretics appear in HEMP. They're "boss" versions of Transgressors with additional, non-psycast powers
     * every deadlifePeriodicity ticks, emit a deadlifeDamage-type explosion (should be DeadlifeDust) with deadlifeRadius
     * reinforcementCount: after a tiny delay, create nearby a random number of pawns within this range
     * reinforcingFaction: the summoned pawns are from a Combat pawngroupmaker of this faction*/
    public class HediffCompProperties_Heresiarch : HediffCompProperties
    {
        public HediffCompProperties_Heresiarch()
        {
            this.compClass = typeof(HediffComp_Heresiarch);
        }
        public int deadlifePeriodicity;
        public float deadlifeRadius;
        public FleckDef fleck;
        public float fleckScale;
        public FactionDef reinforcingFaction;
        public IntRange reinforcementCount;
    }
    public class HediffComp_Heresiarch : HediffComp
    {
        public HediffCompProperties_Heresiarch Props
        {
            get
            {
                return (HediffCompProperties_Heresiarch)this.props;
            }
        }
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (this.Pawn.IsHashIntervalTick(this.Props.deadlifePeriodicity, delta))
            {
                Pawn p = this.Pawn;
                if (!this.calledReinforcementsYet && p.Map != null && p.Map.Tile.Valid)
                {
                    this.calledReinforcementsYet = true;
                    Faction f = p.Faction ?? Find.FactionManager.FirstFactionOfDef(this.Props.reinforcingFaction);
                    Lord lord = p.lord ?? LordMaker.MakeNewLord(f, new LordJob_AssaultColony(f, false, false, false, false, false, false, false), p.Map, null);
                    int toSpawn = this.Props.reinforcementCount.RandomInRange;
                    while (toSpawn > 0)
                    {
                        toSpawn--;
                        Pawn reinforcement = PawnGenerator.GeneratePawn(f.def.pawnGroupMakers.Where((PawnGroupMaker pgm) => pgm.kindDef == PawnGroupKindDefOf.Combat).RandomElement().options.RandomElement().kind, f, null);
                        GenSpawn.Spawn(reinforcement, CellFinder.RandomClosewalkCellNear(p.Position, p.Map, 10), p.Map, WipeMode.Vanish);
                        FleckMaker.Static(reinforcement.Position, p.Map, FleckDefOf.PsycastSkipInnerExit, 1f);
                        FleckMaker.Static(reinforcement.Position, p.Map, FleckDefOf.PsycastSkipOuterRingExit, 1f);
                        if (!reinforcement.Downed)
                        {
                            Lord lord2 = reinforcement.lord;
                            if (lord2 != null)
                            {
                                lord2.RemovePawn(reinforcement);
                            }
                            lord.AddPawn(reinforcement);
                        }
                    }
                    if (p.lord == null)
                    {
                        lord.AddPawn(p);
                    } else if (p.lord != lord) {
                        p.lord.RemovePawn(p);
                        lord.AddPawn(p);
                    }
                }
                if (p.Spawned && ModsConfig.AnomalyActive)
                {
                    FleckMaker.AttachedOverlay(p, this.Props.fleck, Vector3.zero, this.Props.fleckScale, -1f);
                    foreach (Corpse c in GenRadial.RadialDistinctThingsAround(p.Position, p.Map, this.Props.deadlifeRadius, true).OfType<Corpse>().Distinct<Corpse>())
                    {
                        if (MutantUtility.CanResurrectAsShambler(c))
                        {
                            c.InnerPawn.MarkDeadlifeDustForFaction(p.Faction);
                            MutantUtility.ResurrectAsShambler(c.InnerPawn, 15000, c.InnerPawn.DeadlifeDustFaction);
                        }
                    }
                }
            }
        }
        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look<bool>(ref this.calledReinforcementsYet, "calledReinforcementsYet", false, false);
        }
        public bool calledReinforcementsYet;
    }
}
