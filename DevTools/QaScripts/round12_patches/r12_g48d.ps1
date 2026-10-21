# Round 12, batch G48d (first person): the shouldered gun a little lower, so that the hands are in the picture (the owner, "the weapon is not visible enough in the hand").
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Scripts\Guns\HeldGun.cs'
Rep 'const float FpForward = 0.42f, FpRight = 0.075f, FpDown = 0.17f;' 'const float FpForward = 0.42f, FpRight = 0.075f, FpDown = 0.215f;'
Save-Src
