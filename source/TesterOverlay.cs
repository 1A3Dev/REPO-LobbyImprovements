using System.Collections.Generic;
using System.Linq;
using Steamworks;
using UnityEngine;

namespace LobbyImprovements;

public class TesterOverlay : MonoBehaviour
{
    private string text;
    private string moduleName;

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(text))
        {
            if (!SteamClient.IsValid || !BuildManager.instance)
            {
                return;
            }
            text = $"{BuildManager.instance.version.title}\n{SteamClient.Name} ({SteamClient.SteamId})";
        }
        
        string dynamicText = text;

        if (PluginLoader.testerOverlayModule.Value && SemiFunc.RunIsLevel())
        {
            List<RoomVolume> currentRooms = PlayerAvatar.instance?.RoomVolumeCheck.CurrentRooms.Where(r => r.Module).ToList();
            if (currentRooms?.Any() == true)
            {
                if (currentRooms.All(r => r.MapModule == currentRooms[0].MapModule))
                {
                    moduleName = currentRooms[0].Module.name.Replace("(Clone)", "");
                }

                if (moduleName != null)
                {
                    dynamicText = $"{moduleName}\n{dynamicText}";
                }
            }
            else
            {
                moduleName = null;
            }
        }
        else
        {
            moduleName = null;
        }

        GUIStyle style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerRight,
            wordWrap = false,
            normal = new GUIStyleState { textColor = Color.white }
        };
        GUIStyle shadowStyle = new GUIStyle(style)
        {
            normal = new GUIStyleState { textColor = Color.black }
        };

        float width = 400f;
        float x = Screen.width - width - 4f;
        
        float height = style.CalcHeight(new GUIContent(dynamicText), width);
        float y = Screen.height - height - 20f;

        Rect rect = new Rect(x, y, width, height);
        GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), dynamicText, shadowStyle);
        GUI.Label(rect, dynamicText, style);
    }
}