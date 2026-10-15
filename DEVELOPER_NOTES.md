# Overtime Escape (BuildingEscape) - developer hand-off notes

Last updated: 2026-10-15 (end of the session that built pinning / busted / backup / buy-back).
Unity 6 (6000.0.45f1), **built-in render pipeline**, Netcode for GameObjects over Steam. Co-op heist: climb 13 floors
from the Loading Docks, bank loot in the service elevator, avoid the police.

## 1. What to do first (nothing below has been run in Unity)

The last session had no Unity available. The code was syntax-checked and the pure rules were compiled and tested
outside Unity, but the Unity-facing code has **not been compiled in the editor or played**.

1. Open the project, let it compile. Fix any red errors first (most likely spots: `HeistManager.cs`, `Vendor.cs`, `GuardAI.cs`, `Net/NetPlayer.cs`).
2. Run the editor tests: `BuildingEscape.EditorTools.NetDevTest.PolicyTest` (batch `-executeMethod`) now calls `CustodyTest()`. Expect every `[NetTest] policy custody: ...` line to say PASS.
3. Turn on Test Mode (Overtime Escape > Test Mode), press F1. New section "Police": **Pin me**, **Free me**, **Call backup now**.
4. Play it through (section 3 lists what to look for), then offline first, then host + one client.
5. Run Overtime Escape > **Audit Rendering (find pink)** once; it writes `Logs/RenderAudit.txt`. No report existed when this was written.
6. Commit the two new scripts **with their .meta files** (`CustodyRules.cs`, `CustodyHud.cs`).

Git note: `VoiceChat.cs`, `VoiceNoise.cs`, `NetDevSneak.cs` and `NetDevTest.cs` showed as modified before this session, but only
because of line endings (`git diff -w` is empty). The working tree uses CRLF; everything delivered in this session is CRLF too.

## 2. What was added (pinned / busted / backup / buy-back)

Rules (the spec the brother wrote down):

- An officer that **touches** a free player **pins** them (held down on the ground). Touch = within `GuardAI.catchRadius` (1.6 m) while chasing.
- A pinned player has **12 s** until they are **busted** (arrested).
- If the pinning officer is **hit by another player** (melee or a thrown item), the pinned player is freed.
- When **nobody is free** (everyone pinned or busted) the whole team is busted: run over (`HeistPhase.Failed`). With one player, a pin is an instant bust, same as before.
- After a guard calls backup, **another officer is called every 30 s** until the floor ends.
- Busted players can be **bought back from the vendor** once the team is at the Loading Docks. The vendor says: *"One of your friends got got. You're lucky I have someone on the inside"*.

Where it lives:

| File | Role |
|---|---|
| `Scripts/CustodyRules.cs` (new) | Pure C#, no Unity types: `CustodyState`, all numbers (12 s, 30 s, 3 s immunity, buy-back price `300 + 50 x level`), `BackupClock`, the vendor lines. Unit tested. |
| `Scripts/WobblyPlayer.cs` | `Custody`, `PinLeft`, `BustedOnLevel`, `PinnedBy`, `PinImmuneUntil`; `ApplyCustody(...)` (every machine); a player in custody cannot walk, jump, crouch, hide; body is tipped over and held down (`HoldDown`). |
| `Scripts/HeistManager.cs` | Host/offline logic: `Pin`, `FreePinned`, `BustPlayer` (timer in `TickCustody`), `ReleaseBusted`, `CheckTeamDown`, `CallBackup` + `TickBackup` (repeat clock), `CountCustody`. Banner texts via `Announce`. |
| `Scripts/GuardAI.cs` | New state `GuardState.Pinning` (appended last, so network snapshots stay compatible). Chase ends in `HeistManager.Pin`; `BeginHold` / `UpdatePinning` / `ReleaseHold`. Any `Stun` or `WeaponHitHost` on a holding officer frees the player. Guards ignore players who are not free (`CanSee`). |
| `Scripts/Net/NetPlayer.cs` | Three host-written NetworkVariables (`custody`, `pinSecondsLeft`, `bustedLevel`) applied on every machine. |
| `Scripts/Net/RunSync.State.cs` | Replicates `BackupIn` (seconds to the next officer). |
| `Scripts/Net/RunSync.Requests.cs` | `RequestBuyBack(slot)` client -> host; host rejects pinned/busted callers at the elevator panel. |
| `Scripts/Vendor.cs` | Shop window lists busted teammates with a **Buy back** button; `HostBuyBack` / `BuyBackFor` check cash, counter and target. Pinned/busted players cannot shop. |
| `Scripts/VendorChat.cs` | Speech bubble: greeting `"Glad you made it. Make it fast and make it a good deal."`, or the buy-back line while a friend is busted. Lines also set in `FloorGenerator.Dock.cs`. |
| `Scripts/CustodyHud.cs` (new) | Self-installing IMGUI: big PINNED countdown / BUSTED for you, labels over downed teammates, "BACKUP: another officer in N s". |
| `Scripts/HeistHud.cs` | Top line now shows `Free / Pinned / Busted` instead of the old, unused `Caught x/3`. |
| `Scripts/ElevatorExtraction.cs`, `WobblyPlayerRegistry.cs` | "Everyone aboard" now ignores busted players (`WobblyPlayerRegistry.AllAboard`, uses a set instead of a counter). |
| `Backpack`, `PlayerGrab`, `MeleeController`, `HideSpot`, `PlayerVisual` | Small gates: no inventory/grab/swing/hide while in custody; lying pose. |
| `TestMode.cs`, `Editor/NetDev/NetDevTest.cs` | Cheat buttons and `CustodyTest()`. |

Design decisions worth knowing (change them if the team disagrees):

- **Busted bodies stay in the world**, lying down, and ride along in the elevator car (the floor loader puts every player in the car). They are invisible to guards and ignored by the "everyone aboard" check.
- A busted player's **backpack is left alone** (not dropped). Carried items in the hands are dropped.
- Buy-back price is `300 + 50 x (level they were busted on)`: $350 on floor 1, $900 on floor 12. Tune in `CustodyRules.BuyBackPrice`. Only available at the docks (that is the only floor with a vendor).
- Only a **chasing** officer pins (same rule the old instant-bust used). Walking into an idle patrol guard does not.
- A freed or bought-back player is immune to pinning for 3 s.
- The first backup call still sends 2 officers (existing behaviour); every repeat sends 1. `HeistManager.BackupCalled` is now set on the first call, so the end screen reads "BACKUP CALLED - BUSTED".
- The pinning officer just stands facing the player; there is no kneeling animation yet.
- Pins and the repeat clock are cleared when a new floor starts; busted players stay busted.

## 3. Things to check when playing

- Offline: get caught by a chasing guard -> you fall over and the run ends immediately (one player = whole team).
- Two players: one gets pinned -> countdown 12..1 on their screen, label over their head for the other player. The other hits the officer with a weapon (or throws a mug) -> pinned player gets up, officer is stunned.
- Let the 12 s run out with a teammate still free -> "BUSTED", the run goes on. Free player finishes the floor, rides to the docks (the busted player is lying in the car), opens the shop (E), **Buy back** appears at the bottom; the vendor's bubble shows the buy-back line.
- Everyone down -> "BUSTED on <floor>" screen.
- After the first backup call the banner "BACKUP: another officer in N s" counts down and an officer arrives from the guest elevator every 30 s.
- Host + client: the same, from the client's side (client pinned, client buys back).

## 4. Other work in the repo (done by the brother, commit 6738b81, checked this session)

- **Loading dock** (`FloorGenerator.Dock.cs`): three garage doors, two shut, the third open with the vendor's trailer backed in; the shop is inside the trailer; punching bag, clutter. Matches the request.
- **Vendor speech bubble** (`VendorChat.cs`): was a placeholder ("Shocked you made it back"); fixed this session to the requested line.
- **Purple (pink) materials**: `Editor/RenderAudit.cs` plus ~100 materials converted. A static scan of every `.mat` found no URP/shader-graph shaders left (109 Standard, 4 on a project shader, 1 skybox) and all runtime `Shader.Find` calls use `Standard` with a legacy fallback. Not seen on screen this session, so run the audit once. Also check *Project Settings > Graphics > Always Included Shaders* contains `Standard` for builds.
- Voice: guards hear you by loudness (`VoiceNoise.LevelFromRms`, 3 m at the mic gate to 30 m at a shout). The commit titled "Added pinned state start" contains only this voice work; there was no pinning code in the repo before this session.

## 5. Known risks / follow-ups

- Network: the NetDev multi-machine tests (`NetDevHarness`, `NetDevQa`) call `CallBackup`; the new 30 s repeat can spawn extra guards in the middle of a long test and upset guard-count comparisons. Raise the interval or stop the clock in those tests if they flake.
- Custody physics runs on the machine that owns the body (the tip-over torque in `ApplyCustody`). Teleporting a busted body into the elevator car resets its rotation; `HoldDown` tips it over again.
- Remote clients are not stopped by the host from sending grab/store requests while in custody (only the local input is blocked). Fine for friends, not for a public build.
- `HeistManager.Caught`, `maxCaught`, `NotifyPlayerCaught` are legacy and unused.
- Nice to have: kneel/pin animation for the officer and a "held down" pose for the player, a handcuffed/arrested look for busted players, a sound for pin and bust, tuning of the 12 s / 30 s / price numbers.

## 6. Workflow notes for the next agent

- Scripts are CRLF. If you edit with tools that expect LF, convert, then convert back before saving into the project.
- Floor generation must stay deterministic: only `System.Random` for floor content, never `UnityEngine.Random`; creation order matters for `Net.NetIds`. Clients regenerate identical floors from the host's seed.
- The host runs all rules (guards, loot, banking, vendor, custody); clients send `Request*` calls (`RunSync.Requests.cs`) and receive events/state (`RunSync.Events.cs`, `RunSync.State.cs`).
- Pure-logic code (`Scripts/Interior/*`, `CustodyRules.cs`) compiles outside Unity with plain `csc` and is the easiest place to add tests.
