using UnityEngine;

/// <summary>
/// Interface for any object in the world that the player can interact with.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Returns the text to display in the interaction UI prompt (e.g. "Press E to Read").
    /// </summary>
    string GetPrompt();

    /// <summary>
    /// Called when the player interacts with this object (e.g. presses 'E').
    /// </summary>
    void Interact();
}
