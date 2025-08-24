using System;
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
using Object = UnityEngine.Object;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class MenuPageV2
    {
        internal static void NewGame_Internal(MenuButton __instance)
        {
            var repoPage = MenuAPI.CreateREPOPopupPage(__instance.menuButtonPopUp?.headerText, shouldCachePage: false, pageDimmerVisibility: true, spacing: 1.5f, localPosition: Vector2.zero);
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Private", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = false;
                    MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>() ?? __instance.parentPage?.pageUnderThisPage?.GetComponent<MenuPageSaves>();
                    menuPageSaves?.OnNewGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Public", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = true;
                    MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>() ?? __instance.parentPage?.pageUnderThisPage?.GetComponent<MenuPageSaves>();
                    menuPageSaves?.OnNewGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Back", () =>
                {
                    repoPage.ClosePage(false);
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.OpenPage(openOnTop: false);
        }
        
        internal static void LoadGame_Internal(MenuButton __instance)
        {
            var repoPage = MenuAPI.CreateREPOPopupPage(__instance.menuButtonPopUp?.headerText, shouldCachePage: false, pageDimmerVisibility: true, spacing: 1.5f, localPosition: Vector2.zero);
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Private", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = false;
                    MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>() ?? __instance.parentPage?.pageUnderThisPage?.GetComponent<MenuPageSaves>();
                    menuPageSaves?.OnLoadGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Public", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = true;
                    MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>() ?? __instance.parentPage?.pageUnderThisPage?.GetComponent<MenuPageSaves>();
                    menuPageSaves?.OnLoadGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.OpenPage(openOnTop: false);
        }

        [HarmonyPatch(typeof(MenuPageMain), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageMain_Start(MenuPageMain __instance)
        {
            PluginLoader.mainMenuOverhaul = PluginLoader.mainMenuOverhaulEnabled.Value;
            
            if (!PluginLoader.mainMenuOverhaul) return;
            
            TextMeshProUGUI privateGameText = __instance.rectTransform.transform.Find("Menu Button - Private Game/ButtonText")?.GetComponent<TextMeshProUGUI>();
            if (privateGameText) privateGameText.text = "Host Game";

            TextMeshProUGUI publicGameText = __instance.rectTransform.transform.Find("Menu Button - Public Game/ButtonText")?.GetComponent<TextMeshProUGUI>();
            if (publicGameText) publicGameText.text = "Join Game";
            
            regionMap.Clear();
        }
        
        // Main Menu > Saves Menu (Skip region menu)
        [HarmonyPatch(typeof(MenuPageMain), "ButtonEventHostGame")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageMain_ButtonEventHostGame()
        {
            if (!PluginLoader.mainMenuOverhaul) return true;
            
            SemiFunc.MainMenuSetMultiplayer();
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Saves);
            return false;
        }
        
        // Main Menu > Server List (Skip region menu)
        [HarmonyPatch(typeof(MenuPageMain), "ButtonEventPlayRandom")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageMain_ButtonEventPlayRandom()
        {
            if (!PluginLoader.mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.PublicGameChoice);
            return false;
        }
        
        [HarmonyPatch(typeof(MenuManager), "PageOpen")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuManager_PageOpen(MenuManager __instance, MenuPage __result, MenuPageIndex menuPageIndex, bool addedPageOnTop = false)
        {
            if (!PluginLoader.mainMenuOverhaul) return;

            if (menuPageIndex == MenuPageIndex.PublicGameChoice)
            {
                TextMeshProUGUI menuPageHeader = __result.transform.Find("Header/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                if (menuPageHeader)
                {
                    menuPageHeader.text = "Join Game";
                    __instance.StartCoroutine(GetRegionsForOtherMenus());
                }
            }
            AddRegionSliders(false);
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
                "" => "Best Region",
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
            List<Region> enabledRegions = PhotonNetwork.NetworkingClient?.RegionHandler?.EnabledRegions;
            if (enabledRegions != null)
            {
                regionMap.Clear();
                foreach (Region region in enabledRegions)
                {
                    regionMap.Add(GetRegionDisplayName(region.Code), region);
                }
            }
            
            List<string> options = new List<string>();
            foreach (Region region in regionMap.Values)
            {
                string displayName = GetRegionDisplayName(region.Code);
                string ping = region.Ping > 200 || region.Ping == RegionPinger.PingWhenFailed ? ">200" : region.Ping.ToString();
                options.Add(showPing ? $"{displayName} [{ping}ms]" : displayName);
            }
            options = options.OrderBy(x => x).ToList();
            
            string[] array = PhotonNetwork.BestRegionSummaryInPreferences.Split(';', StringSplitOptions.None);
            if (array.Length >= 3 && int.TryParse(array[1], out int num) && !string.IsNullOrEmpty(array[0]) && !string.IsNullOrEmpty(array[2]) && regionMap.Values.Any(x => x.Code == array[0]))
            {
                string displayName = GetRegionDisplayName(array[0]);
                options = options.Prepend($"Best Region [{displayName}]").ToList();
            }
            else
            {
                options = options.Prepend("Best Region").ToList();
            }
            
            if (regionSlider)
                Object.Destroy(regionSlider.gameObject);
            
            string defaultOption = options.FirstOrDefault(x => x.StartsWith(GetRegionDisplayName(PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion)));
            regionSlider = MenuAPI.CreateREPOSlider("Region", null, OnRegionSelected, parent, stringOptions: options.ToArray(), defaultOption: defaultOption, localPosition: localPosition);
            regionSlider.labelTMP.text = "";
            regionSlider.transform.Find("SliderBG").gameObject.SetActive(false);
        }

        // [Server List] Random Matchmaking & Refresh Buttons
        private static void RefreshLobbies()
        {
            MenuPageServerList menuPageServerList = MenuManager.instance.currentMenuPage.GetComponent<MenuPageServerList>();
            if (!menuPageServerList || !menuPageServerList.receivedList || menuPageServerList.searchInProgress) return;
            
            foreach (Transform item2 in menuPageServerList.serverElementParent)
            {
                Object.Destroy(item2.gameObject);
            }
            menuPageServerList.loadingGraphics.Reset();
            menuPageServerList.roomListSearched.Clear();
            
            menuPageServerList.receivedList = false;
            menuPageServerList.pageMax = 0;
            menuPageServerList.buttonNext.HideSetInstant();
            menuPageServerList.buttonPrevious.HideSetInstant();
            menuPageServerList.StartCoroutine(menuPageServerList.GetServerList());
        }
        
        [HarmonyPatch(typeof(MenuPageServerList), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageServerList_Start(MenuPageServerList __instance)
        {
            if (!PluginLoader.mainMenuOverhaul) return;
            
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
        }
        
        [HarmonyPatch(typeof(ConnectionCallbacksContainer), "OnRegionListReceived")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void OnRegionListReceived()
        {
            if (!PluginLoader.mainMenuOverhaul) return;
            AddRegionSliders(false);
        }
        
        [HarmonyPatch(typeof(ConnectionCallbacksContainer), "OnConnectedToMaster")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void OnConnectedToMaster()
        {
            if (fetchingRegions && MenuManager.instance.currentMenuPageIndex != MenuPageIndex.ServerList)
                PhotonNetwork.Disconnect();
            
            fetchingRegions = false;
            
            if (!PluginLoader.mainMenuOverhaul) return;
            AddRegionSliders(true);
        }

        private static void AddRegionSliders(bool showPing)
        {
            if (regionMap.Count > 0)
                showPing = true;
            
            MenuPagePublicGameChoice menuPagePublicChoice = MenuManager.instance?.currentMenuPage?.GetComponent<MenuPagePublicGameChoice>();
            if (menuPagePublicChoice)
            {
                LoadRegions(menuPagePublicChoice.transform, new Vector2(190, 10), showPing);
            }
            
            MenuPageServerList menuPageServerList = MenuManager.instance?.currentMenuPage?.GetComponent<MenuPageServerList>();
            if (menuPageServerList)
            {
                LoadRegions(menuPageServerList.transform.Find("Panel"), new Vector2(-170, -195), showPing);
            }
            
            MenuPageSaves menuPageSaves = MenuManager.instance?.currentMenuPage?.GetComponent<MenuPageSaves>();
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
            if (!PluginLoader.mainMenuOverhaul) return true;
            
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
        private static bool MenuPageServerList_ExitPage()
        {
            if (!PluginLoader.mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Main);
            return false;
        }
        
        // Public Game Choice > Main Menu
        [HarmonyPatch(typeof(MenuPagePublicGameChoice), "ExitPage")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPagePublicGameChoice_ExitPage()
        {
            if (!PluginLoader.mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(MenuPageIndex.Main);
            return false;
        }
        
        private static bool fetchingRegions;
        [HarmonyPatch(typeof(MenuPageSaves), "Start")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuPageSaves_Start(MenuPageSaves __instance)
        {
            if (!PluginLoader.mainMenuOverhaul || !SemiFunc.MainMenuIsMultiplayer()) return;
            MenuManager.instance.StartCoroutine(GetRegionsForOtherMenus());
        }

        private static IEnumerator GetRegionsForOtherMenus()
        {
            if (fetchingRegions)
            {
                PluginLoader.StaticLogger.LogInfo("[Public Lobby] Already fetching regions");
                yield break;
            }
            if (regionMap.Count > 0)
            {
                yield break;
            }
            
            PluginLoader.StaticLogger.LogInfo("[Public Lobby] Started fetching regions");
            fetchingRegions = true;
            PhotonNetwork.Disconnect();
            while (MenuManager.instance.currentMenuPageIndex != MenuPageIndex.ServerList && PhotonNetwork.NetworkingClient.State != ClientState.Disconnected && PhotonNetwork.NetworkingClient.State != 0)
            {
                yield return null;
            }
            if (MenuManager.instance.currentMenuPageIndex != MenuPageIndex.ServerList)
            {
                SteamManager.instance.SendSteamAuthTicket();
                DataDirector.instance.PhotonSetRegion();
                DataDirector.instance.PhotonSetVersion();
                DataDirector.instance.PhotonSetAppId();
                ServerSettings.ResetBestRegionCodeInPreferences();
                PhotonNetwork.ConnectUsingSettings();
            }
        }
    }
}