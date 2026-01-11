using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LobbyImprovements.Patches;
using Photon.Pun;
using UnityEngine;

namespace LobbyImprovements
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.SoftDependency)]
    internal class PluginLoader : BaseUnityPlugin
    {
        internal static List<string> modDevSteamIDs = [
            "76561198286895332", // 1A3
            "76561199523762804" // 1A3Test
        ];
        
        internal static Dictionary<string, string> namePrefixMap = new() {
            { "developer", "<color=#ff0062>[DEV]</color> " },
            { "tester", "<color=#ff8b00>[TESTER]</color> " }
        };
        
        internal static Dictionary<string, string> nameSuffixMap = new() {
        };
        
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private static bool initialized;

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }
        
        internal static ConfigEntry<bool> debugConsole;
        internal static ConfigEntry<KeyboardShortcut> debugConsoleKeybind;
        
        internal static ConfigEntry<bool> moonPhaseUIEnabled;
        internal static ConfigEntry<bool> splashScreenUIEnabled;
        
        internal static ConfigEntry<bool> singleplayerLobbyMenu;
        
        internal static ConfigEntry<int> maxPlayerCount;
        
        internal static ConfigEntry<bool> mainMenuOverhaulEnabled;
        
        // internal static ConfigEntry<bool> playerNamePrefixEnabled;
        internal static ConfigEntry<string> playerNamePrefixSelected;
        
        internal static ConfigEntry<bool> saveDeleteEnabled;
        internal static ConfigEntry<int> saveFileMaxAmount;
        internal static ConfigEntry<bool> savePublicEnabled;
        internal static ConfigEntry<bool> saveMatchmakingEnabled;
        
        internal static ConfigEntry<bool> testerOverlayEnabled;
        
        internal static bool mainMenuOverhaul;
        
        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            
            StaticLogger = Logger;
            StaticConfig = Config;
            
            #region Debug Console
            debugConsole = StaticConfig.Bind("Debug Console", "Enabled", false, "Enables the vanilla debug console.");
            debugConsoleKeybind = StaticConfig.Bind("Debug Console", "Keybind", new KeyboardShortcut(KeyCode.BackQuote));
            debugConsoleKeybind.SettingChanged += (sender, args) => {
                if(DebugConsoleUI.instance){
                    DebugConsoleUI.instance.toggleKey = debugConsoleKeybind.Value.MainKey != KeyCode.None ? debugConsoleKeybind.Value.MainKey : KeyCode.BackQuote;
                }
            };
            #endregion

            #region Fast Startup
            moonPhaseUIEnabled = StaticConfig.Bind("Fast Startup", "Moon Phase", true, "Should the moon phase animation be shown?");
            splashScreenUIEnabled = StaticConfig.Bind("Fast Startup", "Splash Screen", true, "Should the splash screen be shown?");
            #endregion
            
            #region Main Menu
            mainMenuOverhaulEnabled = StaticConfig.Bind("Main Menu", "Improved Layout", false, "Improved layout for the main menu to reduce clicks.");
            if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("nickklmao.menulib")){
                mainMenuOverhaulEnabled.SettingChanged += (sender, args) => {
                    PublicLobbySaves.ToggleLobbyTypeSaving(GameManager.LobbyTypes.Public, savePublicEnabled.Value || mainMenuOverhaulEnabled.Value);
                    PublicLobbySaves.ToggleLobbyTypeSaving(GameManager.LobbyTypes.Matchmaking, saveMatchmakingEnabled.Value || mainMenuOverhaulEnabled.Value);
                };
                try{
                    harmony.PatchAll(typeof(MenuPageV2));
                }catch(Exception e){
                    StaticLogger.LogError("MenuPageV2 Patch Failed: " + e);
                }
                #if DEBUG
                mainMenuOverhaul = mainMenuOverhaulEnabled.Value;
                #endif
            }else if(mainMenuOverhaulEnabled.Value){
                StaticLogger.LogWarning("The 'Improved Layout' of the main menu requires the MenuLib mod. Please install it if you wish to use that feature.");
            }
            #endregion
            
            #region Saves
            saveDeleteEnabled = StaticConfig.Bind("Saves", "Deletion", true, "Should saves be automatically deleted when everyone dies?");
            saveFileMaxAmount = StaticConfig.Bind("Saves", "Max Amount", 10, new ConfigDescription("What is the max amount of save files? 0 = Unlimited", new AcceptableValueRange<int>(0, 100)));
            savePublicEnabled = StaticConfig.Bind("Saves", "Public Lobbies", true, "Should public lobbies be saved?");
            savePublicEnabled.SettingChanged += (sender, args) => {
                PublicLobbySaves.ToggleLobbyTypeSaving(GameManager.LobbyTypes.Public, savePublicEnabled.Value || mainMenuOverhaulEnabled.Value);
            };
            saveMatchmakingEnabled = StaticConfig.Bind("Saves", "Random Lobbies", true, "Should random matchmaking lobbies be saved?");
            saveMatchmakingEnabled.SettingChanged += (sender, args) => {
                PublicLobbySaves.ToggleLobbyTypeSaving(GameManager.LobbyTypes.Matchmaking, savePublicEnabled.Value || mainMenuOverhaulEnabled.Value);
            };
            #endregion

            #region Singleplayer
            singleplayerLobbyMenu = StaticConfig.Bind("Singleplayer", "Lobby Menu", false, "Should the lobby menu be enabled in singleplayer?");
            #endregion

            #region Multiplayer
            maxPlayerCount = StaticConfig.Bind("Multiplayer", "Max Players", 6, new ConfigDescription("Sets the maximum number of players allowed in a multiplayer lobby. 0 = Default", new AcceptableValueRange<int>(0, 20)));
            maxPlayerCount.SettingChanged += (sender, args) => {
                if(GameManager.instance) GameManager.instance.maxPlayers = maxPlayerCount.Value > 0 ? maxPlayerCount.Value : GameManager.maxPlayersDefault;
            };
            #endregion

            #region Tester Overlay
            testerOverlayEnabled = StaticConfig.Bind("Tester Overlay", "Enabled", false, "Should the tester overlay be shown?");
            testerOverlayEnabled.SettingChanged += (sender, args) => {
                if(!Debug.isDebugBuild && DebugCommandHandler.instance){
                    DebugCommandHandler.instance.debugOverlay = testerOverlayEnabled.Value;
                }
            };
            #endregion
            
            #region Name Prefix
            try{
                harmony.PatchAll(typeof(PlayerNamePrefix_SteamManager));
            }catch(Exception e){
                StaticLogger.LogError("PlayerNamePrefix Patch Failed: " + e);

                if(playerNamePrefixSelected == null){
                    playerNamePrefixSelected = StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?"));
                    playerNamePrefixSelected.SettingChanged += (sender, args) => {
                        PlayerNamePrefix_SteamManager.WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar.instance);
                        if(GameManager.Multiplayer()){
                            PlayerNamePrefix_SemiFunc.PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", playerNamePrefixSelected.Value);
                        }
                    };
                }
            }
            #endregion

            #region Patches
            try{
                harmony.PatchAll(typeof(ChatCommands));
            }catch(Exception e){
                StaticLogger.LogError("ChatCommands Patch Failed: " + e);
            }
            
            try{
                harmony.PatchAll(typeof(FastStartup));
            }catch(Exception e){
                StaticLogger.LogError("FastStartup Patch Failed: " + e);
            }
            
            try{
                harmony.PatchAll(typeof(MenuPageLobbySP));
            }catch(Exception e){
                StaticLogger.LogError("MenuPageLobbySP Patch Failed: " + e);
            }
            
            try{
                harmony.PatchAll(typeof(PublicLobbySaves));
            }catch(Exception e){
                StaticLogger.LogError("PublicLobbySaves Patch Failed: " + e);
            }
            
            try{
                harmony.PatchAll(typeof(ServerListSearch));
            }catch(Exception e){
                StaticLogger.LogError("ServerListSearch Patch Failed: " + e);
            }
            
            try{
                harmony.PatchAll(typeof(TesterOverlayPatches));
            }catch(Exception e){
                StaticLogger.LogError("TesterOverlay Patch Failed: " + e);
            }
            #endregion

            StaticLogger.LogInfo("Patches Loaded");
        }

#if DEBUG
        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            StaticLogger.LogInfo("Patches Unloaded");
        }
#endif
    }
}