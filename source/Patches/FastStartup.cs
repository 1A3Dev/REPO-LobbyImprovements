using HarmonyLib;
using Photon.Pun;
using UnityEngine;

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

        #region Level Loading Stuck Fix
        [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Update))]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool NetworkManager_Update(NetworkManager __instance)
        {
            if(GameDirector.instance.currentState != GameDirector.gameState.Start || LoadingUI.instance.levelAnimationCompleted || MenuPagePassword.instance){
                return true;
            }

            if(GameManager.instance.gameMode == 1 && PhotonNetwork.IsMasterClient && !__instance.LoadingDone && __instance.instantiatedPlayerAvatars >= PhotonNetwork.CurrentRoom.PlayerCount){
                __instance.photonView.RPC("AllPlayerSpawnedRPC", RpcTarget.AllBuffered);
                __instance.LoadingDone = true;
            }

            __instance.loadingScreenTimer += Time.deltaTime;
            if(__instance.loadingScreenTimer >= 25f){
                LoadingUI.instance.stuckActive = true;
                if(SemiFunc.InputDown(InputKey.Menu)){
                    __instance.TriggerLeavePhotonRoomForcedStuckInLoading();
                }
            }

            return false;
        }

        [HarmonyPatch(typeof(GameDirector), nameof(GameDirector.SetStart))]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void GameDirector_SetStart()
        {
            if(NetworkManager.instance && LoadingUI.instance){
                NetworkManager.instance.loadingScreenTimer = 10f; // 25-10 = 15s
            }
        }
        #endregion
    }
}