# R14 (2nd step): the long sight is a FACTOR of the ordinary range (3.15 x: 44 m for 14 m), so a guard made blind by a tiny sightRange (the tests and the picture tools do that) stays blind.
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
Open-Src "$Tree\Assets\Scripts\GuardRules.cs"
if ($s.Contains('LongSightFactor')) { "already patched"; return }
Rep 'within <see cref="LongSightHalfAngle"/> degrees of the way he faces his sight grows from his ordinary range to <see cref="LongSightRange"/> metres, most of all dead ahead.' 'within <see cref="LongSightHalfAngle"/> degrees of the way he faces his sight grows from his ordinary range to <see cref="LongSightFactor"/> times that (44 m for the usual 14 m), most of all dead ahead.'
Rep '        public const float LongSightRange = 44f, LongSightHalfAngle = 9f;' '        public const float LongSightFactor = 3.15f, LongSightHalfAngle = 9f;'
Save-Src

Open-Src "$Tree\Assets\Scripts\GuardAI.cs"
Rep @'
        [Tooltip("Down a straight line of sight (a hallway) he sees this far, in a narrow beam of 'longSightAngle' degrees each side of the way he faces (GuardRules.RangeAt).")]
        public float longSightRange = GuardRules.LongSightRange, longSightAngle = GuardRules.LongSightHalfAngle;
'@ @'
        [Tooltip("Down a straight line of sight (a hallway) he sees this many times as far as usual, in a narrow beam of 'longSightAngle' degrees each side of the way he faces (GuardRules.RangeAt).")]
        public float longSightFactor = GuardRules.LongSightFactor, longSightAngle = GuardRules.LongSightHalfAngle;
'@
Rep '            float far = p.IsHidden ? range : Mathf.Max(range, longSightRange);       // (down a straight line of sight he sees much further)' '            float far = p.IsHidden ? range : range * Mathf.Max(1f, longSightFactor);       // (down a straight line of sight he sees much further)'
Save-Src

Open-Src "$Tree\Assets\Scripts\GuardVision.cs"
Rep '            float far = Mathf.Max(range, ai.longSightRange), longHalf = ai.longSightAngle;' '            float far = range * Mathf.Max(1f, ai.longSightFactor), longHalf = ai.longSightAngle;'
Save-Src

Open-Src "$Tree\Assets\Editor\NetDev\NetDevR3Test.cs"
Rep '            const float range = 14f, angle = 65f, far = GuardRules.LongSightRange, longHalf = GuardRules.LongSightHalfAngle;' '            const float range = 14f, angle = 65f, far = range * GuardRules.LongSightFactor, longHalf = GuardRules.LongSightHalfAngle;'
Rep '            Check("no long beam when the long range is not longer (a hiding player is seen from close by only)", Mathf.Approximately(GuardRules.RangeAt(0f, 1.5f, 1.5f, longHalf), 1.5f));' '            Check("no long beam when the long range is not longer (a hiding player is seen from close by only)", Mathf.Approximately(GuardRules.RangeAt(0f, 1.5f, 1.5f, longHalf), 1.5f));
            Check("a guard made blind (a tiny range) stays blind down the hall too", !GuardRules.InVision(0f, 1f, 0f, 5f, 65f, 0.1f, 0.1f * GuardRules.LongSightFactor, longHalf));'
Save-Src
