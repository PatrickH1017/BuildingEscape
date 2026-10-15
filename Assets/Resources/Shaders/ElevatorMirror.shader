// A flat mirror. It shows the picture that the PlanarMirror script draws from the far side of the glass (the world seen through
// the mirror), lined up with the screen, so it looks like a real reflection. Lives in Resources so a build always includes it.
Shader "BuildingEscape/ElevatorMirror"
{
    Properties
    {
        _MainTex ("Reflection (set by PlanarMirror)", 2D) = "black" {}
        _Tint ("Tint", Color) = (0.90, 0.93, 0.96, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100
        Cull Back
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2Dproj(_MainTex, UNITY_PROJ_COORD(i.screenPos));
                return fixed4(c.rgb * _Tint.rgb, 1.0);
            }
            ENDCG
        }
    }
}
