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
}