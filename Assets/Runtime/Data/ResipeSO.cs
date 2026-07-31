using System;
using UnityEngine;

namespace CraftPlanner.Data
{
    [Serializable]
    public class RecipeIngredient
    {
        [SerializeField] private ResourceSO _resource;
        [SerializeField] private int _amount;

        public ResourceSO Resource => _resource;
        public int Amount => _amount;
    }

    /// <summary>
    /// Определение рецепта. Создаёт производимый ресурс из ингредиентов.
    /// </summary>
    [CreateAssetMenu(fileName = "NewRecipe", menuName = "Craft/Recipe")]
    public class RecipeSO : ScriptableObject
    {
        [SerializeField] private ResourceSO _result;
        [SerializeField] private int _resultCount = 1;
        [SerializeField] private RecipeIngredient[] _ingredients;
        [SerializeField] private float _durationSeconds = 1f;

        public ResourceSO Result => _result;
        public int ResultCount => _resultCount;
        public RecipeIngredient[] Ingredients => _ingredients;
        public float DurationSeconds => _durationSeconds;

        public bool IsValid => _result != null && _ingredients != null && _ingredients.Length > 0;
    }
}