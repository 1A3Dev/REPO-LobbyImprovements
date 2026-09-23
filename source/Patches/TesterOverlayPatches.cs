using HarmonyLib;
using Platforms;
using Steamworks;
using UnityEngine;

namespace LobbyImprovements.Patches;

[HarmonyPatch]
public class TesterOverlayPatches
{
	[HarmonyPatch(typeof(DebugTesterUI), "Start")]
	[HarmonyPrefix]
	[HarmonyWrapSafe]
	private static bool DebugTesterUI_Start(DebugTesterUI __instance)
	{
		return Debug.isDebugBuild || PlatformManager.instance.developerFlags.debug_console || PlatformManager.instance.developerFlags.debug_overlay;
	}
	
	// [HarmonyPatch(typeof(GameManager), "Awake")]
	// [HarmonyPostfix]
	// [HarmonyWrapSafe]
	// private static void GameManager_Awake(GameManager __instance)
	// {
	// 	if(__instance != GameManager.instance || PluginLoader.maxPlayerCount.Value <= 0) return;
	// 	if(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("Linkoid.Repo.RoboUnion")){
	// 		PluginLoader.StaticLogger.LogWarning("Detected RoboUnion mod, skipping max player count override to prevent potential conflicts.");
	// 		return;
	// 	}
	// 	int _maxPlayers = PluginLoader.maxPlayerCount.Value > 0 ? PluginLoader.maxPlayerCount.Value : GameManager.maxPlayersDefault;
	// 	__instance.SetMaxPlayers(_maxPlayers);
	// }
}