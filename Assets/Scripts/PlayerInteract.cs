using UnityEngine;
using UnityEngine.UI;

public class PlayerInteract : MonoBehaviour
{
    [Header("Interaction Settings")]
    [Tooltip("Maximum distance at which the player can interact with an object.")]
    public float interactRange = 2.35f;

    [Tooltip("Radius of the interaction ray to make pointing at items more forgiving.")]
    public float interactRadius = 0.14f;

    [Tooltip("The layers that can be interacted with.")]
    public LayerMask interactableLayer;

    [Header("Interaction UI")]
    [Tooltip("The UI Text that displays the interaction prompt.")]
    public Text promptText;

    private Camera playerCamera;

    private PickupItem currentTarget;
    private RaycastHit[] hitBuffer = new RaycastHit[32];

    private void Start()
    {
        // Ensure comfortable reach across tables/desks
        if (interactRange < 2.45f || interactRange > 2.8f) interactRange = 2.55f;
        if (interactRadius < 0.16f || interactRadius > 0.25f) interactRadius = 0.18f;

        // Find the camera attached to the player or one of its children.
        playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera == null)
        {
            Debug.LogError(
                "PlayerInteract could not find a Camera. " +
                "Make sure the Player has a Camera component on itself or one of its children."
            );
        }

        // Hide the interaction prompt when the game starts and style with retro font
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);

            Font typewriter = Resources.Load<Font>("Fonts/Typewriter_Bold") ?? Resources.Load<Font>("Fonts/Lucida_Typewriter_Bold");
#if UNITY_EDITOR
            if (typewriter == null)
                typewriter = UnityEditor.AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Typewriter_Bold.ttf");
#endif
            if (typewriter != null) promptText.font = typewriter;

            promptText.fontSize = 22;
            promptText.fontStyle = FontStyle.Bold;
            promptText.alignment = TextAnchor.MiddleCenter;
            promptText.horizontalOverflow = HorizontalWrapMode.Wrap;

            var rt = promptText.GetComponent<RectTransform>();
            if (rt != null)
            {
                // Anchor to bottom of screen (just above stamina gauge at Y=145), well clear of center reticle
                rt.anchorMin = new Vector2(0.5f, 0.0f);
                rt.anchorMax = new Vector2(0.5f, 0.0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(900f, 60f);
                rt.anchoredPosition = new Vector2(0f, 195f);
            }

            var outline = promptText.GetComponent<Outline>();
            if (outline == null) outline = promptText.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2f, -2f);
        }
        else
        {
            Debug.LogWarning(
                "PlayerInteract has no Prompt Text assigned. " +
                "The interaction may work, but the interaction message cannot be displayed."
            );
        }
    }

    private void Update()
    {
        if (playerCamera == null || PauseMenu.GameIsPaused || LockerHideManager.IsPlayerHidden || PauseMenu.IsAnyModalOrPuzzleOpen)
        {
            if (promptText != null) promptText.gameObject.SetActive(false);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        // Cast out to 5.50m so runaway Registrar in hallways can be caught comfortably
        float maxCastDist = Mathf.Max(interactRange, 5.50f);
        int hitCount = Physics.SphereCastNonAlloc(ray, interactRadius, hitBuffer, maxCastDist, interactableLayer, QueryTriggerInteraction.Collide);

        float closestDistance = float.MaxValue;
        IInteractable closestInteractable = null;
        PickupItem closestPickup = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (h.collider == null) continue;

            if (h.collider.transform.IsChildOf(transform) || (playerCamera != null && h.collider.transform.IsChildOf(playerCamera.transform)))
            {
                continue;
            }

            IInteractable interactable = h.collider.GetComponentInParent<IInteractable>();
            if (interactable == null)
            {
                interactable = h.collider.GetComponentInChildren<IInteractable>();
            }
            if (interactable == null) continue;

            bool isStaffNPC = (interactable is ClearanceNPC) || (interactable is SecurityGuardInteract) || (interactable is HamAndCheeseVendor);
            bool isDoor = (interactable is DoorInteract);
            float allowedRange = interactRange;
            if (isStaffNPC)
            {
                var clr = interactable as ClearanceNPC;
                var staffAI = clr != null ? clr.GetComponent<RoomStaffAI>() : null;
                allowedRange = (staffAI != null && staffAI.isRunawayActive) ? 5.50f : 3.85f;
            }
            else if (isDoor)
            {
                allowedRange = 3.10f;
            }
            if (h.distance > allowedRange) continue;

            if (h.distance < closestDistance)
            {
                if (isStaffNPC)
                {
                    // For staff NPCs seated behind counters/desks/windows, only reject if an actual solid room wall/door blocks them
                    MonoBehaviour npcMb = interactable as MonoBehaviour;
                    Vector3 npcEyePos = npcMb != null ? (npcMb.transform.position + Vector3.up * 1.45f) : (h.collider.bounds.center + Vector3.up * 0.35f);
                    if (IsBlockedBySolidRoomWall(ray.origin, npcEyePos, h.collider.transform.root))
                    {
                        continue;
                    }
                }
                else
                {
                    // Line of sight check for items & props: do not allow interacting through solid walls,
                    // but never let a table/desk/counter surface block an item, or door frame block a door!
                    Vector3 targetPos = h.collider.bounds.center + Vector3.up * 0.05f;
                    Vector3 rayDir = targetPos - ray.origin;
                    float rayDist = rayDir.magnitude;
                    if (rayDist > 0.08f && Physics.Raycast(ray.origin, rayDir.normalized, out RaycastHit losHit, rayDist - 0.08f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (losHit.collider != null &&
                            losHit.collider != h.collider &&
                            losHit.collider.transform.root != transform.root &&
                            !losHit.collider.transform.IsChildOf(h.collider.transform) &&
                            !h.collider.transform.IsChildOf(losHit.collider.transform))
                        {
                            string hitName = losHit.collider.gameObject.name.ToLowerInvariant();
                            bool isTableOrSurface = hitName.Contains("table") || hitName.Contains("desk") || hitName.Contains("counter") ||
                                                    hitName.Contains("shelf") || hitName.Contains("blocker") || hitName.Contains("chair");
                            bool isPickupTarget = (interactable is PickupItem) || (interactable is CoinPickup) || (interactable is FragmentPickup) || (interactable is PaperReamPickup);
                            bool isDoorPart = isDoor && (hitName.Contains("door") || hitName.Contains("frame") || hitName.Contains("jamb") || hitName.Contains("lintel"));

                            if (!(isTableOrSurface && isPickupTarget) && !isDoorPart)
                            {
                                continue;
                            }
                        }
                    }
                }

                closestDistance = h.distance;
                closestInteractable = interactable;
                closestPickup = interactable as PickupItem;
            }
        }

        // Forgiving view-cone assist for ClearanceNPCs so looking anywhere near the staff member,
        // their desk, laptop, or service counter window effortlessly shows the interaction prompt!
        if (closestInteractable == null)
        {
            closestInteractable = FindClearanceNPCInViewCone(ray);
        }

        if (closestInteractable != null)
        {
            if (closestPickup != null) SetCurrentTarget(closestPickup);

            if (promptText != null)
            {
                string prompt = closestInteractable.GetPrompt();
                if (!string.IsNullOrEmpty(prompt))
                {
                    promptText.text = prompt;
                    promptText.gameObject.SetActive(true);
                }
                else
                {
                    promptText.gameObject.SetActive(false);
                }
            }

            if (GameSettingsManager.GetKeyDown(GameAction.Interact) || Input.GetKeyDown(KeyCode.E))
            {
                closestInteractable.Interact();
            }

            return;
        }

        ClearCurrentTarget();
    }

    private readonly RaycastHit[] _losBuffer = new RaycastHit[16];

    /// <summary>
    /// Returns true ONLY if an opaque structural room wall/door/floor blocks the line between origin and target,
    /// ignoring desks, counters, service windows, glass bays, monitors, laptops, book stacks, and furniture.
    /// </summary>
    private bool IsBlockedBySolidRoomWall(Vector3 origin, Vector3 targetPos, Transform targetRoot)
    {
        Vector3 dir = targetPos - origin;
        float dist = dir.magnitude;
        if (dist <= 0.15f) return false;

        int count = Physics.RaycastNonAlloc(origin, dir.normalized, _losBuffer, dist - 0.10f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider c = _losBuffer[i].collider;
            if (c == null) continue;
            if (c.transform.root == transform.root) continue;
            if (targetRoot != null && (c.transform.root == targetRoot || c.transform.IsChildOf(targetRoot))) continue;

            string n = c.gameObject.name.ToLowerInvariant();
            string p = c.transform.parent != null ? c.transform.parent.name.ToLowerInvariant() : string.Empty;

            // Desks, counters, service windows, glass bays, slot trims, laptops, monitors, books, chairs never block NPC interaction
            if (n.Contains("desk") || n.Contains("counter") || n.Contains("table") || n.Contains("glass") ||
                n.Contains("window") || n.Contains("slot") || n.Contains("trim") || n.Contains("bay") ||
                n.Contains("laptop") || n.Contains("monitor") || n.Contains("screen") || n.Contains("stack") ||
                n.Contains("book") || n.Contains("chair") || n.Contains("tray") || n.Contains("paper") ||
                n.Contains("sign") || n.Contains("plaque") || n.Contains("divider") || n.Contains("partition") ||
                p.Contains("desk") || p.Contains("counter") || p.Contains("window") || p.Contains("workstation"))
            {
                continue;
            }

            // Structural walls, closed hallway doors, or floor slabs do block interaction across rooms
            if (n.Contains("wall") || n.Contains("door") || n.Contains("floor") || n.Contains("ceiling") || n.Contains("slab"))
            {
                return true;
            }
        }
        return false;
    }

    private IInteractable FindClearanceNPCInViewCone(Ray ray)
    {
        var npcs = FindObjectsByType<ClearanceNPC>();
        ClearanceNPC bestNpc = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < npcs.Length; i++)
        {
            ClearanceNPC npc = npcs[i];
            if (npc == null || !npc.gameObject.activeInHierarchy) continue;

            float distToPlayer = Vector3.Distance(transform.position, npc.transform.position);
            if (distToPlayer > Mathf.Min(npc.interactionRange, 3.85f)) continue;

            // Check chest/desk level (y + 1.0m) and head level (y + 1.5m)
            Vector3 chestPos = npc.transform.position + Vector3.up * 1.05f;
            Vector3 headPos  = npc.transform.position + Vector3.up * 1.50f;

            float angleChest = Vector3.Angle(ray.direction, chestPos - ray.origin);
            float angleHead  = Vector3.Angle(ray.direction, headPos - ray.origin);
            float minAngle   = Mathf.Min(angleChest, angleHead);

            // Generous 30-degree cone when close to the NPC/counter
            if (minAngle <= 30f)
            {
                if (!IsBlockedBySolidRoomWall(ray.origin, headPos, npc.transform.root))
                {
                    float score = minAngle + distToPlayer * 2.5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        bestNpc = npc;
                    }
                }
            }
        }

        return bestNpc;
    }

    private void SetCurrentTarget(PickupItem pickupItem)
    {
        currentTarget = pickupItem;
    }

    private void ClearCurrentTarget()
    {
        currentTarget = null;

        // Always hide the prompt when there is no valid target.
        if (promptText != null)
        {
            promptText.gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Only draw the interaction ray when the player is selected
        // and the camera has already been found.
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null) return;
        }

        Gizmos.color = Color.yellow;

        Ray ray = playerCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f)
        );

        Gizmos.DrawRay(
            ray.origin,
            ray.direction * interactRange
        );

        // Draw the sphere at the end of the cast
        Gizmos.DrawWireSphere(ray.origin + ray.direction * interactRange, interactRadius);
    }
}