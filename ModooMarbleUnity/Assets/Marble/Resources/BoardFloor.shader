Shader "Marble/BoardFloor"
{
    Properties { _Color("Tile color",Color)=(1,1,1,1) }
    SubShader { Tags { "RenderType"="Opaque" } Pass {
        ZTest LEqual ZWrite On Cull Back
        CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #include "UnityCG.cginc"
        fixed4 _Color;
        struct input {float4 vertex:POSITION;float3 normal:NORMAL;};
        struct output {float4 vertex:SV_POSITION;float shade:TEXCOORD0;};
        output vert(input v) {
            output o;o.vertex=UnityObjectToClipPos(v.vertex);
            float3 n=UnityObjectToWorldNormal(v.normal);
            o.shade=.57+.43*saturate(dot(normalize(n),normalize(float3(-.35,1,-.5))));return o;
        }
        fixed4 frag(output i):SV_Target {return fixed4(_Color.rgb*i.shade,1);}
        ENDCG
    }}
}
