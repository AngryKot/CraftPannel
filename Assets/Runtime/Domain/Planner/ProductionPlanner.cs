using System;
using System.Collections.Generic;
using System.Linq;
using CraftPlanner.Data;
using CraftPlanner.Domain.Inventory;
using UnityEngine;

namespace CraftPlanner.Domain.Planner
{
    using Inventory = CraftPlanner.Domain.Inventory.Inventory;

    public class ProductionPlanner
    {
        private readonly RecipeDatabaseSO _recipeDatabase;
        private readonly Inventory _originalInventory;

        public ProductionPlanner(RecipeDatabaseSO recipeDatabase, Inventory inventory)
        {
            _recipeDatabase = recipeDatabase ?? throw new ArgumentNullException(nameof(recipeDatabase));
            _originalInventory = inventory ?? throw new ArgumentNullException(nameof(inventory));

            _recipeDatabase.Initialize();
        }

        public ProductionPlan BuildPlan(ResourceSO targetResource, int requiredAmount)
        {
            if (targetResource == null)
            {
                return ProductionPlan.Error(PlanErrorType.InvalidTarget, "Целевой ресурс не указан");
            }

            if (requiredAmount <= 0)
            {
                return ProductionPlan.Error(PlanErrorType.InvalidTarget, "Требуемое количество должно быть больше 0");
            }

            if (_recipeDatabase.HasCyclicDependencies(out var cyclePath))
            {
                var cycleStr = string.Join(" -> ", cyclePath);
                return ProductionPlan.Error(PlanErrorType.CyclicDependency,
                    $"Обнаружена циклическая зависимость: {cycleStr}");
            }

            var workingInventory = _originalInventory.Snapshot();

            var operations = new List<PlannedOperation>();
            var visited = new HashSet<string>();
            var missingResources = new List<string>();

            var result = BuildPlanRecursiveForTarget(
                targetResource.Id,
                requiredAmount,
                workingInventory,
                operations,
                visited,
                missingResources,
                0);

            if (!result)
            {
                var errorPlan = ProductionPlan.Error(PlanErrorType.MissingBaseResource,
                    $"Недостаточно ресурсов: {string.Join(", ", missingResources)}");
                errorPlan.MissingBaseResources = missingResources;
                return errorPlan;
            }

            operations = operations.OrderBy(o => o.Order).ToList();

            var delta = CalculateDelta(operations);

            var plan = ProductionPlan.Success(operations, delta, targetResource, requiredAmount);
            plan.MissingBaseResources = missingResources;

            return plan;
        }

        private bool BuildPlanRecursive(
            string resourceId,
            int requiredAmount,
            Inventory workingInventory,
            List<PlannedOperation> operations,
            HashSet<string> visited,
            List<string> missingResources,
            int depth)
        {
            if (depth > 100)
            {
                missingResources.Add($"Превышена максимальная глубина рекурсии для {resourceId}");
                return false;
            }

            if (visited.Contains(resourceId))
            {
                missingResources.Add($"Обнаружена циклическая зависимость: {resourceId}");
                return false;
            }

            visited.Add(resourceId);

            var available = workingInventory.GetAmount(resourceId);
            var remaining = requiredAmount - available;

            if (remaining <= 0)
            {
                visited.Remove(resourceId);
                return true;
            }

            var recipe = _recipeDatabase.GetRecipeForResource(resourceId);
            if (recipe == null || !recipe.IsValid)
            {
                var resource = _recipeDatabase.GetResourceById(resourceId);
                if (resource != null && resource.IsBase)
                {
                    missingResources.Add($"{resource.DisplayName} (требуется: {remaining})");
                }
                else
                {
                    missingResources.Add($"Рецепт не найден для {resourceId}");
                }
                visited.Remove(resourceId);
                return false;
            }

            if (recipe.Result.IsBase)
            {
                missingResources.Add($"{recipe.Result.DisplayName} (требуется: {remaining})");
                visited.Remove(resourceId);
                return false;
            }

            var runs = Mathf.CeilToInt((float)remaining / recipe.ResultCount);

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Resource == null) continue;

                var totalNeeded = ingredient.Amount * runs;
                if (!BuildPlanRecursive(
                    ingredient.Resource.Id,
                    totalNeeded,
                    workingInventory,
                    operations,
                    visited,
                    missingResources,
                    depth + 1))
                {
                    visited.Remove(resourceId);
                    return false;
                }
            }

            var operation = new PlannedOperation
            {
                Recipe = recipe,
                RepeatCount = runs,
                TotalDuration = recipe.DurationSeconds * runs,
                Order = operations.Count
            };
            operations.Add(operation);

            var produced = recipe.ResultCount * runs;
            workingInventory.AddResource(recipe.Result, produced);

            visited.Remove(resourceId);
            return true;
        }

        private bool BuildPlanRecursiveForTarget(
            string resourceId,
            int requiredAmount,
            Inventory workingInventory,
            List<PlannedOperation> operations,
            HashSet<string> visited,
            List<string> missingResources,
            int depth)
        {
            if (depth > 100)
            {
                missingResources.Add($"Превышена максимальная глубина рекурсии для {resourceId}");
                return false;
            }

            if (visited.Contains(resourceId))
            {
                missingResources.Add($"Обнаружена циклическая зависимость: {resourceId}");
                return false;
            }

            visited.Add(resourceId);

            var recipe = _recipeDatabase.GetRecipeForResource(resourceId);
            if (recipe == null || !recipe.IsValid)
            {
                var resource = _recipeDatabase.GetResourceById(resourceId);
                if (resource != null && resource.IsBase)
                {
                    missingResources.Add($"{resource.DisplayName} (требуется: {requiredAmount})");
                }
                else
                {
                    missingResources.Add($"Рецепт не найден для {resourceId}");
                }
                visited.Remove(resourceId);
                return false;
            }

            if (recipe.Result.IsBase)
            {
                missingResources.Add($"{recipe.Result.DisplayName} (требуется: {requiredAmount})");
                visited.Remove(resourceId);
                return false;
            }

            var runs = Mathf.CeilToInt((float)requiredAmount / recipe.ResultCount);

            foreach (var ingredient in recipe.Ingredients)
            {
                if (ingredient.Resource == null) continue;

                var totalNeeded = ingredient.Amount * runs;
                if (!BuildPlanRecursive(
                    ingredient.Resource.Id,
                    totalNeeded,
                    workingInventory,
                    operations,
                    visited,
                    missingResources,
                    depth + 1))
                {
                    visited.Remove(resourceId);
                    return false;
                }
            }

            var operation = new PlannedOperation
            {
                Recipe = recipe,
                RepeatCount = runs,
                TotalDuration = recipe.DurationSeconds * runs,
                Order = operations.Count
            };
            operations.Add(operation);

            var produced = recipe.ResultCount * runs;
            workingInventory.AddResource(recipe.Result, produced);

            visited.Remove(resourceId);
            return true;
        }

        private Dictionary<string, int> CalculateDelta(List<PlannedOperation> operations)
        {
            var delta = new Dictionary<string, int>();

            foreach (var op in operations)
            {
                foreach (var ingredient in op.Recipe.Ingredients)
                {
                    var consumed = ingredient.Amount * op.RepeatCount;
                    var id = ingredient.Resource.Id;
                    if (delta.ContainsKey(id))
                        delta[id] -= consumed;
                    else
                        delta[id] = -consumed;
                }

                var produced = op.Recipe.ResultCount * op.RepeatCount;
                var resultId = op.Recipe.Result.Id;
                if (delta.ContainsKey(resultId))
                    delta[resultId] += produced;
                else
                    delta[resultId] = produced;
            }

            return delta;
        }

        public bool CanBuildPlan(ResourceSO target, int amount)
        {
            var plan = BuildPlan(target, amount);
            return plan.IsValid;
        }
    }
}