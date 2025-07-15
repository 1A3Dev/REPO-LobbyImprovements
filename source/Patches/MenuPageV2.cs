using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class MenuPageV2
    {
        internal static bool mainMenuOverhaul;
        
        [HarmonyPatch(typeof(MenuPageMain), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageMain_Start(MenuPageMain __instance)
        {
            mainMenuOverhaul = PluginLoader.mainMenuOverhaulEnabled.Value;
            
            if (!mainMenuOverhaul) return;
            
            TextMeshProUGUI privateGameText = __instance.rectTransform.transform.Find("Menu Button - Private Game/ButtonText")?.GetComponent<TextMeshProUGUI>();
            if (privateGameText) privateGameText.text = "Host Game";

            TextMeshProUGUI publicGameText = __instance.rectTransform.transform.Find("Menu Button - Public Game/ButtonText")?.GetComponent<TextMeshProUGUI>();
            if (publicGameText) publicGameText.text = "Join Game";
        }
        
        // Main Menu > Saves Menu (Skip region menu)
        [HarmonyPatch(typeof(MenuPageMain), "ButtonEventHostGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageMain_ButtonEventHostGame()
        {
            if (!mainMenuOverhaul) return true;
            
            SemiFunc.MainMenuSetMultiplayer();
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Saves);
            return false;
        }
        
        // Main Menu > Server List (Skip region and public game choice menus)
        [HarmonyPatch(typeof(MenuPageMain), "ButtonEventPlayRandom")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageMain_ButtonEventPlayRandom()
        {
            if (!mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.ServerList);
            return false;
        }
        
        private static REPOSlider regionSlider;
        private static Dictionary<string, Region> regionMap = new Dictionary<string, Region>();
        private static void OnRegionSelected(string regionIndex)
        {
            string regionName = regionIndex.Contains(" [") ? regionIndex.Split(" [")[0] : regionIndex;
            regionMap.TryGetValue(regionName, out Region value);
            PluginLoader.StaticLogger.LogInfo($"[Public Lobby] Selected Region: {regionName} ({value?.Code})");
            DataDirector.instance.networkRegion = value?.Code ?? "";
            DataDirector.instance.PhotonSetRegion();
        }
        private static string GetRegionDisplayName(string regionCode)
        {
            return (regionCode?.ToUpper() ?? "") switch
            {
                "" => "Pick Best Region",
                "ASIA" => "Asia",
                "AU" => "Australia",
                "CAE" => "Canada East",
                "CN" => "Chinese Mainland",
                "EU" => "Europe",
                "HK" => "Hong Kong",
                "IN" => "India",
                "JP" => "Japan",
                "ZA" => "South Africa",
                "SA" => "South America",
                "KR" => "South Korea",
                "TR" => "Turkey",
                "UAE" => "United Arab Emirates",
                "US" => "USA East",
                "USW" => "USA West",
                "USSC" => "USA South Central",
                _ => regionCode?.ToUpper()
            };
        }
        private static void LoadRegions(Transform parent, Vector2 localPosition, bool showPing)
        {
            regionMap.Clear();
        
            List<string> options = new List<string>();
            if (PhotonNetwork.NetworkingClient?.RegionHandler?.EnabledRegions != null)
            {
                foreach (Region region in PhotonNetwork.NetworkingClient.RegionHandler.EnabledRegions)
                {
                    string displayName = GetRegionDisplayName(region.Code);
                    regionMap.Add(displayName, region);
                    string ping = region.Ping > 200 || region.Ping == RegionPinger.PingWhenFailed ? ">200" : region.Ping.ToString();
                    options.Add(showPing ? $"{displayName} [{ping}ms]" : displayName);
                }
            }

            if (regionSlider)
                Object.Destroy(regionSlider.gameObject);
            
            string defaultOption = options.FirstOrDefault(x => x.StartsWith(GetRegionDisplayName(PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion)));
            regionSlider = MenuAPI.CreateREPOSlider("Region", null, OnRegionSelected, parent, stringOptions: options.OrderBy(x => x).Prepend("Pick Best Region").ToArray(), defaultOption: defaultOption, localPosition: localPosition);
            regionSlider.labelTMP.text = "";
            regionSlider.transform.Find("SliderBG").gameObject.SetActive(false);
        }

        private static void RefreshLobbies()
        {
            MenuPageServerList menuPageServerList = MenuManager.instance.currentMenuPage.GetComponent<MenuPageServerList>();
            if (!menuPageServerList || !menuPageServerList.receivedList) return;
            
            foreach (Transform item2 in menuPageServerList.serverElementParent)
            {
                Object.Destroy(item2.gameObject);
            }
            menuPageServerList.loadingGraphics.Reset();
            menuPageServerList.roomListSearched.Clear();
            
            menuPageServerList.receivedList = false;
            menuPageServerList.buttonNext.HideSetInstant();
            menuPageServerList.buttonPrevious.HideSetInstant();
            menuPageServerList.StartCoroutine(menuPageServerList.GetServerList());
        }

        // [Server List] Random Matchmaking
        [HarmonyPatch(typeof(MenuPageServerList), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageServerList_Start(MenuPageServerList __instance)
        {
            if (!mainMenuOverhaul) return;
            
            Transform createNewObj = __instance.transform.Find("Panel/Create New");
            MenuButton createNewBtn = createNewObj?.Find("Menu Button - CREATE NEW")?.GetComponent<MenuButton>();
            if (createNewBtn)
            {
                createNewBtn.buttonText.text = "Refresh";
                createNewBtn.button.onClick = new Button.ButtonClickedEvent();
                createNewBtn.button.onClick.AddListener(RefreshLobbies);
                
                GameObject randomLobbyObj = Object.Instantiate(createNewObj.gameObject, createNewObj.parent);
                randomLobbyObj.name = "Random Server";
                randomLobbyObj.transform.localPosition = new Vector2(0, createNewObj.localPosition.y);
                MenuButton randomLobbyBtn = randomLobbyObj.GetComponentInChildren<MenuButton>();
                randomLobbyBtn.buttonText.text = "Random";
                randomLobbyBtn.button.onClick = new Button.ButtonClickedEvent();
                randomLobbyBtn.menuButtonPopUp = randomLobbyBtn.gameObject.AddComponent<MenuButtonPopUp>();
                randomLobbyBtn.menuButtonPopUp.headerText = "Join Server";
                randomLobbyBtn.menuButtonPopUp.bodyText = "Are you sure you want to join a random server?";
                randomLobbyBtn.menuButtonPopUp.option1Event = new UnityEvent();
                randomLobbyBtn.menuButtonPopUp.option1Event.AddListener(__instance.ButtonCreateNew);
            }

            ServerSettings.ResetBestRegionCodeInPreferences();
        }
        
        [HarmonyPatch(typeof(ConnectionCallbacksContainer), "OnRegionListReceived")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void OnRegionListReceived()
        {
            if (!mainMenuOverhaul) return;
            
            MenuPageServerList menuPageServerList = MenuManager.instance.currentMenuPage.GetComponent<MenuPageServerList>();
            if (menuPageServerList)
            {
                LoadRegions(menuPageServerList.transform.Find("Panel"), new Vector2(-170, -195), false);
            }
            
            MenuPageSaves menuPageSaves = MenuManager.instance.currentMenuPage.GetComponent<MenuPageSaves>();
            if (menuPageSaves && MainMenuOpen.instance?.mainMenuGameModeState == MainMenuOpen.MainMenuGameModeState.MultiPlayer)
            {
                LoadRegions(menuPageSaves.transform, new Vector2(400, 328), false);
            }
        }
        
        [HarmonyPatch(typeof(ConnectionCallbacksContainer), "OnConnectedToMaster")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void OnConnectedToMaster()
        {
            if (!mainMenuOverhaul) return;
            
            MenuPageServerList menuPageServerList = MenuManager.instance.currentMenuPage.GetComponent<MenuPageServerList>();
            if (menuPageServerList)
            {
                LoadRegions(menuPageServerList.transform.Find("Panel"), new Vector2(-170, -195), true);
            }
            
            MenuPageSaves menuPageSaves = MenuManager.instance.currentMenuPage.GetComponent<MenuPageSaves>();
            if (menuPageSaves && MainMenuOpen.instance?.mainMenuGameModeState == MainMenuOpen.MainMenuGameModeState.MultiPlayer)
            {
                Transform backButtonObj = menuPageSaves.gameObject.transform.Find("Menu Button - < GO BACK");
                LoadRegions(backButtonObj.transform.parent, new Vector2(400, 328), true);
            }
        }
        
        [HarmonyPatch(typeof(MenuPageServerList), "ButtonCreateNew")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_ButtonCreateNew()
        {
            if (!mainMenuOverhaul) return true;
            
            RunManager.instance.ResetProgress();
            StatsManager.instance.saveFileCurrent = "";
            GameManager.instance.SetConnectRandom(true);
            GameManager.instance.localTest = false;
            RunManager.instance.ChangeLevel(true, false, RunManager.ChangeLevelType.LobbyMenu);
            RunManager.instance.lobbyJoin = true;
            return false;
        }
        
        // Server List > Main Menu
        [HarmonyPatch(typeof(MenuPageServerList), "ExitPage")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageSaves_OnGoBack(MenuPageSaves __instance)
        {
            if (!mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Main);
            return false;
        }

        [HarmonyPatch(typeof(MenuPageTwoOptions), "Update")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageTwoOptions_Update(MenuPageTwoOptions __instance)
        {
            if (!mainMenuOverhaul || __instance.option2Button?.buttonTextString != "Public") return true;
            
            if (SemiFunc.InputDown(InputKey.Back) && MenuManager.instance.currentMenuPageIndex == MenuPageIndex.PopUpTwoOptions)
            {
                MenuManager.instance.PageReactivatePageUnderThisPage(__instance.menuPage);
                __instance.menuPage.PageStateSet(MenuPage.PageState.Closing);
            }
            if (__instance.option1Button.buttonText.text != __instance.option1Button.buttonTextString)
            {
                __instance.option1Button.buttonText.text = __instance.option1Button.buttonTextString;
            }
            if (__instance.option2Button.buttonText.text != __instance.option2Button.buttonTextString)
            {
                __instance.option2Button.buttonText.text = __instance.option2Button.buttonTextString;
            }
            return false;
        }
        
        [HarmonyPatch(typeof(MenuPageSaves), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageSaves_Start(MenuPageSaves __instance)
        {
            if (!mainMenuOverhaul || !SemiFunc.MainMenuIsMultiplayer()) return;
            __instance.StartCoroutine(GetRegionsForSavesMenu());
        }

        private static IEnumerator GetRegionsForSavesMenu()
        {
            PhotonNetwork.Disconnect();
            while (PhotonNetwork.NetworkingClient.State != ClientState.Disconnected && PhotonNetwork.NetworkingClient.State != 0)
            {
                yield return null;
            }
            SteamManager.instance.SendSteamAuthTicket();
            DataDirector.instance.PhotonSetRegion();
            DataDirector.instance.PhotonSetVersion();
            DataDirector.instance.PhotonSetAppId();
            ServerSettings.ResetBestRegionCodeInPreferences();
            PhotonNetwork.ConnectUsingSettings();
            while (PhotonNetwork.NetworkingClient.State != ClientState.ConnectedToMasterServer)
            {
                yield return null;
            }
            PhotonNetwork.Disconnect();
        }
    }
}