using UnityEngine;

namespace Klirans.Environment
{
    [ExecuteAlways]
    public class OutsideWorldController : MonoBehaviour
    {
        [Header("Backdrop Renderers (Individual Window Quads)")]
        [SerializeField] private Renderer frontBackdrop;
        [SerializeField] private Renderer backBackdrop;
        [SerializeField] private Renderer[] frontBackdrops;
        [SerializeField] private Renderer[] backBackdrops;

        [Header("Textures")]
        [SerializeField] private Texture frontTexture;
        [SerializeField] private Texture backTexture;

        [Header("Tiling & Base Tint")]
        [SerializeField] private Vector2 frontTiling = new Vector2(1f, 1f);
        [SerializeField] private Vector2 frontOffset = Vector2.zero;
        [SerializeField] private Color frontBaseColor = new Color(0.92f, 0.92f, 0.96f, 1f);

        [SerializeField] private Vector2 backTiling = new Vector2(1f, 1f);
        [SerializeField] private Vector2 backOffset = Vector2.zero;
        [SerializeField] private Color backBaseColor = new Color(0.85f, 0.88f, 0.95f, 1f);

        [Header("Dynamic Parallax")]
        [Tooltip("Shifts backdrop texture based on player camera position to simulate realistic distance.")]
        [SerializeField] private bool enableParallax = true;
        [Range(0.001f, 0.05f)]
        [SerializeField] private float parallaxStrength = 0.015f;

        [Header("Dynamic Night Shimmer / Light Breathing")]
        [Tooltip("Subtly flickers and breathes distant lights in the neighborhood.")]
        [SerializeField] private bool enableNightShimmer = true;
        [Range(0.1f, 5f)]
        [SerializeField] private float shimmerSpeed = 1.2f;
        [Range(0.01f, 0.2f)]
        [SerializeField] private float shimmerAmount = 0.04f;

        private MaterialPropertyBlock frontPropertyBlock;
        private MaterialPropertyBlock backPropertyBlock;
        private Transform playerCameraTransform;

        private void OnEnable()
        {
            InitializePropertyBlocks();
            ApplySettings();
        }

        private void Start()
        {
            FindPlayerCamera();
            ApplySettings();
        }

        private void InitializePropertyBlocks()
        {
            if (frontPropertyBlock == null) frontPropertyBlock = new MaterialPropertyBlock();
            if (backPropertyBlock == null) backPropertyBlock = new MaterialPropertyBlock();
        }

        private void FindPlayerCamera()
        {
            if (Camera.main != null)
            {
                playerCameraTransform = Camera.main.transform;
            }
            else
            {
                var cam = FindAnyObjectByType<Camera>();
                if (cam != null) playerCameraTransform = cam.transform;
            }
        }

        private void LateUpdate()
        {
            if (enableParallax || enableNightShimmer)
            {
                UpdateDynamicEffects();
            }
        }

        private void UpdateDynamicEffects()
        {
            if (playerCameraTransform == null && Application.isPlaying)
            {
                FindPlayerCamera();
            }

            // Calculate subtle shimmer
            float shimmer = 0f;
            if (enableNightShimmer)
            {
                float time = Application.isPlaying ? Time.time : 0f;
                shimmer = (Mathf.Sin(time * shimmerSpeed) * 0.5f + Mathf.PerlinNoise(time * shimmerSpeed * 0.7f, 0f) * 0.5f) * shimmerAmount;
            }

            // Calculate parallax offsets based on camera position relative to center of building (Z=16.5)
            float camZOffset = 0f;
            float camYOffset = 0f;
            if (enableParallax && playerCameraTransform != null)
            {
                camZOffset = (playerCameraTransform.position.z - 16.5f) * parallaxStrength * 0.05f;
                camYOffset = (playerCameraTransform.position.y - 11f) * parallaxStrength * 0.05f;
            }

            // Apply to Front (West windows)
            ApplyDynamicToRenderers(frontBackdrops, frontBackdrop, frontPropertyBlock, frontTexture, frontBaseColor, shimmer, frontTiling, frontOffset.x + camZOffset, frontOffset.y + camYOffset);

            // Apply to Back (East windows)
            ApplyDynamicToRenderers(backBackdrops, backBackdrop, backPropertyBlock, backTexture, backBaseColor, shimmer * 0.8f, backTiling, backOffset.x - camZOffset, backOffset.y + camYOffset);
        }

        private void ApplyDynamicToRenderers(Renderer[] rends, Renderer fallbackRend, MaterialPropertyBlock pb, Texture tex, Color baseCol, float shimmerVal, Vector2 tiling, float offsetX, float offsetY)
        {
            if (pb == null) pb = new MaterialPropertyBlock();

            if (tex != null)
            {
                pb.SetTexture("_BaseMap", tex);
                pb.SetTexture("_MainTex", tex);
            }

            Color col = baseCol * (1f + shimmerVal);
            pb.SetColor("_BaseColor", col);
            pb.SetColor("_Color", col);

            Vector4 scaleOffset = new Vector4(tiling.x, tiling.y, offsetX, offsetY);
            pb.SetVector("_BaseMap_ST", scaleOffset);
            pb.SetVector("_MainTex_ST", scaleOffset);

            if (rends != null && rends.Length > 0)
            {
                foreach (var r in rends)
                {
                    if (r != null) r.SetPropertyBlock(pb);
                }
            }
            else if (fallbackRend != null)
            {
                fallbackRend.SetPropertyBlock(pb);
            }
        }

        [ContextMenu("Apply Settings")]
        public void ApplySettings()
        {
            InitializePropertyBlocks();

            ApplyStaticToRenderers(frontBackdrops, frontBackdrop, frontPropertyBlock, frontTexture, frontBaseColor, frontTiling, frontOffset);
            ApplyStaticToRenderers(backBackdrops, backBackdrop, backPropertyBlock, backTexture, backBaseColor, backTiling, backOffset);
        }

        private void ApplyStaticToRenderers(Renderer[] rends, Renderer fallbackRend, MaterialPropertyBlock pb, Texture tex, Color baseCol, Vector2 tiling, Vector2 offset)
        {
            if (pb == null) pb = new MaterialPropertyBlock();

            if (tex != null)
            {
                pb.SetTexture("_BaseMap", tex);
                pb.SetTexture("_MainTex", tex);
            }

            pb.SetColor("_BaseColor", baseCol);
            pb.SetColor("_Color", baseCol);

            Vector4 st = new Vector4(tiling.x, tiling.y, offset.x, offset.y);
            pb.SetVector("_BaseMap_ST", st);
            pb.SetVector("_MainTex_ST", st);

            if (rends != null && rends.Length > 0)
            {
                foreach (var r in rends)
                {
                    if (r != null) r.SetPropertyBlock(pb);
                }
            }
            else if (fallbackRend != null)
            {
                fallbackRend.SetPropertyBlock(pb);
            }
        }
    }
}
