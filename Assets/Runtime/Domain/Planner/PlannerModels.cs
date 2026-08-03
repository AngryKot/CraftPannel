using System;
using System.Collections.Generic;
using System.Linq;
using CraftPlanner.Data;

namespace CraftPlanner.Domain.Planner
{
    public enum PlanErrorType
    {
        None,
        MissingBaseResource,
        RecipeNotFound,
        CyclicDependency,
        InsufficientInventory,
        InvalidTarget,
        InvalidData,
        PlanStale
    }

    /// <summary>
    /// Результат построения плана
    /// </summary>
    public class ProductionPlan
    {
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public PlanErrorType ErrorType { get; set; }

        public List<PlannedOperation> Operations { get; set; } = new();
        public Dictionary<string, int> Delta { get; set; } = new();
        public List<string> MissingBaseResources { get; set; } = new();
        public float TotalDuration { get; set; }
        public ResourceSO TargetResource { get; set; }
        public int RequiredAmount { get; set; }

        public bool HasMissingResources => MissingBaseResources.Count > 0;
        public int TotalOperations => Operations.Count;

        public static ProductionPlan Success(
            List<PlannedOperation> operations,
            Dictionary<string, int> delta,
            ResourceSO target,
            int requiredAmount)
        {
            return new ProductionPlan
            {
                IsValid = true,
                Operations = operations,
                Delta = delta,
                TotalDuration = operations.Sum(o => o.TotalDuration),
                TargetResource = target,
                RequiredAmount = requiredAmount,
                ErrorType = PlanErrorType.None
            };
        }

        public static ProductionPlan Error(PlanErrorType type, string message)
        {
            return new ProductionPlan
            {
                IsValid = false,
                ErrorType = type,
                ErrorMessage = message
            };
        }

        /// <summary>
        /// Проверить, можно ли выполнить план с текущим инвентарём
        /// </summary>
        public bool IsExecutableWith(Inventory.Inventory inventory)
        {
            if (!IsValid) return false;

            foreach (var op in Operations)
            {
                foreach (var ingredient in op.Recipe.Ingredients)
                {
                    var needed = ingredient.Amount * op.RepeatCount;
                    if (!inventory.HasResources(ingredient.Resource, needed))
                        return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Одна операция в плане производства
    /// </summary>
    public class PlannedOperation
    {
        public RecipeSO Recipe { get; set; }
        public int RepeatCount { get; set; }
        public float TotalDuration { get; set; }
        public int Order { get; set; }

        public string DisplayName => Recipe?.Result?.DisplayName ?? "Неизвестный";
        public int OutputAmount => Recipe != null ? Recipe.ResultCount * RepeatCount : 0;

        public Dictionary<string, int> RequiredIngredients =>
            Recipe?.Ingredients.ToDictionary(
                i => i.Resource.Id,
                i => i.Amount * RepeatCount) ?? new Dictionary<string, int>();
    }
}