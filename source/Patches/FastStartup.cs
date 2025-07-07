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
        
        // [HarmonyPatch(typeof(SplashScreen), "Awake")]
        // [HarmonyPrefix]
        // [HarmonyWrapSafe]
        // private static bool SplashScreen_Awake(SplashScreen __instance)
        // {
        //     if (PluginLoader.splashScreenUIEnabled.Value) return true;
        //     
        //     RunManager.instance.levelCurrent = RunManager.instance.levelMainMenu;
        //     SceneManager.LoadSceneAsync("Reload");
        //     return false;
        // }
    }
}