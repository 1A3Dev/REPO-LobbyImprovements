# 1.0.7

### NEW

- Added steam rich presence grouping

### FIXES

- Improved stability of public lobby saving

# 1.0.6

### CHANGES

- Changed saves menu header for multiplayer to show whether it's private/public multiplayer
- Disabled text-to-speech for successful commands

### FIXES

- Fixed hosting a public lobby showing an infinite loading screen on some occasions

# 1.0.5

### NEW

- Added `/setname [string]` to rename the current save file
- Added more info to the README.md (such as stating which features are only used by the host)
- Added `/setscene random` which makes it do a random gameplay level

### CHANGES

- Changed `/level` to `/setscene` (to reduce confusion with `/setlevel`)
- Made `/setcash` automatically reload the scene if in the shop (or in a gameplay level if there is an extraction in-progress)
- Made `/setlevel` automatically reload the scene if in a gameplay level or the shop
- When hosting a public lobby using a save file it will no longer give the confirmation popup before the server name input popup (since the server name input prompt is basically a confirmation)

### FIXES

- Fixed cursor disappearing when exiting server name input popup
- Fixed `/valuable` not working in multiplayer
- Fixed pasting into chat not working on some occasions

# 1.0.4

### NEW

- Added config option to skip moon phase animation
- Added config option to skip splash screen
- Added config option to enable save files for public lobbies

### FIXES

- Fixed being able to open chat in the main menu
- Fixed singleplayer deaths taking you to the lobby menu (when it's not enabled in the config)

# 1.0.3

### NEW

- Added missing thing to v1.0.2 changelog

### FIXES

- Fixed pasting into chat bypassing the max chat message length

# 1.0.2

### NEW

- Added `/item [`[string](https://1a3.uk/games/repo/diffs/?tab=4&tabItems=0)`]`
- Added `/valuable [`[string](https://1a3.uk/games/repo/diffs/?tab=4&tabItems=1)`]`
- Added ability to paste into chat

### FIXES

- Fixed name prefixes sometimes not being able to be set after the changes in 1.0.1

# 1.0.1

### CHANGES

- Small tweaks to name prefixes

### FIXES

- Fixed chat commands not working
