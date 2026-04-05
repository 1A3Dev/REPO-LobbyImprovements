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
	
	[HarmonyPatch(typeof(GameManager), "Awake")]
	[HarmonyPostfix]
	[HarmonyWrapSafe]
	private static void GameManager_Awake(GameManager __instance)
	{
		int _maxPlayers = PluginLoader.maxPlayerCount.Value > 0 ? PluginLoader.maxPlayerCount.Value : GameManager.maxPlayersDefault;
		__instance.SetMaxPlayers(_maxPlayers);
	}
}