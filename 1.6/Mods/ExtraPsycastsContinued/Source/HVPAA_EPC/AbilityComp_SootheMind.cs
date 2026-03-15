using RimWorld;
using Verse;

namespace HVPAA_EPC
{
    //xpath patch replaces Soothe Mind's normal comp with this, so it's no longer flatly superior to Word of Serenity in every way shape and form
    public class CompAbilityEffect_SootheMind : CompAbilityEffect_GiveHediff
    {
        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (target.Thing != null && target.Thing is Pawn p && !p.InMentalState)
            {
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }
}
