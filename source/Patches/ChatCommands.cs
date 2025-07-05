using System.Linq;
using HarmonyLib;
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
            if (!SemiFunc.IsMasterClientOrSingleplayer()) return;
            if (!Debug.isDebugBuild) return; // Only allow on tester build
            
            string[] args = _command.Split(' ');
            string command = args.Length > 0 ? args[0].ToLower() : "";
            string[] commandArgs = args.Length > 1 ? args[1..] : [];
            
            switch (command)
            {
                case "/enemy":
                    if (SemiFunc.IsMasterClientOrSingleplayer())
                    {
                        var enemySetups = EnemyDirector.instance.enemiesDifficulty1
                            .Concat(EnemyDirector.instance.enemiesDifficulty2)
                            .Concat(EnemyDirector.instance.enemiesDifficulty3);
                        string enemyName = string.Join(' ', commandArgs).ToLower();
                        EnemySetup enemySetup = enemySetups.FirstOrDefault(x => x.name.Replace("Enemy Group - ", "").Replace("Enemy - ", "").ToLower() == enemyName);
                        if (enemySetup != null)
                        {
                            LevelPoint levelPoint = SemiFunc.LevelPointsGetClosestToPlayer();
                            LevelGenerator.Instance.EnemySpawn(enemySetup, levelPoint.transform.position);
                            EnemyDirector.instance.debugSpawnClose = true;
                            EnemyDirector.instance.debugNoSpawnedPause = true;
                            EnemyDirector.instance.debugNoSpawnIdlePause = true;
                            EnemyDirector.instance.debugEnemyEnableTime = 999f;
                            EnemyDirector.instance.debugEnemyDisableTime = 3f;

                            PlayerController.instance.playerAvatarScript.ChatMessageSend("Spawned Enemy!");
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
                            RunManager.instance.debugLevel = RunManager.instance.levels.FirstOrDefault(x => x.name.Replace("Level - ", "").ToLower() == levelName);
                            if (RunManager.instance.debugLevel != null)
                            {
                                RunManager.instance.ChangeLevel(true, false);
                                RunManager.instance.debugLevel = null;
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