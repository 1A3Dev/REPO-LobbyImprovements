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
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class PlayerNamePrefix
    {
        // Adds a prefix to the start of player names
        private static string playerPrefixUrl = "https://api.1a3.uk/srv1/repo/prefixes.json";
        private static Dictionary<string, List<string>> playerPrefixData;
        private static IEnumerator GetPlayerNamePrefixes()
        {
            UnityWebRequest www = UnityWebRequest.Get(playerPrefixUrl);
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    playerPrefixData = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(www.downloadHandler.text);
                    PluginLoader.StaticLogger.LogInfo($"Successfully parsed name prefixes: {playerPrefixData.Count} players");
                    
                    List<string> prefixes = GetPrefixDataForSteamId(SteamClient.SteamId.ToString());
                    if (prefixes.Count > 0)
                    {
                        PluginLoader.StaticLogger.LogInfo($"Found {prefixes.Count} prefixes for local player: {string.Join(", ", prefixes)}");
                    }
                    PluginLoader.playerNamePrefixSelected = PluginLoader.StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?", new AcceptableValueList<string>(prefixes.Prepend("none").ToArray())));
                }
                catch (JsonException e)
                {
                    PluginLoader.StaticLogger.LogError("Failed to parse name prefixes: " + e.Message);
                    PluginLoader.playerNamePrefixSelected = PluginLoader.StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?"));
                }
            }
            else
            {
                PluginLoader.StaticLogger.LogError("Failed to fetch name prefixes: " + www.error);
                PluginLoader.playerNamePrefixSelected = PluginLoader.StaticConfig.Bind("Name Prefix", "Selected", "none", new ConfigDescription("Which prefix would you like to use?"));
            }
            
            PluginLoader.playerNamePrefixSelected.SettingChanged += (sender, args) =>
            {
                WorldSpaceUIParent_UpdatePlayerName(PlayerAvatar.instance);
                if (GameManager.Multiplayer())
                {
                    PhotonSetCustomProperty(PhotonNetwork.LocalPlayer, "playerNamePrefix", PluginLoader.playerNamePrefixSelected?.Value);
                }
            };
        }

        public static List<string> GetPrefixDataForSteamId(string steamId)
        {
            if (playerPrefixData != null && playerPrefixData.TryGetValue(steamId, out List<string> prefixes))
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
        
        [HarmonyPatch(typeof(SteamManager), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        internal static void SteamManager_Awake(SteamManager __instance)
        {
            __instance.StartCoroutine(GetPlayerNamePrefixes());
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
    }
}
