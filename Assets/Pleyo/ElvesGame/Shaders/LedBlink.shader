// Blinking garland bulbs (URP, unlit): every other bulb is lit, then the other half, in turn.
// Which half a bulb belongs to is in the mesh's vertex colour (red channel 0 or 1), written by the menu
// Elves Workshop/Make Blinking Garlands In Open Scene.
Shader "Elves/LedBlink"
{
    Properties
    {
        [HDR] _OnColor ("Lit bulb", Color) = (3, 2.3, 0.6, 1)
        _OffColor ("Off bulb", Color) = (0.03, 0.02, 0.01, 1)
        _Rate ("Switches per second", Float) = 1.5
        [Toggle] _Smooth ("Smooth fade", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OnColor;
                half4 _OffColor;
                float _Rate;
                float _Smooth;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half parity : TEXCOORD0; };

            Varyings vert (Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.parity = round(input.color.r);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _Rate;
                // 0 / 1 switching, or a soft fade between the two halves.
                half phase = _Smooth > 0.5 ? 0.5 + 0.5 * sin(t * 3.14159265) : fmod(floor(t), 2.0);
                half lit = abs(i.parity - phase);
                return lerp(_OffColor, _OnColor, lit);
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            float4 vert (Attributes input) : SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
