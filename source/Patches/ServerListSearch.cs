using System.Text.RegularExpressions;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class ServerListSearch
    {
        // Allow pasting into the search input
        [HarmonyPatch(typeof(MenuPageServerListSearch), "Update")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void MenuPageServerListSearch_Update(MenuPageServerListSearch __instance)
        {
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                Match match = Regex.Match(clipboard, @"steam://joinlobby/\d+/(\d+)/\d+");
                if (match.Success)
                {
                    string lobbyId = match.Groups[1].Value;
                    __instance.menuTextInput.textCurrent += lobbyId;
                }
                else
                {
                    __instance.menuTextInput.textCurrent += clipboard;
                }
            }
        }

        // Allow joining lobbies by putting steam lobby id in the search input
        [HarmonyPatch(typeof(MenuPageServerList), "SetSearch")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_SetSearch(string _searchString)
        {
            if (!string.IsNullOrEmpty(_searchString) && Regex.IsMatch(_searchString, "^[0-9]{17,19}$") && ulong.TryParse(_searchString, out ulong steamId))
            {
                if (SteamApps.BuildId <= 18995935)
                {
                    // Fix for infinite loading screen
                    MenuManager.instance?.PageCloseAll();
                    MenuManager.instance?.PageOpen(MenuPageIndex.Main);
                }

                SteamManager.instance.OnGameLobbyJoinRequested(new Lobby(steamId), SteamClient.SteamId);
                return false;
            }
            
            return true;
        }
    }
}
