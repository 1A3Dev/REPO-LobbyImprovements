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
        internal static string defaultTeamName;

        private static bool ExecuteCommand(string _command)
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
                            
                            PlayerAvatar.instance.ChatMessageSpeak("Spawned Enemy", PlayerAvatar.instance.isCrouching);
                            return true;
                        }

                        PluginLoader.StaticLogger.LogInfo($"Available Enemies: {string.Join(", ", enemySetups.Select(x => Regex.Replace(x.name, "^Enemy - ", "").ToLower()).OrderBy(x => x))}");
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
                            
                            PlayerAvatar.instance.ChatMessageSpeak("Spawned Item", PlayerAvatar.instance.isCrouching);
                            return true;
                        }

                        PluginLoader.StaticLogger.LogInfo($"Available Items: {string.Join(", ", items.Select(x => Regex.Replace(x.name, "^Item ", "").ToLower()).OrderBy(x => x))}");
                    }
                    break;
                case "/setcash":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string cashNumString = string.Join(' ', commandArgs).ToLower();
                        if (int.TryParse(cashNumString, out var cashNum))
                        {
                            SemiFunc.StatSetRunCurrency(cashNum);
                            if (SemiFunc.RunIsShop())
                            {
                                RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.Shop);
                            }
                            else if (SemiFunc.RunIsLevel() && (RoundDirector.instance?.extractionPointCurrent?.currentState ?? 0) > ExtractionPoint.State.Idle)
                            {
                                RunManager.instance.ChangeLevel(false, false);
                            }
                            else
                            {
                                PlayerAvatar.instance.ChatMessageSpeak("Updated Cash Amount", PlayerAvatar.instance.isCrouching);
                            }

                            return true;
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
                            if (SemiFunc.RunIsShop())
                            {
                                RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.Shop);
                            }
                            else if (SemiFunc.RunIsLevel())
                            {
                                RunManager.instance.ChangeLevel(false, false);
                            }
                            else
                            {
                                PlayerAvatar.instance.ChatMessageSpeak("Updated Level Number", PlayerAvatar.instance.isCrouching);
                            }

                            return true;
                        }
                    }
                    break;
                case "/setname":
                    if (SemiFunc.IsMasterClientOrSingleplayer() && !string.IsNullOrWhiteSpace(StatsManager.instance.saveFileCurrent))
                    {
                        string teamName = string.Join(' ', commandArgs).Trim();
                        if (teamName == StatsManager.instance.teamName)
                            break;
                        
                        if (string.IsNullOrWhiteSpace(teamName))
                            teamName = defaultTeamName ?? "R.E.P.O.";
                        
                        StatsManager.instance.teamName = teamName;
                        SemiFunc.SaveFileSave();
                        PluginLoader.StaticLogger.LogInfo($"Updated name of {StatsManager.instance.saveFileCurrent} to {StatsManager.instance.teamName}");
                        
                        PlayerAvatar.instance.ChatMessageSpeak("Renamed Save File", PlayerAvatar.instance.isCrouching);
                        return true;
                    }
                    break;
                case "/setscene":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string levelName = string.Join(' ', commandArgs).ToLower();
                        if (levelName == "recording")
                        {
                            RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.Recording);
                            return true;
                        }

                        if (levelName == "shop")
                        {
                            RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.Shop);
                            return true;
                        }

                        if (levelName == "menu")
                        {
                            RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.LobbyMenu);
                            return true;
                        }

                        if (levelName == "random")
                        {
                            RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.RunLevel);
                            return true;
                        }

                        List<Level> levels = Resources.FindObjectsOfTypeAll<Level>()
                            .Where(x => x && x.name != "Level - Main Menu" && x.name != "Level - Splash Screen" && x.name != "Level - Tutorial")
                            .ToList();
                        RunManager.instance.debugLevel = levels.FirstOrDefault(x => Regex.Replace(x.name, "^Level - ", "").ToLower() == levelName);
                        if (RunManager.instance.debugLevel != null)
                        {
                            RunManager.instance.ChangeLevel(false, false);
                            RunManager.instance.debugLevel = null;
                            return true;
                        }

                        PluginLoader.StaticLogger.LogInfo($"Available Levels: {string.Join(", ", levels.Select(x => Regex.Replace(x.name, "^Level - ", "").ToLower()).Concat(["random"]).OrderBy(x => x))}");
                    }
                    break;
                case "/valuable":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        string itemName = string.Join(' ', commandArgs).ToLower();
                        List<ValuableObject> items = Resources.FindObjectsOfTypeAll<Level>()
                            .SelectMany(l => l.ValuablePresets
                                .SelectMany(p => p.tiny.Concat(p.small).Concat(p.medium).Concat(p.big).Concat(p.wide).Concat(p.tall).Concat(p.veryTall))
                                .Select(x => x.GetComponent<ValuableObject>())
                                .Where(x => x)
                            )
                            .Concat(Resources.FindObjectsOfTypeAll<ValuableObject>().Where(x => x.name.StartsWith("Enemy Valuable") || x.name.StartsWith("Surplus Valuable")))
                            .ToList();
                        ValuableObject itemToSpawn = items.FirstOrDefault(x => Regex.Replace(x.name, "^Valuable ", "").ToLower() == itemName);
                        if (itemToSpawn != null)
                        {
                            LevelPoint levelPoint = SemiFunc.LevelPointsGetClosestToPlayer();
                            Vector3 position = new Vector3(levelPoint.transform.position.x, levelPoint.transform.position.y + 1f, levelPoint.transform.position.z);

                            string itemPath = itemToSpawn.volumeType switch
                            {
                                ValuableVolume.Type.Tiny => ValuableDirector.instance.tinyPath+"/",
                                ValuableVolume.Type.Small => ValuableDirector.instance.smallPath+"/",
                                ValuableVolume.Type.Medium => ValuableDirector.instance.mediumPath+"/",
                                ValuableVolume.Type.Big => ValuableDirector.instance.bigPath+"/",
                                ValuableVolume.Type.Wide => ValuableDirector.instance.widePath+"/",
                                ValuableVolume.Type.Tall => ValuableDirector.instance.tallPath+"/",
                                ValuableVolume.Type.VeryTall => ValuableDirector.instance.veryTallPath+"/",
                                _ => ""
                            };
                            if (itemToSpawn.name.StartsWith("Enemy Valuable") || itemToSpawn.name.StartsWith("Surplus Valuable"))
                            {
                                itemPath = "";
                            }

                            GameObject _valuable = GameManager.instance.gameMode != 0
                                ? PhotonNetwork.InstantiateRoomObject($"{ValuableDirector.instance.resourcePath}{itemPath}{itemToSpawn.name}", position, levelPoint.transform.rotation)
                                : Object.Instantiate(itemToSpawn.gameObject, position, levelPoint.transform.rotation);
                            ValuableObject component = _valuable.GetComponent<ValuableObject>();
                            component.DollarValueSetLogic();
                            
                            PlayerAvatar.instance.ChatMessageSpeak("Spawned Valuable", PlayerAvatar.instance.isCrouching);
                            return true;
                        }

                        PluginLoader.StaticLogger.LogInfo($"Available Valuables: {string.Join(", ", items.Select(x => Regex.Replace(x.name, "( |^)Valuable( |$)", "", RegexOptions.IgnoreCase).ToLower()).OrderBy(x => x))}");
                    }
                    break;
            }
            
            return false;
        }

        [HarmonyPatch(typeof(PlayerAvatar), "ChatMessageSend")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool PlayerAvatar_ChatMessageSend(string _message)
        {
            return !ExecuteCommand(_message);
        }
        
        [HarmonyPatch(typeof(ChatManager), "Update")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool ChatManager_Update(ChatManager __instance)
        {
            if (__instance.chatState == ChatManager.ChatState.Active && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                string newMessage = $"{__instance.chatMessage}{GUIUtility.systemCopyBuffer}";
                __instance.chatMessage = newMessage.Substring(0, Math.Min(newMessage.Length, 50));
                __instance.chatText.text = __instance.chatMessage;
                ChatUI.instance.SemiUITextFlashColor(Color.cyan, 0.2f);
                ChatUI.instance.SemiUISpringShakeY(2f, 5f, 0.2f);
                MenuManager.instance.MenuEffectClick(MenuManager.MenuClickEffectType.Tick, pitch: 1f, volume: 0.2f, soundOnly: true);
            }
            
            if (SemiFunc.IsMultiplayer())
                return true; // Don't bother overwriting for multiplayer

            __instance.PossessionActive();
            if (__instance.playerAvatar && __instance.playerAvatar.isDisabled && (__instance.possessBatchQueue.Count > 0 || __instance.currentBatch != null))
            {
                __instance.InterruptCurrentPossessBatch();
            }
            if (SemiFunc.IsMainMenu()) // !IsMultiplayer -> IsMainMenu
            {
                ChatUI.instance.Hide();
                return false;
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
            if (SemiFunc.IsMainMenu()) // !IsMultiplayer -> IsMainMenu
            {
                if (__instance.chatState != ChatManager.ChatState.Inactive)
                    __instance.StateSet(ChatManager.ChatState.Inactive);
                __instance.chatActive = false;
                return false;
            }
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
        
        // Save File Naming
        [HarmonyPatch(typeof(StatsManager), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void StatsManager_Awake(StatsManager __instance)
        {
            if (defaultTeamName == null)
                defaultTeamName = __instance.teamName;
        }
        
        [HarmonyPatch(typeof(StatsManager), "ResetAllStats")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void StatsManager_ResetAllStats(StatsManager __instance)
        {
            if (defaultTeamName != null)
                __instance.teamName = defaultTeamName;
        }
    }
}