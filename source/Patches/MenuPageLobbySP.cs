using System.Collections.Generic;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class MenuPageLobbySP
    {
        [HarmonyPatch(typeof(DataDirector), "SaveDeleteCheck")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool DataDirector_SaveDeleteCheck()
        {
            return PluginLoader.saveDeleteEnabled.Value;
        }
        
        [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void RunManager_ChangeLevel_Prefix(RunManager __instance, bool _completedLevel, bool _levelFailed, RunManager.ChangeLevelType _changeLevelType = RunManager.ChangeLevelType.Normal)
        {
            if (!PluginLoader.singleplayerLobbyMenu.Value || SemiFunc.IsMultiplayer()) return;
            
            if ((!SemiFunc.MenuLevel() && !SemiFunc.IsMasterClientOrSingleplayer()) || __instance.restarting)
            {
                return;
            }
            if (_levelFailed && __instance.levelCurrent == __instance.levelArena)
            {
                __instance.debugLevel = __instance.levelLobbyMenu;
                PluginLoader.StaticLogger.LogInfo("Setting debug level");
            }
        }
        
        [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void RunManager_ChangeLevel_Postfix(RunManager __instance, bool _completedLevel, bool _levelFailed, RunManager.ChangeLevelType _changeLevelType = RunManager.ChangeLevelType.Normal)
        {
            if (!SemiFunc.IsMultiplayer() && __instance.gameOver && __instance.debugLevel == __instance.levelLobbyMenu)
            {
                __instance.debugLevel = null;
                PluginLoader.StaticLogger.LogInfo("Resetting debug level");
            }
        }
        
        [HarmonyPatch(typeof(SemiFunc), "MenuActionSingleplayerGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool SemiFunc_MenuActionSingleplayerGame(string saveFileName, List<string> saveFileBackups)
        {
            if (!PluginLoader.singleplayerLobbyMenu.Value) return true;
            
            RunManager.instance.ResetProgress();
            GameManager.instance.SetConnectRandom(false);
            if (saveFileName != null)
            {
                PluginLoader.StaticLogger.LogInfo("Loading save");
                SemiFunc.SaveFileLoad(saveFileName, saveFileBackups);
            }
            else
            {
                SemiFunc.SaveFileCreate();
            }
            RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.LobbyMenu);
            
            return false;
        }
        
        [HarmonyPatch(typeof(MenuPageLobby), "Awake")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageLobby_Awake(MenuPageLobby __instance)
        {
            if (SemiFunc.IsMultiplayer()) return true;

            MenuPageLobby.instance = __instance;
            __instance.menuPage = __instance.GetComponent<MenuPage>();
            __instance.roomNameText.text = $"{PhotonNetwork.CloudRegion} {PhotonNetwork.CurrentRoom?.Name}";
            __instance.chatPromptText.text = InputManager.instance.InputDisplayReplaceTags("Press [chat] to use commands");
            
            return false;
        }
        
        [HarmonyPatch(typeof(MenuPageLobby), "Start")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageLobby_Start(MenuPageLobby __instance)
        {
            if (SemiFunc.IsMultiplayer()) return true;

            __instance.inviteButton.gameObject.SetActive(false);
            
            return false;
        }
        
        [HarmonyPatch(typeof(MenuPageLobby), "ButtonStart")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageLobby_ButtonStart(MenuPageLobby __instance)
        {
            if (SemiFunc.IsMultiplayer() || __instance.joiningPlayer) return true;

            SteamManager.instance.LockLobby();
            DataDirector.instance.RunsPlayedAdd();
            if (RunManager.instance.loadLevel == 0)
            {
                RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.RunLevel);
            }
            else
            {
                RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.Shop);
            }
            
            return false;
        }
    }
}