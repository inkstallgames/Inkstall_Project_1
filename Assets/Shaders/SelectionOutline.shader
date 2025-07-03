Shader "Custom/SelectionOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Range(0.001, 10)) = 2.0
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        
        // Outline pass - only renders the outline
        Pass
        {
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Front // Cull front faces to only show back faces (outline)
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            
            float4 _OutlineColor;
            float _OutlineWidth;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 pos : SV_POSITION;
            };
            
            v2f vert(appdata v)
            {
                v2f o;
                
                // Expand the vertex along its normal
                float3 normal = normalize(v.normal);
                float3 outlineOffset = normal * (_OutlineWidth * 0.001);
                float4 position = float4(v.vertex.xyz + outlineOffset, v.vertex.w);
                
                o.pos = UnityObjectToClipPos(position);
                
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            
            ENDCG
        }
    }
    
    FallBack "Diffuse"
}
