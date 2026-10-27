The other weapons' Blender animations (2026-10-27): from Blender into the game, like the dock worker's pistol
(DevTools\PistolAnim_2026-10-27). DEVELOPER_NOTES.md section 2.69 says how the game uses them.

What the game uses (Assets\Resources\Animations; only the Synty skeleton and its animation, read as HUMANOID clips, so
they play on every character of the game - the dock workers and the officers alike)
  GuardPistol_OneHand.fbx   the officers' pistol: raise from the side (frames 8-24), aim (24-36), one shot (36-46)
  GuardPistol_Reload.fbx    the officers' magazine change (1-66; the same moments as the dock worker's)
  Smg_Fire.fbx              the SMG: raise from the low ready (8-19), aim (19-23), one shot (23-26)
  Rifle_Fire.fbx            the rifle: raise (8-19), aim (19-23), one shot (51-61)
  Shotgun_Fire.fbx          the shotgun: raise (8-19), aim (19-23), one shot (23-49)
  Sniper_Fire.fbx           the sniper rifle: raise (8-21), aim (21-27), one shot (27-67)
  Crowbar_Swing.fbx         the crowbar: held ready (1-4), the swing (1-46: the blow at 19)
  Bat_Swing.fbx             the bat: the same
  Hatchet_Swing.fbx         the hatchet (the Synty fire axe): the same

Where they came from (Blender 5.2, made 2026-10-26/27; they are in the owner's Downloads folder, not in the repo)
  Pistol_Raise_Aim_Fire.blend  (an officer, SM_Chr_Security_Male_01, with SM_Wep_Pistol_Heavy_01)
  Guard_Pistol_Reload.blend
  SMG_Fire.blend, Rifle_Fire.blend, Shotgun_Fire.blend, Sniper_Fire.blend  (Dock worker 1 with the game's guns)
  Crowbar_Swing.blend, Bat_Swing.blend, Hatchet_Swing.blend               (Dock worker 1)
  The *_LightArmor.blend files are the same animations with a light armour vest on the character (to look at): not used.
  Every file has the same skeleton as the dock worker's pistol files (checked bone by bone: under 0.3 degrees apart).

To change an animation
  1. Edit the .blend in Blender (keep the armature and the bone names; the action is what is exported).
  2. Export it with ..\PistolAnim_2026-10-27\export_fbx.py (the armature alone, the right hand's finger bones renamed
     "Thumb_01.001" -> "Thumb_01 1"), for example:
       blender -b Rifle_Fire.blend --python ..\PistolAnim_2026-10-27\export_fbx.py -- Rifle_Fire.fbx ref_rifle.json 1
  3. Copy the FBX over the one in Assets\Resources\Animations (keep the .meta file).
  4. In Unity: Employees Take Revenge > Setup Weapon Animations. If the frames moved (a longer raise, the shot at another
     frame, the blow at another frame), change them in Editor\WeaponAnimationSetup.cs first (and
     WeaponAnimation.SwingContact for the blow).
  5. Check it: qa_regress.ps1 -Methods WeaponAnim:gfx (pictures in %TEMP%\be_shot_weaponanim_*.png).

If a weapon is moved in the hand in Blender, its place in Scripts\Guns\WeaponAnimation.cs (GunHolds / MeleeHolds: the
model's origin and its +Y and +Z axes in the right hand's frame, and its scale against the pack's model) must be measured
again with measure.py at a frame where it is held still (the aim of a gun, the ready of a swing):
  blender -b Rifle_Fire.blend --python measure.py -- 21
It prints "origin_in_hand", "y_in_hand", "z_in_hand" and "world_scale" (the scale the game wants is world_scale / 0.01).
The test NetDevWeaponAnim.cs compares both wrists with "right_wrist_in_weapon" / "left_wrist_in_weapon" from the same output.
timing.py prints every frame's hand positions, the weapon's angle and the speed of its tip, and the keys of the muzzle
flash and the moving parts (the bolt, the pump): where the clip cuts and the bolt / pump timings came from.
inspect_blend.py lists the objects, the actions and their frame ranges of a .blend.
