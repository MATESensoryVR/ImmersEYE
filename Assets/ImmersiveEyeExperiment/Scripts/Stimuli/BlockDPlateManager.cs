using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Click food to move it to an available plate slot; click it again to return.
/// Retains all existing serialized references and original food behaviour.
/// Adds a detailed event (including slot number) for durable data recording.
/// </summary>
public class BlockDPlateManager : MonoBehaviour
{
    [Header("Plate Slots (Maximum 4)")]
    [SerializeField] private Transform[] plateSlots = new Transform[4];

    [Header("Clickable Food Objects")]
    [SerializeField] private XRSimpleInteractable[] foods;

    private const int MaxFoods = 4;

    private class FoodState
    {
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public int slotIndex = -1;
    }

    private readonly Dictionary<XRSimpleInteractable, FoodState> foodStates =
        new Dictionary<XRSimpleInteractable, FoodState>();

    private readonly Dictionary<XRSimpleInteractable, UnityAction<SelectEnterEventArgs>> listeners =
        new Dictionary<XRSimpleInteractable, UnityAction<SelectEnterEventArgs>>();

    private readonly XRSimpleInteractable[] slotOccupants = new XRSimpleInteractable[MaxFoods];
    private bool interactionEnabled = false;

    public int SelectedCount
    {
        get
        {
            int count = 0;
            foreach (var occupant in slotOccupants)
                if (occupant != null) count++;
            return count;
        }
    }

    public bool HasExactlyFourFoods => SelectedCount == MaxFoods;

    // Original event is preserved for any existing UI/controller subscribers.
    public event Action<string, bool, int> FoodSelectionChanged;

    // NEW: food GameObject name, selected (true) / removed (false),
    // current count, and slot number (1..4, including the old slot on removal).
    public event Action<string, bool, int, int> FoodSelectionDetailed;

    private void Awake() => InitializeFoodStates();

    private void InitializeFoodStates()
    {
        if (foods == null) return;
        foreach (XRSimpleInteractable food in foods)
        {
            if (food == null || foodStates.ContainsKey(food)) continue;
            foodStates.Add(food, new FoodState
            {
                originalPosition = food.transform.position,
                originalRotation = food.transform.rotation
            });
        }
    }

    private void OnEnable()
    {
        InitializeFoodStates();
        if (foods == null) return;
        foreach (XRSimpleInteractable food in foods)
        {
            if (food == null || listeners.ContainsKey(food)) continue;
            XRSimpleInteractable capturedFood = food;
            UnityAction<SelectEnterEventArgs> callback = args => ToggleFood(capturedFood);
            food.selectEntered.AddListener(callback);
            listeners.Add(food, callback);
        }
    }

    private void OnDisable()
    {
        foreach (var entry in listeners)
        {
            if (entry.Key != null)
                entry.Key.selectEntered.RemoveListener(entry.Value);
        }
        listeners.Clear();
        interactionEnabled = false;
    }

    public void SetInteractionEnabled(bool enabled) => interactionEnabled = enabled;

    public void ResetSelections()
    {
        InitializeFoodStates();
        foreach (var entry in foodStates)
        {
            if (entry.Key == null) continue;
            entry.Key.transform.SetPositionAndRotation(
                entry.Value.originalPosition, entry.Value.originalRotation);
            entry.Value.slotIndex = -1;
        }
        Array.Clear(slotOccupants, 0, slotOccupants.Length);
    }

    public List<string> GetSelectedFoodNamesInSlotOrder()
    {
        var result = new List<string>();
        foreach (var occupant in slotOccupants)
            if (occupant != null) result.Add(occupant.gameObject.name);
        return result;
    }

    public void ToggleFood(XRSimpleInteractable food)
    {
        if (!interactionEnabled || food == null) return;
        if (!foodStates.TryGetValue(food, out FoodState state)) return;

        if (state.slotIndex >= 0)
        {
            int oldSlot = state.slotIndex;
            food.transform.SetPositionAndRotation(state.originalPosition, state.originalRotation);
            slotOccupants[oldSlot] = null;
            state.slotIndex = -1;
            Debug.Log($"BLOCK_D_FOOD_REMOVED|food={food.name}|slot={oldSlot + 1}|count={SelectedCount}|utc={DateTime.UtcNow:o}");
            FoodSelectionChanged?.Invoke(food.name, false, SelectedCount);
            FoodSelectionDetailed?.Invoke(food.name, false, SelectedCount, oldSlot + 1);
            return;
        }

        int availableSlots = plateSlots == null ? 0 : Mathf.Min(MaxFoods, plateSlots.Length);
        for (int i = 0; i < availableSlots; i++)
        {
            if (plateSlots[i] == null || slotOccupants[i] != null) continue;

            // Keep original parent and scale; move in WORLD space.
            food.transform.SetPositionAndRotation(plateSlots[i].position, state.originalRotation);
            slotOccupants[i] = food;
            state.slotIndex = i;
            Debug.Log($"BLOCK_D_FOOD_ADDED|food={food.name}|slot={i + 1}|count={SelectedCount}|utc={DateTime.UtcNow:o}");
            FoodSelectionChanged?.Invoke(food.name, true, SelectedCount);
            FoodSelectionDetailed?.Invoke(food.name, true, SelectedCount, i + 1);
            return;
        }

        Debug.Log("BLOCK_D_PLATE_FULL|Maximum 4 foods");
    }
}
