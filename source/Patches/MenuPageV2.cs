using HarmonyLib;
using MenuLib;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyImprovements.Patches
{
    [HarmonyPatch]
    public class MenuPageV2
    {
        private static MenuPageIndex regionsPreviousPage = MenuPageIndex.Saves;
        
        internal static void NewGame_Internal(MenuButton __instance)
        {
            MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>() ?? __instance.parentPage?.pageUnderThisPage?.GetComponent<MenuPageSaves>();
            if(menuPageSaves && menuPageSaves.maxSaveFiles > 0 && menuPageSaves.saveFiles.Count >= menuPageSaves.maxSaveFiles){
                MenuManager.instance.PageCloseAllAddedOnTop();
                MenuManager.instance.PagePopUp("Save file limit reached", Color.red, $"You can only have {menuPageSaves.maxSaveFiles} save files at a time. Please delete some save files to make room for new ones.", "OK", true);
                return;
            }
            var repoPage = MenuAPI.CreateREPOPopupPage(__instance.menuButtonPopUp?.headerText, shouldCachePage: false, pageDimmerVisibility: true, spacing: 1.5f, localPosition: Vector2.zero);
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Private", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = false;
                    menuPageSaves?.OnNewGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Public (Server List)", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = true;
                    menuPageSaves?.OnNewGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Public (Random)", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = false;
                    GameManager.instance.matchmakingMode = GameManager.RandomMatchmakingModes.Create;
                    SemiFunc.MenuActionRandomMatchmaking();
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
            MenuPageSaves menuPageSaves = __instance.parentPage?.GetComponent<MenuPageSaves>() ?? __instance.parentPage?.pageUnderThisPage?.GetComponent<MenuPageSaves>();
            
            var repoPage = MenuAPI.CreateREPOPopupPage(__instance.menuButtonPopUp?.headerText, shouldCachePage: false, pageDimmerVisibility: true, spacing: 1.5f, localPosition: Vector2.zero);
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Private", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = false;
                    menuPageSaves?.OnLoadGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Public (Server List)", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = true;
                    menuPageSaves?.OnLoadGame();
                }, parent: scrollView, localPosition: Vector2.zero);
                return repoButton.rectTransform;
            });
            repoPage.AddElementToScrollView(scrollView =>
            {
                var repoButton = MenuAPI.CreateREPOButton("Public (Random)", () =>
                {
                    PublicLobbySaves.publicSavesMenuOpen = false;
                    GameManager.instance.matchmakingMode = GameManager.RandomMatchmakingModes.Create;
                    SemiFunc.MenuActionRandomMatchmaking(StatsManager.instance.saveFileCurrent);
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
        
        // Main Menu > Public Game Choice (Skip region menu)
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
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void MenuManager_PageOpen_Prefix(MenuManager __instance, MenuPage __result, MenuPageIndex menuPageIndex, bool addedPageOnTop = false)
        {
            if (!PluginLoader.mainMenuOverhaul) return;

            if (menuPageIndex == MenuPageIndex.Saves || menuPageIndex == MenuPageIndex.PublicGameChoice || menuPageIndex == MenuPageIndex.ServerList)
            {
                regionsPreviousPage = menuPageIndex;
            }
        }
        
        [HarmonyPatch(typeof(MenuManager), "PageOpen")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void MenuManager_PageOpen(MenuManager __instance, MenuPage __result, MenuPageIndex menuPageIndex, bool addedPageOnTop = false)
        {
            if (!PluginLoader.mainMenuOverhaul) return;

            switch (menuPageIndex)
            {
                case MenuPageIndex.PublicGameChoice:
                {
                    TextMeshProUGUI menuPageHeader = __result.transform.Find("Header/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                    if (menuPageHeader)
                    {
                        menuPageHeader.text = "Join Game";
                    }

                    AddRegionMenuButton(__result.transform, new Vector2(8f, 8f), MenuPageRegions.Type.PlayRandom);
                    break;
                }
                case MenuPageIndex.Saves:
                {
                    if (SemiFunc.MainMenuIsMultiplayer())
                    {
                        AddRegionMenuButton(__result.transform, new Vector2(520f, 325f), MenuPageRegions.Type.HostGame);
                    }
                    break;
                }
                case MenuPageIndex.ServerList:
                {
                    AddRegionMenuButton(__result.transform, new Vector2(8f, 8f), MenuPageRegions.Type.PlayRandom);
                    break;
                }
            }
        }

        private static void AddRegionMenuButton(Transform parent, Vector2 localPosition, MenuPageRegions.Type pageType)
        {
            DataDirector.instance.networkRegion = PlayerPrefs.GetString("PUNSelectedRegion", "");
            ServerSettings.ResetBestRegionCodeInPreferences();
            MenuAPI.CreateREPOButton(GetRegionDisplayName(DataDirector.instance.networkRegion), () =>
            {
                MenuManager.instance.PageCloseAll();
                MenuManager.instance.PageOpen(MenuPageIndex.Regions).GetComponent<MenuPageRegions>().type = pageType;
            }, parent: parent, localPosition: localPosition);
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
            if (createNewBtn) createNewBtn.buttonText.text = "Refresh";
        }

        [HarmonyPatch(typeof(MenuPageServerList), "OnDestroy")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_OnDestroy()
        {
            return !MenuManager.instance || MenuManager.instance.currentMenuPageIndex != MenuPageIndex.Regions;
        }

        [HarmonyPatch(typeof(MenuPageServerList), "ButtonCreateNew")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageServerList_ButtonCreateNew()
        {
            if (!PluginLoader.mainMenuOverhaul) return true;

            RefreshLobbies();
            return false;
        }
        
        [HarmonyPatch(typeof(MenuPagePublicGameChoice), "ButtonRandomMatchmaking")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static void MenuPagePublicGameChoice_ButtonRandomMatchmaking()
        {
            GameManager.instance.matchmakingMode = PluginLoader.saveMatchmakingEnabled.Value ? GameManager.RandomMatchmakingModes.Join : GameManager.RandomMatchmakingModes.JoinOrCreate;
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
        
        [HarmonyPatch(typeof(MenuPageRegions), "PickRegion")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        [HarmonyPriority(Priority.First)]
        private static bool MenuPageRegions_PickRegion(MenuPageRegions __instance, string _region)
        {
            PlayerPrefs.SetString("PUNSelectedRegion", _region);
            PlayerPrefs.Save();
            
            if (regionsPreviousPage == MenuPageIndex.ServerList)
            {
                DataDirector.instance.networkRegion = _region;
                MenuManager.instance.PageCloseAll();
                MenuManager.instance.PageOpen(regionsPreviousPage);
                return false;
            }

            return true;
        }
        
        [HarmonyPatch(typeof(MenuPageRegions), "ExitPage")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageRegions_ExitPage(MenuPageRegions __instance)
        {
            if (!PluginLoader.mainMenuOverhaul) return true;
            
            MenuManager.instance.PageCloseAll();
            MenuManager.instance.PageOpen(regionsPreviousPage);
            return false;
        }
        
        // Fix server list infinite loading when closing regions page
        [HarmonyPatch(typeof(MenuPageRegions), "OnDestroy")]
        [HarmonyPrefix]
        [HarmonyWrapSafe]
        private static bool MenuPageRegions_OnDestroy()
        {
            return !MenuManager.instance || MenuManager.instance.currentMenuPageIndex != MenuPageIndex.ServerList;
        }
        
        [HarmonyPatch(typeof(NetworkManager), "OnDisconnected")]
        [HarmonyPostfix]
        [HarmonyWrapSafe]
        private static void NetworkManager_OnDisconnected(DisconnectCause cause)
        {
            if (cause == DisconnectCause.InvalidRegion)
            {
                PlayerPrefs.DeleteKey("PUNSelectedRegion");
                PlayerPrefs.Save();
            }
        }
    }
}