using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Steamworks;
using UnityEngine;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class PublicLobbySaves
    {
        internal static bool publicSavesMenuOpen;
        internal static bool compatNoSaveDelete;
        
        public static void ToggleLobbyTypeSaving(GameManager.LobbyTypes lobbyType, bool state)
        {
            if(!StatsManager.instance) return;
            
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

            if(!compatNoSaveDelete && Harmony.HasAnyPatches("com.PxntxrezStudio.nosavedelete")){
                compatNoSaveDelete = true;
                PluginLoader.StaticLogger.LogInfo("Compatibility Enabled: NoSaveDelete");
                PluginLoader.harmony.Unpatch(typeof(MenuPageSaves).GetMethod("OnNewGame"), HarmonyPatchType.Prefix, "com.PxntxrezStudio.nosavedelete");
            }
        }
        
        // Saves Menu > New Game/Load Save > Server Name (Skip Confirmation Popup)
        [HarmonyPatch(typeof(MenuButton), "OnSelect")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool MenuButton_OnSelect(MenuButton __instance)
        {
            if (__instance.menuButtonPopUp?.headerText == "New Game" && __instance.menuButtonPopUp?.bodyText == "Do you want to start a game?")
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
        
        // Saves Menu > New Game/Load Save > Lobby Name Input
        private static void OpenLobbyNameInput(string saveFileName, List<string> saveFileBackups)
        {
            MenuManager.instance.PagePopUpTextInput("Create Server", null, new Color(1f, 0.55f, 0f), "Enter a server name", null, $"{SteamClient.Name}'s Lobby", "Confirm", null, false, 60, false, _text => {
                DataDirector.instance.networkServerName = _text;

                MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Confirm, null, 1f, 1f);

                RunManager.instance.ResetProgress();
                GameManager.instance.SetConnectRandom(true);
                GameManager.instance.SetLobbyType(GameManager.LobbyTypes.Public);

                if (saveFileName != null)
                    SemiFunc.SaveFileLoad(saveFileName, saveFileBackups);
                else
                    SemiFunc.SaveFileCreate();

                GameManager.instance.localTest = false;
                RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.LobbyMenu);
                RunManager.instance.lobbyJoin = true;
            }, null);
            ServerListSearch.textInputType = PopUpTextInputType.ServerListCreateNew;
        }
        
        // Saves Menu > New Game > Lobby Name Input
        [HarmonyPatch(typeof(MenuPageSaves), "OnNewGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPriority(Priority.First)]
        private static bool MenuPageSaves_OnNewGame(MenuPageSaves __instance)
        {
            if (!publicSavesMenuOpen || (__instance.maxSaveFiles > 0 && __instance.saveFiles.Count >= __instance.maxSaveFiles))
                return true;

            OpenLobbyNameInput(null, null);
            return false;
        }
        
        // Saves Menu > Load Save > Lobby Name Input
        [HarmonyPatch(typeof(MenuPageSaves), "OnLoadGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnLoadGame(MenuPageSaves __instance)
        {
            if(!publicSavesMenuOpen) return true;

            OpenLobbyNameInput(__instance.currentSaveFileName, __instance.currentSaveFileBackups);
            return false;
        }
        
        // Saves Menu > Server List
        [HarmonyPatch(typeof(MenuPageSaves), "OnGoBack")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnGoBack(MenuPageSaves __instance)
        {
            if(!publicSavesMenuOpen){
                if(!SemiFunc.MainMenuIsMultiplayer()) return true;

                MenuManager.instance.PageCloseAll();
                MenuManager.instance.PageOpen(MenuPageIndex.Main);
                return false;

            }

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
