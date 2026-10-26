# Batch A1, second patch (T8): "drop all loot" drops what the pack's LOOT list shows (never a weapon) and lays the pieces out in rings instead of on one spot. Patches the scratch copy C:\bd.
param([switch]$Dry)
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
$A = 'C:\bd\Assets\'

$SC = $A + 'Scripts\Pickup.cs'; Open-Src $SC
Rep 'public bool CountsAsLoot => Loot != null || bankValue > 0;' 'public bool CountsAsLoot => !RangedWeapon.IsWeapon(this) && (Loot != null || bankValue > 0);'
Save-Src

$SC = $A + 'Scripts\Backpack.cs'; Open-Src $SC
Rep ('        public Vector3 DropPosition() => transform.position + transform.forward * 1.3f + Vector3.up * 0.5f' + "`n" + '                                         + Random.insideUnitSphere * 0.15f;') (
  '        public Vector3 DropPosition() => transform.position + transform.forward * 1.3f + Vector3.up * 0.5f' + "`n" + '                                         + Random.insideUnitSphere * 0.15f;' + "`n" + "`n" +
  '        /// <summary>Where the <paramref name="index"/>-th piece of a "drop all" lies, relative to <see cref="DropPosition"/>: the first on it, the rest in rings around it (the owner, 2026-10-23: "Drop all loot is not dropping all loot properly"), so thirty pieces never lie on one spot and shove each other through the floor.</summary>' + "`n" +
  '        public static Vector3 DropSpread(int index)' + "`n" + '        {' + "`n" +
  '            if (index <= 0) return Vector3.zero;' + "`n" +
  '            int ring = (index - 1) / 6, slot = (index - 1) % 6;' + "`n" +
  '            float radius = 0.32f + ring * 0.24f, angle = (slot * 60f + ring * 30f) * Mathf.Deg2Rad;' + "`n" +
  '            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);' + "`n" + '        }')
RepRx 'foreach \(var p in new List<Pickup>\(items\)\) if \(p && p\.CountsAsLoot\) ApplyDropped\(p, DropPosition\(\)\);' (
  'int n = 0;' + "`n" +
  '            foreach (var p in new List<Pickup>(items)) if (p && p.CountsAsLoot) ApplyDropped(p, DropPosition() + DropSpread(n++));')
Save-Src

$SC = $A + 'Scripts\Net\RunSync.Requests.cs'; Open-Src $SC
Rep ('            foreach (var p in bp.Snapshot())' + "`n" + '                if (p && p.CountsAsLoot && p.NetId >= 0) DroppedRpc((ushort)p.NetId, (byte)np.Slot, bp.DropPosition());') (
  '            int n = 0;' + "`n" +
  '            foreach (var p in bp.Snapshot())' + "`n" + '                if (p && p.CountsAsLoot && p.NetId >= 0) DroppedRpc((ushort)p.NetId, (byte)np.Slot, bp.DropPosition() + Backpack.DropSpread(n++));')
Save-Src
"A1b patched"
