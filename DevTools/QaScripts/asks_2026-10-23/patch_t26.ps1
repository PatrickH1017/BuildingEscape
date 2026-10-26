# T26 (the owner, 2026-10-23, added mid-task): "Clicking Alt should not unlock the camera view only when holding it": the free look unlocks the view only after the key has been HELD (FreeLookRules). Patches the scratch copy C:\bd.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'

$SC = $A + 'Scripts\CameraFollow.cs'; Open-Src $SC
Rep '        public bool FreeLooking { get; private set; }' (
  '        public bool FreeLooking { get; private set; }' + "`n" +
  '        /// <summary>Tests: the free look key is held like the real key (the view then unlocks only after <see cref="FreeLookRules.HoldSeconds"/>, as with the real one).</summary>' + "`n" +
  '        public static bool TestFreeLookKey;' + "`n" +
  '        float freeLookHeld;                   // seconds the free look key has been held (FreeLookRules)' + "`n" + "`n" +
  '        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]' + "`n" +
  '        static void ResetTestHooks() { TestFreeLookKey = false; }')
Rep '            if (!locked) return;' ('            if (!locked) { freeLookHeld = 0f; FreeLooking = false; return; }          // (a menu is open: the key does not count)')
Rep '            FreeLooking = !aimLimited && (Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey));' (
  '            // Alt (or V) unlocks the view only while it is HELD, and only after FreeLookRules.HoldSeconds: a tap does nothing (the owner, 2026-10-23: "Clicking Alt should not unlock the camera view only when holding it");' + "`n" +
  '            // a window that lost the focus (Alt+Tab) counts as let go' + "`n" +
  '            bool freeKey = (Application.isFocused && (Input.GetKey(freeLookKey) || Input.GetKey(altFreeLookKey))) || TestFreeLookKey;' + "`n" +
  '            freeLookHeld = FreeLookRules.Held(freeLookHeld, freeKey, Time.unscaledDeltaTime);' + "`n" +
  '            FreeLooking = !aimLimited && FreeLookRules.Unlocked(freeLookHeld);')
Save-Src
"T26 patched"
