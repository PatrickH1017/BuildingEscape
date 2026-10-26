Floor 7 doors, before and after (2026-10-23; DEVELOPER_NOTES.md section 2.62).

Every picture has BEFORE on the left (the plain light-blue leaves floor 7 had) and AFTER on the right (the doors of the Synty POLYGON Office pack). 00_overview.png shows the four kinds of door in one sheet.

HOW THEY WERE MADE: in Unity, not outside it. The game's own floor 7 (default seed 20240) was built in a hidden Editor window of a scratch copy of the project and photographed by the Play-mode scenario
NetDevDoorLooks (ui.ps1 -Methods DoorLooks). BEFORE and AFTER are the same program with the pack's doors switched off (BE_DOOR_PACK=0) or on, and the same doors (BE_DOOR_PICKS=6,0,2,12,19,23,4,18).
The lighting is the floor's own night ambient plus one lamp at the camera so that the doors can be judged: the game itself is darker (the ceiling lights only come on near the player).

THE DOORS (numbers are the door's place in DoorManager):
  6   a room's white double door (slides into the wall)           01, 02, 03
  0   a room's white double door that swings (no room to slide)    04, 05
  2   an office's wooden double door (slides)                      06, 07
  12  an office's wooden double door that swings                   08
  19  an office with a glass front: the glass double door          09, 10, 11
  23  the wide 4.8 m hall door, two panels in each leaf             12, 13, 14
  4   a restroom door (the same white door; it never opens)        15
  18  the guard outpost's door: NOT changed (its walls keep their own dark look too)   16

Remake them: see section 2.62 "How to look at it" (scratch copy, ui.ps1 -Methods DoorLooks, environment BE_DOOR_TAG / BE_DOOR_PICKS / BE_DOOR_PACK / BE_DOOR_PICS / BE_DOOR_FLOOR).
