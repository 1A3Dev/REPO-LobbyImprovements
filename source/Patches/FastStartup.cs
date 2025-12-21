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
        
        [HarmonyPatch(typeof(SemiFunc), nameof(SemiFunc.SplashScreenSkip))]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void SemiFunc_SplashScreenSkip(ref bool forceSkip)
        {
            if(PluginLoader.splashScreenUIEnabled.Value) return;
            forceSkip = true;
        }
    }
}