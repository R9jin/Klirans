Shader "Hidden/Custom/Pixelize"
{
    Properties
    {
        _PixelSize ("Pixel Size", Float) = 8
        _ColorBleed ("Color Bleed", Float) = 0.005
        _ScanlineIntensity ("Scanline Intensity", Float) = 0.15
        _NoiseIntensity ("Noise Intensity", Float) = 0.1
        _VignetteIntensity ("Vignette Intensity", Float) = 0.8
        _VignetteSmoothness ("Vignette Smoothness", Float) = 0.5
        _DirtIntensity ("Dirt Intensity", Float) = 0.2
    }
    
    HLSLINCLUDE

    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float _PixelSize;
    float _ColorBleed;
    float _ScanlineIntensity;
    float _NoiseIntensity;
    float _VignetteIntensity;
    float _VignetteSmoothness;
    float _DirtIntensity;

    float rand(float2 n) 
    { 
        return frac(sin(dot(n, float2(12.9898, 78.233))) * 43758.5453);
    }

    half4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float2 uv = input.texcoord;
        
        // VHS Tape Tracking/Jitter effect
        float trackingOffset = step(0.98, sin(uv.y * 15.0 + _Time.y * 10.0)) * 0.01 * sin(_Time.y * 50.0);
        uv.x += trackingOffset;

        // Calculate the grid size for pixelation
        float2 size = _ScreenParams.xy / max(_PixelSize, 1.0);
        
        // Floor the UVs to create blocks
        float2 pixelatedUV = floor(uv * size) / size;
        
        // Chromatic Aberration (Color Bleeding)
        float2 redShift = float2(_ColorBleed, 0.0);
        float2 blueShift = float2(-_ColorBleed, 0.0);
        
        half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV + redShift).r;
        half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV).g;
        half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV + blueShift).b;
        
        half4 color = half4(r, g, b, 1.0);

        // Scanlines
        float scanline = sin(uv.y * _ScreenParams.y * 1.5) * 0.5 + 0.5;
        color.rgb *= lerp(1.0, scanline, _ScanlineIntensity);

        // Film Grain / Static Noise
        float noise = rand(pixelatedUV + _Time.y);
        color.rgb += (noise - 0.5) * _NoiseIntensity;
        
        // Vignette (Darkens the edges)
        float2 center = uv - 0.5;
        float dist = length(center);
        float vignette = smoothstep(0.8, 0.8 - _VignetteSmoothness, dist);
        color.rgb *= lerp(1.0, vignette, _VignetteIntensity);

        // ARG Dirty Screen Effect (Procedural Grime focused on the edges)
        float dirtNoise = rand(floor(uv * 40.0)) * rand(floor(uv * 15.0));
        float dirtMask = smoothstep(0.4, 0.8, dist); // Only dirty the edges
        color.rgb -= dirtNoise * dirtMask * _DirtIntensity;
        
        return color;
    }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off ZTest Always
        
        Pass
        {
            Name "Pixelize"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            ENDHLSL
        }
    }
}
