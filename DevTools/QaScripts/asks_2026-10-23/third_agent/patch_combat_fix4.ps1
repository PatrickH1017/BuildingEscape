# The older scenarios that counted bullets by the old numbers (the owner, 2026-10-27: light ammo 25 HP a hit = 2.5 of a guard's ten hits, shotgun shell 75 once per officer): Guns (the shotgun on the dummy), Pew (the guard on duty), Combat (bullets through each shield), Demo (the dummy), and the two animation scenarios, whose guns were always aimed and are now at the hip until the right mouse button is held.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = $Root

# ---- Guns: a shotgun shot is 75 HP once; the pellets are still 8 tracers
Open-Src ($A + 'Editor\NetDev\NetDevGuns.cs')
Rep '                    GunCheck($"{tag} the {spec.Pellets} pellets each take {spec.damage} off the dummy, from {dist} m all {spec.Pellets} land", Mathf.Abs(dmg - hits * spec.damage) < 0.01f && hits == spec.Pellets, $"({hits} of {spec.Pellets} pellets, {dmg} health)");' '                    GunCheck($"{tag} the {spec.Pellets} pellets together take {spec.damage} off the dummy once (the owner, 2026-10-27: shotgun shells 75 damage within range), from {dist} m", Mathf.Approximately(dmg, spec.damage), $"({dmg} health)");'
Save-Src

# ---- Pew: the guard on duty goes down after FOUR bullets (25 HP each = 2.5 hits)
Open-Src ($A + 'Editor\NetDev\NetDevPew.cs')
RepRegion '                int hits0 = victim.Hits;' '                PewCheck("a guard who is down is not a foe any more", !(bool)args[0]);' @'
                float dmg0 = victim.Damage; float bulletHits = RangedWeapon.Specs[(int)RangedWeapon.Kind.Pew].guardHits;
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                bool firedAtVictim = gc.Fire();
                var li = gc.LastImpact;
                yield return null;
                PewCheck("one bullet is 25 HP (2.5 of the ten hits) to a guard and does not knock him over (the owner, 2026-10-27: light ammo 25 a hit)", Mathf.Approximately(victim.Damage, dmg0 + bulletHits) && victim.State != GuardState.Stunned && !victim.Dead,
                    $"(fired {firedAtVictim}, damage {victim.Damage}, {victim.State}; bullet from {gc.LastMuzzle} dir {gc.LastDirection}, hit {(li.hit ? li.collider.name : "nothing")} at {li.distance:0.0} m, guard at {victim.transform.position}, player {player.transform.position})");
                bool second = false, third = false, fourth = false;
                yield return new WaitForSeconds(0.32f);
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                second = gc.Fire();
                yield return null;
                PewCheck("the second bullet hits him again, but he is not down yet (five of ten hits)", second && !victim.Dead && Mathf.Approximately(victim.Damage, dmg0 + 2f * bulletHits), $"(damage {victim.Damage}, dead {victim.Dead})");
                yield return new WaitForSeconds(0.32f);
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                third = gc.Fire();
                yield return null;
                PewCheck("the third bullet leaves him on his feet (seven and a half of ten hits)", third && !victim.Dead && Mathf.Approximately(victim.Damage, dmg0 + 3f * bulletHits), $"(damage {victim.Damage}, dead {victim.Dead})");
                yield return new WaitForSeconds(0.32f);
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                fourth = gc.Fire();
                yield return null;
                PewCheck("the fourth bullet puts him down for good", fourth && victim.Dead, $"(damage {victim.Damage}, dead {victim.Dead})");
                args = new object[] { true };
                AimCamera(cam, player, victim.transform.position + Vector3.up * 0.3f);
                aimPoint.Invoke(gc, args);
                PewCheck("a guard who is down is not a foe any more", !(bool)args[0]);
'@
Save-Src

# ---- Combat: bullets through each shield (a shield takes a share of each 2.5-hit bullet: none 4 bullets, light 6, medium 8, heavy 14)
Open-Src ($A + 'Editor\NetDev\NetDevCombat.cs')
Rep '            var expectBullets = new[] { 3, 4, 5, 9 };' '            var expectBullets = new[] { 4, 6, 8, 14 };                          // (25 HP a bullet = 2.5 of ten hits; the shield takes its share of that: .75 / .5 / .3 - the owner, 2026-10-27)'
Rep 'CombatCheck("an armed guard (pistol, light shield) goes down after four bullets and drops three bullets of pistol ammunition where he fell (the roll of 99)",' 'CombatCheck("an armed guard (pistol, light shield) goes down after six bullets and drops three bullets of pistol ammunition where he fell (the roll of 99)",'
Rep 'victim.Dead && n == 4 && GuardDrops.Dropped == dropped0 + 1' 'victim.Dead && n == 6 && GuardDrops.Dropped == dropped0 + 1'
Rep 'CombatCheck("a bullet does its four hits to a guard, does not knock him over, and he reacts: a patrolling guard stops and radios for backup (he goes after the shooter when the call is through)", a.Hits == pewBullet &&' 'CombatCheck("a bullet does its 2.5 hits (25 HP) to a guard, does not knock him over, and he reacts: a patrolling guard stops and radios for backup (he goes after the shooter when the call is through)", Mathf.Approximately(a.Damage, pewBullet) &&'
Rep '$"(was {before}, now {a.State}, hits {a.Hits})");' '$"(was {before}, now {a.State}, damage {a.Damage})");'
Save-Src

# ---- Demo: the dummy
Open-Src ($A + 'Editor\NetDev\NetDevDemo.cs')
Rep 'DemoCheck("a bullet takes 60 health off the dummy", fired && Mathf.Approximately(h0 - dummy.Health, 60f), $"({h0} -> {dummy.Health})");' 'DemoCheck("a bullet takes 25 health off the dummy (light ammo, the owner, 2026-10-27)", fired && Mathf.Approximately(h0 - dummy.Health, 25f), $"({h0} -> {dummy.Health})");'
Rep '                DemoCheck("two bullets bring it down", dummy.Down || dummy.Health <= 0f, $"(health {dummy.Health})");' @'
                DemoCheck("two bullets leave it standing (50 of 100 health)", !dummy.Down && Mathf.Approximately(dummy.Health, 50f), $"(health {dummy.Health})");
                for (int more = 0; more < 2; more++)
                {
                    yield return new WaitForSeconds(0.35f);
                    AimCamera(Camera.main, player, aimAt);
                    gc.Fire();
                    yield return null;
                }
                DemoCheck("four bullets bring it down", dummy.Down || dummy.Health <= 0f, $"(health {dummy.Health})");
'@
Save-Src
# ---- PistolAnim / WeaponAnim: their guns were always up on the sights; now a gun is at the hip until the right mouse button is held, so these scenarios hold it (the test's own right mouse button)
foreach ($f in 'NetDevPistolAnim.cs', 'NetDevWeaponAnim.cs') {
  Open-Src ($A + 'Editor\NetDev\' + $f)
  Rep '            GunController.TestCursorLocked = true;' @'
            GunController.TestCursorLocked = true;
            GunController.TestAim = true;              // (the right mouse button held: since 2026-10-27 a gun is at the hip without it, and these scenarios look at the aimed pose)
'@
  Save-Src
}
"combat fix 4 patched"
