The dock worker's silenced pistol animation (2026-10-27): from Blender into the game.

What the game uses
  Assets\Resources\Animations\Pistol_OneHand.fbx   raise (frames 6-18), aim (18-26), one shot (26-33)
  Assets\Resources\Animations\Pistol_Reload.fbx    the 2.2 s magazine change (frames 1-66)
  Both hold only the PolygonConstruction skeleton and its animation. Unity reads them as HUMANOID clips, so they play on
  every character a player can pick (DEVELOPER_NOTES.md section 2.66 says how the game uses them).

Where they came from
  Silenced_Pistol_OneHand.blend and Silenced_Pistol_Reload.blend (Blender 5.2, made 2026-10-26 on Dock worker 1 with the
  game's silenced pistol; they were in the owner's Downloads folder, not in the repo).

To change the animation
  1. Edit the .blend in Blender (keep the armature and the bone names; the action is what is exported).
  2. Export it with this script (Blender in the background; the frames listed only go into the JSON of reference numbers):
       blender -b Silenced_Pistol_OneHand.blend --python export_fbx.py -- Pistol_OneHand.fbx ref_onehand.json 1,18,28
       blender -b Silenced_Pistol_Reload.blend  --python export_fbx.py -- Pistol_Reload.fbx  ref_reload.json  1,23,37
     The script exports the armature alone, with the right hand's finger bones renamed from Blender's "Thumb_01.001" to
     Unity's "Thumb_01 1" (the names the Synty characters' avatars use).
  3. Copy the two FBX files over the ones in Assets\Resources\Animations (keep the .meta files).
  4. In Unity: Employees Take Revenge > Setup Weapon Animations (it was "Setup Pistol Animations" until the other weapons'
     files came, see DevTools\WeaponAnim_2026-10-27). It sets the files up as humanoid clips with the Synty
     characters' bone mapping and rest pose, and cuts the clips at the frames in Editor\WeaponAnimationSetup.cs. If the new
     animation's frames moved (a longer raise, a shot at another frame), change the frames there first.
  5. Check it: qa_regress.ps1 -Methods PistolAnim:gfx (pictures in %TEMP%\be_shot_pistolanim_*.png).

If the pistol is moved in the hand in Blender, PistolAnimation.GunOrigin (the gun's place in the hand) must be measured
again: export_fbx.py writes it into the JSON as "gun_in_hand" ("origin"; take it from the reload file, whose pistol is the
game's size, 0.30 m). The magazine in the left hand (HeldGun.MagInHand...) was measured from the reload file the same way.
