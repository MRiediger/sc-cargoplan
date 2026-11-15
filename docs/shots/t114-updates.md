# Shot list: Updates (T11.4)

Two stills for the wiki page [Updates](https://github.com/MRiediger/sc-cargoplan/wiki/Updates), recorded for v0.5.0.
Conventions as in [`SHOTLIST.md`](SHOTLIST.md): look preset **Default**, PNG, the overlay's area only, no player or org
names. Page prefix `updates`. Both files go into the wiki repo's `media/` folder (`..\sc-cargoplan.wiki\media\`); the
wiki page already carries their `![…](media/<file>)` line and the `<!-- SHOT: <id> -->` comment.

## Setup: an update to offer

The overlay offers an update only when a newer version exists. Build two local versions, as in
[`docs/update-testing.md`](../update-testing.md), and start the older one against them, with a data folder of its own
so your profile stays untouched:

```powershell
$run = "$env:UserProfile\CargoPlan-shots"          # outside the repo, delete afterwards
./build/publish.ps1 -Version 0.5.0
New-Item -ItemType Directory "$run\feed" | Out-Null
Copy-Item out\release\* "$run\feed\"
Expand-Archive out\release\SC-CargoPlan-v0.5.0-win-x64.zip "$run\portable-0.5.0"
./build/publish.ps1 -Version 0.5.1 -DeltaBase "$run\feed\CargoPlan-0.5.0-full.nupkg"
Copy-Item out\release\* "$run\feed\" -Force
& "$run\portable-0.5.0\SC-CargoPlan.exe" --data-dir "$run\data" --updates-source "$run\feed"
```

In the overlay: unlock it (padlock). No plan: the empty overlay keeps the eye on the line. The bottom line says
`Update available · v0.5.1`.

## Shots

| Id | Prio | File | Type | Page | What it shows | State |
|---|---|---|---|---|---|---|
| U1 | must | `updates-line.png` | PNG | Updates | The overlay without a plan, unlocked; its bottom line `Update available · v0.5.1` with **Update** and **Skip this version** | Before you click anything on the line |
| U2 | must | `updates-settings.png` | PNG | Updates | Settings window, section **Updates**: *Check for updates on startup* ticked, `Skipped version: v0.5.1` with **Clear**, **Check now** without a status | After U1: tick **Skip this version** (the line goes), gear → scroll to **Updates**; don't click **Check now** |

Don't click **Update**: it would swap `portable-0.5.0` to 0.5.1. Afterwards close the overlay and delete `$run`.
