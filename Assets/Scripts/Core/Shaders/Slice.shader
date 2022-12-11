Shader "Custom/Slice"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0

	_BumpScale("Scale", Float) = 1.0
	_BumpMap("Normal Map", 2D) = "bump" {}

	_OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
	_OcclusionMap("Occlusion", 2D) = "white" {}

        sliceNormal("normal", Vector) = (0,0,0,0)
        sliceCentre ("centre", Vector) = (0,0,0,0)
        sliceOffsetDst("offset", Float) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "IgnoreProjector" = "True"  "RenderType"="Geometry" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard addshadow
        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _MainTex;
	sampler2D _BumpMap;
	sampler2D _OcclusionMap;

        struct Input
        {
            float2 uv_MainTex;
            float3 worldPos;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;

        // World space normal of slice, anything along this direction from centre will be invisible
        float3 sliceNormal;
        // World space centre of slice
        float3 sliceCentre;
        // Increasing makes more of the mesh visible, decreasing makes less of the mesh visible
        float sliceOffsetDst;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 adjustedCentre = sliceCentre + sliceNormal * sliceOffsetDst;
            float3 offsetToSliceCentre = adjustedCentre - IN.worldPos;
            clip (dot(offsetToSliceCentre, sliceNormal));
            
            // Albedo comes from a texture tinted by color
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
	    o.Normal = UnpackNormal (tex2D (_BumpMap, IN.uv_MainTex));
	    // o.Normal = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
	    o.Occlusion = tex2D(_OcclusionMap, IN.uv_MainTex).r;

            // Metallic and smoothness come from slider variables
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "VertexLit"
}
