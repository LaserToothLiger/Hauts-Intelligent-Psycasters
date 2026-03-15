using RimWorld;
using System.Linq;
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
        public DamageDef deadlifeDamage;
        public float deadlifeRadius;
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
                if (!this.calledReinforcementsYet && this.Pawn.Map != null && this.Pawn.Map.Tile.Valid)
                {
                    this.calledReinforcementsYet = true;
                    Faction f = this.Pawn.Faction ?? Find.FactionManager.FirstFactionOfDef(this.Props.reinforcingFaction);
                    Lord lord = this.Pawn.lord ?? LordMaker.MakeNewLord(f, new LordJob_AssaultColony(f, false, false, false, false, false, false, false), this.Pawn.Map, null);
                    int toSpawn = this.Props.reinforcementCount.RandomInRange;
                    while (toSpawn > 0)
                    {
                        toSpawn--;
                        Pawn reinforcement = PawnGenerator.GeneratePawn(f.def.pawnGroupMakers.Where((PawnGroupMaker pgm) => pgm.kindDef == PawnGroupKindDefOf.Combat).RandomElement().options.RandomElement().kind, f, null);
                        GenSpawn.Spawn(reinforcement, CellFinder.RandomClosewalkCellNear(this.Pawn.Position, this.Pawn.Map, 10), this.Pawn.Map, WipeMode.Vanish);
                        FleckMaker.Static(reinforcement.Position, this.Pawn.Map, FleckDefOf.PsycastSkipInnerExit, 1f);
                        FleckMaker.Static(reinforcement.Position, this.Pawn.Map, FleckDefOf.PsycastSkipOuterRingExit, 1f);
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
                    if (this.Pawn.lord == null)
                    {
                        lord.AddPawn(this.Pawn);
                    }
                    else if (this.Pawn.lord != lord)
                    {
                        this.Pawn.lord.RemovePawn(this.Pawn);
                        lord.AddPawn(this.Pawn);
                    }
                }
                if (this.Pawn.Spawned && ModsConfig.AnomalyActive)
                {
                    GenExplosion.DoExplosion(this.Pawn.Position, this.Pawn.Map, this.Props.deadlifeRadius, this.Props.deadlifeDamage, this.Pawn, postExplosionGasType: GasType.DeadlifeDust);
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
