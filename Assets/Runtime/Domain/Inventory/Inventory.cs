using System;
using System.Collections.Generic;
using CraftPlanner.Data;
using UnityEngine;  

namespace CraftPlanner.Domain.Inventory
{
    public class Inventory
    {
        private readonly Dictionary<string, int> _resources = new();
        private readonly Dictionary<string, ResourceSO> _resourceMap = new();

        public event Action<ResourceSO, int> OnResourceChanged;

        public IReadOnlyDictionary<string, int> AllResources => _resources;

        public Inventory() { }

        private Inventory(Dictionary<string, ResourceSO> resourceMap)
        {
            _resourceMap = new Dictionary<string, ResourceSO>(resourceMap);
        }

        public int GetAmount(ResourceSO resource)
        {
            if (resource == null)
            {
                Debug.LogError("GetAmount: resource is null");
                return 0;
            }

            if (string.IsNullOrEmpty(resource.Id))
            {
                Debug.LogError($"GetAmount: resource.Id is null or empty for {resource.DisplayName}");
                return 0;
            }

            return _resources.TryGetValue(resource.Id, out var amount) ? amount : 0;
        }

        public int GetAmount(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return 0;
            return _resources.TryGetValue(resourceId, out var amount) ? amount : 0;
        }

        public void AddResource(ResourceSO resource, int amount)
        {
            if (resource == null)
            {
                Debug.LogError("AddResource: resource is null");
                return;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"AddResource: amount {amount} is not positive for {resource.DisplayName}");
                return;
            }

            if (string.IsNullOrEmpty(resource.Id))
            {
                Debug.LogError($"AddResource: resource.Id is null or empty for {resource.DisplayName}");
                return;
            }

            var current = GetAmount(resource);
            _resources[resource.Id] = current + amount;

            if (!_resourceMap.ContainsKey(resource.Id))
                _resourceMap[resource.Id] = resource;

            OnResourceChanged?.Invoke(resource, GetAmount(resource));
        }

        public bool RemoveResource(ResourceSO resource, int amount)
        {
            if (resource == null)
            {
                Debug.LogError("RemoveResource: resource is null");
                return false;
            }

            if (amount <= 0)
            {
                Debug.LogWarning($"RemoveResource: amount {amount} is not positive for {resource.DisplayName}");
                return false;
            }

            if (string.IsNullOrEmpty(resource.Id))
            {
                Debug.LogError($"RemoveResource: resource.Id is null or empty for {resource.DisplayName}");
                return false;
            }

            var current = GetAmount(resource);
            if (current < amount)
            {
                Debug.LogWarning($"RemoveResource: not enough {resource.DisplayName}. Have {current}, need {amount}");
                return false;
            }

            var newAmount = current - amount;
            if (newAmount == 0)
                _resources.Remove(resource.Id);
            else
                _resources[resource.Id] = newAmount;

            if (!_resourceMap.ContainsKey(resource.Id))
                _resourceMap[resource.Id] = resource;

            OnResourceChanged?.Invoke(resource, GetAmount(resource));
            return true;
        }

        public bool HasResources(ResourceSO resource, int amount)
        {
            return GetAmount(resource) >= amount;
        }

        public bool HasResources(string resourceId, int amount)
        {
            return GetAmount(resourceId) >= amount;
        }

        public bool CanAfford(Dictionary<ResourceSO, int> requirements)
        {
            foreach (var req in requirements)
            {
                if (!HasResources(req.Key, req.Value))
                    return false;
            }
            return true;
        }

        public Inventory Snapshot()
        {
            var snapshot = new Inventory(_resourceMap);

            foreach (var kvp in _resources)
            {
                snapshot._resources[kvp.Key] = kvp.Value;
            }

            return snapshot;
        }

        public ResourceSO GetResourceById(string id)
        {
            return _resourceMap.TryGetValue(id, out var resource) ? resource : null;
        }

        public bool HasChangesSince(Inventory snapshot)
        {
            if (snapshot == null) return true;

            foreach (var kvp in _resources)
            {
                var snapshotAmount = snapshot.GetAmount(kvp.Key);
                if (snapshotAmount != kvp.Value)
                    return true;
            }

            foreach (var kvp in snapshot._resources)
            {
                if (!_resources.ContainsKey(kvp.Key))
                    return true;
            }

            return false;
        }
    }
}