using UnityEngine;

namespace Klirans.Environment
{
    [ExecuteAlways]
    public class OutsideWorldController : MonoBehaviour
    {
        [Header("Backdrop Renderers")]
        [SerializeField] private Renderer frontBackdrop;
        [SerializeField] private Renderer backBackdrop;

        [Header("Textures")]
        [SerializeField] private Texture frontTexture;
        [SerializeField] private Texture backTexture;

        [Header("Tiling & Base Tint")]
        [SerializeField] private Vector2 frontTiling = new Vector2(3f, 1f);
        [SerializeField] private Vector2 frontOffset = Vector2.zero;
        [SerializeField] private Color frontBaseColor = new Color(0.92f, 0.92f, 0.96f, 1f);

        [SerializeField] private Vector2 backTiling = new Vector2(3f, 1f);
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
                var cam = FindFirstObjectByType<Camera>();
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

            // Apply to Front
            if (frontBackdrop != null)
            {
                if (frontPropertyBlock == null) frontPropertyBlock = new MaterialPropertyBlock();
                frontBackdrop.GetPropertyBlock(frontPropertyBlock);

                if (frontTexture != null)
                {
                    frontPropertyBlock.SetTexture("_BaseMap", frontTexture);
                    frontPropertyBlock.SetTexture("_MainTex", frontTexture);
                }

                Color fCol = frontBaseColor * (1f + shimmer);
                frontPropertyBlock.SetColor("_BaseColor", fCol);
                frontPropertyBlock.SetColor("_Color", fCol);

                Vector4 fScaleOffset = new Vector4(frontTiling.x, frontTiling.y, frontOffset.x + camZOffset, frontOffset.y + camYOffset);
                frontPropertyBlock.SetVector("_BaseMap_ST", fScaleOffset);
                frontPropertyBlock.SetVector("_MainTex_ST", fScaleOffset);

                frontBackdrop.SetPropertyBlock(frontPropertyBlock);
            }

            // Apply to Back
            if (backBackdrop != null)
            {
                if (backPropertyBlock == null) backPropertyBlock = new MaterialPropertyBlock();
                backBackdrop.GetPropertyBlock(backPropertyBlock);

                if (backTexture != null)
                {
                    backPropertyBlock.SetTexture("_BaseMap", backTexture);
                    backPropertyBlock.SetTexture("_MainTex", backTexture);
                }

                // Subtle out-of-phase shimmer for back
                Color bCol = backBaseColor * (1f + shimmer * 0.8f);
                backPropertyBlock.SetColor("_BaseColor", bCol);
                backPropertyBlock.SetColor("_Color", bCol);

                Vector4 bScaleOffset = new Vector4(backTiling.x, backTiling.y, backOffset.x - camZOffset, backOffset.y + camYOffset);
                backPropertyBlock.SetVector("_BaseMap_ST", bScaleOffset);
                backPropertyBlock.SetVector("_MainTex_ST", bScaleOffset);

                backBackdrop.SetPropertyBlock(backPropertyBlock);
            }
        }

        [ContextMenu("Apply Settings")]
        public void ApplySettings()
        {
            InitializePropertyBlocks();

            if (frontBackdrop != null)
            {
                frontBackdrop.GetPropertyBlock(frontPropertyBlock);
                if (frontTexture != null)
                {
                    frontPropertyBlock.SetTexture("_BaseMap", frontTexture);
                    frontPropertyBlock.SetTexture("_MainTex", frontTexture);
                }
                frontPropertyBlock.SetColor("_BaseColor", frontBaseColor);
                frontPropertyBlock.SetColor("_Color", frontBaseColor);
                Vector4 st = new Vector4(frontTiling.x, frontTiling.y, frontOffset.x, frontOffset.y);
                frontPropertyBlock.SetVector("_BaseMap_ST", st);
                frontPropertyBlock.SetVector("_MainTex_ST", st);
                frontBackdrop.SetPropertyBlock(frontPropertyBlock);
            }

            if (backBackdrop != null)
            {
                backBackdrop.GetPropertyBlock(backPropertyBlock);
                if (backTexture != null)
                {
                    backPropertyBlock.SetTexture("_BaseMap", backTexture);
                    backPropertyBlock.SetTexture("_MainTex", backTexture);
                }
                backPropertyBlock.SetColor("_BaseColor", backBaseColor);
                backPropertyBlock.SetColor("_Color", backBaseColor);
                Vector4 st = new Vector4(backTiling.x, backTiling.y, backOffset.x, backOffset.y);
                backPropertyBlock.SetVector("_BaseMap_ST", st);
                backPropertyBlock.SetVector("_MainTex_ST", st);
                backBackdrop.SetPropertyBlock(backPropertyBlock);
            }
        }
    }
}
