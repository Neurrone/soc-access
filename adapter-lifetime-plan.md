# Adapter lifetime plan — one adapter per subject

Written 2026-09-18 after the stale-state audit (commits `6a7d95ae`..`56c06aca` on `main`, 55
commits). `hotseat-plan.md` builds on this plan and must not start before Phase 4 here lands.

## The problem

`LiveScreen.SyncLive` (`soc-access/screens/LiveScreen.cs:88`) and `AdaptedSource.Current`
(`soc-access/screens/ScreenSource.cs:464`) build a new adapter only when the game OBJECT the
source answers with changes. AGENTS.md ("Screen Resolution") turns that into a rule: an adapter
"lives exactly as long as the menu instance it wraps", so a cache "about this menu" is an adapter
field and "needs no reset".

That is true only if one game object serves one subject. The game breaks it constantly:

- one `PreBattleMenu` for every battle the adventure scene does not unload for
  (`PreBattleMenu.cs:281` assigns a new `_mapFormat` per battle);
- one settlement menu, one defence menu, one hostile-join menu, one post-battle menu, one mini
  menu per adventure scene, each re-shown for another town, encounter, battle or entity;
- kingdom overviews refilled from an entry POOL on every `Show(teamId)`; `Hide` only deactivates;
- the project-container menus (options, pause, save/load, codex, tutorials, quit popup) are one
  instance for the whole process, across scenes, games and saves;
- the language changes live, in place, with no object replaced.

14 of the audit's commits (about 1,040 added lines) do nothing but key ONE adapter cache on its
subject, its opening or the language: `6a7d95ae`, `a228128a`, `7a8174ba`, `155c16e2`, `353491c7`,
`c6576280`, `4b51e1c1`, `f0d26986`, `3d49a0fc`, `8541041b`, `a7e4563d`, `eb07d0d8`, `af171931`,
`56c06aca`. Every one was found by audit, not by design. The next cache someone adds will be
wrong by default, because the documented rule says it needs no key.

## The change

The slot that holds an adapter is keyed on the game object AND a subject token. When the token
moves, the slot disposes the adapter and builds a new one, and every cache on it goes with it.

### One slot type, used by both holders

`SubjectSlot<TAdapter>` (new, `soc-access/screens/`), used by `LiveScreen` and by `AdaptedSource`
so the two cannot drift apart. It holds: the menu object, the subject token, the language, the
presence flag, the adapter. `Sync(menu, subject)` answers the adapter and rebuilds when any part
of the key moved.

### The key, every part read from the game each frame

1. **The menu object** — as today.
2. **The subject token** — `protected virtual object SubjectToken(object menu)` on `LiveScreen`
   (and a `Func<TMenu, object>` on `AdaptedSource`), compared by `Equals`. Default null. A screen
   whose menu is re-shown for another subject answers with that subject: the `MapFormat` for the
   pre-battle page, the `IBattleResult` for the post-battle page, the two commanders for hostile
   join, the map entity for the settlement, defence and mini menus, `_async` for the kingdom
   overviews, the current tutorial entry for the tutorial screens.
3. **One continuous presence** (default ON, opt-out): an adapter that has been present and is
   then seen not present is SPENT. It is kept, because `IsPresent` is asked through it and an
   adapter must not be built per frame while a menu is closed; on the frame a spent adapter
   answers present again, the slot disposes it and builds a new one before anything else reads
   it. Rebuild happens on the RISING edge of presence. This covers every "closed, then opened for
   something else" case with no per-screen code.
   - The known hole: a Hide and a Show inside one frame (the kingdom HUD re-toggling a tab,
     `TutorialManager.ShowTutorialInternal`, `MapEntityMiniMenu.Show` for another entity). Those
     screens MUST name a subject token; the lint below cannot prove which screens need one, so the
     list in Phase 3 comes from the audit and is checked against the decompiled `Show` methods.
4. **The language** (default ON): `GlobalLocalizationVariables.LocalizationHandler.CurrentLanguage`
   by reference. A language switch rebuilds the adapter; every string it froze goes with it.

### Two kinds of change

The slot reports WHY it rebuilt: `SubjectChanged` (menu, token or presence) or `LanguageChanged`.
`LiveScreen` reacts differently:

- `SubjectChanged`: say the screen's name while focused (as `Live`'s setter does today) and drop
  the navigator's kept state for this screen (`Navigator.ScreenClosed(this)` when the navigator
  is not attached to it, `Blur()` when it is — `af171931` found `ScreenClosed` detaches the
  navigator and nothing re-attaches it while `Current` is unchanged).
- `LanguageChanged`: rebuild silently and keep the cursor. The focused node's live text re-speaks
  through the navigator's own live watch.

This replaces the three "start from the top" fixes (`a7e4563d`, `af171931`, `56c06aca`):
`KeepStateOnPop` goes back to a constant `true` on the four screens that have it, because the
kept state is now dropped by the subject changing, not by which pop it was.

### Opt-outs from presence

- `AdventureMapScreen`: popped for every story sequence and quick battle with the same adapter
  coming back; the adapter holds the event listener, the grid, scanner state and beacon voices.
  Presence OFF. (Its subject token arrives with `hotseat-plan.md`.)
- `CombatScreen`: the game already gives it a new scene and adapter per battle; the story gap
  inside a battle must keep the adapter. Presence OFF.
- The four community-maps screens that keep a selection across open/close
  (`CommunityMapsSources`, not `ScreenSource`): presence OFF; language ON replaces their
  `SyncLabelLanguage()`.
- Anything whose adapter constructor measures over 2 ms (Phase 0 measures them all).

### Reload safety (the rule this must not break)

A hot reload starts every slot empty; the first frame builds the adapter and records the key
read from the game at that moment. No part of the key is an event, a counter a patch increments,
or anything a hook writes. Verified 2026-09-18 on the current code: reload with the troop
overview open and on the siege pre-battle page, tree identical both times.

New lint (`soc-access/tests/Lint/`): a `SubjectToken` override, and every member it calls in the
mod, may not read a `[HookWritable]` member or a static of a class under `soc-access/patches/`.
An exception is reported to the owner before it merges, like the existing lints.

## What gets deleted, and only after its screen is migrated

Each deletion is its own commit, made after reading that the slot key covers the case, with the
spoken tree diffed before and after (`GET /gui/graph?buffers=1`).

| Today | Replaced by |
|---|---|
| `PreBattleMenuAdapter.GetMap()` invalidating key, terrain, warning flag (`6a7d95ae`) | token = `MapFormat` |
| `TroopPlacementHexGrid._snapshotState` + `RebuildAfterStateChanged` (`a228128a`), carry release in `WatchInstruction` (`8cc474d7`) | token = `MapFormat` + placement state name; the grid is keyed on adapter identity already |
| `Kingdom*OverviewAdapter.SyncOpening()` (`7a8174ba`) | token = the menu's `_async` |
| `PostBattleResultAdapter` result + language in the list keys (`155c16e2`) | token = `IBattleResult`. KEEP `ShownCommanderId` (how "no commander" is read is not a lifetime question) |
| `HostileJoinMenuAdapter._seenStage` reset in `IsPresent` (`353491c7`) | presence |
| `OptionsMenuAdapter` capture stage reset in `IsPresent` (`f0d26986`) | presence |
| `SpellbookAdapter` header miss keyed on instance + language (`eb07d0d8`) | presence + language |
| `CommunityMapsHomeAdapter` row-label miss re-probe (`8541041b`) | KEEP (presence is off for it) |
| Per-site language guards in 18 files (`4b51e1c1`) | language in the key |
| `TutorialS*Screen._lastTutorial` (`af171931`) + its two roster entries | token = current tutorial entry |
| `SettlementScreen` / `DefenceMenuScreen` / `CombatScreen` kept-state fixes | `SubjectChanged` drops kept state |

KEEP the language guard inside `SlotSnapshot` and `ContainerMemo` (`c6576280`): `ModDialog` and
other non-`LiveScreen` users read through them, and it costs one reference comparison.

## What this does not touch

Staleness inside one subject, and state outside adapters: the HUD lists keyed on entity id, the
troop carry, the combat inspect/tooltip/preview keys, the mini menu's deferred-destroy frame, the
artifact market sweep invalidation, `MenuRows` survivor indices, the revealed registry, beacons
and arming, discovery signatures, the narrator reset, the sweep cancel, the keybind retry, the
story-camera dedupe. About 60% of the audit's lines. They stay as they are.

## Phases

**Phase 0 — groundwork, no behaviour change.**
- Correct AGENTS.md, "Screen Resolution", the paragraph beginning "Per-menu state lives on the
  adapter". Replacement text: *"A screen object lives for the whole mod load. An adapter lives as
  long as the SUBJECT it describes: the slot that holds it (`SubjectSlot`) is keyed on the menu
  object, the subject the menu is showing, one continuous presence and the language, all read
  from the game every frame, and builds a new adapter when any of them moves. The game reuses
  its menu objects - one pre-battle page for every battle, one settlement menu for every town,
  one options window for the process - so 'one adapter per menu object' was never 'one adapter
  per subject'. A cache about the subject is an adapter field and needs no key of its own; a
  cache that can go stale WITHIN one subject (a list the game re-sorts, a pooled row, a turn
  passing) is keyed on something read from the game each frame."*
- Measure every adapter constructor (eval recipe in `docs/dev-loop.md` §4a, timing `Adapt`), put
  the table in this file. Anything over 2 ms is a finding to fix or an opt-out.
- Write `SubjectSlot` with unit tests over a fake `IPresent` adapter: same key keeps; menu,
  token, language each rebuild; spent adapter rebuilt on the rising edge and never while closed;
  the previous adapter is disposed exactly once; a throwing `Dispose` does not lose the new one.

**Phase 1 — the slot, behaving exactly as today.** `LiveScreen` and `AdaptedSource` use
`SubjectSlot` with token null, presence OFF, language OFF. Prove no change: `GET /gui/graph` on
every reachable screen before and after, and a hot reload on three of them.

**Phase 2 — project-container menus** (options, pause, save/load, codex, tutorial simple and
slideshow, quit popup, message dialog's popup sources, platform user menu, bug report): presence
and language ON, tutorial token. Delete the options capture reset and the tutorial baselines.
In-game per screen: open, close, reopen from another scene; switch language with it open.

**Phase 3 — adventure-scene menus**, one commit per screen, in this order (highest audit yield
first): pre-battle, post-battle, kingdom overviews, hostile join, settlement + defence + their
draft/upgrade sub-pages, mini menu, spellbook, the town and trade menus, level-up, world choice
and confirm, claim, teleport, dialogue, random event, player menu. For each: name the token (or
state that presence is enough and why), delete what the table lists, run the two-subjects test
(open for A, close, open for B: the tree is B's and the arrival starts at the top).

**Phase 4 — navigator kept state follows the subject.** `SubjectChanged` drops it; revert the
three `KeepStateOnPop` conditionals to constants; delete `CombatScreen`'s `ScreenClosed` call.

**Phase 5 — menu scenes and lobby.** Language ON; delete the per-site language guards in the
lobby, campaign and community-maps adapters.

**Phase 6 — the lint**, then remove this file's "What gets deleted" rows as they land.

## Verification, every phase

1. `dotnet test` and the localization validator.
2. Spoken-tree diff per touched screen, before and after.
3. Hot reload with the touched screen open: `modAssemblyName` increments, stack and tree
   identical, no exception in `/log`. (A `POST /reload` needs `-d ""`; without a body the server
   answers 411 and nothing reloads.)
4. Build time per touched screen in the commit message; over 1 ms is a finding.
5. The two-subjects test where the screen has a token.

## Risks

- A presence default that is wrong for a screen nobody listed as an opt-out: the symptom is a
  cursor or a selection lost on reopen. Phase 1's no-change proof and the per-screen commits
  bound the blast radius to one screen per commit.
- Constructor cost per opening instead of per scene. Measured in Phase 0 before anything moves.
- A token that does not move when the subject does is the same bug in a new place. The
  two-subjects test per screen is what catches it; the lint cannot.
- Every claim about which game objects are reused comes from the decompiled source plus one
  in-game session (2026-09-18: pre-battle page reuse, facade identity across a manual battle and
  a loaded save, overview per-opening token). The rest is unverified until its phase runs.

## Out of scope

Hot-seat (see `hotseat-plan.md`); the copied graph engine under `soc-access/ui/graph/` (two
upstream notes from the audit: the search scope frozen for the life of a search, `ControlId`
hashing at construction); custom scanner keywords matching localized names; the persisted default
custom slot names (owner decision: leave).
