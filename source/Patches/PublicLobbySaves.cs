using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class PublicLobbySaves
    {
        private static bool publicSavesMenuOpen;
        private static string currentSaveFileName;
        
        // Server List Menu -> Create New -> Saves Menu
        [HarmonyPatch(typeof(MenuPageServerList), "ButtonCreateNew")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_ButtonCreateNew(MenuPageServerList __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value)
                return true;

            SemiFunc.MainMenuSetMultiplayer();
            publicSavesMenuOpen = true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Saves);
            return false;
        }
        
        // Override saves menu header
        [HarmonyPatch(typeof(MenuPageSaves), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageSaves_Start(MenuPageSaves __instance)
        {
            if (SemiFunc.MainMenuIsMultiplayer())
            {
                __instance.gameModeHeader.text = publicSavesMenuOpen ? "Public Multiplayer" : "Private Multiplayer";
            }
        }
        
        // Saves Menu > New Game/Load Save > Server Name (Skip Confirmation Popup)
        [HarmonyPatch(typeof(MenuButton), "OnSelect")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuButton_OnSelect(MenuButton __instance)
        {
            if (!publicSavesMenuOpen || !__instance.menuButtonPopUp)
                return true;

            if (__instance.menuButtonPopUp.headerText == "Start a new game?" && __instance.menuButtonPopUp?.bodyText == "Do you want to start a game?")
            {
                MenuPageSaves menuPageSaves = MenuManager.instance.currentMenuPage?.GetComponent<MenuPageSaves>();
                menuPageSaves?.OnNewGame();
                return !menuPageSaves;
            }

            if (__instance.menuButtonPopUp.headerText == "Load save?" && __instance.menuButtonPopUp.bodyText == "Load this save file?")
            {
                MenuPageSaves menuPageSaves = MenuManager.instance.currentMenuPage?.GetComponent<MenuPageSaves>();
                menuPageSaves?.OnLoadGame();
                return !menuPageSaves;
            }

            return true;
        }
        
        // Saves Menu > New Game > Lobby Name Input
        [HarmonyPatch(typeof(MenuPageSaves), "OnNewGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnNewGame(MenuPageSaves __instance)
        {
            currentSaveFileName = null;
            
            if (!publicSavesMenuOpen || __instance.saveFiles.Count >= 10)
                return true;

            MenuPage prevPage = MenuManager.instance.currentMenuPage;
            MenuPageServerListCreateNew menuPageServerListCreateNew = MenuManager.instance.PageOpenOnTop(MenuPageIndex.ServerListCreateNew).GetComponent<MenuPageServerListCreateNew>();
            menuPageServerListCreateNew.menuPageParent = prevPage;
            
            menuPageServerListCreateNew.menuTextInput.textCurrent = $"{SteamClient.Name}'s Lobby";
            return false;
        }
        
        // Saves Menu > Load Save > Lobby Name Input
        [HarmonyPatch(typeof(MenuPageSaves), "OnLoadGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnLoadGame(MenuPageSaves __instance)
        {
            if (!publicSavesMenuOpen)
            {
                currentSaveFileName = null;
                return true;
            }

            currentSaveFileName = __instance.currentSaveFileName;

            MenuPage prevPage = MenuManager.instance.currentMenuPage;
            MenuPageServerListCreateNew menuPageServerListCreateNew = MenuManager.instance.PageOpenOnTop(MenuPageIndex.ServerListCreateNew).GetComponent<MenuPageServerListCreateNew>();
            menuPageServerListCreateNew.menuPageParent = prevPage;
            
            string teamName = StatsManager.instance.SaveFileGetTeamName(currentSaveFileName);
            if (!string.IsNullOrWhiteSpace(teamName) && teamName != ChatCommands.defaultTeamName)
                menuPageServerListCreateNew.menuTextInput.textCurrent = teamName;
            else
                menuPageServerListCreateNew.menuTextInput.textCurrent = $"{SteamClient.Name}'s Lobby";
            return false;
        }
        
        // Lobby Name Input > Saves Menu
        [HarmonyPatch(typeof(MenuPageServerListCreateNew), "ExitPage")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerListCreateNew_ExitPage(MenuPageServerListCreateNew __instance)
        {
            if (!publicSavesMenuOpen)
                return true;
            
            MenuManager.instance.PageCloseAllExcept(MenuPageIndex.Saves);
            MenuManager.instance.PageSetCurrent(MenuPageIndex.Saves, __instance.menuPageParent);
            return false;
        }
        
        // Saves Menu > Server List
        [HarmonyPatch(typeof(MenuPageSaves), "OnGoBack")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnGoBack(MenuPageSaves __instance)
        {
            if (!publicSavesMenuOpen)
                return true;

            publicSavesMenuOpen = false;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.ServerList);
            return false;
        }
        
        // Cache the initial value of connectRandom when the lobby is created
        private static bool connectRandomCached;
        
        [HarmonyPatch(typeof(GameManager), "SetConnectRandom")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void GameManager_SetConnectRandom(bool _connectRandom)
        {
            connectRandomCached = GameManager.instance.connectRandom;
        }
        
        // Prevent NetworkConnect.OnJoinedRoom from creating an extra save file
        [HarmonyPatch(typeof(SemiFunc), "SaveFileCreate")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool SemiFunc_SaveFileCreate(NetworkConnect __instance)
        {
            if (!connectRandomCached)
                return true;

            // If creating a new save file, use the network server name as the team name
            if (string.IsNullOrWhiteSpace(currentSaveFileName))
            {
                if (!string.IsNullOrWhiteSpace(DataDirector.instance.networkServerName))
                    StatsManager.instance.teamName = DataDirector.instance.networkServerName;
                return true;
            }
            
            PluginLoader.StaticLogger.LogInfo("[Public Lobby] Loading Save File: " + currentSaveFileName);
            SemiFunc.SaveFileLoad(currentSaveFileName);
            
            return false;
        }
        
        // Override logic that prevents saving when connectRandom is true
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
            if (!PluginLoader.savePublicEnabled.Value || !connectRandomCached)
                return;
            
            GameManager.instance.connectRandom = false;
        }
        
        [HarmonyPatch(typeof(DataDirector), "SaveDeleteCheck")]
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyWrapSafe]
        private static void DataDirector_SaveDeleteCheck_Postfix()
        {
            if (!connectRandomCached)
                return;
            
            GameManager.instance.connectRandom = true;
        }
    }
}
