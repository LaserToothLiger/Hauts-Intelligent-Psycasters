using RimWorld;
using Verse;

namespace HVPAA
{
    /*for use with Cooler Psycasts' Embed Psycast. UseCaseTags that make use of a particular TPW can force certain locations to be bad positions for that psycast to be embedded.
     * NoRoof: for stuff like Flashstorm, where you need to guarantee an unroofed spot
     * NearWildAnimals: e.g. Manhunter Pulse, where there should be animals nearby that could be affected
     * AdjacentToChunk: for Chunk Rain, which does not work without an adjacent chunk*/
    public class TrapPlacementWorker
    {
        public virtual bool IsGoodSpot(IntVec3 iv3, Map map)
        {
            return true;
        }
    }
    public class TrapPlacementWorker_NoRoof : TrapPlacementWorker
    {
        public override bool IsGoodSpot(IntVec3 iv3, Map map)
        {
            return !map.roofGrid.Roofed(iv3);
        }
    }
    public class TrapPlacementWorker_NearWildAnimals : TrapPlacementWorker
    {
        public override bool IsGoodSpot(IntVec3 iv3, Map map)
        {
            int validCount = 0;
            foreach (Pawn p in map.mapPawns.AllPawnsSpawned)
            {
                if (p.AnimalOrWildMan())
                {
                    validCount++;
                    if (p.Position.DistanceTo(iv3) <= 27.9f)
                    {
                        return true;
                    }
                }
            }
            return validCount >= 25;
        }
    }
    public class TrapPlacementWorker_AdjacentToChunk : TrapPlacementWorker
    {
        public override bool IsGoodSpot(IntVec3 iv3, Map map)
        {
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(iv3, map, 1.9f, true))
            {
                if (GenSight.LineOfSight(iv3, t.Position, map) && (t.HasThingCategory(ThingCategoryDefOf.Chunks) || t.HasThingCategory(ThingCategoryDefOf.StoneChunks)))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
