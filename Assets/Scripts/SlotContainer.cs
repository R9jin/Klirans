using System;
using System.Collections.Generic;
using UnityEngine;

// Shared logic used by BOTH InventoryManager (6 held slots) and StorageManager
// (12 bag slots). Neither one talks to UI directly - they just track data and
// fire an event when something changes, and the *Menu scripts react to that.
public abstract class SlotContainer : MonoBehaviour
{
    [SerializeField] protected int size = 6;

    public List<InventorySlot> Slots { get; private set; }
    public event Action OnInventoryChanged;

    protected virtual void Awake()
    {
        Slots = new List<InventorySlot>();
        for (int i = 0; i < size; i++)
            Slots.Add(new InventorySlot());
    }

    public bool AddItem(InventoryItem newItem, int amount = 1)
    {
        if (newItem == null) return false;

        if (newItem.isStackable)
        {
            foreach (var slot in Slots)
            {
                if (!slot.IsEmpty && slot.item == newItem && slot.quantity < newItem.maxStack)
                {
                    int space = newItem.maxStack - slot.quantity;
                    int toAdd = Mathf.Min(space, amount);
                    slot.quantity += toAdd;
                    amount -= toAdd;

                    if (amount <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return true;
                    }
                }
            }
        }

        foreach (var slot in Slots)
        {
            if (slot.IsEmpty)
            {
                slot.item = newItem;
                slot.quantity = amount;
                OnInventoryChanged?.Invoke();
                return true;
            }
        }

        Debug.Log($"{name} is full - cannot add {newItem.itemName}");
        return false;
    }

    public bool RemoveItem(InventoryItem itemToRemove, int amount = 1)
    {
        foreach (var slot in Slots)
        {
            if (!slot.IsEmpty && slot.item == itemToRemove)
            {
                slot.quantity -= amount;
                if (slot.quantity <= 0) slot.Clear();
                OnInventoryChanged?.Invoke();
                return true;
            }
        }
        return false;
    }

    // Matches the Proctor's "steal one item on catch" punishment.
    public void RemoveRandomItem()
    {
        List<int> filled = new List<int>();
        for (int i = 0; i < Slots.Count; i++)
            if (!Slots[i].IsEmpty && Slots[i].item.canBeDropped) filled.Add(i);

        if (filled.Count == 0) return;

        int chosen = filled[UnityEngine.Random.Range(0, filled.Count)];
        Slots[chosen].Clear();
        OnInventoryChanged?.Invoke();
    }

    public bool HasItem(InventoryItem item)
    {
        foreach (var slot in Slots)
            if (!slot.IsEmpty && slot.item == item) return true;
        return false;
    }

    public bool HasFreeSlot()
    {
        foreach (var slot in Slots)
            if (slot.IsEmpty) return true;
        return false;
    }
}
