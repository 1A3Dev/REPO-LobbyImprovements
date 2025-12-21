using System;
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
        
        [HarmonyPatch(typeof(LevelGenerator), "Start")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void LevelGenerator_Start()
        {
            if(PluginLoader.splashScreenUIEnabled.Value) return;
            
            if(SemiFunc.IsSplashScreen() && RunManager.instance && DataDirector.instance && DataDirector.instance.SettingValueFetch(DataDirector.Setting.SplashScreenCount) == 1){
                RunManager.instance.levelCurrent = RunManager.instance.levelMainMenu;
                PluginLoader.StaticLogger.LogInfo("[Splash Screen] Automatically Skipped");
            }
        }
    }
}