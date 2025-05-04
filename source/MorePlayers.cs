using HarmonyLib;
using Photon.Realtime;
using Steamworks;
using Steamworks.Data;

namespace LobbyImprovements
{
    [HarmonyPatch]
    public class Patches_Photon
    {
        [HarmonyPatch(typeof(LoadBalancingClient), "OpCreateRoom")]
        [HarmonyPrefix]
        private static void CreateRoom_Prefix(ref EnterRoomParams enterRoomParams)
        {
            if (enterRoomParams.RoomOptions?.MaxPlayers == 6)
            {
                enterRoomParams.RoomOptions.MaxPlayers = PluginLoader.maxPlayerCount.Value;
                PluginLoader.StaticLogger.LogInfo("Changed MaxPlayers for PhotonNetwork.CreateRoom");
            }
        }
        
        [HarmonyPatch(typeof(LoadBalancingClient), "OpJoinRandomOrCreateRoom")]
        [HarmonyPrefix]
        private static void JoinRandomOrCreateRoom_Prefix(ref OpJoinRandomRoomParams opJoinRandomRoomParams, ref EnterRoomParams createRoomParams)
        {
            if (opJoinRandomRoomParams.ExpectedMaxPlayers == 6)
            {
                // opJoinRandomRoomParams.ExpectedMaxPlayers = (byte)PluginLoader.maxPlayerCount.Value;
                PluginLoader.StaticLogger.LogInfo("Changed ExpectedMaxPlayers for PhotonNetwork.JoinRandomOrCreateRoom");
            }
            
            if (createRoomParams.RoomOptions?.MaxPlayers == 6)
            {
                createRoomParams.RoomOptions.MaxPlayers = PluginLoader.maxPlayerCount.Value;
                PluginLoader.StaticLogger.LogInfo("Changed MaxPlayers for PhotonNetwork.JoinRandomOrCreateRoom");
            }
        }
        
        [HarmonyPatch(typeof(LoadBalancingClient), "OpJoinOrCreateRoom")]
        [HarmonyPrefix]
        private static void JoinOrCreateRoom_Prefix(ref EnterRoomParams enterRoomParams)
        {
            if (enterRoomParams.RoomOptions?.MaxPlayers == 6)
            {
                enterRoomParams.RoomOptions.MaxPlayers = PluginLoader.maxPlayerCount.Value;
                PluginLoader.StaticLogger.LogInfo("Changed MaxPlayers for PhotonNetwork.JoinOrCreateRoom");
            }
        }
    }

    [HarmonyPatch]
    public class Patches_Steam
    {
        [HarmonyPatch(typeof(SteamManager), "OnLobbyCreated")]
        [HarmonyPrefix]
        private static void OnLobbyCreated_Prefix(Result _result, ref Lobby _lobby)
        {
            if (_result == Result.OK && _lobby.MaxMembers == 6)
            {
                _lobby.MaxMembers = PluginLoader.maxPlayerCount.Value;
                PluginLoader.StaticLogger.LogInfo("Changed MaxMembers for SteamManager.OnLobbyCreated");
            }
        }
    }
}