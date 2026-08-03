using System;
using System.Collections.Generic;
using CraftPlanner.Data;

namespace CraftPlanner.Domain.Inventory
{
    public class Inventory
    {
        private readonly Dictionary<string, int> _resources = new();
        private readonly Dictionary<string, ResourceSO> _resourceMap = new();

        public event Action<ResourceSO, int> OnResourceChanged;
        public IReadOnlyDictionary<string, int> AllResources => _resources;

        public Inventory() { }

        private Inventory(Dictionary<string, int> resources, Dictionary<string, ResourceSO> resourceMap)
        {
            _resources = new Dictionary<string, int>(resources);
            _resourceMap = new Dictionary<string, ResourceSO>(resourceMap);
        }

        public int GetAmount(ResourceSO resource)
        {
            if (resource == null) return 0;
            return _resources.TryGetValue(resource.Id, out var amount) ? amount : 0;
        }

        public int GetAmount(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId)) return 0;
            return _resources.TryGetValue(resourceId, out var amount) ? amount : 0;
        }

        public void AddResource(ResourceSO resource, int amount)
        {
            if (resource == null || amount <= 0) return;

            var current = GetAmount(resource);
            _resources[resource.Id] = current + amount;
            _resourceMap[resource.Id] = resource;
            OnResourceChanged?.Invoke(resource, GetAmount(resource));
        }

        public bool RemoveResource(ResourceSO resource, int amount)
        {
            if (resource == null || amount <= 0) return false;

            var current = GetAmount(resource);
            if (current < amount) return false;

            var newAmount = current - amount;
            if (newAmount == 0)
                _resources.Remove(resource.Id);
            else
                _resources[resource.Id] = newAmount;

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

        public Inventory Snapshot()
        {
            return new Inventory(_resources, _resourceMap);
        }

        public ResourceSO GetResourceById(string id)
        {
            return _resourceMap.TryGetValue(id, out var resource) ? resource : null;
        }
    }
}