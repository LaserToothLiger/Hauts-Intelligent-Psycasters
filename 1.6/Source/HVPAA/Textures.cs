using UnityEngine;
using Verse;

namespace HVPAA
{
    [StaticConstructorOnStartup]
    public class HVPAATextures
    {
        public static readonly Texture2D HireSellcastCommandTex = ContentFinder<Texture2D>.Get("UI/Commands/HVPAA_HireSellcast", true);
    }
}
