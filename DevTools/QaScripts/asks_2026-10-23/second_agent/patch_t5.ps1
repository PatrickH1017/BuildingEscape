# T5 (the owner, 2026-10-23: "There should be a walkie talkie that the Vendor sells and the player should have to hold out the walkie talkie to use it, please use the SPAWNCAMPGAMES asset"). Patches the scratch copy C:\bd;
# exact matches only (patchlib), line endings kept; new files are written with CRLF. After this patch run WeaponCatalogBuilder.WalkieBatch once (run_method_bd.ps1) so the catalog asset points at the prefab.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
function Put-Crlf([string]$from, [string]$to) {
  $t = [IO.File]::ReadAllText($from).Replace("`r`n", "`n").Replace("`n", "`r`n")
  if ($Dry) { Write-Output "dry ok (would write) $to"; return }
  [IO.File]::WriteAllText($to, $t, (New-Object Text.UTF8Encoding($false)))
  Write-Output "wrote $to"
}
New-Item -ItemType Directory -Force -Path ($A + 'Scripts\Walkie') | Out-Null
Put-Crlf (Join-Path $here 't5_new\WalkieRules.cs') ($A + 'Scripts\Walkie\WalkieRules.cs')
Put-Crlf (Join-Path $here 't5_new\WalkieTalkie.cs') ($A + 'Scripts\Walkie\WalkieTalkie.cs')
Put-Crlf (Join-Path $here 't5_new\HeldWalkie.cs') ($A + 'Scripts\Walkie\HeldWalkie.cs')
Put-Crlf (Join-Path $here 't5_new\NetDevAsks1023GTest.cs') ($A + 'Editor\NetDev\NetDevAsks1023GTest.cs')
Put-Crlf (Join-Path $here 't5_new\NetDevAsks1023G.cs') ($A + 'Editor\NetDev\NetDevAsks1023G.cs')
Put-Crlf (Join-Path $here 't5_new\NetDevWalkieLook.cs') ($A + 'Editor\NetDev\NetDevWalkieLook.cs')

# ---- the catalog knows the model
$SC = $A + 'Scripts\WeaponCatalog.cs'; Open-Src $SC
Rep '        /// <summary>The model of a piece of this tier lying about, or null when the catalog has none (then the boxes are used).</summary>' @'
        [Header("The walkie talkie (the owner, 2026-10-23: the SpawnCampGames model)")]
        [Tooltip("The walkie talkie the Vendor sells and the player holds out to use the radio (SpawnCampGames: Assets/SpawnCampGames/WalkieTalkie/Prefabs/Walkie.prefab). Empty = a dark box.")]
        public GameObject walkie;

        /// <summary>The model of a piece of this tier lying about, or null when the catalog has none (then the boxes are used).</summary>
'@.TrimEnd()
Save-Src

$SC = $A + 'Editor\WeaponCatalogBuilder.cs'; Open-Src $SC
Rep '        /// <summary>The pew and its cartridge from the Guns pack (only what is missing is filled).</summary>' @'
        const string WalkiePath = "Assets/SpawnCampGames/WalkieTalkie/Prefabs/Walkie.prefab";

        /// <summary>The SpawnCampGames walkie talkie (the owner, 2026-10-23: "please use the SPAWNCAMPGAMES asset"): only filled when it is missing, so a model chosen by hand stays.</summary>
        static void FillWalkie(WeaponCatalog cat, bool log)
        {
            if (!cat.walkie) cat.walkie = Load<GameObject>(WalkiePath);
            EditorUtility.SetDirty(cat);
            AssetDatabase.SaveAssets();
            if (log) Debug.Log(cat.walkie ? "Walkie talkie set in the weapon catalog (SpawnCampGames)." : "The SpawnCampGames walkie talkie was not found at " + WalkiePath + ": the Vendor's walkie talkie is a dark box.");
        }

        /// <summary>For the QA runs (<c>-executeMethod</c>): puts the walkie talkie into the catalog now, the same as the first editor start does, and exits.</summary>
        public static void WalkieBatch()
        {
            try
            {
                var cat = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
                if (!cat) { Debug.LogError("[Weapons] no catalog"); return; }
                FillWalkie(cat, true);
            }
            finally { EditorApplication.Exit(0); }
        }

        /// <summary>The pew and its cartridge from the Guns pack (only what is missing is filled).</summary>
'@.TrimEnd()
Rep '                    if (!ArmourFilled(cat)) FillArmour(cat, true);' ('                    if (!ArmourFilled(cat)) FillArmour(cat, true);' + "`n" + '                    if (!cat.walkie) FillWalkie(cat, true);')
Rep '            FillArmour(cat, false);' ('            FillArmour(cat, false);' + "`n" + '            FillWalkie(cat, false);')
Save-Src

# ---- the Vendor sells it
$SC = $A + 'Scripts\Vendor.cs'; Open-Src $SC
Rep '            public ShieldTier armour;' @'
            public ShieldTier armour;
            /// <summary>The walkie talkie (the owner, 2026-10-23): an item that is held out to use the radio (<see cref="WalkieTalkie"/>).</summary>
            public bool isWalkie;
'@.TrimEnd()
Rep '            new Product { name = "Heavy Armour", blurb = "armour: a guard''s bullet knocks you down for 30% of the time", price = ArmourRules.Price(ShieldTier.Heavy), isArmour = true, armour = ShieldTier.Heavy },' @'
            new Product { name = "Heavy Armour", blurb = "armour: a guard's bullet knocks you down for 30% of the time", price = ArmourRules.Price(ShieldTier.Heavy), isArmour = true, armour = ShieldTier.Heavy },
            // the walkie talkie (the owner, 2026-10-23): always on offer, the last entry; the price and the weight are placeholders (WalkieRules), the blurb a plain description for the owner to reword
            new Product { name = WalkieRules.Name, blurb = "radio: hold it out and talk to a teammate who holds one out too, at any distance", price = WalkieRules.Price, isWalkie = true },
'@.TrimEnd()
Rep '        public const int PewIndex = 7, AmmoIndex = 8;' @'
        public const int PewIndex = 7, AmmoIndex = 8;
        /// <summary>The walkie talkie's place in <see cref="Catalog"/> (the last entry).</summary>
        public static int WalkieIndex { get { for (int i = 0; i < Catalog.Length; i++) if (Catalog[i].isWalkie) return i; return -1; } }
'@.TrimEnd()
Rep 'static float ItemWeight(Product p) => p.isArmour ?' 'static float ItemWeight(Product p) => p.isWalkie ? WalkieRules.Weight : p.isArmour ?'
Rep '            if (p.isArmour) return ArmourItem.Create(p.armour, pos, transform.parent, yaw);' ('            if (p.isWalkie) return WalkieTalkie.Create(pos, transform.parent, yaw);' + "`n" + '            if (p.isArmour) return ArmourItem.Create(p.armour, pos, transform.parent, yaw);')
Save-Src

# ---- it is equipped like a weapon, but never by itself
$SC = $A + 'Scripts\Guns\RangedWeapon.cs'; Open-Src $SC
Rep '        /// <summary>True for any item that is a weapon (the backpack equips these).</summary>' '        /// <summary>True for any item that is a weapon (the backpack equips these) - and for the walkie talkie, which is equipped the same way (the owner, 2026-10-23: the player holds it out to use it).</summary>'
Rep '        public static bool IsWeapon(Pickup p) => p && (p.TryGetComponent(out MeleeWeapon _) || p.TryGetComponent(out RangedWeapon _));' '        public static bool IsWeapon(Pickup p) => p && (p.TryGetComponent(out MeleeWeapon _) || p.TryGetComponent(out RangedWeapon _) || p.TryGetComponent(out WalkieTalkie _));'
Save-Src
$SC = $A + 'Scripts\Backpack.cs'; Open-Src $SC
Rep '            if (!Equipped && RangedWeapon.IsWeapon(p)) Equipped = p;' '            if (!Equipped && RangedWeapon.IsWeapon(p) && !WalkieTalkie.IsWalkie(p)) Equipped = p;          // (a walkie talkie is held out by choice, never by itself)'
Save-Src

# ---- every player shows the radio in his hand
$SC = $A + 'Scripts\WobblyPlayer.cs'; Open-Src $SC
Rep '            if (!GetComponent<PlayerArmourVisual>()) gameObject.AddComponent<PlayerArmourVisual>();          // (the vest of the armour he wears: every machine reads his pack)' ('            if (!GetComponent<PlayerArmourVisual>()) gameObject.AddComponent<PlayerArmourVisual>();          // (the vest of the armour he wears: every machine reads his pack)' + "`n" + '            if (!GetComponent<HeldWalkie>()) gameObject.AddComponent<HeldWalkie>();                              // (the walkie talkie he holds out: the same)')
Save-Src

# ---- the scenario and the pure test are registered
$SC = $A + 'Editor\NetDev\NetDevHarness.cs'; Open-Src $SC
Rep '        public static void Asks1023D()' ('        public static void Asks1023G()' + "`n" + '        {' + "`n" + '            SessionState.SetString("netdev.mode", "asks1023g");' + "`n" + '            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");' + "`n" + '            EditorApplication.isPlaying = true;' + "`n" + '        }' + "`n" + '        public static void WalkieLook()' + "`n" + '        {' + "`n" + '            SessionState.SetString("netdev.mode", "walkielook");' + "`n" + '            EditorSceneManager.OpenScene("Assets/Scenes/Greybox.unity");' + "`n" + '            EditorApplication.isPlaying = true;' + "`n" + '        }' + "`n" + '        public static void Asks1023D()')
Rep '&& mode != "asks1023f")' '&& mode != "asks1023f" && mode != "asks1023g" && mode != "walkielook")'
Rep '                case "asks1023f": yield return Asks1023FScenario(); break;' ('                case "asks1023f": yield return Asks1023FScenario(); break;' + "`n" + '                case "asks1023g": yield return Asks1023GScenario(); break;' + "`n" + '                case "walkielook": yield return WalkieLookScenario(); break;')
Save-Src
$SC = $A + 'Editor\NetDev\NetDevTest.cs'; Open-Src $SC
Rep '            Asks1023FTest();' ('            Asks1023FTest();' + "`n" + '            Asks1023GTest();')
Save-Src
"T5 patched"
