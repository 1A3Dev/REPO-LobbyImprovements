using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using Photon.Pun;
using Photon.Realtime;
using Steamworks;
using Steamworks.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class PlayerNamePrefix
    {
        private static bool prefixSingleRequest = true; // Should prefixes for all players only be requested once at game start (if false it will request the needed players on lobby join)
        private static string playerPrefixUrl = "https://api.1a3.uk/srv1/repo/prefixes.json"; // URL to fetch the allowed prefixes from
        
        private static Dictionary<string, List<string>> playerPrefixData = new Dictionary<string, List<string>>();
        private static IEnumerator GetPlayerNamePrefixes(string[] steamIds, string logType)
        {
            steamIds = steamIds.Where(x => Regex.IsMatch(x, "^76[0-9]{15}$")).OrderBy(x => x).ToArray();
            string url = $"{playerPrefixUrl}";
            if (steamIds.Length > 0)
            {
                url += $"?{string.Join("&", steamIds.Select(id => $"id={id}"))}";
            }
            UnityWebRequest www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();
            bool includesLocalPlayer = steamIds.Contains(SteamClient.SteamId.ToString());
            AcceptableValueList<string> acceptableValueList = null;
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    Dictionary<string, List<string>> newPlayerPrefixData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(www.downloadHandler.text);
                    PluginLoader.StaticLogger.LogInfo($"[GetPlayerNamePrefixes | {logType}] Successfully found prefixes for {newPlayerPrefixData.Count} players");
                    if (steamIds.Length > 0)
                    {
                        foreach (KeyValuePair<string, List<string>> entry in newPlayerPrefixData)
                        {
                            playerPrefixData[entry.Key] = entry.Value;
                            // PluginLoader.StaticLogger.LogDebug($"[GetPlayerNamePrefixes | {logType}] {entry.Key} has {entry.Value.Count} prefixes: {string.Join(", ", entry.Value)}");
                        }
                    }
                    else
                    {
                        playerPrefixData = newPlayerPrefixData;
                    }

                    // Update the config with the latest prefixes
                    if (!includesLocalPlayer && newPlayerPrefixData.ContainsKey(SteamClient.SteamId.ToString()))
                        includesLocalPlayer = true;
                    
                    if (includesLocalPlayer)
                    {
                        List<string> prefixes = GetPrefixDataForSteamId(SteamClient.SteamId.ToString());
                        if (prefixes.Count > 0)
                        {
                            PluginLoader.StaticLogger.LogInfo($"[GetPlayerNamePrefixes | {logType}] {SteamClient.SteamId} has {prefixes.Count} prefixes: {string.Join(", ", prefixes)}");
                        }
                        acceptableValueList = new AcceptableValueList<string>(prefixes.Prepend("none").ToArray());
                    }
                }
                catch (JsonException e)
                {
                    PluginLoader.StaticLogger.LogError($"[GetPlayerNamePrefixes | {logType}] Failed to parse prefixes: " + e.Message);
                }
            }
            else
            {
                PluginLoader.StaticLogger.LogError($"[GetPlayerNamePrefixes | {logType}] Failed to fetch prefixes: " + www.error);
            }

            if (includesLocalPlayer)
            {
                PluginLoader.playerNamePrefixSelected = PluginLoader.StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?", acceptableValueList));
                PluginLoader.playerNamePrefixSelected.SettingChanged += (sender, args) =>
                {
                    WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar.instance);
                    if (GameManager.Multiplayer())
                    {
                        PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", PluginLoader.playerNamePrefixSelected?.Value);
                    }
                };
            }
        }

        public static List<string> GetPrefixDataForSteamId(string steamId)
        {
            if (playerPrefixData.TryGetValue(steamId, out List<string> prefixes))
            {
                return prefixes;
            }
            
            return [];
        }

        public static string GetPrefixStringForPlayer(PlayerAvatar playerAvatar)
        {
            // if (!PluginLoader.playerNamePrefixEnabled.Value) return null;
            if (!playerAvatar) return null;
            
            string prefix = "";
            string suffix = "";
            
            // if (SteamManager.instance.developerList.Any(x => x.steamID == playerAvatar.steamID))
            // {
            //     prefix = "<color=#ff0062>[DEV]</color> ";
            // }
            
            string selectedPrefix = null;
            if (playerAvatar.isLocal)
            {
                selectedPrefix = PluginLoader.playerNamePrefixSelected?.Value;
            }
            else if (playerAvatar.photonView.Owner.CustomProperties.ContainsKey("playerNamePrefix"))
            {
                selectedPrefix = (string)playerAvatar.photonView.Owner.CustomProperties["playerNamePrefix"];
            }
            
            List<string> prefixes = GetPrefixDataForSteamId(playerAvatar.steamID);
            if (prefixes.Contains("developer") && selectedPrefix == "developer")
            {
                prefix = "<color=#ff0062>[DEV]</color> ";
            }
            else if (prefixes.Contains("tester") && selectedPrefix == "tester")
            {
                prefix = "<color=#ff8b00>[TESTER]</color> ";
            }

            return string.IsNullOrWhiteSpace(prefix) ? null : $"{prefix}{Regex.Replace(playerAvatar.playerName ?? "", "<.*?>", string.Empty)}{suffix}";
        }

        public static void PhotonSetCustomProperty(Player photonPlayer, object key, object value)
        {
            var currentProps = PhotonNetwork.LocalPlayer.CustomProperties;
            var updatedProps = new ExitGames.Client.Photon.Hashtable();
            foreach (DictionaryEntry entry in currentProps)
            {
                updatedProps[entry.Key] = entry.Value;
            }
            updatedProps[key] = value;
            photonPlayer.SetCustomProperties(updatedProps);
        }
        
        public static void WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar _player)
        {
            if (!_player?.worldSpaceUIPlayerName) return;
            string prefix = GetPrefixStringForPlayer(_player);
            if (!string.IsNullOrWhiteSpace(prefix))
            // if (!string.IsNullOrWhiteSpace(prefix) && PluginLoader.playerNamePrefixEnabled.Value)
            {
                _player.worldSpaceUIPlayerName.text.richText = true;
                _player.worldSpaceUIPlayerName.text.text = prefix;
            }
            else
            {
                _player.worldSpaceUIPlayerName.text.richText = false;
                _player.worldSpaceUIPlayerName.text.text = _player.playerName;
            }
        }
        
        private static bool prefixRequestFailed;
        
        [HarmonyPatch(typeof(MainMenuOpen), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void MainMenuOpen_Start(MainMenuOpen __instance)
        {
            if (prefixSingleRequest)
            {
                if (SteamClient.IsValid)
                    __instance.StartCoroutine(GetPlayerNamePrefixes([], "MainMenuOpen_Start"));
                else
                    prefixRequestFailed = true;
                return;
            }
            if (!SteamClient.IsValid) return;
            string[] steamIds = [SteamClient.SteamId.ToString()];
            if (steamIds.Length > 0)
                __instance.StartCoroutine(GetPlayerNamePrefixes(steamIds, "MainMenuOpen_Start"));
        }
        
        [HarmonyPatch(typeof(SteamManager), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void SteamManager_Awake(SteamManager __instance)
        {
            if (prefixSingleRequest && SteamClient.IsValid && prefixRequestFailed)
            {
                prefixRequestFailed = false;
                __instance.StartCoroutine(GetPlayerNamePrefixes([], "SteamManager_Awake"));
            }
        }
        
        [HarmonyPatch(typeof(SteamManager), "OnLobbyEntered")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void SteamManager_OnLobbyEntered(SteamManager __instance, Lobby _lobby)
        {
            SteamFriends.SetRichPresence("steam_player_group", _lobby.Id.ToString());
            SteamFriends.SetRichPresence("steam_player_group_size", _lobby.MemberCount.ToString());
            
            if (prefixSingleRequest) return;
            string[] steamIds = _lobby.Members.Select(x => x.Id.ToString())
                .Where(x => x != SteamClient.SteamId.ToString()).ToArray();
            if (steamIds.Length > 0)
                __instance.StartCoroutine(GetPlayerNamePrefixes(steamIds, "SteamManager_OnLobbyEntered"));
        }
        
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberJoined")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void SteamManager_OnLobbyMemberJoined(SteamManager __instance, Lobby _lobby, Friend _friend)
        {
            SteamFriends.SetRichPresence("steam_player_group_size", _lobby.MemberCount.ToString());
            
            if (prefixSingleRequest) return;
            string[] steamIds = [_friend.Id.ToString()];
            if (steamIds.Length > 0)
                __instance.StartCoroutine(GetPlayerNamePrefixes(steamIds, "SteamManager_OnLobbyMemberJoined"));
        }
        
        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void PlayerAvatar_Awake(PlayerAvatar __instance)
        {
            if (SemiFunc.IsMultiplayer() && __instance.isLocal)
            {
                PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", PluginLoader.playerNamePrefixSelected?.Value);
            }
        }
        
        // Lobby Menu Player List
        [HarmonyPatch(typeof(MenuPageLobby), "Update")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageLobby_Update(MenuPageLobby __instance)
        {
            // if (!PluginLoader.playerNamePrefixEnabled.Value) return;
            
            foreach (GameObject listObject in __instance.listObjects)
            {
                MenuPlayerListed menuPlayerListed = listObject.GetComponent<MenuPlayerListed>();
                PlayerAvatar playerAvatar = menuPlayerListed.playerAvatar;
                if (!playerAvatar) continue;
                
                TextMeshProUGUI playerName = menuPlayerListed.playerName;
                string prefix = GetPrefixStringForPlayer(playerAvatar);
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    playerName.richText = true;
                    playerName.text = prefix;
                }
                else
                {
                    playerName.richText = false;
                    playerName.text = playerAvatar.playerName;
                }
            }
        }

        // Pause Menu Player List
        private static IEnumerator DelayedUpdatePauseMenuSliders(MenuPageEsc __instance)
        {
            yield return null;
            
            foreach (KeyValuePair<PlayerAvatar, MenuSliderPlayerMicGain> gameObject in __instance.playerMicGainSliders)
            {
                PlayerAvatar playerAvatar = gameObject.Key;
                TextMeshProUGUI playerName = gameObject.Value.menuSlider.elementNameText;
                string prefix = GetPrefixStringForPlayer(playerAvatar);
                if (!string.IsNullOrWhiteSpace(prefix))
                {
                    playerName.richText = true;
                    playerName.text = prefix;
                }
                else
                {
                    playerName.richText = false;
                    playerName.text = playerAvatar.playerName;
                }
            }
        }
        
        [HarmonyPatch(typeof(MenuPageEsc), "PlayerGainSlidersUpdate")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageEsc_PlayerGainSlidersUpdate(MenuPageEsc __instance)
        {
            __instance.StartCoroutine(DelayedUpdatePauseMenuSliders(__instance));
        }
        
        // In-Game Player Name
        [HarmonyPatch(typeof(WorldSpaceUIParent), "PlayerName")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void WorldSpaceUIParent_PlayerName(PlayerAvatar _player)
        {
            WorldSpaceUIParent_UpdatePlayerName(_player);
        }
        
        [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), "OnPlayerPropertiesUpdate")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
        {
            if (changedProps.ContainsKey("playerNamePrefix"))
            {
                foreach (PlayerAvatar playerAvatar in GameDirector.instance.PlayerList)
                {
                    if (playerAvatar.photonView.Owner != targetPlayer) continue;
                    WorldSpaceUIParent_UpdatePlayerName(playerAvatar);
                    break;
                }
            }
        }
        
        // Steam Rich Presence
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberLeft")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void SteamManager_OnLobbyMemberLeft(Lobby _lobby, Friend _friend)
        {
            SteamFriends.SetRichPresence("steam_player_group_size", _lobby.MemberCount.ToString());
        }
        
        [HarmonyPatch(typeof(SteamManager), "LeaveLobby")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void SteamManager_LeaveLobby()
        {
            SteamFriends.ClearRichPresence();
        }
    }
}
