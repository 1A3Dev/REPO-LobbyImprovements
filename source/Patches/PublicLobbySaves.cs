using System.Runtime.CompilerServices;
using HarmonyLib;
using Steamworks;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class PublicLobbySaves
    {
        internal static bool publicSavesMenuOpen;
        
        public static void ToggleLobbyTypeSaving(GameManager.LobbyTypes lobbyType, bool state)
        {
            if(StatsManager.instance) return;
            
            if(state && !StatsManager.instance.savedLobbyTypes.Contains(lobbyType)){
                StatsManager.instance.savedLobbyTypes.Add(lobbyType);
            }else if(!state && StatsManager.instance.savedLobbyTypes.Contains(lobbyType)){
                StatsManager.instance.savedLobbyTypes.Remove(lobbyType);
            }
        }
        
        // Server List Menu -> Create New -> Saves Menu
        [HarmonyPatch(typeof(MenuPageServerList), "ButtonCreateNew")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_ButtonCreateNew(MenuPageServerList __instance)
        {
            if (!PluginLoader.savePublicEnabled.Value || PluginLoader.mainMenuOverhaul)
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
            if (SemiFunc.MainMenuIsMultiplayer() && !PluginLoader.mainMenuOverhaul)
            {
                __instance.gameModeHeader.text = publicSavesMenuOpen ? "Public Multiplayer" : "Private Multiplayer";
            }
            else
            {
                publicSavesMenuOpen = false;
            }

            __instance.maxSaveFiles = PluginLoader.saveFileMaxAmount.Value;
        }
        
        // Saves Menu > New Game/Load Save > Server Name (Skip Confirmation Popup)
        [HarmonyPatch(typeof(MenuButton), "OnSelect")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool MenuButton_OnSelect(MenuButton __instance)
        {
            if (__instance.menuButtonPopUp?.headerText == "Start a new game?" && __instance.menuButtonPopUp?.bodyText == "Do you want to start a game?")
            {
                if (PluginLoader.mainMenuOverhaul && SemiFunc.MainMenuIsMultiplayer())
                {
                    MenuPageV2.NewGame_Internal(__instance);
                    return false;
                }
                
                if (publicSavesMenuOpen)
                {
                    MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>();
                    menuPageSaves?.OnNewGame();
                    return false;
                }
            }

            if (__instance.menuButtonPopUp?.headerText == "Load save?" && __instance.menuButtonPopUp?.bodyText == "Load this save file?")
            {
                if (PluginLoader.mainMenuOverhaul && SemiFunc.MainMenuIsMultiplayer())
                {
                    MenuPageV2.LoadGame_Internal(__instance);
                    return false;
                }
                
                if (publicSavesMenuOpen)
                {
                    MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>();
                    menuPageSaves?.OnLoadGame();
                    return false;
                }
            }

            return true;
        }
        
        // Saves Menu > New Game > Lobby Name Input
        [HarmonyPatch(typeof(MenuPageSaves), "OnNewGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPriority(Priority.First)]
        private static bool MenuPageSaves_OnNewGame(MenuPageSaves __instance)
        {
            if (!publicSavesMenuOpen || __instance.saveFiles.Count >= __instance.maxSaveFiles)
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
            if(!publicSavesMenuOpen) return true;

            MenuPage prevPage = MenuManager.instance.currentMenuPage;
            MenuPageServerListCreateNew menuPageServerListCreateNew = MenuManager.instance.PageOpenOnTop(MenuPageIndex.ServerListCreateNew).GetComponent<MenuPageServerListCreateNew>();
            menuPageServerListCreateNew.menuPageParent = prevPage;
            menuPageServerListCreateNew.menuTextInput.textCurrent = $"{SteamClient.Name}'s Lobby";
            menuPageServerListCreateNew.saveFileName = __instance.currentSaveFileName;
            return false;
        }
        
        // Lobby Name Input > Saves Menu
        [HarmonyPatch(typeof(MenuPageServerListCreateNew), "ExitPage")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerListCreateNew_ExitPage(MenuPageServerListCreateNew __instance)
        {
            if (!publicSavesMenuOpen && !PluginLoader.mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Saves);
            return false;
        }
        
        // Saves Menu > Server List
        [HarmonyPatch(typeof(MenuPageSaves), "OnGoBack")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnGoBack(MenuPageSaves __instance)
        {
            if(!publicSavesMenuOpen) return true;

            publicSavesMenuOpen = false;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.ServerList);
            return false;
        }
        
        [HarmonyPatch(typeof(StatsManager), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void StatsManager_Awake_Postfix(StatsManager __instance)
        {
            ToggleLobbyTypeSaving(GameManager.LobbyTypes.Public, PluginLoader.savePublicEnabled.Value || PluginLoader.mainMenuOverhaulEnabled.Value);
            ToggleLobbyTypeSaving(GameManager.LobbyTypes.Matchmaking, PluginLoader.saveMatchmakingEnabled.Value || PluginLoader.mainMenuOverhaulEnabled.Value);
        }
    }
}
