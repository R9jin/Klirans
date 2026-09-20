using System.Collections;
using UnityEngine;

/// <summary>
/// Controls humanoid NPC chair seating, standing, and head tracking behaviors:
/// - Detects if the NPC is stationed on or near a chair.
/// - Sits naturally on the chair by default (pelvis lowered, thighs horizontal, knees bent, arms resting).
/// - Stateless skeletal posing ensures bone angles never compound across frames.
/// - Smooth head tracking: continuously turns head and neck to follow the player when nearby.
/// - When interacted with (dialogue/puzzle), smoothly stands up and faces the player.
/// - When interaction ends (dialogue closes), smoothly turns back and sits back down on the chair.
/// </summary>
[RequireComponent(typeof(CapsuleCollider))]
public class NPCSitController : MonoBehaviour
{
    public enum SitState
    {
        Seated,
        StandingUp,
        Standing,
        SittingDown
    }

    [Header("Chair Configuration")]
    [Tooltip("The chair this NPC sits on. If null, automatically detected within detection radius.")]
    public GameObject assignedChair;
    [Tooltip("Maximum distance to search for a chair on startup if not explicitly assigned.")]
    public float chairDetectionRadius = 1.5f;

    [Header("Seating Poses & Offsets")]
    [Tooltip("Local Y offset applied to Hips bone when fully seated.")]
    public float hipsDropOffset = -0.0115f;
    [Tooltip("Local Z offset applied to Hips bone when fully seated.")]
    public float hipsForwardOffset = 0.002f;
    [Tooltip("Forward angle bend for thighs (LeftUpLeg, RightUpLeg) around pitch axis.")]
    public float thighPitchAngle = 78.0f;
    [Tooltip("Knee bend angle for shins (LeftLeg, RightLeg) around pitch axis.")]
    public float kneePitchAngle = -85.0f;
    [Tooltip("Foot pitch angle correction to keep soles flat.")]
    public float footPitchAngle = 10.0f;
    [Tooltip("Arm pitch angle resting towards desk/lap.")]
    public float armPitchAngle = 40.0f;

    [Header("Stand / Sit Transition Speeds")]
    [Tooltip("Duration in seconds to stand up from the chair.")]
    public float standUpDuration = 0.45f;
    [Tooltip("Duration in seconds to sit back down into the chair.")]
    public float sitDownDuration = 0.60f;
    [Tooltip("Distance in meters the NPC steps forward from chair when standing.")]
    public float standForwardDistance = 0.35f;

    [Header("Head Tracking")]
    [Tooltip("Enable continuous head and neck tracking of the player.")]
    public bool enableHeadTracking = true;
    [Tooltip("Maximum distance to track the player with head.")]
    public float maxHeadTrackDistance = 10.0f;
    [Tooltip("Maximum horizontal angle (degrees) from forward to track player.")]
    public float maxHeadTrackAngle = 80.0f;
    [Tooltip("Speed of head turning.")]
    public float headTrackSpeed = 5.0f;

    [Header("Runtime State")]
    [SerializeField] private SitState _state = SitState.Seated;
    [SerializeField] [Range(0f, 1f)] private float _sitWeight = 1.0f;
    [SerializeField] private bool _hasChair = false;

    [SerializeField] private Vector3 _seatedRootPos;
    [SerializeField] private Quaternion _seatedRootRot;
    [SerializeField] private Vector3 _standingRootPos;
    [SerializeField] private Quaternion _standingRootRot;
    [SerializeField] private Vector3 _initialHipsLocalPos;

    public SitState State => _state;
    public float SitWeight => _sitWeight;
    public bool HasChair => _hasChair;
    public bool IsSeated => _state == SitState.Seated;
    public bool IsStanding => _state == SitState.Standing;

    public Vector3 SeatedPosition => _seatedRootPos;
    public Vector3 StandingPosition => _standingRootPos;
    public Vector3 SeatedRootPos => _seatedRootPos;
    public Quaternion SeatedRootRot => _seatedRootRot;
    public Vector3 StandingRootPos => _standingRootPos;
    public Quaternion StandingRootRot => _standingRootRot;

    // Bone Transforms cache
    private Transform _hips;
    private Transform _leftUpLeg;
    private Transform _leftLeg;
    private Transform _leftFoot;
    private Transform _rightUpLeg;
    private Transform _rightLeg;
    private Transform _rightFoot;
    private Transform _leftArm;
    private Transform _rightArm;
    private Transform _neck;
    private Transform _head;

    // Pristine base standing rotations (captured once upon rebind/start)
    private Quaternion _baseLeftUpLegRot;
    private Quaternion _baseRightUpLegRot;
    private Quaternion _baseLeftLegRot;
    private Quaternion _baseRightLegRot;
    private Quaternion _baseLeftFootRot;
    private Quaternion _baseRightFootRot;
    private Quaternion _baseLeftArmRot;
    private Quaternion _baseRightArmRot;
    private bool _bonesCaptured = false;

    private CapsuleCollider _col;
    private float _standingColHeight = 1.8f;
    private Vector3 _standingColCenter = new Vector3(0f, 0.9f, 0f);
    private float _seatedColHeight = 1.1f;
    private Vector3 _seatedColCenter = new Vector3(0f, 0.55f, 0f);

    private Coroutine _transitionRoutine;
    private Transform _playerCamTransform;
    private float _currentHeadWeight = 0f;

    private void Awake()
    {
        if (kneePitchAngle > 0f) kneePitchAngle = -kneePitchAngle;
        CacheCollider();
        CapturePristineBones();
    }

    private void CacheCollider()
    {
        _col = GetComponent<CapsuleCollider>();
        if (_col != null)
        {
            _standingColHeight = _col.height;
            _standingColCenter = _col.center;
            _seatedColHeight = Mathf.Max(0.85f, _standingColHeight * 0.65f);
            _seatedColCenter = new Vector3(_standingColCenter.x, _standingColCenter.y * 0.65f, _standingColCenter.z);
        }
    }

    private void Start()
    {
        CapturePristineBones();

        var cam = Camera.main;
        if (cam != null) _playerCamTransform = cam.transform;

        if (_hasChair && _seatedRootPos != Vector3.zero)
        {
            transform.position = _seatedRootPos;
            transform.rotation = _seatedRootRot;
            _state = SitState.Seated;
            _sitWeight = 1.0f;
            UpdateCollider(1.0f);
        }
        else
        {
            InitChairBinding();
        }
    }

    /// <summary>
    /// Finds all skeletal bones and rebinds animator to capture pristine standing base rotations.
    /// </summary>
    public void CapturePristineBones()
    {
        if (_bonesCaptured && _hips != null) return;

        var anim = GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.Rebind();
            anim.Update(0f);
        }

        Transform root = anim != null ? anim.transform : transform;
        _hips = FindChildRecursive(root, "mixamorig:Hips");

        if (_hips != null)
        {
            _initialHipsLocalPos = _hips.localPosition;

            _leftUpLeg = FindChildRecursive(_hips, "mixamorig:LeftUpLeg");
            if (_leftUpLeg != null)
            {
                _baseLeftUpLegRot = _leftUpLeg.localRotation;
                _leftLeg = FindChildRecursive(_leftUpLeg, "mixamorig:LeftLeg");
                if (_leftLeg != null)
                {
                    _baseLeftLegRot = _leftLeg.localRotation;
                    _leftFoot = FindChildRecursive(_leftLeg, "mixamorig:LeftFoot");
                    if (_leftFoot != null) _baseLeftFootRot = _leftFoot.localRotation;
                }
            }

            _rightUpLeg = FindChildRecursive(_hips, "mixamorig:RightUpLeg");
            if (_rightUpLeg != null)
            {
                _baseRightUpLegRot = _rightUpLeg.localRotation;
                _rightLeg = FindChildRecursive(_rightUpLeg, "mixamorig:RightLeg");
                if (_rightLeg != null)
                {
                    _baseRightLegRot = _rightLeg.localRotation;
                    _rightFoot = FindChildRecursive(_rightLeg, "mixamorig:RightFoot");
                    if (_rightFoot != null) _baseRightFootRot = _rightFoot.localRotation;
                }
            }

            var spine = FindChildRecursive(_hips, "mixamorig:Spine");
            var spine2 = FindChildRecursive(spine != null ? spine : _hips, "mixamorig:Spine2");
            if (spine2 != null)
            {
                _leftArm = FindChildRecursive(spine2, "mixamorig:LeftArm");
                if (_leftArm != null) _baseLeftArmRot = _leftArm.localRotation;

                _rightArm = FindChildRecursive(spine2, "mixamorig:RightArm");
                if (_rightArm != null) _baseRightArmRot = _rightArm.localRotation;

                _neck = FindChildRecursive(spine2, "mixamorig:Neck");
                if (_neck != null)
                {
                    _head = FindChildRecursive(_neck, "mixamorig:Head");
                }
            }

            _bonesCaptured = true;
        }
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent == null) return null;
        if (parent.name == name) return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindChildRecursive(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>
    /// Searches for and binds to the nearest chair if within chairDetectionRadius.
    /// </summary>
    public void InitChairBinding()
    {
        if (assignedChair == null)
        {
            assignedChair = FindClosestChair(transform.position, chairDetectionRadius);
        }

        if (assignedChair != null)
        {
            _hasChair = true;

            Vector3 chairPos = assignedChair.transform.position;
            var rend = assignedChair.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                chairPos = rend.bounds.center;
                chairPos.y = rend.bounds.min.y;
            }

            chairPos.y = transform.position.y;

            _seatedRootPos = new Vector3(chairPos.x, transform.position.y, chairPos.z);
            _seatedRootRot = transform.rotation;

            Vector3 forwardStep = transform.forward * standForwardDistance;
            _standingRootPos = _seatedRootPos + forwardStep;
            _standingRootRot = transform.rotation;

            _state = SitState.Seated;
            _sitWeight = 1.0f;
            transform.position = _seatedRootPos;
            transform.rotation = _seatedRootRot;
            UpdateCollider(_sitWeight);
        }
        else
        {
            _hasChair = false;
            _state = SitState.Standing;
            _sitWeight = 0.0f;
        }
    }

    private GameObject FindClosestChair(Vector3 origin, float radius)
    {
        var allGOs = FindObjectsOfType<GameObject>();
        GameObject closest = null;
        float minDist = radius;

        foreach (var go in allGOs)
        {
            string lower = go.name.ToLower();
            if (lower.Contains("chair") || lower.Contains("seat") || lower.Contains("stool"))
            {
                if (go.transform.parent != null && go.transform.parent.name.ToLower().Contains("chair"))
                    continue;

                float d = Vector3.Distance(origin, go.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = go;
                }
            }
        }
        return closest;
    }

    /// <summary>
    /// Called when the player starts interacting with this NPC.
    /// </summary>
    public void OnInteract()
    {
        if (!_hasChair) return;
        StandUp();
    }

    /// <summary>
    /// Called when interaction or dialogue concludes.
    /// </summary>
    public void OnEndInteract()
    {
        if (!_hasChair) return;
        SitDown();
    }

    /// <summary>
    /// Smoothly transitions the NPC to the standing posture.
    /// </summary>
    public void StandUp()
    {
        if (!_hasChair) return;
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionRoutine(0.0f, standUpDuration, SitState.Standing));
    }

    /// <summary>
    /// Smoothly transitions the NPC back down to the seated posture.
    /// </summary>
    public void SitDown()
    {
        if (!_hasChair) return;
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(TransitionRoutine(1.0f, sitDownDuration, SitState.Seated));
    }

    private IEnumerator TransitionRoutine(float targetWeight, float duration, SitState finalState)
    {
        _state = targetWeight < 0.5f ? SitState.StandingUp : SitState.SittingDown;

        float startWeight = _sitWeight;
        Vector3 startPos = transform.position;
        Quaternion startRot = transform.rotation;

        Vector3 targetPos = _seatedRootPos;
        Quaternion targetRot = _seatedRootRot;

        // When standing up to talk, turn body to face the player (front, sides, or behind)
        if (targetWeight < 0.5f)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Vector3 toPlayer = player.transform.position - transform.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    targetRot = Quaternion.LookRotation(toPlayer);
                }
            }
        }
        else
        {
            // When sitting back down: turn back to chair and seat alignment ("and vice versa")
            targetPos = _seatedRootPos;
            targetRot = _seatedRootRot;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

            _sitWeight = Mathf.Lerp(startWeight, targetWeight, t);
            transform.position = Vector3.Lerp(startPos, targetPos, t);
            transform.rotation = Quaternion.Slerp(startRot, targetRot, t);

            UpdateCollider(_sitWeight);
            yield return null;
        }

        _sitWeight = targetWeight;
        transform.position = targetPos;
        transform.rotation = targetRot;
        UpdateCollider(_sitWeight);

        _state = finalState;
        _transitionRoutine = null;
    }

    private void UpdateCollider(float weight)
    {
        if (_col == null) CacheCollider();
        if (_col == null) return;
        _col.height = Mathf.Lerp(_standingColHeight, _seatedColHeight, weight);
        _col.center = Vector3.Lerp(_standingColCenter, _seatedColCenter, weight);
    }

    private void LateUpdate()
    {
        if (_sitWeight > 0.0001f)
        {
            ApplySittingPose(_sitWeight);
        }

        if (enableHeadTracking)
        {
            ApplyHeadTracking();
        }
    }

    /// <summary>
    /// Evaluates and applies the skeletal sitting pose statelessly.
    /// Guaranteed NEVER to compound because it interpolates from the pristine _base* rotations.
    /// </summary>
    public void ApplySittingPose(float weight)
    {
        if (!_bonesCaptured || _hips == null)
        {
            CapturePristineBones();
            if (!_bonesCaptured || _hips == null) return;
        }

        // 1. Lower pelvis / hips
        Vector3 seatedHipsLocal = _initialHipsLocalPos + new Vector3(0f, hipsDropOffset, hipsForwardOffset);
        _hips.localPosition = Vector3.Lerp(_initialHipsLocalPos, seatedHipsLocal, weight);

        // 2. Thigh forward pitch (LeftUpLeg, RightUpLeg)
        if (_leftUpLeg != null)
        {
            Quaternion sitLUpLeg = _baseLeftUpLegRot * Quaternion.Euler(thighPitchAngle, 0f, 0f);
            _leftUpLeg.localRotation = Quaternion.Slerp(_baseLeftUpLegRot, sitLUpLeg, weight);
        }

        if (_rightUpLeg != null)
        {
            Quaternion sitRUpLeg = _baseRightUpLegRot * Quaternion.Euler(thighPitchAngle, 0f, 0f);
            _rightUpLeg.localRotation = Quaternion.Slerp(_baseRightUpLegRot, sitRUpLeg, weight);
        }

        // 3. Knee downward flexion (LeftLeg, RightLeg)
        if (_leftLeg != null)
        {
            Quaternion sitLLeg = _baseLeftLegRot * Quaternion.Euler(kneePitchAngle, 0f, 0f);
            _leftLeg.localRotation = Quaternion.Slerp(_baseLeftLegRot, sitLLeg, weight);
        }

        if (_rightLeg != null)
        {
            Quaternion sitRLeg = _baseRightLegRot * Quaternion.Euler(kneePitchAngle, 0f, 0f);
            _rightLeg.localRotation = Quaternion.Slerp(_baseRightLegRot, sitRLeg, weight);
        }

        // 4. Feet posture alignment
        if (_leftFoot != null)
        {
            Quaternion sitLFoot = _baseLeftFootRot * Quaternion.Euler(footPitchAngle, 0f, 0f);
            _leftFoot.localRotation = Quaternion.Slerp(_baseLeftFootRot, sitLFoot, weight);
        }

        if (_rightFoot != null)
        {
            Quaternion sitRFoot = _baseRightFootRot * Quaternion.Euler(footPitchAngle, 0f, 0f);
            _rightFoot.localRotation = Quaternion.Slerp(_baseRightFootRot, sitRFoot, weight);
        }

        // 5. Rest arms naturally on desk or lap
        if (_leftArm != null)
        {
            Quaternion sitLArm = _baseLeftArmRot * Quaternion.Euler(armPitchAngle, 12f, 0f);
            _leftArm.localRotation = Quaternion.Slerp(_leftArm.localRotation, sitLArm, weight);
        }

        if (_rightArm != null)
        {
            Quaternion sitRArm = _baseRightArmRot * Quaternion.Euler(armPitchAngle, -12f, 0f);
            _rightArm.localRotation = Quaternion.Slerp(_baseRightArmRot, sitRArm, weight);
        }
    }

    /// <summary>
    /// Smoothly turns head and neck to look at the player's camera when in range and view cone.
    /// </summary>
    private void ApplyHeadTracking()
    {
        if (_head == null) return;

        if (_playerCamTransform == null)
        {
            var cam = Camera.main;
            if (cam != null) _playerCamTransform = cam.transform;
            if (_playerCamTransform == null) return;
        }

        Vector3 toCam = _playerCamTransform.position - _head.position;
        float dist = toCam.magnitude;
        float targetWeight = 0f;

        bool isInteracting = _state == SitState.Standing || _state == SitState.StandingUp;
        if (dist <= maxHeadTrackDistance && dist > 0.1f)
        {
            Vector3 dir = toCam / dist;
            float angle = Vector3.Angle(transform.forward, dir);
            float allowedAngle = isInteracting ? 85f : maxHeadTrackAngle;
            if (angle <= allowedAngle)
            {
                // Full direct gaze when talking / interacting, smooth falloff when idling
                targetWeight = isInteracting ? 1.0f : Mathf.Clamp01(1f - (angle / allowedAngle) * 0.3f);
            }
        }

        _currentHeadWeight = Mathf.MoveTowards(_currentHeadWeight, targetWeight, Time.deltaTime * headTrackSpeed);

        if (_currentHeadWeight > 0.001f)
        {
            Vector3 lookDir = (_playerCamTransform.position - _head.position).normalized;
            Quaternion targetLookRot = Quaternion.LookRotation(lookDir, Vector3.up);

            // Blend head (70%) and neck (30%)
            _head.rotation = Quaternion.Slerp(_head.rotation, targetLookRot, _currentHeadWeight * 0.70f);
            if (_neck != null)
            {
                _neck.rotation = Quaternion.Slerp(_neck.rotation, targetLookRot, _currentHeadWeight * 0.30f);
            }
        }
    }

    /// <summary>
    /// Explicitly anchors the chair sitting pose and standing transforms.
    /// </summary>
    public void ConfigureChairAnchor(GameObject chair, Vector3 seatedPos, Quaternion seatedRot, Vector3 standingPos, Quaternion standingRot)
    {
        assignedChair = chair;
        _hasChair = chair != null;
        _seatedRootPos = seatedPos;
        _seatedRootRot = seatedRot;
        _standingRootPos = standingPos;
        _standingRootRot = standingRot;

        _state = SitState.Seated;
        _sitWeight = 1.0f;
        transform.position = _seatedRootPos;
        transform.rotation = _seatedRootRot;
        UpdateCollider(1.0f);
    }
}
