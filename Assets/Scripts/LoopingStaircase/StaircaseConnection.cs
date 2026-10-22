using System;
using UnityEngine;

/// <summary>
/// Defines one directed stair connection: travelling from one floor to another
/// through a specific stairwell, plus the probability distribution for where
/// the player actually ends up.
/// 
/// Add multiple StaircaseConnection entries to the LoopingStaircaseSystem to
/// cover all floor-pairs in both directions.
/// </summary>
[Serializable]
public class StaircaseConnection
{
    [Header("Connection Identity")]
    [Tooltip("Which stairwell this rule applies to. Must match a StairTriggerZone's stairwellID.")]
    public string stairwellID = "MainStairs";

    [Tooltip("The floor the player is leaving FROM.")]
    public int fromFloor = 1;

    [Tooltip("True = player is travelling upward (ascending). False = downward (descending).\n" +
             "This is auto-detected from CharacterController velocity at runtime.")]
    public bool goingUp = true;

    [Tooltip("The intended destination floor (the normal case).")]
    public int normalDestination = 2;

    [Tooltip("If true, ALWAYS redirects to the normalDestination via the looping conceal logic.")]
    public bool forceLoop = false;

    [Header("Probability Outcomes")]
    [Tooltip("0–1 probability of arriving at the normal destination (e.g. 0.80).")]
    [Range(0f, 1f)]
    public float normalProbability = 0.80f;

    [Tooltip("List of alternate outcomes. Each entry has a floor number and a weight. " +
             "Weights will be normalised against 1 - normalProbability automatically.")]
    public AlternateOutcome[] alternateOutcomes = new AlternateOutcome[0];

    // ──────────────────────────────────────────────────────────────────────
    // Runtime helper: pick a destination floor using the configured probabilities
    // ──────────────────────────────────────────────────────────────────────
    public int PickDestination()
    {
        float roll = UnityEngine.Random.value;

        if (roll <= normalProbability)
            return normalDestination;

        // Distribute remaining probability among alternates
        float remaining = 1f - normalProbability;
        if (remaining <= 0f || alternateOutcomes == null || alternateOutcomes.Length == 0)
            return normalDestination;

        float totalWeight = 0f;
        foreach (var alt in alternateOutcomes)
            totalWeight += Mathf.Max(0f, alt.weight);

        if (totalWeight <= 0f)
            return normalDestination;

        float pick = (roll - normalProbability) / remaining * totalWeight;
        float cumulative = 0f;
        foreach (var alt in alternateOutcomes)
        {
            cumulative += Mathf.Max(0f, alt.weight);
            if (pick <= cumulative)
                return alt.destinationFloor;
        }

        return normalDestination; // fallback
    }
}

/// <summary>
/// One alternate outcome entry: a floor the player may be sent to, and
/// the relative weight (not a strict probability – the system normalises it).
/// </summary>
[Serializable]
public class AlternateOutcome
{
    [Tooltip("Floor number the player will be sent to if this outcome is chosen.")]
    public int destinationFloor = 1;

    [Tooltip("Relative weight. Higher = more likely among alternate outcomes.")]
    [Range(0f, 1f)]
    public float weight = 0.15f;
}
