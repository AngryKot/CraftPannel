using System;
using System.Collections.Generic;
using CraftPlanner.Data;
using CraftPlanner.Domain.Executor;
using CraftPlanner.Domain.Inventory;
using CraftPlanner.Domain.Planner;
using UnityEngine;
using CraftPlanner.UI;

namespace CraftPlanner.Controllers
{
    /// <summary>
    /// Главный контроллер. Mediator между UI и бизнес-логикой.
    /// </summary>
    public class CraftController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private RecipeDatabaseSO _recipeDatabase;

        [Header("References")]
        [SerializeField] private InventoryUI _inventoryUI;
        [SerializeField] private CraftRequestUI _craftRequestUI;
        [SerializeField] private ExecutionUI _executionUI;

        private Inventory _inventory;
        private ProductionPlanner _planner;
        private PlanExecutor _executor;
        private ProductionPlan _lastPlan;

        // События
        public event Action<Dictionary<string, int>> OnInventoryUpdated;
        public event Action<ProductionPlan> OnPlanBuilt;
        public event Action<ExecutionState> OnExecutionStateChanged;
        public event Action<PlannedOperation, float> OnExecutionProgress;

        public Inventory Inventory => _inventory;
        public PlanExecutor Executor => _executor;
        public ProductionPlan LastPlan => _lastPlan;
        public RecipeDatabaseSO RecipeDatabase => _recipeDatabase;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            _inventory = new Inventory();

            if (_recipeDatabase != null)
                _recipeDatabase.Initialize();

            _planner = new ProductionPlanner(_recipeDatabase, _inventory);
            _executor = new PlanExecutor(_inventory, this);

            _executor.OnStateChanged += state =>
            {
                OnExecutionStateChanged?.Invoke(state);
                UpdateUIState(state);
            };

            _executor.OnProgressUpdated += (op, progress) =>
                OnExecutionProgress?.Invoke(op, progress);

            _executor.OnPlanCompleted += plan =>
            {
                OnPlanBuilt?.Invoke(plan);
                // Auto-reset after completion
                _executor.ResetToIdle();
            };

            _executor.OnExecutionFailed += error =>
            {
                Debug.LogError($"Ошибка исполнения: {error}");
                // Auto-reset after failure
                _executor.ResetToIdle();
            };

            _inventory.OnResourceChanged += (resource, amount) =>
            {
                if (_lastPlan != null && _executor.State == ExecutionState.Idle)
                {
                    _lastPlan = null;
                }

                var snapshot = new Dictionary<string, int>(_inventory.AllResources);
                OnInventoryUpdated?.Invoke(snapshot);
            };

            AddStartResources();

            if (_inventoryUI != null) _inventoryUI.Initialize(this);
            if (_craftRequestUI != null) _craftRequestUI.Initialize(this);
            if (_executionUI != null) _executionUI.Initialize(this);

            OnInventoryUpdated?.Invoke(_inventory.AllResources as Dictionary<string, int>);
        }

        private void AddStartResources()
        {
            if (_recipeDatabase == null) return;

            var baseResources = _recipeDatabase.GetBaseResources();
            foreach (var resource in baseResources)
            {
                _inventory.AddResource(resource, 20);
            }
        }

        private void UpdateUIState(ExecutionState state)
        {
            // Дополнительная логика для UI
        }

        public void RequestProduction(ResourceSO targetResource, int amount)
        {
            if (_executor.State == ExecutionState.Running || _executor.State == ExecutionState.Paused)
            {
                Debug.LogWarning("Сначала завершите действубщий план");
                return;
            }

            if (targetResource == null)
            {
                Debug.LogError("Недостаточно ресурса");
                return;
            }

            if (amount <= 0)
            {
                Debug.LogError("Количество должно быть больше нуля");
                return;
            }

            try
            {
                _lastPlan = _planner.BuildPlan(targetResource, amount);
                OnPlanBuilt?.Invoke(_lastPlan);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Ошибка плана: {ex.Message}\n{ex.StackTrace}");
            }
        }

        public void StartExecution()
        {
            if (_lastPlan == null)
            {
                Debug.LogWarning("Нет плана для выполнения");
                return;
            }

            if (_executor.StartExecution(_lastPlan))
            {
                Debug.Log("Выполнение запущено");
            }
        }

        public void AddBaseResource(ResourceSO resource, int amount)
        {
            if (_executor.State == ExecutionState.Running || _executor.State == ExecutionState.Paused)
            {
                Debug.LogWarning("Нельзя изменять инвентарь во время выполнения");
                return;
            }

            if (resource != null && resource.IsBase)
            {
                _inventory.AddResource(resource, amount);
                _lastPlan = null; // План устарел
            }
        }

        public void RemoveBaseResource(ResourceSO resource, int amount)
        {
            if (_executor.State == ExecutionState.Running || _executor.State == ExecutionState.Paused)
            {
                Debug.LogWarning("Нельзя изменять инвентарь во время выполнения");
                return;
            }

            if (resource != null && resource.IsBase)
            {
                if (_inventory.RemoveResource(resource, amount))
                {
                    _lastPlan = null; // План устарел
                }
            }
        }

        public void PauseExecution() => _executor.Pause();
        public void ResumeExecution() => _executor.Resume();
        public void StopExecution() => _executor.Stop();

        public bool IsPlanValid() => _executor.CanStartPlan(_lastPlan);

        public List<ResourceSO> GetBaseResources() => _recipeDatabase?.GetBaseResources() ?? new List<ResourceSO>();
        public List<ResourceSO> GetProducibleResources() => _recipeDatabase?.GetProducibleResources() ?? new List<ResourceSO>();

        private void OnDestroy()
        {
            _executor?.Stop();
        }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLogMessage;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLogMessage;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error)
            {
                Debug.LogError($"=== ERROR DETECTED ===\n{condition}\n{stackTrace}");
            }
        }
    }
}