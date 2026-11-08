# Nexus Mods upload checklist

Nexus is the second place to get SC-CargoPlan: the GitHub release comes first, Nexus gets the same ZIP, and every text
on the page comes from this folder (D87). Follow it for the first upload and for every new version. The steps follow
the Nexus mod editor: **General → Media → Files → Requirements → Permissions**, then **Publish** at the top right.

Texts: [`summary.txt`](summary.txt), [`description.bbcode`](description.bbcode). Before a new version, bring
`description.bbcode` in line with the release notes and the README (same sentences), then upload.

## 0. Before you start

- **First upload only:** Nexus staff have answered [`staff-request.md`](staff-request.md) and agreed, **or**
  November 16, 2026, the date the request names, has passed without an answer. Date and wording (or "no answer") go
  into TASKS.md. If they refuse, stop: no upload.
- The GitHub release of the version is out and marked Latest.

## 1. The ZIP, checked against the release

In PowerShell, into an empty folder:

```powershell
$version = '0.3.0'
$dir = New-Item -ItemType Directory "$env:TEMP\nexus-$version"
& "C:\Program Files\GitHub CLI\gh.exe" release download "v$version" -R MRiediger/sc-cargoplan -p "SC-CargoPlan-v$version-win-x64.zip*" -D $dir
$zip = Get-Item "$dir\SC-CargoPlan-v$version-win-x64.zip"; (Get-FileHash $zip).Hash -eq (Get-Content "$zip.sha256").Split(' ')[0]
& "C:\Program Files\GitHub CLI\gh.exe" release view "v$version" -R MRiediger/sc-cargoplan --json assets --jq '.assets[] | "\(.name) \(.size)"'
```

`True`, then upload exactly this file, never a local build. Note the ZIP's size in bytes from the last line, for step 6.

## 2. General

| Field | Value |
|---|---|
| Game | Star Citizen |
| Name | `SC-CargoPlan` |
| Summary (350 characters) | [`summary.txt`](summary.txt) |
| Full description | [`description.bbcode`](description.bbcode): open the BBCode source with the **[ ]** button, replace the template's text with the file's, switch back and check the five blocks (Description, Installation instructions, Main features, Requirements, Shout outs), the links and the lists |
| Category | Miscellaneous (the only one for Star Citizen) |
| Mod version | `$version` (e.g. `0.3.0`), without "v" |
| Author | Martin Riediger |
| Language | English |
| Tags (where the editor offers them) | Utilities for Players, Quality of Life, User Interface |
| Generative AI tags (where the editor offers them) | **AI-Generated Content** and **AI Media**, as the guidelines ask (D87) |
| Adult content | No |

## 3. Media

The hero GIF and the Hull B picture are from v0.3.0; the other pictures show the app before the perspective view (D85)
and with the old hull. That trade-off is accepted until the backlog line "Screenshots/GIFs auf Perspektive nachdrehen"
fires.

Files from this repository (`docs/images/`) and the wiki repository (`media/`), in this order:

| # | File | Caption |
|---|---|---|
| 1 | `docs/images/hero.gif` | SC-CargoPlan next to the cargo elevator |
| 2 | wiki `media/legend-overview.png` | The legend with mission headers and lines |
| 3 | wiki `media/shipview-move-crate.gif` | Dragging a crate into another rack |
| 4 | wiki `media/quickstart-mark-loaded.gif` | Clicking a legend line marks it loaded |
| 5 | wiki `media/quickstart-deliver.gif` | Clicking a crate delivers the whole stop |
| 6 | wiki `media/ships-select.gif` | Picking a ship by typing into the ship list |
| 7 | wiki `media/editor-rows.png` | The mission editor with the test plan |
| 8 | wiki `media/ct-settings-found.png` | The ContractTracker section with the database found |
| 9 | wiki `media/prefs-presets.gif` | Switching between the three presets |
| 10 | wiki `media/shipview-hologram.gif` | The ship view with the shaded hologram, an optional download |

If Nexus refuses `hero.gif` (6.2 MB) or shows it without motion, take one frame from it instead of recording anew:

```powershell
ffmpeg -ss 4 -i docs/images/hero.gif -frames:v 1 docs/nexus/hero.png
```

Commit `docs/nexus/hero.png` and use it as picture 1.

## 4. Files

| Field | Value |
|---|---|
| File | the ZIP checked in step 1 |
| File name | `SC-CargoPlan v$version` |
| Category | Main files |
| Version | `$version` |
| Description | [`summary.txt`](summary.txt) |
| Older version | on an update, move the previous ZIP to Old files |

## 5. Requirements

None. ContractTracker is optional (the editor works without it) and Nexus has no "optional" mark, so it stays in the
description's Requirements block only. .NET comes with the ZIP.

## 6. Permissions

The app is MIT, but the switches apply to the whole file, and `outlines/` in the ZIP isn't MIT.

| Field | Value |
|---|---|
| Upload, modification, conversion | allowed, with credit and the MIT license notice kept |
| Asset use | ask the author / credit the author (the ship silhouettes aren't MIT) |
| Donation Points | never opt in; the description says nothing about donations |
| Permission notes | `The app is under the MIT license (LICENSE in the ZIP). Not under MIT: the ship silhouettes (outlines/NOTICE.md, in this ZIP) and the ship holograms (not in this ZIP; their own NOTICE in the holo-v2 release on GitHub).` |
| Credits | the Shout outs block of [`description.bbcode`](description.bbcode), without the license line |

## 7. Publish and check

1. Preview the page, then **Publish**.
2. Download the ZIP **from Nexus** into a new folder, put the `.sha256` from step 1 next to it and run the check line
   from step 1 again: `True`.
3. Compare the size Nexus shows with the bytes from step 1, in MB (bytes / 1,000,000) or MiB (bytes / 1,048,576):
   which unit Nexus uses isn't documented.
4. Write the page URL, the date and both checks into TASKS.md; a new page also goes into the README's Install section.
