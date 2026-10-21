# Round 12, batch G47c: the arms shader's forearms are handed in just before a camera culls (Camera.onPreCull: it is raised for every render, a manual Camera.Render too), not in Application.onBeforeRender (which only the engine's own
# frame render raises: the pictures of the QA runs - and any camera rendered by hand - got no arms, so the shader threw the whole body away).
. 'C:\bqs\patchlib.ps1'
Open-Src 'C:\bqm\Assets\Scripts\CameraFollow.cs'
Rep '        void OnEnable() { Application.onBeforeRender += PushArms; Current = this; }' '        void OnEnable() { Camera.onPreCull += PreCullArms; Current = this; }'
Rep '        void OnDisable() { Application.onBeforeRender -= PushArms; if (Current == this) Current = null; }' @"
        void OnDisable() { Camera.onPreCull -= PreCullArms; if (Current == this) Current = null; }
        void PreCullArms(Camera c) => PushArms();
"@
Rep '        /// <summary>Hands the two forearms to the arms shader just before the picture is drawn (after the gun''s arm solver has put them where they are).</summary>' '        /// <summary>Hands the two forearms to the arms shader just before a camera draws (after the gun''s arm solver has put them where they are).</summary>'
Save-Src
