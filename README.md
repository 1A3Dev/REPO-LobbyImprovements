# LobbyImprovements

[![Latest Version](https://img.shields.io/thunderstore/v/Dev1A3/LobbyImprovements_REPO?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/Dev1A3/LobbyImprovements_REPO)
[![Total Downloads](https://img.shields.io/thunderstore/dt/Dev1A3/LobbyImprovements_REPO?style=for-the-badge&logo=thunderstore&logoColor=white)](https://thunderstore.io/c/repo/p/Dev1A3/LobbyImprovements_REPO)
[![Discord](https://img.shields.io/discord/646323142737788928?style=for-the-badge&logo=discord&logoColor=white&label=Discord)](https://discord.gg/CKqVFPRtKp)
[![Ko-fi](https://img.shields.io/badge/Donate-F16061.svg?style=for-the-badge&logo=ko-fi&logoColor=white&label=Ko-fi)](https://ko-fi.com/K3K8SOM8U)

### Features

- Chat
  - Added some chat commands (scroll down to view the full list)
  - Enabled pasting into chat input
  - Enabled chat input in singleplayer
- Fast Startup
  - Config option to skip moon phase animation
  - Config option to skip splash screen
- Lobby Menu
  - [Host] Config option to make the lobby menu be shown when loading a singleplayer save file
  - Fixed a few exceptions when going to the lobby menu in singleplayer
- Player Name Prefixes
  - This adds name prefixes for the game's developers and testers
    - If you are one of the R.E.P.O testers and don't see the option for the tester prefix please let me know
    - Developer - https://i.gyazo.com/e81a1b64264d50b624e940b46bd9e5cb.png
    - Tester - https://i.gyazo.com/555a9a4e36615d045aea7dce7ca32ca9.png
- Saves
  - [Host] Command to rename save files
  - [Host] Config option to disable save file deletion
  - [Host] Config option to enable save files for public lobbies
- Server List
  - Made searching for a steam lobby id in the search list attempt to join the lobby
  - Ability to paste into the search input (pasting a lobby link works as well)
- Tester Overlay
  - An overlay in the bottom right which shows the current room name and game version (mainly for the game testers)

### Commands

| Name      | Arguments                                                       | Description                                  | Required Access |
| --------- | --------------------------------------------------------------- | -------------------------------------------- | --------------- |
| /enemy    | [[string](https://1a3.uk/games/repo/diffs/?tab=3&tabEnemies=1)] | Spawns an enemy at the closest level point   | Host            |
| /item     | [[string](https://1a3.uk/games/repo/diffs/?tab=4&tabItems=0)]   | Spawns an item at the closest level point    | Host            |
| /level    | [[string](https://1a3.uk/games/repo/diffs/?tab=2)]              | Switch to a different level                  | Host            |
| /valuable | [[string](https://1a3.uk/games/repo/diffs/?tab=4&tabItems=1)]   | Spawns a valuable at the closest level point | Host            |
| /setcash  | [number]                                                        | Set the total cash amount                    | Host            |
| /setlevel | [number]                                                        | Switch to a different level number           | Host            |
| /setname  | [string]                                                        | Renames the current save file from R.E.P.O.  | Host            |

### Support

You can get support in any the following places:

- The [thread](https://discord.com/channels/1344557689979670578/1391111846823465082) in the [REPO Modding Discord Server](https://discord.gg/repomodding)
- [GitHub Issues](https://github.com/1A3Dev/REPO-LobbyImprovements/issues)
- [My Discord Server](https://discord.gg/CKqVFPRtKp)

### Compatibility

- Supported Game Versions:
  - v0.2.0+
- Not Compatible With:
  - N/A
