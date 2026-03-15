using HautsFramework;
using System.Collections.Generic;
using Verse;

namespace HVPAA
{
    /*in situations where there's tons of psycasters, HIPAA-AI will obviously incur a bigger performance cost.
     * To partially meliorate this, psycasters of a given faction intermittently share their determinations of who their allies and adversaries are.
     * This is handled by each faction's AlliesAndAdversaries comp, which contains a mapsCovered dictionary (Map keys, MapALliesAndAdversaries values).
     * If a psycaster of that faction begins its casting attempt and realizes its current map isn't on the mapsCovered, it adds the map to the dictionary,
     *   along with a MapAlliesAndAdversaries object containing the allies & foes it has determined.
     * This dictionary resets every 250 ticks to force periodic redeterminations.
     * Note that whichever psycaster sets the allies list does so based on their own niceToEvil rating and animal affinity. Everybody else in the faction accepts it*/
    public class HautsFactionCompProperties_AlliesAndAdversaries : HautsFactionCompProperties
    {
        public HautsFactionCompProperties_AlliesAndAdversaries()
        {
            this.compClass = typeof(HautsFactionComp_AlliesAndAdversaries);
        }
    }
    public class HautsFactionComp_AlliesAndAdversaries : HautsFactionComp
    {
        public HautsFactionCompProperties_AlliesAndAdversaries Props
        {
            get
            {
                return (HautsFactionCompProperties_AlliesAndAdversaries)this.props;
            }
        }
        public override void CompPostTick()
        {
            base.CompPostTick();
            if (Find.TickManager.TicksGame % 250 == 0)
            {
                this.mapsCovered.Clear();
            }
        }
        public Dictionary<Map, MapAlliesAndAdversaries> mapsCovered = new Dictionary<Map, MapAlliesAndAdversaries>();
    }
    public class MapAlliesAndAdversaries
    {
        public MapAlliesAndAdversaries()
        {
            this.allies = new List<Pawn>();
            this.foes = new List<Pawn>();
        }
        public List<Pawn> allies = new List<Pawn>();
        public List<Pawn> foes = new List<Pawn>();
    }
}
