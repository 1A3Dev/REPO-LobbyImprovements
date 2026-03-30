using HarmonyLib;
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
		return Debug.isDebugBuild || SemiFunc.DebugDev();
	}
	
	[HarmonyPatch(typeof(GameManager), "Start")]
	[HarmonyPostfix]
	[HarmonyWrapSafe]
	private static void GameManager_Start(GameManager __instance)
	{
		__instance.maxPlayers = PluginLoader.maxPlayerCount.Value > 0 ? PluginLoader.maxPlayerCount.Value : GameManager.maxPlayersDefault;
	}
}