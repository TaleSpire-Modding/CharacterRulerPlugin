using HarmonyLib;

namespace CharacterRulerPlugin
{
    [HarmonyPatch(typeof(Ruler), nameof(Ruler.Dispose))]
    public class RulerDisposePatch
    {
        static void Prefix(Ruler __instance)
        {
            CharacterRuler.CharacterRulerPlugin.RemoveRulerTracking(__instance);
        }
    }
}
