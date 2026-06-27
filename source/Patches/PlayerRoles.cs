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
    public class RoleDisplay {
        public string prefix { get; set; } = "";
        public string suffix { get; set; } = "";
    }

    public class PlayerRolesResponse {
        public Dictionary<string, List<string>> userRoles { get; set; } = new();
        public Dictionary<string, Dictionary<string, RoleDisplay>> validRoles { get; set; } = new();
    }

    [HarmonyPatch]
    public class PlayerRoles_SteamManager {
        internal static string playerRolesProperty = "playerNamePrefix"; // Photon property name used for syncing the player's selected role
        #if DEBUG
        private static string playerRolesUrl = "http://1a3.localhost/api/games/repo/mods/lobbyimprovements/player-roles.json"; // API to fetch the player roles from
        #else
        private static string playerRolesUrl = "https://1a3.uk/api/games/repo/mods/lobbyimprovements/player-roles.json"; // API to fetch the player roles from
        #endif

        private static bool fetchedLocalPlayer;
        public static List<string> localRoles = new(); // Prefixes for Local Player
        public static Dictionary<string, List<string>> playerRoles = new(); // Prefixes for Other Players

        private static IEnumerator GetPlayerRoles(string[] steamIds, string logType){
            steamIds = steamIds.Where(x => Regex.IsMatch(x, "^76[0-9]{15}$")).OrderBy(x => x).ToArray();
            if(steamIds.Length == 0) yield break;

            string localSteamId = SteamClient.SteamId.ToString();
            bool includesLocalPlayer = steamIds.Contains(localSteamId);

            string url = $"{playerRolesUrl}?{string.Join("&", steamIds.Select(id => $"id={id}"))}";
            UnityWebRequest www = UnityWebRequest.Get(url);
            www.SetRequestHeader("Cache-Control", "no-cache");
            www.SetRequestHeader("x-steam-id", localSteamId);

            yield return www.SendWebRequest();

            AcceptableValueList<string> acceptableValueList = null;
            if(www.result == UnityWebRequest.Result.Success){
                try {
                    PlayerRolesResponse apiData = JsonConvert.DeserializeObject<PlayerRolesResponse>(www.downloadHandler.text);

                    bool rolesDisplayChanged = false;

                    foreach(var kvp in apiData.validRoles){
                        if(!rolesDisplayChanged){
                            if(!PluginLoader.validRoles.TryGetValue(kvp.Key, out var existing)){
                                rolesDisplayChanged = true;
                            }else{
                                foreach(var ctx in kvp.Value){
                                    if(existing.TryGetValue(ctx.Key, out var existingDisplay) && existingDisplay.prefix == ctx.Value.prefix && existingDisplay.suffix == ctx.Value.suffix) continue;
                                    rolesDisplayChanged = true;
                                    break;
                                }
                            }
                        }
                        PluginLoader.validRoles[kvp.Key] = kvp.Value;
                    }

                    if(rolesDisplayChanged && GameDirector.instance){
                        foreach(PlayerAvatar player in GameDirector.instance.PlayerList){
                            WorldSpaceUIParent_UpdatePlayerName(player);
                        }
                    }

                    foreach(string steamId in steamIds){
                        if(steamId == localSteamId){
                            if(apiData.userRoles.TryGetValue(steamId, out List<string> _roles)){
                                localRoles = _roles;
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerRoles | {logType}] {steamId} has {_roles.Count} roles: {string.Join(", ", _roles)}");
                            }else{
                                localRoles.Clear();
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerRoles | {logType}] {steamId} has 0 roles");
                            }
                        }else{
                            if(apiData.userRoles.TryGetValue(steamId, out List<string> _roles)){
                                playerRoles[steamId] = _roles;
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerRoles | {logType}] {steamId} has {_roles.Count} roles: {string.Join(", ", _roles)}");
                            }else{
                                playerRoles.Remove(steamId);
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerRoles | {logType}] {steamId} has 0 roles");
                            }
                        }
                    }

                    if(includesLocalPlayer){
                        acceptableValueList = new AcceptableValueList<string>(localRoles.Prepend("none").ToArray());
                    }
                }catch(JsonException e){
                    PluginLoader.StaticLogger.LogWarning($"[GetPlayerRoles | {logType}] Failed to parse roles: " + e.Message);
                }
            }else{
                PluginLoader.StaticLogger.LogWarning($"[GetPlayerRoles | {logType}] Failed to fetch roles: " + www.error);
            }

            www.Dispose();

            if(includesLocalPlayer){
                fetchedLocalPlayer = true;
                PluginLoader.playerRoleSelected = PluginLoader.StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which role would you like to use?", acceptableValueList));
                PluginLoader.playerRoleSelected.SettingChanged += (sender, args) => {
                    WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar.instance);
                    if(GameManager.Multiplayer()){
                        PlayerRoles_SemiFunc.PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, playerRolesProperty, PluginLoader.playerRoleSelected?.Value);
                    }
                };
            }
        }

        public static void WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar _player){
            if(_player?.worldSpaceUIPlayerName){
                string prefix = PlayerRoles_SemiFunc.GetPrefixStringForPlayer(_player, "avatar_nametag");
                if(!string.IsNullOrWhiteSpace(prefix)){
                    _player.worldSpaceUIPlayerName.text.richText = true;
                    _player.worldSpaceUIPlayerName.text.text = prefix;
                }else{
                    _player.worldSpaceUIPlayerName.text.richText = false;
                    _player.worldSpaceUIPlayerName.text.text = _player.playerName;
                }
            }
        }

        // Fetch local player's name prefixes
        [HarmonyPatch(typeof(SteamManager), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_Awake(SteamManager __instance) {
            if(SteamClient.IsValid && !fetchedLocalPlayer){
                __instance.StartCoroutine(GetPlayerRoles([SteamClient.SteamId.ToString()], "SteamManager_Awake"));
            }
        }

        // Fetch other players name prefixes
        [HarmonyPatch(typeof(SteamManager), "OnLobbyEntered")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_OnLobbyEntered(SteamManager __instance, Lobby _lobby){
            string[] steamIds = _lobby.Members.Select(x => x.Id.ToString()).Where(x => x != SteamClient.SteamId.ToString()).ToArray();
            if(steamIds.Length > 0) __instance.StartCoroutine(GetPlayerRoles(steamIds, "SteamManager_OnLobbyEntered"));
        }

        // Fetch joining player's name prefixes
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberJoined")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_OnLobbyMemberJoined(SteamManager __instance, Lobby _lobby, Friend _friend){
            __instance.StartCoroutine(GetPlayerRoles([_friend.Id.ToString()], "SteamManager_OnLobbyMemberJoined"));
        }

        // Remove leaving player's name prefixes
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberLeft")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_OnLobbyMemberLeft(Lobby _lobby, Friend _friend){
            if(playerRoles.ContainsKey(_friend.Id.ToString())){
                PluginLoader.StaticLogger.LogDebug($"[playerRoles | SteamManager_OnLobbyMemberLeft] Removing 1 player ({_friend.Id})");
                playerRoles.Remove(_friend.Id.ToString());
            }
        }

        // Remove all name prefixes
        [HarmonyPatch(typeof(SteamManager), "LeaveLobby")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_LeaveLobby(){
            if(playerRoles.Count > 0){
                PluginLoader.StaticLogger.LogDebug($"[playerRoles | SteamManager_LeaveLobby] Removing {playerRoles.Count} players");
                playerRoles.Clear();
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void PlayerAvatar_Awake(PlayerAvatar __instance){
            if(SemiFunc.IsMultiplayer() && __instance.isLocal){
                PlayerRoles_SemiFunc.PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, playerRolesProperty, PluginLoader.playerRoleSelected?.Value);
            }
        }

        // Lobby Menu Player List
        [HarmonyPatch(typeof(MenuPageLobby), "Update")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageLobby_Update(MenuPageLobby __instance){
            foreach(GameObject listObject in __instance.listObjects){
                MenuPlayerListed menuPlayerListed = listObject.GetComponent<MenuPlayerListed>();
                PlayerAvatar playerAvatar = menuPlayerListed.playerAvatar;
                if(!playerAvatar) continue;

                TextMeshProUGUI playerName = menuPlayerListed.playerName;
                string prefix = PlayerRoles_SemiFunc.GetPrefixStringForPlayer(playerAvatar, "menu_page_lobby");
                if(!string.IsNullOrWhiteSpace(prefix)){
                    playerName.richText = true;
                    playerName.text = prefix;
                }else{
                    playerName.richText = false;
                    playerName.text = playerAvatar.playerName;
                }
            }
        }

        // Pause Menu Player List
        private static IEnumerator DelayedUpdatePauseMenuSliders(MenuPageEsc __instance){
            yield return null;

            foreach(KeyValuePair<PlayerAvatar, MenuSliderPlayerMicGain> gameObject in __instance.playerMicGainSliders){
                PlayerAvatar playerAvatar = gameObject.Key;
                TextMeshProUGUI playerName = gameObject.Value.menuSlider.elementNameText;
                string prefix = PlayerRoles_SemiFunc.GetPrefixStringForPlayer(playerAvatar, "menu_page_esc");
                if(!string.IsNullOrWhiteSpace(prefix)){
                    playerName.richText = true;
                    playerName.text = prefix;
                }else{
                    playerName.richText = false;
                    playerName.text = playerAvatar.playerName;
                }
            }
        }

        [HarmonyPatch(typeof(MenuPageEsc), "PlayerGainSlidersUpdate")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageEsc_PlayerGainSlidersUpdate(MenuPageEsc __instance){
            __instance.StartCoroutine(DelayedUpdatePauseMenuSliders(__instance));
        }

        // In-Game Player Name
        [HarmonyPatch(typeof(WorldSpaceUIParent), "PlayerName")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void WorldSpaceUIParent_PlayerName(PlayerAvatar _player){
            WorldSpaceUIParent_UpdatePlayerName(_player);
        }

        [HarmonyPatch(typeof(MonoBehaviourPunCallbacks), "OnPlayerPropertiesUpdate")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps){
            if(changedProps.ContainsKey(playerRolesProperty)){
                foreach(PlayerAvatar playerAvatar in GameDirector.instance.PlayerList){
                    if(playerAvatar.photonView.Owner != targetPlayer) continue;
                    WorldSpaceUIParent_UpdatePlayerName(playerAvatar);
                    break;
                }
            }
        }
    }

    public class PlayerRoles_SemiFunc {
        public static List<string> GetPrefixDataForSteamId(string steamId){
            if(steamId == SteamClient.SteamId.ToString()){
                return PlayerRoles_SteamManager.localRoles;
            }

            if(PlayerRoles_SteamManager.playerRoles.TryGetValue(steamId, out List<string> prefixes)){
                return prefixes;
            }

            return [];
        }

        public static string GetPrefixStringForPlayer(PlayerAvatar playerAvatar, string contextKey){
            if(!playerAvatar) return null;

            string selectedPrefix = null;
            if(playerAvatar.isLocal){
                selectedPrefix = PluginLoader.playerRoleSelected?.Value;
            }else if (playerAvatar.photonView.Owner.CustomProperties.ContainsKey(PlayerRoles_SteamManager.playerRolesProperty)){
                selectedPrefix = (string)playerAvatar.photonView.Owner.CustomProperties[PlayerRoles_SteamManager.playerRolesProperty];
            }

            List<string> playerPrefixes = GetPrefixDataForSteamId(playerAvatar.steamID);
            bool isDevViewing = PluginLoader.modDevSteamIDs.Contains(SteamClient.SteamId.ToString());

            RoleDisplay entry = null;
            bool usingDefault = false;

            if(!string.IsNullOrWhiteSpace(selectedPrefix) && PluginLoader.validRoles.TryGetValue(selectedPrefix, out var selectedContextMap)){
                entry = GetContextEntry(selectedContextMap, contextKey);
            }

            // Dev fallback: if no valid selected prefix, use first allowed prefix with a dev marker
            if(entry == null && isDevViewing && playerPrefixes.Count > 0){
                string firstValid = playerPrefixes.FirstOrDefault(p => PluginLoader.validRoles.ContainsKey(p));
                if(firstValid != null && PluginLoader.validRoles.TryGetValue(firstValid, out var defaultContextMap)){
                    entry = GetContextEntry(defaultContextMap, contextKey);
                    usingDefault = true;
                }
            }

            if(entry == null) return null;

            string prefix = entry.prefix ?? "";
            string suffix = entry.suffix ?? "";

            if(usingDefault){
                string devMarker = "<color=#7289da>[!]</color>";
                if(!string.IsNullOrWhiteSpace(prefix)) prefix += devMarker;
                else suffix += devMarker;
            }

            return string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(suffix) ? null : $"{prefix}{Regex.Replace(playerAvatar.playerName ?? "", "<.*?>", string.Empty)}{suffix}";
        }

        private static RoleDisplay GetContextEntry(Dictionary<string, RoleDisplay> contextMap, string contextKey){
            if(contextMap.TryGetValue(contextKey, out var entry) && (!string.IsNullOrEmpty(entry.prefix) || !string.IsNullOrEmpty(entry.suffix))){
                return entry;
            }
            if(contextMap.TryGetValue("default", out var defaultEntry) && (!string.IsNullOrEmpty(defaultEntry.prefix) || !string.IsNullOrEmpty(defaultEntry.suffix))){
                return defaultEntry;
            }
            return null;
        }

        public static void PhotonSetCustomProperty(Player photonPlayer, object key, object value){
            var currentProps = PhotonNetwork.LocalPlayer.CustomProperties;
            var updatedProps = new ExitGames.Client.Photon.Hashtable();
            foreach(DictionaryEntry entry in currentProps){
                updatedProps[entry.Key] = entry.Value;
            }
            updatedProps[key] = value;
            photonPlayer.SetCustomProperties(updatedProps);
        }
    }
}