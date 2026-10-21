// What is seen of the player's own body in first person (the owner, 2026-10-20: "When in first person with a weapon the character is somewhat visible blocking the screen", with a picture of a first-person
// shooter: the gun and a forearm, nothing else): the character's colour atlas, drawn only within a hand's width of the two forearms - everything else is thrown away. The body is one skinned mesh that a build
// cannot cut up (its vertices cannot be read), so the cut is made per pixel. CameraFollow hands the forearms in every frame: _BEArmRA / _BEArmRB are where the right arm's cut begins (a little above the elbow)
// and its fingertips, _BEArmLA / _BEArmLB the left arm's; _BEArmReach is how far from that line the body still shows. The shadow comes from the fallback, so the whole body still casts it.
// Lives in Resources so a build always includes it.
Shader "BuildingEscape/FirstPersonArms"
{
    Properties
    {
        _MainTex ("Colour atlas", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Glossiness ("Smoothness", Range(0, 1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        float4 _BEArmRA, _BEArmRB, _BEArmLA, _BEArmLB;
        float _BEArmReach;
        float4 _BEHead;          // xyz: the middle of the head, w: its radius (0: no head to cut)

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        float SegDist(float3 p, float3 a, float3 b)
        {
            float3 ab = b - a;
            float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 0.0001));
            return length(p - (a + ab * t));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float d = min(SegDist(IN.worldPos, _BEArmRA.xyz, _BEArmRB.xyz), SegDist(IN.worldPos, _BEArmLA.xyz, _BEArmLB.xyz));
            clip(_BEArmReach - d);
            // the head is never drawn, except within a hand's reach of a fingertip (a hand held up to the face stays whole)
            float dHead = length(IN.worldPos - _BEHead.xyz);
            float dTip = min(length(IN.worldPos - _BEArmRB.xyz), length(IN.worldPos - _BEArmLB.xyz));
            clip((dHead >= _BEHead.w || dTip < 0.12) ? 1 : -1);
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
