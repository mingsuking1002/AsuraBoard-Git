Shader "Marble/SidekickPalette"
{
    Properties { _MainTex("Color palette",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) }
    SubShader { Tags { "RenderType"="Opaque" } Pass {
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        sampler2D _MainTex;float4 _MainTex_TexelSize;fixed4 _Color;
        struct input {float4 vertex:POSITION;float3 normal:NORMAL;float2 uv:TEXCOORD0;};
        struct output {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float shade:TEXCOORD1;};
        output vert(input v){output o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;float3 n=UnityObjectToWorldNormal(v.normal);o.shade=.58+.42*saturate(dot(normalize(n),normalize(float3(-.4,.8,-.6))));return o;}
        fixed4 frag(output i):SV_Target {
            // Palette entries must never be blended with neighboring colors or mip levels.
            float2 uv=(floor(saturate(i.uv)*_MainTex_TexelSize.zw)+.5)*_MainTex_TexelSize.xy;
            return fixed4(tex2Dlod(_MainTex,float4(uv,0,0)).rgb*_Color.rgb*i.shade,1);
        }
        ENDCG
    }}
}
