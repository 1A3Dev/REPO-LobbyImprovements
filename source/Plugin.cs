using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace LobbyImprovements
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    internal class PluginLoader : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);

        private static bool initialized;

        public static PluginLoader Instance { get; private set; }

        internal static ManualLogSource StaticLogger { get; private set; }
        internal static ConfigFile StaticConfig { get; private set; }

        public static ConfigEntry<bool> recentlyPlayedWithOrbit;
        
        public static ConfigEntry<int> maxPlayerCount;

        private void Awake()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            Instance = this;
            StaticLogger = Logger;
            StaticConfig = Config;

            recentlyPlayedWithOrbit = StaticConfig.Bind("Steam", "Recent Players In Lobby", true, "Should players be added to the steam recent players list whilst you are in the lobby menu?");

            maxPlayerCount = StaticConfig.Bind("Player Count", "Max Players", 10, new ConfigDescription("How many players can be in a lobby?", new AcceptableValueRange<int>(1, 100)));

            Assembly patches = Assembly.GetExecutingAssembly();
            harmony.PatchAll(patches);

            StaticLogger.LogInfo("Patches Loaded");
        }
    }
}