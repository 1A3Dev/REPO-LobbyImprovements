using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Photon.Pun;
using UnityEngine;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class ChatCommands
    {
        [HarmonyPatch(typeof(SemiFunc), "Command")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SemiFunc_Command(string _command)
        {
            string[] args = _command.Split(' ');
            string command = args.Length > 0 ? args[0].ToLower() : "";
            string[] commandArgs = args.Length > 1 ? args[1..] : [];
            
            switch (command)
            {
                case "/enemy":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string enemyName = string.Join(' ', commandArgs).ToLower();
                        List<EnemySetup> enemySetups = EnemyDirector.instance.enemiesDifficulty1
                            .Concat(EnemyDirector.instance.enemiesDifficulty2)
                            .Concat(EnemyDirector.instance.enemiesDifficulty3)
                            .Where(x => x && !x.name.Contains("Enemy Group - "))
                            .ToList();
                        EnemySetup enemySetup = enemySetups.FirstOrDefault(x => Regex.Replace(x.name, "^Enemy - ", "").ToLower() == enemyName);
                        if (enemySetup != null)
                        {
                            EnemyDirector.instance.debugSpawnClose = true;
                            EnemyDirector.instance.debugNoSpawnedPause = true;
                            EnemyDirector.instance.debugNoSpawnIdlePause = true;
                            EnemyDirector.instance.debugEnemyEnableTime = 999f;
                            EnemyDirector.instance.debugEnemyDisableTime = 3f;
                            LevelPoint levelPoint = SemiFunc.LevelPointsGetClosestToPlayer();
                            // LevelGenerator.Instance.EnemySpawn(enemySetup, levelPoint.transform.position);
                            foreach (GameObject spawnObject in enemySetup.spawnObjects)
                            {
                                GameObject gameObject = GameManager.instance.gameMode != 0
                                    ? PhotonNetwork.InstantiateRoomObject($"{LevelGenerator.Instance.ResourceEnemies}/{spawnObject.name}", levelPoint.transform.position, Quaternion.identity)
                                    : Object.Instantiate(spawnObject, levelPoint.transform.position, Quaternion.identity);

                                EnemyParent component = gameObject.GetComponent<EnemyParent>();
                                if (!component) continue;

                                component.SetupDone = true;
                                gameObject.GetComponentInChildren<Enemy>()?.EnemyTeleported(levelPoint.transform.position);
                                component.firstSpawnPointUsed = true;
                            }

                            PlayerController.instance.playerAvatarScript.ChatMessageSend("Spawned Enemy!");
                        }
                        else
                        {
                            PluginLoader.StaticLogger.LogInfo($"Available Enemies: {string.Join(", ", enemySetups.Select(x => Regex.Replace(x.name, "^Enemy - ", "").ToLower()).OrderBy(x => x))}");
                        }
                    }
                    break;
                case "/item":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string itemName = string.Join(' ', commandArgs).ToLower();
                        List<Item> items = Resources.FindObjectsOfTypeAll<Item>()
                            .Where(x => x)
                            .ToList();
                        Item itemToSpawn = items.FirstOrDefault(x => Regex.Replace(x.name, "^Item ", "").ToLower() == itemName);
                        if (itemToSpawn != null)
                        {
                            LevelPoint levelPoint = SemiFunc.LevelPointsGetClosestToPlayer();
                            Vector3 position = new Vector3(levelPoint.transform.position.x, levelPoint.transform.position.y + 1f, levelPoint.transform.position.z);
                            GameObject gameObject = GameManager.instance.gameMode != 0
                                ? PhotonNetwork.InstantiateRoomObject($"Items/{itemToSpawn.prefab.name}", position, levelPoint.transform.rotation)
                                : Object.Instantiate(itemToSpawn.prefab, position, levelPoint.transform.rotation);

                            PlayerController.instance.playerAvatarScript.ChatMessageSend("Spawned Item!");
                        }
                        else
                        {
                            PluginLoader.StaticLogger.LogInfo($"Available Items: {string.Join(", ", items.Select(x => Regex.Replace(x.name, "^Item ", "").ToLower()).OrderBy(x => x))}");
                        }
                    }
                    break;
                case "/valuable":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string itemName = string.Join(' ', commandArgs).ToLower();
                        List<ValuableObject> items = Resources.FindObjectsOfTypeAll<ValuableObject>()
                            .Where(x => x)
                            .ToList();
                        ValuableObject itemToSpawn = items.FirstOrDefault(x => Regex.Replace(x.name, "( |^)Valuable( |$)", "", RegexOptions.IgnoreCase).ToLower() == itemName);
                        if (itemToSpawn != null)
                        {
                            LevelPoint levelPoint = SemiFunc.LevelPointsGetClosestToPlayer();
                            Vector3 position = new Vector3(levelPoint.transform.position.x, levelPoint.transform.position.y + 1f, levelPoint.transform.position.z);
                            GameObject _valuable = GameManager.instance.gameMode != 0
                                ? PhotonNetwork.InstantiateRoomObject($"{ValuableDirector.instance.resourcePath}/{itemToSpawn.name}", position, levelPoint.transform.rotation)
                                : Object.Instantiate(itemToSpawn.gameObject, position, levelPoint.transform.rotation);
                            ValuableObject component = _valuable.GetComponent<ValuableObject>();
                            component.DollarValueSetLogic();
                            PlayerController.instance.playerAvatarScript.ChatMessageSend("Spawned Valuable!");
                        }
                        else
                        {
                            PluginLoader.StaticLogger.LogInfo($"Available Valuables: {string.Join(", ", items.Select(x => Regex.Replace(x.name, "( |^)Valuable( |$)", "", RegexOptions.IgnoreCase).ToLower()).OrderBy(x => x))}");
                        }
                    }
                    break;
                case "/level":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string levelName = string.Join(' ', commandArgs).ToLower();
                        if (levelName == "recording")
                        {
                            RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.Recording);
                        }
                        else if (levelName == "shop")
                        {
                            RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.Shop);
                        }
                        else if (levelName == "menu")
                        {
                            RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.LobbyMenu);
                        }
                        else
                        {
                            List<Level> levels = Resources.FindObjectsOfTypeAll<Level>()
                                .Where(x => x && x.name != "Level - Main Menu" && x.name != "Level - Splash Screen" && x.name != "Level - Tutorial")
                                .ToList();
                            RunManager.instance.debugLevel = levels.FirstOrDefault(x => Regex.Replace(x.name, "^Level - ", "").ToLower() == levelName);
                            if (RunManager.instance.debugLevel != null)
                            {
                                RunManager.instance.ChangeLevel(true, false);
                                RunManager.instance.debugLevel = null;
                            }
                            else
                            {
                                PluginLoader.StaticLogger.LogInfo($"Available Levels: {string.Join(", ", levels.Select(x => Regex.Replace(x.name, "^Level - ", "").ToLower()).OrderBy(x => x))}");
                            }
                        }
                    }
                    break;
                case "/setcash":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string fpsString = string.Join(' ', commandArgs).ToLower();
                        if (int.TryParse(fpsString, out var cashNum))
                        {
                            SemiFunc.StatSetRunCurrency(cashNum);
                        }
                    }
                    break;
                case "/setlevel":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string levelNumString = string.Join(' ', commandArgs).ToLower();
                        if (int.TryParse(levelNumString, out var levelNum) && levelNum > 0)
                        {
                            RunManager.instance.levelsCompleted = levelNum - 1;
                            SemiFunc.StatSetRunLevel(RunManager.instance.levelsCompleted);
                        }
                    }
                    break;
            }
        }
        
        [HarmonyPatch(typeof(ChatManager), "Update")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool ChatManager_Update(ChatManager __instance)
        {
            if (__instance.chatState == ChatManager.ChatState.Active && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                __instance.chatMessage = $"{__instance.chatMessage}{GUIUtility.systemCopyBuffer}".Substring(0, 50);
                __instance.chatText.text = __instance.chatMessage;
                ChatUI.instance.SemiUITextFlashColor(Color.cyan, 0.2f);
                ChatUI.instance.SemiUISpringShakeY(2f, 5f, 0.2f);
                MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, pitch: 1f, volume: 0.2f, soundOnly: true);
            }
            
            if (SemiFunc.IsMultiplayer()) return true;

            __instance.PossessionActive();
            if (__instance.playerAvatar && __instance.playerAvatar.isDisabled && (__instance.possessBatchQueue.Count > 0 || __instance.currentBatch != null))
            {
                __instance.InterruptCurrentPossessBatch();
            }
            if (!LevelGenerator.Instance.Generated)
            {
                __instance.NewLevelResets();
                return false;
            }
            __instance.ImportantFetches();
            __instance.PossessChatCustomLogic();
            if (!__instance.textMeshFetched || !__instance.localPlayerAvatarFetched)
            {
                return false;
            }
            switch (__instance.chatState)
            {
                case ChatManager.ChatState.Inactive:
                    __instance.StateInactive();
                    break;
                case ChatManager.ChatState.Active:
                    __instance.StateActive();
                    break;
                case ChatManager.ChatState.Possessed:
                    __instance.StatePossessed();
                    break;
                case ChatManager.ChatState.Send:
                    __instance.StateSend();
                    break;
            }
            __instance.PossessChatCustomLogic();
            if (__instance.spamTimer > 0f)
            {
                __instance.spamTimer -= Time.deltaTime;
            }
            if (SemiFunc.FPSImpulse15() && __instance.betrayalActive && PlayerController.instance.playerAvatarScript.RoomVolumeCheck.inTruck)
            {
                __instance.PossessCancelSelfDestruction();
            }
            
            return false;
        }
    }
}