# SC-CargoPlan user guide

Everything the [README](../README.md) leaves out. Same order: first start, entering missions, ContractTracker, at the
cargo lift, settings.

- [First start](#first-start)
- [Reading the ship view](#reading-the-ship-view)
- [Entering missions](#entering-missions)
- [Hotkeys](#hotkeys)
- [ContractTracker](#contracttracker)
- [At the cargo lift](#at-the-cargo-lift)
- [Click-through and padlock](#click-through-and-padlock)
- [Settings and your look](#settings-and-your-look)
- [Where your data lives](#where-your-data-lives)
- [Overlay unreadable?](#overlay-unreadable)

## First start

Run Star Citizen in **Borderless** window mode. In exclusive fullscreen the game covers every other window, the
overlay included.

Without a plan, the overlay shows your ship with empty racks, "No plan. Open the editor to enter missions." and an
**Open editor** button (see [Entering missions](#entering-missions)).

**Your ship:** click the ship name at the header's left end and pick your ship from the list, grouped by manufacturer.
The ships you chose last stand on top under "Recent". To find a ship faster, just type: the name line becomes a search
field while the list is open, and every key narrows the list by name or manufacturer code ("hull c", "crus"). Enter
picks the ship when only one is left, Esc empties the search, a second Esc closes the list. While the list is open your
keys go to the search, not to the game; after picking, one click into the game gives it the keys back.
A ship whose cargo module changes its hold has a line of its own, "RSI Aurora Mk II · TS module" next to
"RSI Aurora Mk II": pick the one you fly, switching is a ship change like any other.
With a plan the overlay asks first, then plans your missions anew for the new ship. Ticks stay, also a single legend line:
cargo you have delivered was never on the new ship, so it takes no room there and isn't drawn, only its legend line
stays struck through. Tick it back and it is laid into free cells. Like the header buttons
the list is hidden while the overlay is locked, the name stays.

**Exit:** the **X** at the header's right end, next to the padlock, closes SC-CargoPlan. There is no "are you sure?":
your plan, ticks, position and look are saved before it closes. Like every header button it is hidden while the overlay
is locked.

**Remembers where you put it:** drag the overlay next to your cargo grid and resize it once; position and size are
saved right away, so they survive a restart and even a crash. If you unplug a monitor, the overlay moves back onto the
screens you have. To move the overlay window, drag it by its header (next to the ship name, which opens the ship list),
the red lines below the legend, if any, or the border.

## Reading the ship view

The overlay draws your ship's cargo racks as boxes, at first seen from the front, from the port side and from above:
the bow points to the lower left, the port side faces you. Under the racks lies your ship's outline seen from above, so
you can tell bow, sides and shape at a glance. It has the ship's real size (larger only where the racks would not fit
in it), and the view opens on the racks and a little room towards the bow, so most of the hull lies outside until you
zoom out; it doesn't say where a rack sits in the hull. A ship without an outline shows a wedge in front of the racks instead, with "Bow" on
its floor. Instead of the outline the ship's 3D hologram can stand behind the racks, true to scale (**Ship view** in the
[settings](#settings-and-your-look)). Turn and tilt the ship until it looks the way you see the hold, even from below for racks under the hull.
Each rack shows its number on its largest visible face. Long ships (more than twice as long as wide, such as the C2,
the Caterpillar or the Hull C) start seen from the port side with the length almost level, and the view shows the racks
that hold cargo; the empty racks further aft are dimmed and come into view when you zoom out. Their numbers stand above
the racks, so a full rack never hides its number. Each destination has its own color, and the legend (below the
ship, unless you [move it](settings-format.md#legend-layout)) has a line per mission and destination,
`To Green Glade Station · 5 SCU (4+1) · Pressurized Ice`; two missions to Riker give two lines in Riker's color.
The legend line shows crate sizes and commodity so you can match boxes at the elevator: `(4+1)` is one 4 SCU and one
1 SCU crate, `(2×4+1)` two 4s and a 1, a single crate has no brackets, and a line with two commodities reads
`12 SCU (8+2) Titanium, (2) Gold`. The sizes are the crates the plan holds, so after a split or merge they are the ones
you have. Once any mission names its pickup or comes from ContractTracker, the legend groups the lines by mission, each
under a header with where its crates are collected: `From Lorville`, `From A, B, C`, or `From ?` where the mission
doesn't say, followed by ContractTracker's number for the contract (`From Lorville · #47`, `#47–48` for one capture of
two commodities) – the number ContractTracker shows, not one from the game. When the overlay is too narrow for a line,
the commodity shortens first, then the destination; sizes, number and markings always stay. Hover a line or header for
its whole text. Missions stand in the order their destinations first appear. A destination split over two racks
has the same color in both. A rack shared by two destinations shows both colors side by side, and a crate on another
destination's cargo shows its color on top of theirs: the colors say what lies where, no text lines needed.

Each crate is drawn as one box in its shape (a 4 SCU crate is 2 × 2 × 1 cells), with a small gap all round; free space
shows as single cells. Which rack a destination gets doesn't depend on the order you entered the missions: the
planner keeps each destination's crates together, gives each destination a rack of its own where it can, and keeps
crates off other destinations' cargo where a layout allows. It places the crates the largest first, layer by layer away
from the plate (on the
Hull A from the spindle outwards, as in the game), and in each layer row by row from the bow. Crates stay where the plan
says, after a restart and for everyone who loads the plan, until you drag them somewhere else (see
[Changing the plan](#changing-the-plan)). Where the elevator really puts a crate is still up to the game. A crate always
rests on the plate or on other crates.

- **Color** = destination. Up to eight colors (six or seven with the other stop color sets, see
  [Settings](#settings-and-your-look)); from then on the colors repeat **hatched** "/", after another round hatched
  "\", the same in the view and on the legend's color square (the legend says so).
- **Bright white edge** = loaded: on board, marked in the legend (see [Loading](#loading)).
- **Gray** = delivered.
- **Outline only** = free cell. Far out, only the free cells in the layers that hold crates are drawn.
- **Red** = a crate that can't lie like this, see [Stacking and red crates](#stacking-and-red-crates).

**How full is the ship?** The header shows it next to the ship name: `56/64 · 8 free` is 56 SCU on board of 64, 8 free.
Once you mark cargo loaded, `· 40 loaded` says how much of it is. While a mission waits for room, `· 16 waiting` follows with its SCU: that is the moment for
[Re-plan](#changing-the-plan). Ticked crates are off the ship and don't count. The numbers stay while the overlay is
locked; in a narrow overlay the ship name gives way first. Each rack says the same as a tool tip anywhere over it,
`Rack 3 · 12 / 16 SCU`, empty racks included.

Hover over a crate to see what it is, e.g. `Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4)`, with its
rack's load in a second line. Inside the view: **left drag** turns and tilts the ship, **right drag** next to the crates moves the view, the **mouse wheel**
zooms (while you drag a single crate it turns the crate instead, see [Changing the plan](#changing-the-plan)), a **double-click** next to the cargo resets the view to the ship's starting view.
**Reset view** in the header, right next to the gear, does the same, and so does its hotkey (none by default, see
[Hotkeys](#hotkeys)), which also works in the game while the overlay is locked.
The wheel zooms in as far on a Hull C as on a Hull A: up to 160 pixels per cell by default, so even on a long ship you
can get close enough to hit a single crate. **Max zoom per cell** in the settings' **Ship view** section sets how far;
zooming out stops at half the size that fits the window, or further where the whole ship outline needs it, so you can
always see the complete hull (move the view if it sits off-centre). Turning keeps the size the ship had when you
started, so a hologram doesn't grow and shrink as it turns; from then on the view no longer fits itself to the window
or to new cargo until you reset it. The view stays as you leave it, per ship: after a
restart, and when you switch to another ship and back, the ship is turned, zoomed and moved as before. The reset brings
the starting view back, also for the next start.

## Entering missions

**This is the full loop:** accept your contracts, enter them in the editor, press **Plan**, load the racks as the
overlay shows, tick each destination at its cargo lift. No terminal, no other tool.

Open the editor with the **pencil** in the overlay's header (left of the gear), or with **Open editor** while there is
no plan. The pencil is hidden while the overlay is locked: planning needs a free cursor. The editor is a normal window,
so it takes the focus while you type; afterwards click back into the game (or Alt+Tab).

- **One row per contract item, two numbers from the contract:** Destination, Commodity, **SCU** and **Max crate** (the
  "4 SCU or smaller" in the contract text). A contract with two commodities for the same destination is two rows with
  the same destination.
- **The crates appear by themselves:** the **Crates** column shows how the freight elevator delivers them, the largest
  allowed crate first, the rest in smaller ones: 6 SCU at max 4 is `4+2`, 15 SCU at max 4 is `4+4+4+2+1`.
- **Your ship's racks set a limit too:** no crate larger than the largest one a rack of your ship takes. Some holds
  have a door smaller than the hold: the Clipper's takes crates up to 2 SCU, so an 8 SCU contract shows `2+2+2+2` and
  the overlay says `crates capped to 2 SCU (door limit)`. On a Hull A a 32 SCU contract becomes `16+16`, `crates capped
  to 16 SCU (largest rack)`. Change the ship and the mix follows; crates typed by hand stay as typed. Some racks take
  only large crates: the Hull C's spindle takes 32 SCU crates only, so a contract with smaller crates waits (see below).
- **The kiosk delivered something else?** Click the **Crates** cell and type what you got, e.g. `2+2+2`. Crates typed by
  hand show in *italics*; clear the cell to go back to the worked-out mix.
- **Stop order:** the **Stop** column numbers the destinations in the order you entered them; the legend and the
  colors follow it. It doesn't decide the racks, and which stop you fly to first is up to you. Stops follow the first
  row of each destination, so `Riker, Shubin, Orison, Shubin` gives stops 1, 2, 3, 2. Use **↑**/**↓** to move rows;
  destinations match regardless of upper or lower case.
- **Sorting:** click the **Mission** or **Destination** header to sort the rows, again to sort them the other way, a
  third time for the order you entered them. Sorting only changes what you see: **Plan** gives the same plan, and stops,
  legend and colors keep the order you entered. Sorted by mission, ContractTracker's contracts come first by number and
  typed rows after them, either way round; empty rows stay at the bottom. **↑**/**↓** work once the sort is off.
- **Pickup** (optional): where you collect the crates, e.g. `Lorville`, or `Area18 / Lorville` for a contract with
  several pickups (the contract names no amounts per pickup, so neither does the row). Two rows with the same commodity
  for the same destination from two pickups stay apart in the plan.
- **New rows:** type into the empty row at the bottom (Tab from the last cell gets you there), or press **Add**. A new
  row takes the max crate size and the pickup of the row above; the first row starts at 4.
- **Mistakes show in red:** an empty destination or commodity, an SCU that isn't a whole number, or crates typed by hand
  that don't add up to the SCU, aren't a crate size or are larger than the max (`2+2+3` for 6 SCU). The reason is in the
  tool tip and below the grid; **Plan** stays off until every row is fixed, and hovering over it shows why. If the
  missions don't fit the ship, the reason shows in the same place.
- **Plan** sends the rows to the overlay, saves them and closes the editor. If the missions don't fit, the editor stays
  open with the reason. Planning again makes a new plan: moved cargo is laid out anew, but what you marked **loaded**
  stays where it lies and the rest goes around it, and what you ticked stays ticked. A destination whose rows you
  changed is planned again; the amber line names it (`Riker: changed in the editor – no longer loaded`). If the rest of a
  contract you loaded in part finds no room around the loaded cargo, Plan says so and the editor stays open.
- **Next route:** the editor opens with your current missions. **Clear rows** empties the rows for the next route; the
  overlay keeps the old plan until you press **Plan**. Closing the editor without **Plan** throws away your changes, not
  your plan.
- **Import… / Export…** read and write [mission files](mission-format.md), e.g. to share a route. Export writes the rows
  as they are, planned or not. Older files with one crate size per row still import; their crates show as typed by hand.
- **Fill order** next to **Plan** chooses which end of each rack the crates start from: **Bow first** or
  **Stern first**. It takes effect when you press **Plan** and is saved with the plan, so it stays after a restart and
  the editor opens with it. Clearing rows keeps it. Export writes it into the mission file, and importing a file that
  names one sets it, so a shared route arrives with the fill order it was planned with; older files keep yours.
- **Copy as text** copies the current plan as plain text, ready to paste into Discord or your org chat.

When Star Citizen takes the mouse (flight), the overlay turns click-through and an open editor hides, so neither catches
a click meant for the game. The editor comes back when the cursor does, without taking the focus. The overlay hotkey
hides it too.

## Hotkeys

Four global hotkeys work while Star Citizen has the focus and the cursor is hidden: one switches the padlock, one hides
the overlay and brings it back (dogfight, landing), one takes new ContractTracker captures in (see
[ContractTracker](#contracttracker)), one re-plans (see [Changing the plan](#changing-the-plan)). There are **no default hotkeys**: a hotkey takes its keys away from every other
program, the game included, and any default could clash with one of your Star Citizen bindings.

**Set them in the settings window** (the gear), section **Hotkeys**: click the button next to *Lock / unlock*,
*Hide / show overlay*, *Sync from ContractTracker*, *Re-plan*, *Discard plan*, *Undo*, *Redo* or *Reset view*, then press the keys. The hotkey works at once and is saved. **Esc**
cancels, **Clear** removes it.

- **At least one of `Ctrl`, `Alt` or `Win` is required**, plus one key: `A`–`Z`, `0`–`9`, `F1`–`F24` (not `F12`,
  Windows reserves it), `Space`, `Pause`, `ScrollLock`, `Insert`, `Delete`, `Home`, `End`, `PageUp`, `PageDown`. Anything
  else is refused right at the button, with the reason.
- Pick a combination that **none of your Star Citizen bindings use**, and check it in the game's keybinding settings.
  If the game still reacts to it, choose another one.
- **Win combinations** may be taken by Windows itself (`Win+L` locks the PC): pressing them while recording does
  nothing.
- A combination another program already uses is refused, and your old hotkey stays. Often you won't even get that far:
  Windows hands such a combination to the other program, so pressing it while recording does nothing. Choose a different
  one.
- The hotkeys can't share a combination; the window says which one has it.
- If a hotkey works in the window but does nothing later, another program swallows the keys before Windows hands them
  out (some tools do that for their own shortcuts). Choose a different combination.
- Hidden, the overlay also leaves the taskbar; press the hotkey again to bring it back where it was.

You can still write them into `%LocalAppData%\SC-CargoPlan\settings.json` by hand (overlay closed), e.g.
`"hotkeys": { "toggleLock": "Ctrl+Alt+K", "toggleOverlay": "Ctrl+Alt+H", "syncContractTracker": "Ctrl+Alt+J", "replan": "Ctrl+Alt+R", "discardPlan": null, "undo": null, "redo": null, "resetView": null }`,
modifiers and key joined by `+` in any order and case; `null` or a missing entry means no hotkey. A hotkey that is
invalid or taken shows as an amber line next to the legend and next to its button in the settings window; the other
hotkeys still work. A typo in a name (e.g. `toggleLok`) makes the whole file unreadable: the overlay starts with
defaults and says so.

No hooks: recording reads the keys you press in the settings window itself, and the hotkeys use the normal Windows
hotkey service; nothing touches the game process.

## ContractTracker

If you use ContractTracker to capture contracts (F3 at the terminal), the overlay takes every capture into the plan by
itself, a few moments after you press F3. No editor, no click.

- **Nothing that's loaded moves.** A new contract goes into free cells: first the rack of a destination already on
  board, then an empty rack, then any rack with room. Crates and ticks you have stay where they are. Ticked crates are
  off the ship, so their cells are free for new ones.
- **Each capture is a mission of its own.** The legend marks what the capture couldn't tell for sure, at the stop it
  concerns:
  - `4 SCU?`: the contract named no max crate size, so the largest crate that fits was assumed. Check the kiosk; pick
    the right size in the editor if it differs.
  - `⚠ 1 of 3 destinations`: the contract lists more destinations than were read. Capture it again, or add the missing
    rows in the editor.
  - `⚠ duplicate?`: another mission without a tick has the same commodity for the same destinations and the same reward
    (or a reward that couldn't be read). Two real contracts: ignore it, it goes with the first tick. A second capture of
    the same contract: delete one in the editor.
  - `⚠ doesn't fit (16 SCU, 8 free) – tick to drop`: no room for the whole contract (the cargo lift hands out whole
    contracts). Click the line to drop it, or wait: it is placed as soon as there is room, at the next capture or with
    **Sync from ContractTracker**.
  - `⚠ doesn't fit (no rack accepts 4 SCU crates, door limit 2 SCU)`: crates you typed by hand are larger than any
    rack's door takes. Clear the **Crates** cell in the editor, or type crates that get through.
  - `⚠ doesn't fit (ship takes 32 SCU crates only)`: your ship's racks take only larger crates than this contract comes
    in (Hull C). The app never makes crates larger than the contract allows; take the contract on another ship.
- **Retry before you load.** A recapture (F4) is a new mission. Before your first tick it is marked `⚠ duplicate?`;
  after a tick it isn't, and you see the destination twice. Delete the wrong one in the editor.
- **Deleted in ContractTracker:** a contract you have fully ticked or loaded (or that didn't fit) leaves the plan as soon
  as you hand it in or delete it there; cargo that lay on its crates settles down (`1 loaded crate settled`). One with
  crates on board that aren't marked loaded stays, and an amber line names it until you tick it or delete it in the editor.
- **Pickup** comes along from ContractTracker as the place's name: when ContractTracker's pickup field runs on into the
  contract's next line (`Everus Harbor. - Freight elevator at …`), only the name stays. Misreadings in the name itself
  stay; correct them in the editor's **Pickup** column if you want the legend tidy. A plan saved before that is cleaned
  once when it loads (`Pickups cleaned: Everus Harbor`).
- **A pickup can be a destination too.** Waste contracts run the other way: you collect the waste at the stations and
  deliver it to Everus Harbor, where the Stims of another contract are collected. The legend then shows
  `To Everus Harbor · 8 SCU` next to `From Everus Harbor`; both are right.
- **In the editor**, the **Mission** column shows where a row came from (`#37–38` = ContractTracker's ids, as in the
  legend's header; empty = typed). **Delete mission** takes the selected row's mission out of the plan at once, with its crates; nothing else
  moves and you don't press **Plan** for it (typed rows of one destination count as one mission).
  **Sync from ContractTracker** takes in what came since the last sync, the same as a capture does.
- The first time, everything that is in ContractTracker comes in. Delete old contracts there first (× or F5) if you
  don't want them. **Discard plan** starts over the same way (see [Changing the plan](#changing-the-plan)).
- **Keep the overlay outside ContractTracker's capture region** (the area you set with F6), or ContractTracker reads
  the legend along with the contract.
- **Settings** (the gear), section **ContractTracker**:
  - The overlay finds ContractTracker by itself. If yours keeps its database elsewhere, pick `contracts.db` with
    **Browse…**; **Detect automatically** goes back. A new path works at once, without a restart.
  - **Auto-sync** off: captures don't come into the plan by themselves; an amber line says
    `2 new contracts in ContractTracker`. **Sync from ContractTracker** in the editor, or its hotkey, takes them in.
  - Without ContractTracker the section says so and links to its page on Nexus Mods.
- **Hotkey "Sync from ContractTracker"** (section Hotkeys, not set by default): takes new captures in like the editor's
  button, without opening the editor. If the database isn't there, an amber line says where the overlay looked.
- **New database:** if ContractTracker starts over with a new file (reinstalled, another path), the overlay notices and
  takes in everything in it once (`ContractTracker database looks new – all contracts imported`).

The overlay only reads ContractTracker's database (by default `%AppData%\SCContractTracker\contracts.db`), never writes
it, and never touches the game.

## At the cargo lift

### Loading

At the elevator: let Sync add contracts; mark a delivery **loaded** in the legend once it is on board. Plan and Re-plan
only move what is not loaded. Discard clears everything.

A click on a line in the legend goes **planned → loaded → delivered → planned**: loaded lines end in `▣` and their crates
get a bright white edge, delivered ones are gray and struck through. Loaded crates stay where they lie: Plan, Re-plan, a
sync and making room for other cargo go around them. You can still drag them yourself, and split or merge them; they
stay loaded. Clicking a crate in the ship view still delivers the whole stop; loaded is set per delivery in the legend.
A misclick? **Undo** (↶) takes every change of state back.

When cargo leaves (you tick it, ContractTracker sees the contract handed in, you delete the mission), the crates that lay
on it sink straight down their column until they rest again, loaded or not; the amber line says so (`1 loaded crate
settled · 1 planned crate moved down`). A crate that comes to rest on cargo with only part of its bottom stays there and
shows red; drag it where it really lies. **Loaded** belongs to this ship: changing the ship plans everything anew and
the amber line says `2 crates were marked loaded – cleared for MISC Freelancer`; ticks stay.

### Unloading

When a destination has taken its cargo, click any of its cargo in the view: every crate for that station turns gray,
whichever mission it belongs to, and its legend lines are struck through, so the next cargo lift only shows what is still
on board. To tick one mission's crates only, click its line in the legend instead (`To Riker` under that mission). The
ship stays where it is. A destination split over two racks is ticked once, both racks turn gray. Clicked the wrong one?
**Undo** (↶) takes it back, or click again: the stop goes back to planned, into its cells while they are free, else into
free cells. When every destination is ticked, the header says "All stops delivered."
Ticking doesn't take the focus from the game, so the terminal keeps working. Every tick is saved right away, so a game
crash or a restart between two stops keeps your plan and your ticks.

### Changing the plan

When the elevator puts a crate somewhere else than planned, make the plan follow the plate. **Right drag a crate** onto
the cell where it really lies, in the same rack or another one. Point at a crate to put it on top of that crate, or at a
free cell to put it into that gap; it drops down to the next crate or the plate below, never hanging in the air. While
you drag, the crate fades and a **cyan ghost** shows where it will go. **Turn the mouse wheel** while you drag to turn
the crate by 90°, as it turns in the game; each notch turns it once more. Let go on the crate's own cell to turn it where
it lies. Only crates of 2, 16, 24 and 32 SCU look different turned. A selection or a legend line keeps how its crates
lie; the wheel zooms then. The app itself turns a crate only where it doesn't fit otherwise.

If the crate can't lie at that cell, the app finds room instead of saying no, and the ghosts show it before you let go:
first the nearest free place in that rack; then the rack laid out anew, so other crates in it may move; then other
destinations' crates move out of that rack into others to make room ("Moved 2 crates for Shubin to rack 1 to make
room"); then another rack ("No room in rack 4 – placed in rack 2"). A rack whose door is too small for the crate is
skipped at once ("Rack 1: door limit 2 SCU – placed in rack 2"). Every crate that changes place fades and gets a cyan
ghost, and the line in amber says what moved. Crates that lay on the one you pulled away settle down where it was. If
the crate fits nowhere, its ghost turns **red** with "no room for 4 SCU crate" when you hover, and nothing moves. Crates
are never split on their own; that stays the scissors' job. Putting a crate on another destination's cargo is your
call: the app does it.

To move several crates at once, **right click** each of them first: a magenta edge marks the selection. Every other
crate of the same mission gets a white edge and all the rest is dimmed, so you see which crates came together; with one
crate selected, a line top left says where it is collected, what it is and where it goes: `from Lorville · Titanium · to
Riker`, or `from ? · …` where the mission doesn't say. Rows you type for one destination count as one mission, captures
each as their own. The legend's **check boxes** select too: the box in front of a line selects that line's
crates on board, the box at a mission's header the whole mission's. Several boxes add up. A box is ticked while all its
crates are selected and shows a bar while only some are, also when you selected them with right clicks; clicking a box
with a bar selects the rest, clicking a ticked box takes its crates out. A click on the box never ticks the line off;
the click on the line next to it does. Right drag any
selected crate and the whole selection goes along, every crate keeping its place relative to the one you hold, even
across racks. If the selection doesn't fit as it is, the app finds room as above, crate by crate, and may spread it over
racks. A selection moves as a whole: if one crate fits nowhere, nothing moves, and the amber line names the crates
without room. Right click a selected crate again to take it out, or right click free space to clear the selection; any
change of the plan clears it too. Locking the overlay keeps it: mark the crates you are looking for, lock, and the
marks stay in view at the elevator. A drag you are in the middle of when the overlay locks ends without moving anything.

To move a line's crates, **right drag the line in the legend** onto a rack; right drag a mission's header to move the
whole mission. The crates fade, the rack gets a cyan edge and the ghosts show where they go. On release they are laid
into that rack, other crates there rearranged or moved to other racks to make room. Like a selection, they move as a
whole: what doesn't fit into that rack goes into another one ("No room in rack 3 – placed in rack 2"), and if one crate
fits nowhere, nothing moves. To take a move back, use **Undo** (below): dragging back only restores it as long as
nothing else had to move for it.

Delivered crates can't be dragged, they are off the ship: their cells count as free, so crates can go where they lay.
Moves are saved like ticks; pressing **Plan** in the editor makes a new plan from the rows and drops them, keeping what
is loaded and what is ticked (see [Entering missions](#entering-missions)); **Re-plan** does the same with the missions
you have (below).

**Re-plan** (↻ in the header next to the pencil, or in the editor next to *Import…*, or its hotkey) lays out everything on board anew, as a fresh plan of the same
missions would; with cargo marked loaded, it leaves that where it lies and lays the rest around it. If that would leave
more waiting than your plan does now, it keeps your plan and says `Re-plan found no better layout`. Use it when the plan has grown in pieces: two contracts are loaded and a third one says `⚠ doesn't fit
(no room for 16 SCU crates)` although there is room, only not in one piece. It is your call, because it costs work: crates
change racks and cells, so **you restack the ship** at the cargo lift. What it drops: where every crate on board lies,
and your moves. What it keeps: your ticks (ticked crates are off the ship and take no room), the crate sizes (split,
merged or typed in the editor), the fill order, the legend's order and colours, and the missions themselves. It always
asks first, in the middle of the window you clicked in (*No* is preselected, so Enter keeps your plan); **Undo** takes
it back. Afterwards the amber line says what happened, `Plan rebuilt – 4 crates moved`, or `Plan unchanged` if the layout
already was the fresh one; a contract that still doesn't fit is named (`– Everus Harbor still doesn't fit`). The buttons
are grayed out while nothing unticked is on board. Rows you are editing stay a draft: Re-plan works on
the plan, not on the rows.

**Discard plan** (in the editor next to *Re-plan*, or its hotkey) empties the plan when the run is over: missions, crates
and ticks go, the overlay shows *No plan* as on the first start, and the amber line says `Plan discarded – the next sync
brings every ContractTracker contract back.` It doesn't ask; **Undo** brings the plan back. The fill order stays. With ContractTracker the overlay
starts over as on the first start: the next **Sync from ContractTracker** takes in every contract still in ContractTracker,
so delete the finished ones there first (× or F5). With auto-sync on that happens at the next capture by itself. Rows you
are editing stay a draft; clean rows become one empty row.

**Undo and redo** (↶ and ↷ in the header left of the reset button, or their hotkeys) go back and forward through your changes
to the plan: moves, legend drags, turning a crate, split and merge, Re-plan, **Plan** in the editor, deleting a mission,
Discard plan, and every click that loads, ticks or unticks. ↶ shows once there is something to undo, ↷ once you have
undone something; a new change drops what you could redo. A sync isn't undone: a contract that came in from ContractTracker after a move is still there
when you undo the move. Changing the ship empties both (the amber line says `Undo history cleared by ship change`),
and so does closing the overlay: undo is not saved. The last 100 changes can be undone.

### When the kiosk delivers other crates

Sometimes the elevator hands out two 2 SCU crates where the plan has one 4 SCU crate, or the other way round. Make the
plan match with the two buttons left of the pencil. **Split** (scissors): select one crate and click it; the crate
becomes the two largest crates that add up to it, in the same place (a 4 becomes 2 + 2, an 8 becomes 4 + 4, a 32
becomes 16 + 16). Need smaller ones? Click again, the parts stay selected. **Merge** (chain links): select two or more
crates and click it; they become one crate. The rules are the game's: only crates of one destination and one commodity,
in one rack, adding up to a crate size (1, 2, 4, 8, 16, 24 or 32 SCU) no larger than the contract allows, and lying
together in exactly that crate's shape (two 2s side by side make a 4, two 2s in a row don't). If they lie apart, drag
them together first. A grayed-out button tells you why when you hover over it. Split and merge never move other crates,
and the mission in the editor follows: its crates column then lists the crates as they are, in italics.

### Stacking and red crates

The app doesn't know your route, so it doesn't tell you which stop comes first; the colors show what lies where. A rack
with two colors holds two destinations: the elevator takes only its own crates. A crate whose color sits on top of
another's has to come off before the cargo below can go. The planner avoids that where a layout allows; where none does
(several destinations fill a rack), it makes the plan anyway.

**Red** is for crates that can't lie like this: overlapping another, or with nothing below them. Only a plan file edited
by hand gets those, or a crate that settled onto cargo with part of its bottom (see [Loading](#loading)); drag it where
it really lies.
Red marks the crate (outline) and its rack (edges), with a red line below the legend.

## Click-through and padlock

**Never eats your tractor-beam click:** the overlay becomes click-through automatically while Star Citizen has the
mouse. In flight and first person the game hides the cursor and it follows your view, so a click over a normal window
would be lost. As soon as the cursor shows (mobiGlas, Esc menu, terminals) or another program is in front, the overlay
is interactive again: you can zoom, hover and move it.

The small padlock in the overlay's top-right corner locks it by hand: the overlay stays click-through even with the
cursor showing, so every click goes to the game. Click the padlock again to unlock. A closed amber padlock means
locked, an open white one unlocked. Clicking the padlock or the overlay doesn't take the focus from the game.

Start with `--locked` to begin with the padlock locked.

## Settings and your look

The **gear** in the header, left of the X, opens the settings window. Every change shows in the overlay at once and is
saved, there is no OK or Cancel. Tune it in the game, where you can see whether the legend reads over the hangar
lights. The sections Window, Legend, Legend layout, Crates, Header, Buttons and Ship view each have **Reset to defaults**; Tool
windows, Hotkeys and ContractTracker have none (their defaults are "follow Windows", "no hotkey" and
**Detect automatically**).

On top of the window, always in reach:

- **Preset:** **Default** (the look the overlay starts with), **High contrast** (solid background, white text, strong
  halos, solid crates) or **Minimal** (see-through, small text, faint crates). A preset applies at once, colours,
  legend layout, tool window theme, backdrop and zoom limit included. Change any value afterwards and the box says
  **Custom**; change it back and the preset shows again.
- **Export…** saves the look as you see it to a file (`my-look.cargoplan-look.json`) to share or keep. **Import…**
  loads one: it changes the look only, never your hotkeys, the ContractTracker settings, where the overlay stands, how
  you left each ship's view or whether holograms may be downloaded. A file of another version or with keys it doesn't
  know changes nothing, and the amber line says why. There is no undo for a preset or an import: the sections'
  **Reset to defaults** take you back, and `settings.json.bak` next to `settings.json` holds the file as it was before the
  first change of this run (see [Where your data lives](#where-your-data-lives)).

The sections:

- **Window:** background colour, and its slider is the overlay's transparency; frame colour and width.
- **Legend:** text colour, text size (or the Windows text size), a halo for reading over bright scenes, and the colours
  of the check boxes: **Check box** is the frame, **Check mark** the tick and the bar inside it, each with its own
  transparency.
- **Legend layout:** where the legend stands (top or bottom; left, centre or right) and whether it shows the
  destination lines at all. A section of its own, so resetting the legend's colours doesn't move it.
- **Crates:** **Stop colors**, the set of destination colors: Okabe-Ito (8 colors, the default), Tol Bright (6) or Tol
  Muted (7), each chosen by its author to stay apart with the common kinds of colour blindness; switching recolours the
  plan at once, every destination keeps its place in the order. Then how solid the crates and their edges are (at the lift, turn the fill down to see the real crates through
  the planned ones), the colour and size of the rack numbers and the "Bow" label on the bow wedge, and their outline.
  The ship outline's line takes the rack numbers' colour, toned down. **Selection**, **Selection width** and **Contour
  around the selection edge** set how crates you selected with a right click are framed (magenta over a dark contour by
  default).
- **Header:** the colour of the ship name and the glyphs (pencil, gear, split, merge, exit, padlock), and a halo around
  the ship name.
- **Buttons:** background, frame and rounded corners of the glyph buttons, and a halo around the glyphs.
- **Ship view:** **Backdrop** shows the ship's outline under the racks (the default), its hologram in one of three
  looks (shaded, flat, hidden line), or **None**: racks and frame only, without any mark of the bow. The hologram is
  true to scale and the view fits all of it, so the crates come out smaller than with the outline, which the view
  cuts to the racks and the bow.
  **Hologram opacity** (10–100 %, default 40 %) sets how strongly it shows, **Hull colour** the hull in the shaded and
  flat looks, **Edge colour** the edges in the hidden-line look. **Download ship holograms:** ship
  holograms are an optional download from this repository's releases; they derive from game models and may be
  withdrawn on request – the app checks at most once a day. Off means no network access at all; holograms already
  downloaded stay. Until a hologram is there, and when it can't be fetched, the outline shows; the first failure of a
  run says why in an amber line. Known limits: on some long ships the rack row reaches out of the hull (Caterpillar);
  the Hull C, D and E show their cargo in the hologram; a hologram look takes about 40–50 MB more memory.
- **Tool windows:** light, dark or follow Windows for the settings window and the mission editor; the overlay keeps the
  look you set above.
- **Hotkeys:** see [Hotkeys](#hotkeys).
- **ContractTracker:** see [ContractTracker](#contracttracker).

A colour is a hex code like `#141414` (the `#` is optional) plus a slider for its transparency; a code that isn't one
turns the field red and changes nothing. Or click the colour square next to it: a picker opens, where you drag across
the square for how vivid and how bright, and along the rainbow bar for the hue. The overlay follows while you drag. Esc
or a click elsewhere closes the picker. The settings window hides with the overlay, like the editor, and opens next to
it. Every key, its default and its range: [settings file format](settings-format.md). There are no free stop colours,
only the three sets above: each is checked to stay apart with colour blindness, a hand-picked one might not be.

The footer of the settings window shows the version, e.g. `SC-CargoPlan 0.1.0+<commit>`; you can select and copy it
for a bug report.

## Where your data lives

The files are in `%LocalAppData%\SC-CargoPlan\`; settings and plan are saved as soon as something changes.

| File | What | Deleting it (overlay closed) |
|---|---|---|
| `settings.json` | Overlay position and size, hotkeys, look, tool window theme, ContractTracker path and auto-sync ([format](settings-format.md)) | Default size, position and look, no hotkeys, ContractTracker detected automatically. Your plan stays. |
| `holo\holo-v1\` | Ship holograms you downloaded, one file per ship, and the list of them ([format](holo-format.md)). | Downloaded again when you pick the ship, if downloads are on. |
| `plan.json` | Your current plan: the missions you entered or captured, which crate lies where in which rack, ticks ([format](plan-format.md)). The editor opens with these missions. A plan file from an older version is converted when the overlay loads it and saved in the new format; your ticks stay. | The overlay starts without a plan. Your settings stay. |

If a file can't be read, the overlay starts without it and says so in an amber line. A single setting out of range or a
colour that isn't one is corrected on its own, named in that line, and the rest of `settings.json` applies. It doesn't
overwrite the file: `settings.json` stays until you move the overlay or change a setting, `plan.json` until you make a
new plan. Before the first save after each start, the old `settings.json` is kept as `settings.json.bak`, so a hand edit
or the look before an import is never lost to one click.

## Overlay unreadable?

If you turned the text and frame invisible, the gear is still there and still takes clicks: move the mouse along the
header from the padlock to the left. The first button is the X (tool tip "Exit", don't click it); the second one is the
gear: wait until the tool tip "Settings" shows, and click. In the settings window, **Preset → Default** brings the whole
look back at once. Or close the overlay and delete the `appearance` part from
`settings.json`: the look goes back to the defaults, your position and hotkeys stay.
