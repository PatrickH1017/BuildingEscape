# T7: R7 wants a few cardboard boxes in the PVP room (8 to 30: the owner, 2026-10-20, "more shelves with boxes"); the battlefield had none in the hall. One block of boxes on its pallet (2 x 2 x 2) stands in the north-west corner now, where the wire spool was.
param([switch]$Dry, [string]$Root = 'C:\bd\Assets\')
$ErrorActionPreference = 'Stop'
. C:\bqs\patchlib.ps1
. C:\bqs\patchlib2.ps1
if ($Dry) { function Save-Src() { Write-Output "dry ok $script:path" } }
Open-Src ($Root + 'Scripts\PvpBattlefieldRules.cs')
Rep '            // north: two concrete block walls with a gap of about a metre in the middle, a wire spool in the north-west corner' '            // north: two concrete block walls with a gap of about a metre in the middle (the north-west corner holds a block of cardboard boxes on a pallet, see BoxBlockX / BoxBlockZ)'
Rep '            new Piece("wire_spool", 2.30f, 22.70f),' '            // (the block of boxes at BoxBlockX / BoxBlockZ: not a Piece, it is the lobby''s own cardboard)'
Rep '        public const float LaneX0 = 5.6f, LaneX1 = 9.3f, LaneZ0 = 14.4f, LaneZ1 = 16.8f;' @'
        public const float LaneX0 = 5.6f, LaneX1 = 9.3f, LaneZ0 = 14.4f, LaneZ1 = 16.8f;
        /// <summary>Where the block of cardboard boxes on a pallet stands (north-west corner; R7 wants a few boxes in the room).</summary>
        public const float BoxBlockX = 1.95f, BoxBlockZ = 22.7f;
'@
Save-Src
Open-Src ($Root + 'Scripts\FloorGenerator.DockRooms.cs')
Rep '            foreach (var p in PvpBattlefieldRules.Pieces) PlaceKey(p.key, p.x, p.z, p.yaw, parent, 0f, true);' @'
            foreach (var p in PvpBattlefieldRules.Pieces) PlaceKey(p.key, p.x, p.z, p.yaw, parent, 0f, true);
            BoxBlock(parent, PvpBattlefieldRules.BoxBlockX, PvpBattlefieldRules.BoxBlockZ, 0f, 2, 2, 2, true);          // (a few cardboard boxes stay: the owner, 2026-10-20, "more shelves with boxes")
'@
Save-Src
"boxes"
