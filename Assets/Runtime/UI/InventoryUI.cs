using System.Collections.Generic;
using CraftPlanner.Controllers;
using CraftPlanner.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftPlanner.UI
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private Transform _slotsContainer;

        private CraftController _controller;
        private Dictionary<string, InventorySlot> _slots = new();
        private bool _isUpdating = false;

        private class InventorySlot
        {
            public TextMeshProUGUI NameText;
            public TextMeshProUGUI AmountText;
            public Button AddButton;
            public Button RemoveButton;
            public ResourceSO Resource;
            public GameObject Root;
        }

        public void Initialize(CraftController controller)
        {
            _controller = controller;
            _controller.OnInventoryUpdated += UpdateUI;
            BuildAllSlots();
        }

        private void BuildAllSlots()
        {
            if (_controller == null)
            {
                Debug.LogError("_controller is NULL!");
                return;
            }

            ClearSlots();

            var allResources = _controller.Inventory.AllResources;

            foreach (var kvp in allResources)
            {
                var resource = _controller.RecipeDatabase.GetResourceById(kvp.Key);
                if (resource != null && kvp.Value > 0)
                {
                    CreateSlot(resource);
                }
            }
        }

        private void ClearSlots()
        {
            foreach (var slot in _slots.Values)
            {
                if (slot.Root != null)
                {
                    Destroy(slot.Root);
                }
            }
            _slots.Clear();
        }

        private void CreateSlot(ResourceSO resource)
        {
            if (_slots.ContainsKey(resource.Id))
            {
                UpdateSlotAmount(resource.Id);
                return;
            }

            var slotGO = Instantiate(_slotPrefab, _slotsContainer);

            var nameText = slotGO.transform.Find("Name")?.GetComponent<TextMeshProUGUI>();
            var amountText = slotGO.transform.Find("Amount")?.GetComponent<TextMeshProUGUI>();
            var addButton = slotGO.transform.Find("AddButton")?.GetComponent<Button>();
            var removeButton = slotGO.transform.Find("RemoveButton")?.GetComponent<Button>();

            if (nameText != null)
                nameText.text = resource.DisplayName;

            if (amountText != null)
                amountText.text = _controller.Inventory.GetAmount(resource).ToString();

            if (resource.IsBase)
            {
                if (addButton != null)
                {
                    addButton.onClick.RemoveAllListeners();
                    addButton.onClick.AddListener(() =>
                        _controller.AddBaseResource(resource, 1));
                    addButton.gameObject.SetActive(true);
                }

                if (removeButton != null)
                {
                    removeButton.onClick.RemoveAllListeners();
                    removeButton.onClick.AddListener(() =>
                        _controller.RemoveBaseResource(resource, 1));
                    removeButton.gameObject.SetActive(true);
                }
            }
            else
            {
                if (addButton != null)
                    addButton.gameObject.SetActive(false);
                if (removeButton != null)
                    removeButton.gameObject.SetActive(false);
            }

            var slot = new InventorySlot
            {
                NameText = nameText,
                AmountText = amountText,
                AddButton = addButton,
                RemoveButton = removeButton,
                Resource = resource,
                Root = slotGO
            };
            _slots[resource.Id] = slot;
        }

        private void UpdateSlotAmount(string resourceId)
        {
            if (_slots.TryGetValue(resourceId, out var slot))
            {
                if (slot.AmountText != null)
                {
                    int amount = _controller.Inventory.GetAmount(slot.Resource);
                    slot.AmountText.text = amount.ToString();
                }
            }
        }

        private void UpdateUI(Dictionary<string, int> inventory)
        {
            if (_isUpdating)
            {
                Debug.LogWarning("UpdateUI is already running, skipping");
                return;
            }

            _isUpdating = true;

            try
            {
                if (inventory == null) return;

                Debug.Log($"UpdateUI called with {inventory.Count} items");

                var resourcesToAdd = new List<ResourceSO>();
                var resourcesToRemove = new List<string>();

                foreach (var kvp in inventory)
                {
                    Debug.Log($"  Resource: {kvp.Key}, Amount: {kvp.Value}");

                    if (kvp.Value <= 0)
                    {
                        if (_slots.ContainsKey(kvp.Key))
                        {
                            resourcesToRemove.Add(kvp.Key);
                        }
                        continue;
                    }

                    var resource = _controller.RecipeDatabase.GetResourceById(kvp.Key);
                    if (resource == null) continue;

                    if (_slots.ContainsKey(kvp.Key))
                    {
                        UpdateSlotAmount(kvp.Key);
                    }
                    else
                    {
                        resourcesToAdd.Add(resource);
                    }
                }

                foreach (var resourceId in resourcesToRemove)
                {
                    RemoveSlot(resourceId);
                }

                foreach (var resource in resourcesToAdd)
                {
                    CreateSlot(resource);
                }

                // Force update all amounts to ensure consistency
                foreach (var kvp in _slots)
                {
                    if (kvp.Value.AmountText != null)
                    {
                        int amount = _controller.Inventory.GetAmount(kvp.Value.Resource);
                        kvp.Value.AmountText.text = amount.ToString();
                    }
                }
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void RemoveSlot(string resourceId)
        {
            if (_slots.TryGetValue(resourceId, out var slot))
            {
                if (slot.Root != null)
                {
                    Destroy(slot.Root);
                }
                _slots.Remove(resourceId);
            }
        }
    }
}