using System;
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
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private static bool initialized;

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }
        
        // internal static ConfigEntry<bool> playerNamePrefixEnabled;
        internal static ConfigEntry<string> playerNamePrefixSelected;
        
        internal static ConfigEntry<bool> singleplayerLobbyMenu;
        
        internal static ConfigEntry<bool> saveDeleteEnabled;
        internal static ConfigEntry<bool> savePublicEnabled;
        
        internal static ConfigEntry<bool> testerOverlayEnabled;
        internal static ConfigEntry<bool> testerOverlayModule;

        internal static ConfigEntry<bool> moonPhaseUIEnabled;
        internal static ConfigEntry<bool> splashScreenUIEnabled;
        
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
            
            // Tester Overlay
            testerOverlayEnabled = StaticConfig.Bind("Tester Overlay", "Enabled", false, "Should the tester overlay be shown?");
            testerOverlayEnabled.SettingChanged += (sender, args) =>
            {
                SetupTesterOverlay(testerOverlayEnabled.Value);
            };
            testerOverlayModule = StaticConfig.Bind("Tester Overlay", "Show Module", true, "Should the name of the module you are in be shown?");
            SetupTesterOverlay(testerOverlayEnabled.Value);
            
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

            if (BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("nickklmao.menulib"))
            {
                mainMenuOverhaulEnabled = StaticConfig.Bind("Main Menu", "Improved Layout", false, "Reduces the number of clicks to access some parts of the main menu.");
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

            StaticLogger.LogInfo("Patches Loaded");
        }

        private static void SetupTesterOverlay(bool _enabled)
        {
            GameObject overlayObj = GameObject.Find("TesterOverlay");
            if (_enabled && overlayObj == null && !Debug.isDebugBuild)
            {
                GameObject testerOverlayObj = new GameObject("TesterOverlay");
                testerOverlayObj.hideFlags = HideFlags.HideAndDontSave;
                DontDestroyOnLoad(testerOverlayObj);
                testerOverlayObj.AddComponent<TesterOverlay>();
            }
            else if (!_enabled && overlayObj != null)
            {
                Destroy(overlayObj);
            }
        }

#if DEBUG
        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            SetupTesterOverlay(false);
            StaticLogger.LogInfo("Patches Unloaded");
        }
#endif
    }
}