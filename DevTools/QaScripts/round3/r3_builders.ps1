# Hardens the four catalog builders: a catalog that EXISTS but does not load (its script is not imported yet, e.g. right after a recompile in a fresh Editor) is left alone;
# before, the builder made a blank one and overwrote the file (the assets lost their script reference: "No script asset for AudioCatalog").
param([string]$Tree = 'C:\Users\phick\AppData\Local\Temp\claude\C--Users-phick-OneDrive-Desktop-BuildingEscape\e7511809-da6a-425a-b8c3-65a911ea9dca\scratchpad\BE_dev')
. "$PSScriptRoot\patchlib.ps1"
$note = '// (a catalog that exists but does not load: its script is not imported yet - rebuilding now would overwrite it with a blank one)'

Open-Src "$Tree\Assets\Editor\AudioCatalogBuilder.cs"
if (-not $s.Contains('does not load')) {
  Rep '                if (!cat) Build(false);
                else if (cat.gunShot == null' "                if (!cat) { if (!System.IO.File.Exists(CatalogPath)) Build(false); }      $note
                else if (cat.gunShot == null"
  Save-Src
}
Open-Src "$Tree\Assets\Editor\WeaponCatalogBuilder.cs"
if (-not $s.Contains('does not load')) {
  Rep '                if (!cat) Build(false);
                else if (!cat.pew' "                if (!cat) { if (!System.IO.File.Exists(CatalogPath)) Build(false); }      $note
                else if (!cat.pew"
  Save-Src
}
Open-Src "$Tree\Assets\Editor\OfficeCatalogBuilder.cs"
if (-not $s.Contains('does not load')) {
  Rep '                if (NeedsBuild(cat)) Build(false);' "                if (!cat && System.IO.File.Exists(AssetPath)) return;      $note
                if (NeedsBuild(cat)) Build(false);"
  Save-Src
}
Open-Src "$Tree\Assets\Editor\CharacterCatalogBuilder.cs"
if (-not $s.Contains('does not load')) {
  Rep '                if (!cat || cat.characters.Length == 0' "                if (!cat && System.IO.File.Exists(AssetPath)) return;      $note
                if (!cat || cat.characters.Length == 0"
  Save-Src
}
