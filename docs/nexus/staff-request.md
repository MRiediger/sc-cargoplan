# Request to Nexus Mods staff before the first upload

Sent by the maintainer to `support@nexusmods.com` before SC-CargoPlan goes on Nexus (D87). The File Submission
Guidelines prohibit files that connect to the internet "unless where it is crucial for the functioning of the
mod/utility" and ask authors of such tools to contact staff with their reasoning and the source code
([File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines), last updated
27 Oct 2026). The code links point to commit `fd75313`, the state of v0.1.0's code, so the line numbers hold.

The request names November 16, 2026 as the publishing date: 14 days, not an open wait (D87). Without an answer by
then, the page goes up with the disclosure of this request; a refusal stops T9.3.

Record the date sent, the date and wording of the answer (or "no answer by November 16") in TASKS.md (T9.3).

---

**Subject:** Pre-upload question: Star Citizen overlay with an optional, user-requested download

Hello,

I'd like to publish SC-CargoPlan on Nexus Mods (Star Citizen, category Miscellaneous) and ask first, because of the
guidelines' rule on files that connect to the internet.

Unless you tell me otherwise, I plan to publish the page on November 16, 2026, with the disclosure below. If you need
more time, I will hold.

**What it is:** a load-plan overlay for Star Citizen haulers: it decides which destination goes into which rack before
you load, and shows it in a translucent window next to the game. A separate window: nothing is injected into the game.
Open source under MIT: https://github.com/MRiediger/sc-cargoplan. The upload would be exactly the ZIP of the GitHub
release (same SHA256).

**1. Network access: a feature the user requests, not a program that phones home.**

- The default look of the ship view is a flat outline. With the outline (or no backdrop), the app makes no request at
  all, neither at the start nor while it runs
  ([HoloLoader.cs#L29-L34](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Holo/HoloLoader.cs#L29-L34),
  default:
  [ShipOutlineMode.cs#L36](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Settings/ShipOutlineMode.cs#L36)).
- Only when the user picks a hologram look does the app download a 3D hologram of their ship: plain HTTPS GET requests
  to one address, `https://github.com/MRiediger/sc-cargoplan/releases/download/holo-v1/`, which GitHub redirects to its
  file host
  ([HoloRelease.cs#L19](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Holo/HoloRelease.cs#L19)).
  No login, no token, no API; nothing is uploaded. The User-Agent is `SC-CargoPlan/<version>`; 5 s timeout, at most
  4 MB per response.
- Once a hologram look is chosen, the app checks again at each start and ship change: a small manifest at most once per
  run, and not at all while the cached one is younger than 24 hours; a ship file only if it isn't cached or its SHA256
  differs
  ([HoloSource.cs#L28-L35](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Holo/HoloSource.cs#L28-L35),
  [HoloRelease.cs#L25](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Holo/HoloRelease.cs#L25)).
- The setting **Download ship holograms** turns it off: then the app reads only files it already has and makes no
  request
  ([HoloSource.cs#L91-L94](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Holo/HoloSource.cs#L91-L94)).
- This is the app's only network code, one `HttpClient`
  ([HoloSource.cs#L67](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.App/Holo/HoloSource.cs#L67)).
  There is no auto-update and no telemetry.

The holograms are not in the ZIP. They are made from CIG's ship models, are not under MIT and live in a GitHub release
of their own with its own NOTICE, so they can be withdrawn without a new app version. The Nexus description would say
all of this; the page would not link to the hologram release.

**2. Ship silhouettes in the ZIP.** The folder `outlines/` holds 2D ship silhouettes, traced automatically from the
top-view renders on fleetyards.net, which depict CIG's ships. They are not under MIT and are used for a non-commercial
fan project; `outlines/NOTICE.md` in the ZIP says so. The maintainer of fleetyards.net (Torlek) gave written consent
on October 23, 2026; Cloud Imperium has neither approved nor objected. The description and the permissions would state
it.

**3. ContractTracker.** The app can read the database of ContractTracker (Nexus, Star Citizen mod 34) to take in
captured contracts.

- Read-only, from the local SQLite file ContractTracker writes; no network access, nothing is written to it
  ([ContractTrackerReader.cs#L108](https://github.com/MRiediger/sc-cargoplan/blob/fd7531389c279a4fe9486b193aa7379f33fff707/src/CargoPlan.Import/ContractTracker/ContractTrackerReader.cs#L108)).
- Optional: the app runs without ContractTracker; missions can be typed into its editor instead.
- I asked the author by direct message on October 20, 2026 whether SC-CargoPlan may read the database and document
  the columns it reads. There has been no answer so far. The Discord invite on the ContractTracker page,
  https://discord.gg/9RNUXAdVn, returns "Unknown Invite" (checked November 2, 2026).
- If the author objects, I will remove the ContractTracker references from the Nexus page.

ContractTracker is credited and linked.

The project is a fan project: no donations, no Donation Points. Generative AI tags would be set as the guidelines ask.

Is this acceptable for upload as it stands? If something needs to change, I'd rather know before publishing.

Thank you,
Martin Riediger
