using System.Collections.Generic;
using CraftPlanner.Data;
using CraftPlanner.Domain.Inventory;
using CraftPlanner.Domain.Planner;
using NUnit.Framework;
using UnityEngine;

namespace CraftPlanner.Tests.PlayMode
{
    public class PlannerTests
    {
        private RecipeDatabaseSO _database;
        private Inventory _inventory;
        private ProductionPlanner _planner;

        private ResourceSO _wood;
        private ResourceSO _plank;
        private ResourceSO _table;
        private ResourceSO _ironOre;
        private ResourceSO _coal;
        private ResourceSO _ironIngot;
        private ResourceSO _ironPlate;
        private ResourceSO _screw;
        private ResourceSO _ironGear;

        [SetUp]
        public void Setup()
        {
            _wood = CreateResource("wood", "Древесина", true);
            _plank = CreateResource("plank", "Доска", false);
            _table = CreateResource("table", "Стол", false);
            _ironOre = CreateResource("iron_ore", "Железная руда", true);
            _coal = CreateResource("coal", "Уголь", true);
            _ironIngot = CreateResource("iron_ingot", "Железный слиток", false);
            _ironPlate = CreateResource("iron_plate", "Железная пластина", false);
            _screw = CreateResource("screw", "Винт", false);
            _ironGear = CreateResource("iron_gear", "Железная шестерня", false);

            var recipePlank = CreateRecipe(_plank, 2, new[] { (_wood, 1) }, 1f);
            var recipeTable = CreateRecipe(_table, 1, new[] { (_plank, 4) }, 2f);
            var recipeIngot = CreateRecipe(_ironIngot, 2, new[] { (_ironOre, 1), (_coal, 1) }, 2f);
            var recipePlate = CreateRecipe(_ironPlate, 1, new[] { (_ironIngot, 2) }, 1.5f);
            var recipeScrew = CreateRecipe(_screw, 4, new[] { (_ironIngot, 1) }, 1f);
            var recipeGear = CreateRecipe(_ironGear, 1, new[] { (_ironPlate, 2), (_screw, 4) }, 3f);

            _database = ScriptableObject.CreateInstance<RecipeDatabaseSO>();
            SetPrivateField(_database, "_recipes", new List<RecipeSO>
                { recipePlank, recipeTable, recipeIngot, recipePlate, recipeScrew, recipeGear });
            _database.Initialize();

            _inventory = new Inventory();
            _planner = new ProductionPlanner(_database, _inventory);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_database);
        }

        [Test]
        public void SimpleChain_ProducesCorrectPlan()
        {
            _inventory.AddResource(_wood, 10);
            var plan = _planner.BuildPlan(_table, 1);

            Assert.IsTrue(plan.IsValid);
            Assert.AreEqual(2, plan.Operations.Count);
            Assert.AreEqual(_plank, plan.Operations[0].Recipe.Result);
            Assert.AreEqual(_table, plan.Operations[1].Recipe.Result);
        }

        [Test]
        public void MultiLevelChain_ProducesCorrectPlan()
        {
            _inventory.AddResource(_ironOre, 10);
            _inventory.AddResource(_coal, 10);
            var plan = _planner.BuildPlan(_ironGear, 1);

            Assert.IsTrue(plan.IsValid);
            Assert.IsTrue(plan.Operations.Count >= 3);

            var resources = new HashSet<string>();
            foreach (var op in plan.Operations)
            {
                resources.Add(op.Recipe.Result.Id);
            }

            Assert.IsTrue(resources.Contains(_ironIngot.Id));
            Assert.IsTrue(resources.Contains(_ironPlate.Id));
            Assert.IsTrue(resources.Contains(_screw.Id));
            Assert.IsTrue(resources.Contains(_ironGear.Id));
        }

        [Test]
        public void UsesExistingResources()
        {
            _inventory.AddResource(_wood, 10);
            _inventory.AddResource(_plank, 2);

            var plan = _planner.BuildPlan(_table, 1);

            Assert.IsTrue(plan.IsValid);
            Assert.AreEqual(2, plan.Operations.Count);
            Assert.AreEqual(_plank, plan.Operations[0].Recipe.Result);
            Assert.AreEqual(_table, plan.Operations[1].Recipe.Result);
            Assert.AreEqual(1, plan.Operations[0].RepeatCount);
        }

        [Test]
        public void MultipleResultCount_CalculatesCorrectRuns()
        {
            _inventory.AddResource(_wood, 10);
            var plan = _planner.BuildPlan(_plank, 5);

            Assert.IsTrue(plan.IsValid);
            Assert.AreEqual(3, plan.Operations[0].RepeatCount);
        }

        [Test]
        public void MissingBaseResource_ReturnsError()
        {
            var plan = _planner.BuildPlan(_table, 1);

            Assert.IsFalse(plan.IsValid);
            Assert.AreEqual(PlanErrorType.MissingBaseResource, plan.ErrorType);
            Assert.IsNotNull(plan.MissingBaseResources);
            Assert.IsTrue(plan.MissingBaseResources.Count > 0);
        }

        [Test]
        public void RecipeNotFound_ReturnsError()
        {
            var unknownResource = CreateResource("unknown", "Неизвестный", false);
            var plan = _planner.BuildPlan(unknownResource, 1);

            Assert.IsFalse(plan.IsValid);
            Assert.AreEqual(PlanErrorType.MissingBaseResource, plan.ErrorType);
        }

        [Test]
        public void CyclicDependency_Detected()
        {
            var resA = CreateResource("res_a", "Ресурс A", false);
            var resB = CreateResource("res_b", "Ресурс B", false);

            var recipeA = CreateRecipe(resA, 1, new[] { (resB, 1) }, 1f);
            var recipeB = CreateRecipe(resB, 1, new[] { (resA, 1) }, 1f);

            var db = ScriptableObject.CreateInstance<RecipeDatabaseSO>();
            SetPrivateField(db, "_recipes", new List<RecipeSO> { recipeA, recipeB });
            db.Initialize();

            var planner = new ProductionPlanner(db, _inventory);
            var plan = planner.BuildPlan(resA, 1);

            Assert.IsFalse(plan.IsValid);
            Assert.AreEqual(PlanErrorType.CyclicDependency, plan.ErrorType);

            Object.DestroyImmediate(db);
        }

        [Test]
        public void PlanIsDeterministic()
        {
            _inventory.AddResource(_wood, 10);
            var plan1 = _planner.BuildPlan(_table, 1);
            var plan2 = _planner.BuildPlan(_table, 1);

            Assert.AreEqual(plan1.Operations.Count, plan2.Operations.Count);
            for (int i = 0; i < plan1.Operations.Count; i++)
            {
                Assert.AreEqual(plan1.Operations[i].Recipe, plan2.Operations[i].Recipe);
                Assert.AreEqual(plan1.Operations[i].RepeatCount, plan2.Operations[i].RepeatCount);
            }
            Assert.AreEqual(plan1.Delta.Count, plan2.Delta.Count);
        }

        [Test]
        public void InventoryNotChangedDuringPlanning()
        {
            _inventory.AddResource(_wood, 10);
            var initialWood = _inventory.GetAmount(_wood);
            _planner.BuildPlan(_table, 1);
            Assert.AreEqual(initialWood, _inventory.GetAmount(_wood));
        }

        [Test]
        public void ComplexChain_UsesCorrectAmounts()
        {
            _inventory.AddResource(_ironOre, 20);
            _inventory.AddResource(_coal, 20);
            var plan = _planner.BuildPlan(_ironGear, 2);

            Assert.IsTrue(plan.IsValid);
            Assert.IsTrue(plan.Delta.ContainsKey(_ironGear.Id));
            Assert.AreEqual(2, plan.Delta[_ironGear.Id]);
        }

        [Test]
        public void PlanRemainsValidAfterBuild()
        {
            _inventory.AddResource(_wood, 10);
            var plan = _planner.BuildPlan(_table, 1);

            Assert.IsTrue(plan.IsValid);

            var tempInventory = _inventory.Snapshot();
            bool executable = true;

            foreach (var op in plan.Operations)
            {
                foreach (var ingredient in op.Recipe.Ingredients)
                {
                    var needed = ingredient.Amount * op.RepeatCount;
                    if (!tempInventory.HasResources(ingredient.Resource, needed))
                    {
                        executable = false;
                        break;
                    }
                }

                if (!executable) break;

                foreach (var ingredient in op.Recipe.Ingredients)
                {
                    var needed = ingredient.Amount * op.RepeatCount;
                    tempInventory.RemoveResource(ingredient.Resource, needed);
                }
                var produced = op.Recipe.ResultCount * op.RepeatCount;
                tempInventory.AddResource(op.Recipe.Result, produced);
            }

            Assert.IsTrue(executable);
        }

        private ResourceSO CreateResource(string id, string displayName, bool isBase)
        {
            var resource = ScriptableObject.CreateInstance<ResourceSO>();
            SetPrivateField(resource, "_id", id);
            SetPrivateField(resource, "_displayName", displayName);
            SetPrivateField(resource, "_isBase", isBase);
            return resource;
        }

        private RecipeSO CreateRecipe(ResourceSO result, int resultCount,
            (ResourceSO resource, int amount)[] ingredients, float duration)
        {
            var recipe = ScriptableObject.CreateInstance<RecipeSO>();

            var ingredientList = new List<RecipeIngredient>();
            foreach (var (resource, amount) in ingredients)
            {
                var ingredient = new RecipeIngredient();
                SetPrivateField(ingredient, "_resource", resource);
                SetPrivateField(ingredient, "_amount", amount);
                ingredientList.Add(ingredient);
            }

            SetPrivateField(recipe, "_result", result);
            SetPrivateField(recipe, "_resultCount", resultCount);
            SetPrivateField(recipe, "_ingredients", ingredientList.ToArray());
            SetPrivateField(recipe, "_durationSeconds", duration);

            return recipe;
        }

        private void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(obj, value);
        }
    }
}