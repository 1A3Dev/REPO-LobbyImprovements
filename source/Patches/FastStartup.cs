using HarmonyLib;
using UnityEngine.SceneManagement;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class FastStartup
    {
        [HarmonyPatch(typeof(MoonUI), "Check")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MoonUI_Check(MoonUI __instance)
        {
            if (PluginLoader.moonPhaseUIEnabled.Value || __instance.state <= MoonUI.State.None) return;
            __instance.skip = true;
            __instance.SetState(MoonUI.State.Hide);
        }
        
        [HarmonyPatch(typeof(SplashScreen), "Start")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool SplashScreen_Start(SplashScreen __instance)
        {
            if(PluginLoader.splashScreenUIEnabled.Value) return true;
            
            __instance.StateSet(SplashScreen.State.Done);
            return false;
        }
    }
}