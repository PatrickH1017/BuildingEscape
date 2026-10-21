# Round 12, batch G45b: the bearded man (Builder Singlet) wears Beard_02 (a moustache that wraps into a chin beard, the cheeks bare) as in the owner's picture, not the full beard Beard_01.
$ErrorActionPreference = 'Stop'
. 'C:\bqs\patchlib.ps1'
. 'C:\bqs\patchlib2.ps1'
if ($env:R2_DRY) { function Save-Src() { Write-Output "dry run: would save $script:path" } }
Open-Src 'C:\bqm\Assets\Editor\CharacterCatalogBuilder.cs'
Rep '["syn_builder_singlet_01"] = new[] { ("SM_Chr_Attach_Helmet_Goggles_01", 2), ("SM_Chr_Attach_Beard_01", 1) },' '["syn_builder_singlet_01"] = new[] { ("SM_Chr_Attach_Helmet_Goggles_01", 2), ("SM_Chr_Attach_Beard_02", 1) },'
Save-Src
Write-Output 'round 12 batch G45b patched'
