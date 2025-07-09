using System.Collections;
using System.Text.RegularExpressions;
using HarmonyLib;
using Photon.Pun;
using Steamworks;
using Steamworks.Data;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class PublicLobbySaves
    {
        public static bool lobbyPublic;
        public static string saveFileCurrent;
        
        // Reset lobbyPublic when hosting a regular lobby
        [HarmonyPatch(typeof(MainMenuOpen), "MainMenuSetState")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MainMenuOpen_MainMenuSetState()
        {
            lobbyPublic = false;
            saveFileCurrent = null;
        }
        
        // Makes the lobby list lobby creation show the save list menu
        [HarmonyPatch(typeof(MenuPageServerList), "ButtonCreateNew")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_ButtonCreateNew(MenuPageServerList __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value || __instance.searchInProgress)
                return true;

            SemiFunc.MainMenuSetMultiplayer();
            lobbyPublic = true;
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Saves);
            return false;
        }
        
        // Makes loading/creating a save file for a public lobby show the lobby name prompt
        [HarmonyPatch(typeof(MenuPageSaves), "OnNewGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnNewGame(MenuPageSaves __instance)
        {
            saveFileCurrent = null;
            if (!PluginLoader.savePublicEnabled.Value || __instance.saveFiles.Count >= 10 || !SemiFunc.MainMenuIsMultiplayer() || !lobbyPublic)
                return true;

            MenuManager.instance.PageOpenOnTop(MenuPageIndex.ServerListCreateNew).GetComponent<MenuPageServerListCreateNew>().menuPageParent = MenuManager.instance.currentMenuPage;
            return false;
        }
        
        [HarmonyPatch(typeof(MenuPageSaves), "OnLoadGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnLoadGame(MenuPageSaves __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value || !SemiFunc.MainMenuIsMultiplayer() || !lobbyPublic)
                return true;

            saveFileCurrent = StatsManager.instance.saveFileCurrent;
            MenuPage prevPage = MenuManager.instance.currentMenuPage;
            MenuManager.instance.PageOpenOnTop(MenuPageIndex.ServerListCreateNew).GetComponent<MenuPageServerListCreateNew>().menuPageParent = prevPage;
            return false;
        }
        
        // When exiting the lobby name prompt, go back to the save list instead of the server list
        [HarmonyPatch(typeof(MenuPageServerListCreateNew), "ExitPage")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerListCreateNew_ExitPage(MenuPageServerListCreateNew __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value)
                return true;
            
            MenuManager.instance.PageCloseAllExcept(MenuPageIndex.Saves);
            MenuManager.instance.PageSetCurrent(MenuPageIndex.Saves, __instance.menuPageParent);
            return false;
        }
        
        // When exiting the save list, go back to the server list
        [HarmonyPatch(typeof(MenuPageSaves), "OnGoBack")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnGoBack(MenuPageSaves __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value || !lobbyPublic)
                return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.ServerList);
            return false;
        }
        
        // Prevent NetworkConnect.OnJoinedRoom from creating an extra save file
        [HarmonyPatch(typeof(SemiFunc), "SaveFileCreate")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool SemiFunc_SaveFileCreate(NetworkConnect __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value || !GameManager.instance.connectRandom || string.IsNullOrWhiteSpace(saveFileCurrent))
            {
                return true;
            }
            
            PluginLoader.StaticLogger.LogInfo("[Public Lobby] Loading Save File: " + saveFileCurrent);
            SemiFunc.SaveFileLoad(saveFileCurrent);
            return false;
        }
        
        // Allow public lobbies to be saved
        [HarmonyPatch(typeof(SemiFunc), "SaveFileSave")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SemiFunc_SaveFileSave()
        {
            if (!PluginLoader.savePublicEnabled.Value || !GameManager.instance.connectRandom)
                return;
            
            StatsManager.instance.SaveFileSave();
        }
        
        [HarmonyPatch(typeof(DataDirector), "SaveDeleteCheck")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        [HarmonyWrapSafe]
        private static void DataDirector_SaveDeleteCheck_Prefix()
        {
            GameManager.instance.connectRandom = false;
        }
        
        [HarmonyPatch(typeof(DataDirector), "SaveDeleteCheck")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyWrapSafe]
        private static void DataDirector_SaveDeleteCheck_Postfix()
        {
            GameManager.instance.connectRandom = lobbyPublic;
        }
    }
}
