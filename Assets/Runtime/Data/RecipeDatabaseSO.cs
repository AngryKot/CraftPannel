using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CraftPlanner.Data
{
    /// <summary>
    /// База данных всех рецептов. Главный источник данных.
    /// Проверяет целостность данных при старте.
    /// </summary>
    [CreateAssetMenu(fileName = "RecipeDatabase", menuName = "Craft/Recipe Database")]
    public class RecipeDatabaseSO : ScriptableObject
    {
        [SerializeField] private List<RecipeSO> _recipes;

        public IReadOnlyList<RecipeSO> Recipes => _recipes;

        private Dictionary<string, RecipeSO> _recipeMap;
        private Dictionary<string, ResourceSO> _resourceMap;

        /// <summary>
        /// Инициализация кэшей (вызывается при старте)
        /// </summary>
        public void Initialize()
        {
            _recipeMap = new Dictionary<string, RecipeSO>();
            _resourceMap = new Dictionary<string, ResourceSO>();

            foreach (var recipe in _recipes)
            {
                if (recipe == null || !recipe.IsValid) continue;

                _recipeMap[recipe.Result.Id] = recipe;

                // Добавляем все ресурсы из рецептов в карту
                _resourceMap[recipe.Result.Id] = recipe.Result;
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (ingredient.Resource != null)
                        _resourceMap[ingredient.Resource.Id] = ingredient.Resource;
                }
            }
        }

        public RecipeSO GetRecipeForResource(ResourceSO resource)
        {
            if (resource == null || _recipeMap == null) return null;
            return _recipeMap.TryGetValue(resource.Id, out var recipe) ? recipe : null;
        }

        public RecipeSO GetRecipeForResource(string resourceId)
        {
            if (string.IsNullOrEmpty(resourceId) || _recipeMap == null) return null;
            return _recipeMap.TryGetValue(resourceId, out var recipe) ? recipe : null;
        }

        public ResourceSO GetResourceById(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError("GetResourceById: id is null or empty");
                return null;
            }

            if (_resourceMap == null)
            {
                Debug.LogError("GetResourceById: _resourceMap is null, call Initialize() first");
                return null;
            }

            if (_resourceMap.TryGetValue(id, out var resource))
            {
                return resource;
            }

            Debug.LogWarning($"GetResourceById: Resource with id '{id}' not found");
            return null;
        }

        public bool HasRecipeForResource(ResourceSO resource)
        {
            return resource != null && _recipeMap != null && _recipeMap.ContainsKey(resource.Id);
        }

        /// <summary>
        /// Проверка на циклические зависимости
        /// </summary>
        public bool HasCyclicDependencies(out List<string> cyclePath)
        {
            cyclePath = new List<string>();
            if (_recipeMap == null) Initialize();

            var visited = new HashSet<string>();
            var visiting = new HashSet<string>();

            foreach (var recipe in _recipes)
            {
                if (recipe == null || !recipe.IsValid) continue;
                if (visited.Contains(recipe.Result.Id)) continue;

                if (HasCycle(recipe.Result.Id, visited, visiting, out var path))
                {
                    cyclePath = path;
                    return true;
                }
            }

            return false;
        }

        private bool HasCycle(string resourceId, HashSet<string> visited, HashSet<string> visiting, out List<string> path)
        {
            path = new List<string>();

            if (visiting.Contains(resourceId))
            {
                path.Add(resourceId);
                return true;
            }

            if (visited.Contains(resourceId))
                return false;

            visiting.Add(resourceId);

            var recipe = GetRecipeForResource(resourceId);
            if (recipe != null)
            {
                foreach (var ingredient in recipe.Ingredients)
                {
                    if (HasCycle(ingredient.Resource.Id, visited, visiting, out var subPath))
                    {
                        path.Add(resourceId);
                        path.AddRange(subPath);
                        visiting.Remove(resourceId);
                        return true;
                    }
                }
            }

            visiting.Remove(resourceId);
            visited.Add(resourceId);
            return false;
        }

        /// <summary>
        /// Получить все производимые ресурсы
        /// </summary>
        public List<ResourceSO> GetProducibleResources()
        {
            var result = new List<ResourceSO>();
            foreach (var recipe in _recipes)
            {
                if (recipe != null && recipe.IsValid && recipe.Result != null)
                    result.Add(recipe.Result);
            }
            return result;
        }

        /// <summary>
        /// Получить все базовые ресурсы
        /// </summary>
        public List<ResourceSO> GetBaseResources()
        {
            var result = new List<ResourceSO>();
            if (_resourceMap == null) Initialize();

            foreach (var kvp in _resourceMap)
            {
                if (kvp.Value.IsBase)
                    result.Add(kvp.Value);
            }
            return result;
        }
    }
}