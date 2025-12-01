using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Steamworks;
using UnityEngine;

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
            if(!Debug.isDebugBuild) DebugCommandHandler.instance.debugOverlay = PluginLoader.testerOverlayEnabled.Value;
            
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
                isEnabled: () => SemiFunc.IsMasterClientOrSingleplayer() && DebugCommandHandler.instance.IsInGame(),
                debugOnly: false,
                execute: (isDebugConsole, args) => {
                    RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.LobbyMenu);
                    Debug.Log("Command Used: /level lobby menu");
                    DebugCommandHandler.instance.CommandSuccessEffect();
                }
            ));
        }
        
        [HarmonyPatch(typeof(RunManager), "RestartScene")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void RunManager_RestartScene(RunManager __instance){
            if(__instance.restarting && !__instance.restartingDone){
                if(GameDirector.instance && GameDirector.instance.PlayerList.All(p => p.outroDone)){
                    if(!__instance.lobbyJoin && !__instance.waitToChangeScene){
                        // Fix objects spawned in previous levels being respawned for late joiners
                        if(SemiFunc.RunIsLobbyMenu()){
                            NetworkManager.instance.DestroyAll();
                        }
                    }
                }
            }
        }
        
        // [HarmonyPatch(typeof(SteamManager), "Awake")]
        // [HarmonyPostfix]
        // [HarmonyWrapSafe]
        // private static void SteamManager_Awake(SteamManager __instance){
        //     if(!__instance.developerMode && PluginLoader.modDevSteamIDs.Contains(SteamClient.SteamId.ToString())){
        //         __instance.developerMode = true;
        //         Debug.Log($"DEVELOPER MODE: {SteamClient.Name.ToUpper()} (MODDED)");
        //     }
        // }
        
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
            // Override keybind if set to something else
            KeyCode toggleKey = PluginLoader.debugConsoleKeybind.Value.MainKey;
            if(toggleKey != KeyCode.None && toggleKey != KeyCode.BackQuote){
                __instance.toggleKey = toggleKey;
            }

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