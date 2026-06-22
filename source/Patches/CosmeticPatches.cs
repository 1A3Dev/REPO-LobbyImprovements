using HarmonyLib;
using Photon.Pun;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class CosmeticPatches
    {
        [HarmonyPatch(typeof(CosmeticShopMachine), "UpdateState")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void CosmeticShopMachine_UpdateState(CosmeticShopMachine __instance, ref CosmeticShopMachine.State _state)
        {
            if(_state != CosmeticShopMachine.State.RewardCurrencyIntro) return;

            switch(GameManager.instance.lobbyType){
                case GameManager.LobbyTypes.Private when PluginLoader.cosmeticMoneyRewardPrivate.Value:
                case GameManager.LobbyTypes.Matchmaking when PluginLoader.cosmeticMoneyRewardMatchmaking.Value:
                case GameManager.LobbyTypes.Public when PluginLoader.cosmeticMoneyRewardPublic.Value:
                    return;
                default:
                    _state = CosmeticShopMachine.State.Idle;
                    break;
            }
        }

        [HarmonyPatch(typeof(CosmeticShopMachine), "UpdateStateRPC")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void CosmeticShopMachine_UpdateStateRPC(CosmeticShopMachine __instance, ref CosmeticShopMachine.State _state, PhotonMessageInfo _info)
        {
            if(!SemiFunc.MasterOnlyRPC(_info)) return;
            if(_state != CosmeticShopMachine.State.Idle || __instance.stateCurrent != CosmeticShopMachine.State.TokenOutro) return; // If state forced then re-add the token to the UI

            if(__instance.interactingPlayer == SemiFunc.PlayerGetLocal()){
                CosmeticTokenUI.instance.Setup();
            }
        }
    }
}