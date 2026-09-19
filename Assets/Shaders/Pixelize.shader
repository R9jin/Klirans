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

    float hash21(float2 p) 
    { 
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    // Smooth value noise using cubic Hermite interpolation - smooth continuous gradients, zero pixelation
    float smoothNoise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        float2 u = f * f * (3.0 - 2.0 * f);

        float a = hash21(i);
        float b = hash21(i + float2(1.0, 0.0));
        float c = hash21(i + float2(0.0, 1.0));
        float d = hash21(i + float2(1.0, 1.0));

        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    // Multi-octave FBM for rich, organic lens smudges and grease stains
    float fbmGrime(float2 p)
    {
        float val = 0.0;
        float amp = 0.5;
        val += amp * smoothNoise(p); p = p * 2.13 + float2(1.7, 9.2); amp *= 0.5;
        val += amp * smoothNoise(p); p = p * 2.37 + float2(8.3, 2.8); amp *= 0.5;
        val += amp * smoothNoise(p);
        return val;
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

        // Smooth Asymmetrical Horror Vignette (Edge Falloff)
        // Uses input.texcoord directly (not pixelatedUV) to eliminate chunky pixelation on the vignette
        float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
        float2 rawUV = input.texcoord;
        
        // Asymmetrical center offset (imbalanced organic framing)
        float2 vigCenter = rawUV - float2(0.485, 0.525);
        vigCenter.x *= aspect;

        // Non-uniform corner weighting: top-left & top-right creep in more organically
        float asymmetry = 1.0 + 0.28 * (rawUV.y - 0.5) + 0.18 * (0.5 - rawUV.x) * (rawUV.y - 0.5);
        float vigDist = length(vigCenter) * asymmetry;

        // Keep the central vision open and clear
        float vigOuter = 1.18;
        float vigInner = max(0.2, vigOuter - _VignetteSmoothness);
        float vignette = smoothstep(vigOuter, vigInner, vigDist);
        color.rgb *= lerp(1.0, vignette, _VignetteIntensity);

        // --- Gritty Analog Lens Grime & Smudges (Continuous, Smooth & Atmospheric) ---
        // Aspect-corrected UV for grime to keep smudges natural (not stretched on widescreen)
        float2 grimeUV = float2(rawUV.x * aspect, rawUV.y);

        // Layer 1: Broad organic lens grease & dirty smudges
        float greaseNoise = fbmGrime(grimeUV * 5.5 + float2(0.23, 0.71));
        float smudges = smoothstep(0.32, 0.70, greaseNoise);

        // Layer 2: Mottled surface grime & dirt accumulation
        float mottledNoise = smoothNoise(grimeUV * 18.0 + float2(3.14, 1.59));
        float mottling = smoothstep(0.38, 0.80, mottledNoise);

        // Layer 3: Organic film dust specks (smooth sub-pixel flecks, not pixelated blocks)
        float dustNoise = smoothNoise(grimeUV * 65.0 + float2(8.21, 5.73));
        float dustFlecks = pow(dustNoise, 6.0) * 6.0;

        // Asymmetric grime distribution:
        // Visible across the glass (preventing sterile/clean look), intensifying heavily towards borders/corners
        float grimeSpread = lerp(0.32, 1.0, smoothstep(0.22, 1.0, vigDist));
        float totalGrime = (smudges * 0.62 + mottling * 0.38 + dustFlecks * 0.45) * grimeSpread;

        // Darken and impart murky analog residue tone
        half3 grimeTone = half3(0.06, 0.05, 0.04);
        color.rgb = lerp(color.rgb, color.rgb * (1.0 - totalGrime * 0.85), _DirtIntensity);
        color.rgb = lerp(color.rgb, grimeTone, totalGrime * _DirtIntensity * 0.35);

        // Subtle analog video noise/grain
        float grain = (hash21(rawUV * 600.0 + frac(_Time.y * 11.37)) - 0.5) * _NoiseIntensity;
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
