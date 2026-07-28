Shader "Hidden/Custom/Pixelize"
{
    Properties
    {
        _PixelSize ("Pixel Size", Float) = 8
    }
    
    HLSLINCLUDE

    #pragma editor_sync_compilation
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float _PixelSize;

    half4 Fragment(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        float2 uv = input.texcoord;
        
        // Calculate the grid size
        float2 size = _ScreenParams.xy / max(_PixelSize, 1.0);
        
        // Floor the UVs to create blocks
        float2 pixelatedUV = floor(uv * size) / size;
        
        // Sample the source texture using point filtering for sharp edges
        half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, pixelatedUV);
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
