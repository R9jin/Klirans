Shader "Hidden/Custom/Pixelize"
{
    Properties
    {
        _PixelSize ("Pixel Size", Float) = 2
        _ColorBleed ("Color Bleed", Float) = 0.002
        _ScanlineIntensity ("Scanline Intensity", Float) = 0.3
        _NoiseIntensity ("Noise Intensity", Float) = 0.025
        _VignetteIntensity ("Vignette Intensity", Float) = 0.55
        _VignetteSmoothness ("Vignette Smoothness", Float) = 0.45
        _DirtIntensity ("Dirt Intensity", Float) = 0.35
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

    // High-performance single-instruction pseudo-random hash (replaces 21 heavy multi-octave noise hashes)
    inline float fastHash(float2 p) 
    { 
        return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
    }

    half4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float2 rawUV = input.texcoord;
        float2 uv = rawUV;
        
        // 1. VHS Tape Tracking / Jitter effect (fast single-frequency jump)
        float trackingOffset = step(0.985, sin(uv.y * 14.0 + _Time.y * 8.0)) * 0.008 * sin(_Time.y * 45.0);
        uv.x += trackingOffset;

        // 2. High-performance pixelation grid
        float2 size = _ScreenParams.xy / max(_PixelSize, 1.0);
        float2 pixelatedUV = floor(uv * size) / size;
        
        // 3. Chromatic Aberration (Color Bleeding) - 3 taps clamped
        float2 redShift = float2(_ColorBleed, 0.0);
        float2 blueShift = float2(-_ColorBleed, 0.0);
        
        half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV + redShift).r;
        half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV).g;
        half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV + blueShift).b;
        
        half4 color = half4(r, g, b, 1.0);

        // 4. CRT / VHS Scanlines (lightweight sine wave)
        float scanline = sin(uv.y * _ScreenParams.y * 1.5) * 0.5 + 0.5;
        color.rgb *= lerp(1.0, scanline, _ScanlineIntensity);

        // 5. Asymmetrical Horror Vignette (Edge Falloff)
        float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
        float2 vigCenter = rawUV - float2(0.485, 0.525);
        vigCenter.x *= aspect;

        float asymmetry = 1.0 + 0.28 * (rawUV.y - 0.5) + 0.18 * (0.5 - rawUV.x) * (rawUV.y - 0.5);
        float vigDist = length(vigCenter) * asymmetry;

        float vigOuter = 1.18;
        float vigInner = max(0.2, vigOuter - _VignetteSmoothness);
        float vignette = smoothstep(vigOuter, vigInner, vigDist);
        color.rgb *= lerp(1.0, vignette, _VignetteIntensity);

        // 6. Ultra-Fast Organic Lens Grime & Analog Mottling
        // Vectorized harmonic sinusoids give rich, organic smudges without per-pixel loops or hash thrashing
        float2 grimeUV = float2(rawUV.x * aspect, rawUV.y) * 4.0;
        float smudgePattern = sin(grimeUV.x * 1.4 + sin(grimeUV.y * 2.1)) * cos(grimeUV.y * 1.6 + sin(grimeUV.x * 1.8)) * 0.5 + 0.5;
        float smudges = smoothstep(0.32, 0.72, smudgePattern);

        // Single PRNG sample evaluated once per pixel for grain + dust specks
        float seed = frac(_Time.y * 0.29);
        float prng = fastHash(rawUV * 460.0 + seed);

        // Rare micro dust specks
        float dustFlecks = step(0.993, prng) * (prng - 0.993) * 140.0;

        // Grime intensifies smoothly towards edges and corners
        float grimeSpread = lerp(0.25, 1.0, smoothstep(0.25, 0.95, vigDist));
        float totalGrime = (smudges * 0.7 + dustFlecks * 0.5) * grimeSpread;

        // Murky analog tint & edge darkening
        half3 grimeTone = half3(0.06, 0.05, 0.04);
        color.rgb = lerp(color.rgb, color.rgb * (1.0 - totalGrime * 0.85), _DirtIntensity);
        color.rgb = lerp(color.rgb, grimeTone, totalGrime * _DirtIntensity * 0.35);

        // Analog video tape grain
        float grain = (prng - 0.5) * _NoiseIntensity;
        color.rgb += grain;
        
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
