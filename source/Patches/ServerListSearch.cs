using System.Text.RegularExpressions;
using HarmonyLib;
using Steamworks;
using Steamworks.Data;
using UnityEngine;

namespace LobbyImprovements.Patches
{
    public enum PopUpTextInputType
    {
        None,
        ServerListCreateNew,
        ServerListSearch
    }

    [HarmonyPatch]
    public class ServerListSearch
    {
        internal static PopUpTextInputType textInputType = PopUpTextInputType.None;

        [HarmonyPatch(typeof(MenuPageServerList), "ButtonCreateNew")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageServerList_ButtonCreateNew(MenuPageServerList __instance, bool __runOriginal)
        {
            if (__runOriginal && !__instance.searchInProgress)
                textInputType = PopUpTextInputType.ServerListCreateNew;
        }

        [HarmonyPatch(typeof(MenuPageServerList), "ButtonSearch")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageServerList_ButtonSearch(MenuPageServerList __instance, bool __runOriginal)
        {
            if (__runOriginal && !__instance.searchInProgress)
                textInputType = PopUpTextInputType.ServerListSearch;
        }

        // Awake runs inside MenuManager.PagePopUpTextInput, before the caller's postfix sets the type
        [HarmonyPatch(typeof(MenuPagePopUpTextInput), "Awake")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPagePopUpTextInput_Awake()
        {
            textInputType = PopUpTextInputType.None;
        }

        [HarmonyPatch(typeof(MenuPagePopUpTextInput), "OnDestroy")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPagePopUpTextInput_OnDestroy(MenuPagePopUpTextInput __instance)
        {
            // Don't clear the type of a newer popup that opened before this one was destroyed
            if (MenuPagePopUpTextInput.instance == __instance || !MenuPagePopUpTextInput.instance)
                textInputType = PopUpTextInputType.None;
        }

        // Allow pasting into the search input
        [HarmonyPatch(typeof(MenuPagePopUpTextInput), "Update")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void MenuPagePopUpTextInput_Update(MenuPagePopUpTextInput __instance)
        {
            if (textInputType != PopUpTextInputType.ServerListSearch) return;

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
            {
                string clipboard = GUIUtility.systemCopyBuffer;
                Match match = Regex.Match(clipboard, @"steam://joinlobby/\d+/(\d+)/\d+");
                if (match.Success)
                {
                    __instance.menuTextInput.textCurrent += match.Groups[1].Value;
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

                SteamLobbyManager.instance.OnGameLobbyJoinRequested(new Lobby(steamId), SteamClient.SteamId);
                return false;
            }

            return true;
        }
    }
}
