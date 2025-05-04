using HarmonyLib;
using Steamworks;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;
using Steamworks.Data;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class RecentlyPlayedWith
    {
        internal static HashSet<ulong> PlayerList = new HashSet<ulong>();
        internal static void SetPlayedWith(ulong[] playerSteamIds, string debugType)
        {
            playerSteamIds = playerSteamIds.Where(x => x != 0f && x != SteamClient.SteamId && !PlayerList.Contains(x)).ToArray();
            if (playerSteamIds.Length > 0)
            {
                foreach (ulong playerSteamId in playerSteamIds)
                {
                    PlayerList.Add(playerSteamId);
                    SteamFriends.SetPlayedWith(playerSteamId);
                }
                PluginLoader.StaticLogger.LogInfo($"Set recently played with ({debugType}) for {playerSteamIds.Length} players.");
                PluginLoader.StaticLogger.LogDebug($"Set recently played with ({debugType}): {string.Join(", ", playerSteamIds)}");
            }
        }

        [HarmonyPatch(typeof(SteamManager), "OnLobbyEntered")]
        [HarmonyPostfix]
        private static void OnLobbyEntered(ref SteamManager __instance)
        {
            if (PlayerList.Count > 0)
            {
                PlayerList.Clear();
                PluginLoader.StaticLogger.LogInfo($"Cleared recently played with");
            }

            if (PluginLoader.recentlyPlayedWithOrbit.Value)
            {
                SetPlayedWith(__instance.currentLobby.Members.Select(x => x.Id.Value).ToArray(), "OnLobbyEntered");
            }
        }

        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberJoined")]
        [HarmonyPostfix]
        private static void OnLobbyMemberJoined(Lobby _lobby, Friend _friend)
        {
            if (PluginLoader.recentlyPlayedWithOrbit.Value)
            {
                SetPlayedWith(new [] { _friend.Id.Value }, "OnLobbyMemberJoined");
            }
        }
        
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberLeft")]
        [HarmonyPostfix]
        private static void OnLobbyMemberLeft(Lobby _lobby, Friend _friend)
        {
            PlayerList.Remove(_friend.Id.Value);
            PluginLoader.StaticLogger.LogInfo($"Removing {_friend.Id.Value} from recently played with.");
        }
        
        [HarmonyPatch(typeof(RunManager), "SetRunLevel")]
        [HarmonyPostfix]
        private static void SetRunLevel()
        {
            if (!PluginLoader.recentlyPlayedWithOrbit.Value && SteamManager.instance != null && SteamManager.instance.currentLobby.Id.IsValid)
            {
                SetPlayedWith(SteamManager.instance.currentLobby.Members.Select(x => x.Id.Value).ToArray(), "SetRunLevel");
            }
        }
    }
}
