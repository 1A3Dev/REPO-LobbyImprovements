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
    public class PlayerNamePrefix_SteamManager {
        private static string playerPrefixUrl = "https://api.1a3.uk/srv1/repo/prefixes.json"; // URL to fetch the allowed prefixes from

        private static bool fetchedLocalPlayer;
        public static List<string> localPrefixData = new(); // Prefixes for Local Player
        public static Dictionary<string, List<string>> playerPrefixData = new(); // Prefixes for Other Players
        
        private static IEnumerator GetPlayerNamePrefixes(string[] steamIds, string logType){
            steamIds = steamIds.Where(x => Regex.IsMatch(x, "^76[0-9]{15}$")).OrderBy(x => x).ToArray();
            if(steamIds.Length == 0) yield break;
            
            string localSteamId = SteamClient.SteamId.ToString();
            bool includesLocalPlayer = steamIds.Contains(localSteamId);
            
            string url = $"{playerPrefixUrl}?{string.Join("&", steamIds.Select(id => $"id={id}"))}";
            UnityWebRequest www = UnityWebRequest.Get(url);
            yield return www.SendWebRequest();
            
            AcceptableValueList<string> acceptableValueList = null;
            if(www.result == UnityWebRequest.Result.Success){
                try {
                    Dictionary<string, List<string>> newPlayerPrefixData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(www.downloadHandler.text);
                    foreach(string steamId in steamIds){
                        if(steamId == localSteamId){
                            if(newPlayerPrefixData.TryGetValue(steamId, out List<string> prefixes)){
                                localPrefixData = prefixes;
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerNamePrefixes | {logType}] {steamId} has {prefixes.Count} prefixes: {string.Join(", ", prefixes)}");
                            }else{
                                localPrefixData.Clear();
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerNamePrefixes | {logType}] {steamId} has 0 prefixes");
                            }
                        }else{
                            if(newPlayerPrefixData.TryGetValue(steamId, out List<string> prefixes)){
                                playerPrefixData[steamId] = prefixes;
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerNamePrefixes | {logType}] {steamId} has {prefixes.Count} prefixes: {string.Join(", ", prefixes)}");
                            }else{
                                playerPrefixData.Remove(steamId);
                                PluginLoader.StaticLogger.LogDebug($"[GetPlayerNamePrefixes | {logType}] {steamId} has 0 prefixes");
                            }
                        }
                    }
                    
                    if(includesLocalPlayer){
                        acceptableValueList = new AcceptableValueList<string>(localPrefixData.Prepend("none").ToArray());
                    }
                }catch(JsonException e){
                    PluginLoader.StaticLogger.LogWarning($"[GetPlayerNamePrefixes | {logType}] Failed to parse prefixes: " + e.Message);
                }
            }else{
                PluginLoader.StaticLogger.LogWarning($"[GetPlayerNamePrefixes | {logType}] Failed to fetch prefixes: " + www.error);
            }

            if(includesLocalPlayer){
                fetchedLocalPlayer = true;
                PluginLoader.playerNamePrefixSelected = PluginLoader.StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?", acceptableValueList));
                PluginLoader.playerNamePrefixSelected.SettingChanged += (sender, args) => {
                    WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar.instance);
                    if(GameManager.Multiplayer()){
                        PlayerNamePrefix_SemiFunc.PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", PluginLoader.playerNamePrefixSelected?.Value);
                    }
                };
            }
        }
        
        public static void WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar _player){
            if(_player?.worldSpaceUIPlayerName){
                string prefix = PlayerNamePrefix_SemiFunc.GetPrefixStringForPlayer(_player);
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
                __instance.StartCoroutine(GetPlayerNamePrefixes([SteamClient.SteamId.ToString()], "SteamManager_Awake"));
            }
        }
        
        // Fetch other players name prefixes
        [HarmonyPatch(typeof(SteamManager), "OnLobbyEntered")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_OnLobbyEntered(SteamManager __instance, Lobby _lobby){
            string[] steamIds = _lobby.Members.Select(x => x.Id.ToString()).Where(x => x != SteamClient.SteamId.ToString()).ToArray();
            if(steamIds.Length > 0) __instance.StartCoroutine(GetPlayerNamePrefixes(steamIds, "SteamManager_OnLobbyEntered"));
        }
        
        // Fetch joining player's name prefixes
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberJoined")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_OnLobbyMemberJoined(SteamManager __instance, Lobby _lobby, Friend _friend){
            __instance.StartCoroutine(GetPlayerNamePrefixes([_friend.Id.ToString()], "SteamManager_OnLobbyMemberJoined"));
        }
        
        // Remove leaving player's name prefixes
        [HarmonyPatch(typeof(SteamManager), "OnLobbyMemberLeft")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_OnLobbyMemberLeft(Lobby _lobby, Friend _friend){
            if(playerPrefixData.ContainsKey(_friend.Id.ToString())){
                PluginLoader.StaticLogger.LogDebug($"[playerPrefixData | SteamManager_OnLobbyMemberLeft] Removing 1 player ({_friend.Id})");
                playerPrefixData.Remove(_friend.Id.ToString());
            }
        }
        
        // Remove all name prefixes
        [HarmonyPatch(typeof(SteamManager), "LeaveLobby")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void SteamManager_LeaveLobby(){
            if(playerPrefixData.Count > 0){
                PluginLoader.StaticLogger.LogDebug($"[playerPrefixData | SteamManager_LeaveLobby] Removing {playerPrefixData.Count} players");
                playerPrefixData.Clear();
            }
        }

        [HarmonyPatch(typeof(PlayerAvatar), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void PlayerAvatar_Awake(PlayerAvatar __instance){
            if(SemiFunc.IsMultiplayer() && __instance.isLocal){
                PlayerNamePrefix_SemiFunc.PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", PluginLoader.playerNamePrefixSelected?.Value);
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
                string prefix = PlayerNamePrefix_SemiFunc.GetPrefixStringForPlayer(playerAvatar);
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
                string prefix = PlayerNamePrefix_SemiFunc.GetPrefixStringForPlayer(playerAvatar);
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
            if(changedProps.ContainsKey("playerNamePrefix")){
                foreach(PlayerAvatar playerAvatar in GameDirector.instance.PlayerList){
                    if(playerAvatar.photonView.Owner != targetPlayer) continue;
                    WorldSpaceUIParent_UpdatePlayerName(playerAvatar);
                    break;
                }
            }
        }
    }

    public class PlayerNamePrefix_SemiFunc {
        public static List<string> GetPrefixDataForSteamId(string steamId){
            if(steamId == SteamClient.SteamId.ToString()){
                return PlayerNamePrefix_SteamManager.localPrefixData;
            }
            
            if(PlayerNamePrefix_SteamManager.playerPrefixData.TryGetValue(steamId, out List<string> prefixes)){
                return prefixes;
            }
            
            return [];
        }

        public static string GetPrefixStringForPlayer(PlayerAvatar playerAvatar){
            if(!playerAvatar) return null;
            
            string prefix = "";
            string suffix = "";
            
            string selectedPrefix = null;
            if(playerAvatar.isLocal){
                selectedPrefix = PluginLoader.playerNamePrefixSelected?.Value;
            }else if (playerAvatar.photonView.Owner.CustomProperties.ContainsKey("playerNamePrefix")){
                selectedPrefix = (string)playerAvatar.photonView.Owner.CustomProperties["playerNamePrefix"];
            }
            
            List<string> prefixes = GetPrefixDataForSteamId(playerAvatar.steamID);
            
            // Check if the selected prefix has a prefix string
            if(string.IsNullOrWhiteSpace(selectedPrefix) || !PluginLoader.namePrefixMap.TryGetValue(selectedPrefix, out prefix)){
                // If mod dev and no prefix is set, then default to the first allowed
                if(PluginLoader.modDevSteamIDs.Contains(SteamClient.SteamId.ToString()) && prefixes.Count > 0 && PluginLoader.namePrefixMap.TryGetValue(prefixes.First(), out prefix)){
                    prefix += "<color=#7289da>[!]</color> "; // Indicate that a default prefix is being used
                }
            }
            // Check if the selected prefix has a suffix string
            if(string.IsNullOrWhiteSpace(selectedPrefix) || !PluginLoader.nameSuffixMap.TryGetValue(selectedPrefix, out suffix)){
                // If mod dev and no suffix is set, then default to the first allowed
                if(PluginLoader.modDevSteamIDs.Contains(SteamClient.SteamId.ToString()) && prefixes.Count > 0 && PluginLoader.nameSuffixMap.TryGetValue(prefixes.First(), out suffix)){
                    suffix += " <color=#7289da>[!]</color>"; // Indicate that a default prefix is being used
                }
            }

            return string.IsNullOrWhiteSpace(prefix) && string.IsNullOrWhiteSpace(suffix) ? null : $"{prefix}{Regex.Replace(playerAvatar.playerName ?? "", "<.*?>", string.Empty)}{suffix}";
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
