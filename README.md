# Songs of Conquest Access

Songs of Conquest Access is a screen reader accessibility mod for Songs of Conquest, a spiritual successor to the Heros of Might and Magic series. It provides full narration of the game’s screens, allowing blind and visually impaired players full access to the game.

Refer to [the documentation](https://neurrone.github.io/soc-access/) for installation and usage instructions.

## Installation

Extract the release archive into the Songs of Conquest installation directory and allow it to merge with the existing files. The mod and its stable loader are installed together under `BepInEx/plugins/SongsOfConquestAccess/`; translations remain under `BepInEx/config/SongsOfConquestAccess/translations/`.

If upgrading from an older release, remove `BepInEx/plugins/ScriptEngine.dll`, the `BepInEx/scripts/` directory, and the old `BepInEx/plugins/SongsOfConquest.Access.dll`. The current loader owns the mod lifecycle and loads the mod from the nested plugin directory.

I have a [Discord](https://discord.gg/4wgAFFyPCH) for discussion of my modding projects.

If you'd like to support my work, you can do so on [Patreon](https://patreon.com/NeurronesMods).

## Features

- Full narration of menus, text, tooltips and other game UI elements
- Support for the Windows version of the game with a keyboard
- Keyboard-based drag-and-drop, which is used extensively throughout the game
- Buffer system for review of tooltips, lengthy text elements and event notifications
- Scanner system, bookmarks and audio beacons for overworld navigation
- Narration of all combat events with summaries to condense similar events for less verbosity
- In combat, readouts for threat information for tiles: attack, deadly and movement range as well as zone of control
- AI written translations for all other languages that the game supports: French, Italian, German, Spanish, Polish, Russian, Portuguese - Brazil, Simplified Chinese, Korean, Traditional Chinese, Ukrainian, Turkish and Japanese
