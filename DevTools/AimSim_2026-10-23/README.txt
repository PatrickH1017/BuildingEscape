AimSim - checks of the first-person aiming and reload code that run OUTSIDE Unity (2026-10-23, DEVELOPER_NOTES.md section 2.61).
Nothing here is part of the game: it is outside Assets, so Unity ignores it.

arm\   A small stand-in for the parts of UnityEngine the aiming layer uses (Shim.cs: Vector3, Quaternion, Transform hierarchy, with Unity's
       conventions), a copy of HeldGun.SolveArm (SolveArmCopy.cs) and the test (Program.cs). It compiles the REAL files
       Assets\Kinemation\SightsAligner\AnimToolkitLib.cs, Assets\Scripts\Guns\SightsAimLayer.cs and ReloadMotionRules.cs.
       78 checks: the layer puts the sight of a gun in two hands (and in one) on the line of sight within 3 mm, turns it to the aim,
       keeps the elbows on their side, follows a whole reload without a jump, and says no (and puts the arms back) for a bad or unreachable aim.
pure\  The editor's NetDevSightsAimTest.cs (unchanged) compiled with a tiny stand-in for NetDevTest.Check: 52 checks of the reload motion.

How they were run (on a Linux machine, not on this one): build_linux_as_run.sh and build_pure_linux_as_run.sh call the C# compiler directly
(the paths in them are the paths of that machine - change them), then `dotnet aimsim.dll` and `dotnet pure.dll`.
The two .csproj files list the same sources with paths relative to this folder; they have NOT been built (no NuGet access there).

What these do not tell: how anything looks or behaves in Unity, and the real characters' bones (the test body is mine: arm 0.585 m, bones hanging down).
