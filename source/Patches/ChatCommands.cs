using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class ChatCommands
    {
        [HarmonyPatch(typeof(DebugCommandHandler), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void DebugCommandHandler_Start()
        {
            DebugCommandHandler.instance.Register(new DebugCommandHandler.ChatCommand(
                name: "screenshot",
                description: "Screenshot all enemies, items, modules, or valuables in the current room.",
                isEnabled: () => SemiFunc.IsMasterClientOrSingleplayer() && SemiFunc.RunIsRecording(),
                execute: (isDebugConsole, args) => {
                    if (!ObjectScreenshotTaker.instance)
                    {
                        GameObject screenshotTakerObj = new GameObject("ObjectScreenshotTaker");
                        screenshotTakerObj.hideFlags = HideFlags.HideAndDontSave;
                        screenshotTakerObj.AddComponent<ObjectScreenshotTaker>();
                    }
                    
                    string ssType = string.Join(' ', args).ToLower();
                    if (ssType == "enemies")
                    {
                        if (!ObjectScreenshotTaker.instance.isTakingScreenshots)
                        {
                            ObjectScreenshotTaker.instance.StartCoroutine(ObjectScreenshotTaker.instance.TakeScreenshotsOfEnemies());
                            DebugCommandHandler.instance.CommandSuccessEffect();
                            return;
                        }
                    }
                    else if (ssType == "items")
                    {
                        if (!ObjectScreenshotTaker.instance.isTakingScreenshots)
                        {
                            ObjectScreenshotTaker.instance.StartCoroutine(ObjectScreenshotTaker.instance.TakeScreenshotsOfItems());
                            DebugCommandHandler.instance.CommandSuccessEffect();
                            return;
                        }
                    }
                    else if (ssType == "modules")
                    {
                        if (!ObjectScreenshotTaker.instance.isTakingScreenshots)
                        {
                            ObjectScreenshotTaker.instance.StartCoroutine(ObjectScreenshotTaker.instance.TakeScreenshotsOfModules());
                            DebugCommandHandler.instance.CommandSuccessEffect();
                            return;
                        }
                    }
                    else if (ssType == "valuables")
                    {
                        if (!ObjectScreenshotTaker.instance.isTakingScreenshots)
                        {
                            ObjectScreenshotTaker.instance.StartCoroutine(ObjectScreenshotTaker.instance.TakeScreenshotsOfValuables());
                            DebugCommandHandler.instance.CommandSuccessEffect();
                            return;
                        }
                    }
                    DebugCommandHandler.instance.CommandFailedEffect();
                },
                suggest: (isDebugConsole, partial, args) => {
                    List<string> groups = new List<string> { "enemies", "items", "modules", "valuables" };
                    return args.Length <= 1 ? groups.Where(g => args.Length == 0 || g.ToLower().StartsWith(args[0].ToLower())).ToList() : new List<string>();
                }
            ));
            
            DebugCommandHandler.instance.Register(new DebugCommandHandler.ChatCommand(
                name: "lobbymenu",
                description: "Return to the lobby menu. This may cause issues!",
                isEnabled: () => SemiFunc.IsMasterClient() && DebugCommandHandler.instance.IsInGame() && !SemiFunc.RunIsLobbyMenu(),
                debugOnly: false,
                execute: (isDebugConsole, args) => {
                    NetworkManager.instance.DestroyAll(); // Fixes objects spawned in previous levels being respawned for late joiners
                    RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.LobbyMenu);
                    Debug.Log("Command Used: /level lobby menu");
                    DebugCommandHandler.instance.CommandSuccessEffect();
                }
            ));
        }
        
        [HarmonyPatch(typeof(SemiFunc), "DebugTester")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SemiFunc_DebugTester(ref bool __result)
        {
            if(PluginLoader.debugConsole.Value && PluginLoader.testerCommands.Value) __result = true;
        }
        
        [HarmonyPatch(typeof(DebugConsoleUI), "Start")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool DebugConsoleUI_Start(DebugConsoleUI __instance)
        {
            // Only override if it won't already run
            if (PluginLoader.debugConsole.Value && !SemiFunc.DebugTester() && !SemiFunc.DebugDev())
            {
                DebugConsoleUI.instance = __instance;
                return false;
            }

            return true;
        }
    }
}