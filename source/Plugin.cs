using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using LobbyImprovements.Patches;
using Photon.Pun;

namespace LobbyImprovements
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.SoftDependency)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private static bool initialized;

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }
        
        // internal static ConfigEntry<bool> playerNamePrefixEnabled;
        internal static ConfigEntry<string> playerNamePrefixSelected;
        
        internal static ConfigEntry<bool> singleplayerLobbyMenu;
        
        internal static ConfigEntry<bool> saveDeleteEnabled;
        internal static ConfigEntry<bool> savePublicEnabled;

        internal static ConfigEntry<bool> moonPhaseUIEnabled;
        internal static ConfigEntry<bool> splashScreenUIEnabled;
        
        internal static ConfigEntry<bool> debugConsole;
        
        internal static ConfigEntry<bool> mainMenuOverhaulEnabled;
        
        internal static bool mainMenuOverhaul;
        
        private void Awake()
        {
            if (initialized) return;
            initialized = true;
            
            StaticLogger = Logger;
            StaticConfig = Config;

            try
            {
                harmony.PatchAll(typeof(ChatCommands));
            }
            catch (Exception e)
            {
                StaticLogger.LogError("ChatCommands Patch Failed: " + e);
            }
            
            // Player Name Prefixes
            // playerNamePrefixEnabled = StaticConfig.Bind("Name Prefix", "Enabled", true, "Should name prefixes of other players be shown?");
            // playerNamePrefixEnabled.SettingChanged += (sender, args) =>
            // {
            //     foreach (PlayerAvatar playerAvatar in GameDirector.instance.PlayerList)
            //     {
            //         PlayerNamePrefix.WorldSpaceUIParent_UpdatePlayerName(playerAvatar);
            //     }
            // };
            
            try
            {
                harmony.PatchAll(typeof(PlayerNamePrefix));
                // if (SteamManager.instance)
                // {
                //     PlayerNamePrefix.SteamManager_Awake(SteamManager.instance);
                // }
            }
            catch (Exception e)
            {
                StaticLogger.LogError("PlayerNamePrefix Patch Failed: " + e);

                if (playerNamePrefixSelected == null)
                {
                    playerNamePrefixSelected = StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?"));
                    playerNamePrefixSelected.SettingChanged += (sender, args) =>
                    {
                        PlayerNamePrefix.WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar.instance);
                        if (GameManager.Multiplayer())
                        {
                            PlayerNamePrefix.PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", playerNamePrefixSelected.Value);
                        }
                    };
                }
            }
            
            saveDeleteEnabled = StaticConfig.Bind("Saves", "Deletion", true, "Should saves be automatically deleted when everyone dies?");
            savePublicEnabled = StaticConfig.Bind("Saves", "Public Lobbies", true, "Should public lobbies have save files?");
            try
            {
                harmony.PatchAll(typeof(PublicLobbySaves));
            }
            catch (Exception e)
            {
                StaticLogger.LogError("PublicLobbySaves Patch Failed: " + e);
            }
            
            try
            {
                harmony.PatchAll(typeof(ServerListSearch));
            }
            catch (Exception e)
            {
                StaticLogger.LogError("ServerListSearch Patch Failed: " + e);
            }
            
            singleplayerLobbyMenu = StaticConfig.Bind("Singleplayer", "Lobby Menu", false, "Should the lobby menu be enabled in singleplayer?");
            try
            {
                harmony.PatchAll(typeof(MenuPageLobbySP));
            }
            catch (Exception e)
            {
                StaticLogger.LogError("MenuPageLobbySP Patch Failed: " + e);
            }
            
            moonPhaseUIEnabled = StaticConfig.Bind("Fast Startup", "Moon Phase", true, "Should the moon phase animation be shown?");
            splashScreenUIEnabled = StaticConfig.Bind("Fast Startup", "Splash Screen", true, "Should the splash screen be shown?");
            try
            {
                harmony.PatchAll(typeof(FastStartup));
            }
            catch (Exception e)
            {
                StaticLogger.LogError("FastStartup Patch Failed: " + e);
            }

            debugConsole = StaticConfig.Bind("Debug", "Debug Console", true, "Enables the vanilla debug console.");
            
            mainMenuOverhaulEnabled = StaticConfig.Bind("Main Menu", "Improved Layout", false, "Reduces the number of clicks to access some parts of the main menu.");
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("nickklmao.menulib"))
            {
                try
                {
                    harmony.PatchAll(typeof(MenuPageV2));
                }
                catch (Exception e)
                {
                    StaticLogger.LogError("MenuPageV2 Patch Failed: " + e);
                }
#if DEBUG
                mainMenuOverhaul = mainMenuOverhaulEnabled.Value;
#endif
            }
            else if (mainMenuOverhaulEnabled.Value)
            {
                StaticLogger.LogWarning("The 'Improved Layout' of the main menu requires the MenuLib mod. Please install it if you wish to use that feature.");
            }

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