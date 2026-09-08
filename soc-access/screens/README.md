# Screens

Accessible screen models and screen-specific navigation helpers live here.

Every screen is registered once, in `SocAccessMod.RegisterScreens`, and polled every frame by
`ScreenManager`: it asks each one `IsActive()`, sorts the survivors by `Layer`, and the top of that
stack (or the deepest child open over it) is the screen the player is on. `ScreenDetector` no longer
pushes or pops anything; it only points a screen's slot (`LiveScreen<TAdapter>.Live`) at the menu the
game has made ready, or clears it.

`IsActive()` runs once per screen per frame: it must never scan the scene, walk a subtree or ask the
game to refresh anything (AGENTS.md, Performance).

## Layers

Layers are STATIC: a screen's number never depends on what is showing. They are numbered with gaps,
and derived from what used to be pushed over what. Two screens on the same layer keep registration
order, which is how combat covers the map.

| Layer | Screen | Why |
|---|---|---|
| 0 | `MainMenuScreen` | the root of the main menu |
| 1 | `CampaignMenuScreen`, `CustomCampaignSelectScreen`, `OnlineGameListScreen`, `CommunityMapsHomeScreen`, `AdventureLobbyMapTypeScreen` | a main-menu page, over the main menu |
| 2 | `TaleSelectScreen` | over the campaign menu that opens it |
| 2 | `OnlineHostGameScreen` | over the game list that opens it |
| 2 | `CommunityMapsCollectionScreen` | over the browser home page |
| 2 | `AdventureLobbyMapSelectScreen`, `AdventureLobbyChallengeMapSelectScreen`, `AdventureLobbyRandomLayoutScreen` | over the map type page that opens them |
| 3 | `CampaignMapSelectScreen` | over the campaign or tale page that opens it |
| 3 | `CommunityMapsDetailsScreen` | over the collection page that opens it |
| 4 | `CommunityMapsSearchResultsScreen` | over the browser pages it was searched from |
| 4 | `AdventureLobbyPlayersScreen` | the lobby proper, over the map pages it was reached through |
| 5 | `CommunityMapsSearchFilterScreen` | over the results it filters |
| 6 | `CommunityMapsModalScreen` | over every browser page: they were pushed underneath it |
| 7 | `PlatformUserMenuScreen` | a popup over the lobby |
| 10 | `AdventureMapScreen` | the world, under everything drawn on it |
| 10 | `CombatScreen` | the battlefield; registered after the map, so it covers it |
| 12 | `PreBattleMenuScreen` | over the map, under what the battle raises |
| 20 | `MapEntityMiniMenuScreen`, `AdventurePlayerMenuScreen`, `OwnedEntitiesScreen`, `TroopOverviewScreen`, `MarketplaceScreen`, `ArtifactMarketScreen`, `TradingScreen` | an in-game panel over the map |
| 20 | `SettlementScreen`, `DefenceMenuScreen` | a landing page, under its own sub-pages |
| 20 | `AdventureLobbyGameSettingsScreen`, `AdventureLobbyPlayerSettingsScreen` | a lobby sub-page, over the lobby |
| 21 | `GiftTownPopupScreen`, `SendResourcePopupScreen` | a popup over the player menu that opens it |
| 22 | `DraftTroopsScreen`, `UpgradeTroopsScreen` | a sub-page over the settlement, dwelling or defence page |
| 22 | `RallyPointScreen` | a dwelling sub-page |
| 22 | `AdventureLobbyInviteProvidersScreen` | over the lobby sub-pages |
| 24 | `BuildMenuScreen`, `ResearchScreen`, `PurchaseWielderScreen` | over the settlement page that opens them |
| 24 | `AdventureLobbyIconDropdownScreen` | over the lobby row it was opened from |
| 26 | `CommanderSheetScreen` | over every panel it can be opened from |
| 26 | `SpellbookScreen` | over the map and the battlefield alike |
| 28 | `PostBattleResultScreen` | the battle result, over what raised the battle |
| 30 | `ClaimMenuScreen` | raised by the battle result, over it |
| 30 | `WorldChoiceMenuScreen`, `HostileJoinMenuScreen` | a story follow-up over the map |
| 30 | `LevelUpScreen` | raised after a battle, over its result |
| 30 | `PlayerStatsScreen` | over the panels it is opened from |
| 31 | `WorldConfirmMenuScreen` | the confirmation of a world choice, over it |
| 32 | `PostAdventureResultScreen` | the end of the adventure, over everything in it |
| 33 | `PostAdventureStatsScreen` | over the adventure result that opens it |
| 34 | `MoveTroopPopupScreen` | over every page that draws a troop row |
| 36 | `TutorialSlideshowScreen`, `TutorialSimpleScreen` | over the page they explain, combat included |
| 38 | `ChatScreen` | over whatever it was opened on |
| 40 | `PauseMenuScreen` | over the game it pauses |
| 42 | `OptionsScreen` | over the pause menu or the main menu that opens it |
| 44 | `SaveLoadGameScreen` | over the pause menu that opens it |
| 46 | `CodexScreen` | over the pause menu or the main menu that opens it |
| 100 | `MessageDialogScreen`, `QuitToDesktopPopupScreen` | a dialog: over every page, whichever raised it |
| 200 | `StoryTextScreen` | the story speaks over everything, dialogs included |
| 1000 | `LoadingCompleteScreen` | nothing can be worked while the game is loading |

Three screens are the mod's own surfaces and are pushed as CHILDREN of whatever page opened them
(`Screen.PushChild`) rather than polled, so the page underneath keeps its cursor while they are up.
Their layers are declared for the dev server's listing only, and are never compared with anything:
`ModOptionsScreen` 50, `ModDialogScreen` 55, `DropListScreen` 60.

## Keeping the cursor

`KeepStateOnPop` is true on `AdventureMapScreen`, `CombatScreen`, `SettlementScreen` and
`DefenceMenuScreen`: a dialog COVERS those by layer rather than deactivating them, and the two
things that do deactivate the map and the battlefield - a story sequence and the loading screen -
are gaps the player comes back from to where they were.
