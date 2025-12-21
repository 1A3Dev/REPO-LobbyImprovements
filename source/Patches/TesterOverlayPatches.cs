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
	
	[HarmonyPatch(typeof(ItemWalkieBox), "OutOfStore")]
	[HarmonyPrefix]
	[HarmonyWrapSafe]
	private static bool ItemWalkieBox_OutOfStore(ItemWalkieBox __instance){
		if(ObjectScreenshotTaker.instance && ObjectScreenshotTaker.instance.isTakingScreenshots){
			__instance.walkiesSpawned = true;
			return false;
		}

		return true;
	}
}