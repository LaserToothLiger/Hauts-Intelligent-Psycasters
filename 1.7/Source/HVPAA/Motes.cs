using HautsFramework;
using RimWorld;
using Verse;

namespace HVPAA
{
    public class MoteNPCasterText : MoteConditionalText
    {
        public override string TextString
        {
            get
            {
                if (HVPAA_Mod.settings.showNPCasterLevel && this.link1.Target != null && this.link1.Target.Thing != null && this.link1.Target.Thing is Pawn p && !p.IsColonistPlayerControlled)
                {
                    return "HVPAA_NPC_mote".Translate(p.GetPsylinkLevel(), this.TotalPsycasts(p));
                }
                return " ";
            }
        }
        public int TotalPsycasts(Pawn p)
        {
            int totalPsycastPower = 0;
            if (p.abilities != null)
            {
                for (int i = 0; i < p.abilities.abilities.Count; i++)
                {
                    if (p.abilities.abilities[i].def.IsPsycast)
                    {
                        totalPsycastPower++;
                    }
                }
            }
            return totalPsycastPower;
        }
    }
}
