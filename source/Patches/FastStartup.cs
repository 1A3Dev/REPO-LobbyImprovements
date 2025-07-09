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
        
        [HarmonyPatch(typeof(RunManager), "Awake")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void RunManager_Awake(RunManager __instance)
        {
            if (PluginLoader.splashScreenUIEnabled.Value || __instance.levelCurrent != __instance.levelSplashScreen) return;
            __instance.levelCurrent = __instance.levelMainMenu;
        }
    }
}