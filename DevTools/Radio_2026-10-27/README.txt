The dock radio (2026-10-27): DEVELOPER_NOTES.md section 2.71 says how it works.

The model is POLYGON Construction's SM_Prop_Radio_01 (a jobsite radio: two speakers, a green display, a pad of eight
buttons in two columns of four). Scripts\Radio\DockRadio.cs holds where its buttons and its display are, measured here:

  blender -b --python radio_buttons.py -- <path>\SM_Prop_Radio_01.fbx
      lists the faces on the front near the middle: the buttons are the boxes 4.43 x 2.19 cm whose front faces are 0.1699 m
      in front of the middle (x +-0.031 m; up 0.2330, 0.1944, 0.1558, 0.1171 m from the bottom); the display is the face
      13 x 4.5 cm at (0, 0.3614 up, 0.1542 forward), leaning back 15 degrees. (Blender's -Y is the radio's front and Unity's +Z.)
  blender -b --python render_radio.py -- <path>\SM_Prop_Radio_01.fbx <path>\PolygonConstruction_Texture_01_A.png <out prefix>
      pictures of it from four sides.

The music: Resources\RadioCatalog.asset, made by Employees Take Revenge > Setup Dock Radio (Editor\RadioSetup.cs) from
  Assets\Midnight Mode vol.1\FULL (10 tracks), Assets\Energy Hard Rock Pack DEMO (10), Assets\Urban Jazz\WAV (the two STANDARD
  versions). Add or remove tracks by putting them in those folders (or changing RadioSetup.Stations) and running the menu again.
