using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CraftPlanner.Data;
using CraftPlanner.Domain.Inventory;
using CraftPlanner.Domain.Planner;
using UnityEngine;

namespace CraftPlanner.Domain.Executor
{
    using Inventory = CraftPlanner.Domain.Inventory.Inventory;

    public enum ExecutionState
    {
        Idle,
        Running,
        Paused,
        Completed,
        Failed
    }

    public class PlanExecutor
    {
        private readonly Inventory _inventory;
        private readonly MonoBehaviour _coroutineHost;

        private ProductionPlan _currentPlan;
        private Queue<PlannedOperation> _operationQueue;
        private PlannedOperation _currentOperation;
        private float _currentOperationProgress;
        private Coroutine _executionCoroutine;
        private ExecutionState _state = ExecutionState.Idle;

        public event Action<ExecutionState> OnStateChanged;
        public event Action<PlannedOperation, float> OnProgressUpdated;
        public event Action<ProductionPlan> OnPlanCompleted;
        public event Action<string> OnExecutionFailed;

        public ExecutionState State => _state;
        public ProductionPlan CurrentPlan => _currentPlan;
        public PlannedOperation CurrentOperation => _currentOperation;
        public float CurrentProgress => _currentOperationProgress;
        public bool IsRunning => _state == ExecutionState.Running || _state == ExecutionState.Paused;

        public PlanExecutor(Inventory inventory, MonoBehaviour coroutineHost)
        {
            _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            _coroutineHost = coroutineHost ?? throw new ArgumentNullException(nameof(coroutineHost));
        }

        public bool IsPlanValid(ProductionPlan plan)
        {
            if (plan == null || !plan.IsValid)
                return false;

            foreach (var op in plan.Operations)
            {
                foreach (var ingredient in op.Recipe.Ingredients)
                {
                    var needed = ingredient.Amount * op.RepeatCount;
                    if (!_inventory.HasResources(ingredient.Resource, needed))
                        return false;
                }
            }

            return true;
        }

        public bool StartExecution(ProductionPlan plan)
        {
            if (_state == ExecutionState.Running)
            {
                Debug.LogWarning("Уже выполняется другой план");
                return false;
            }

            if (plan == null || !plan.IsValid)
            {
                OnExecutionFailed?.Invoke("План недействителен");
                return false;
            }

            if (!IsPlanValid(plan))
            {
                OnExecutionFailed?.Invoke("План устарел. Инвентарь изменился");
                return false;
            }

            _currentPlan = plan;
            _operationQueue = new Queue<PlannedOperation>(plan.Operations);
            _currentOperation = null;
            _currentOperationProgress = 0f;

            _state = ExecutionState.Running;
            OnStateChanged?.Invoke(_state);

            _executionCoroutine = _coroutineHost.StartCoroutine(ExecuteCoroutine());
            return true;
        }

        private IEnumerator ExecuteCoroutine()
        {
            while (_operationQueue.Count > 0)
            {
                while (_state == ExecutionState.Paused)
                {
                    yield return null;
                }

                if (_state != ExecutionState.Running)
                    yield break;

                _currentOperation = _operationQueue.Dequeue();
                _currentOperationProgress = 0f;

                var duration = _currentOperation.TotalDuration;

                while (_currentOperationProgress < duration)
                {
                    while (_state == ExecutionState.Paused)
                    {
                        yield return null;
                    }

                    if (_state != ExecutionState.Running)
                        yield break;

                    var delta = Time.deltaTime;
                    _currentOperationProgress += delta;
                    OnProgressUpdated?.Invoke(_currentOperation, _currentOperationProgress / duration);

                    yield return null;
                }

                if (!ApplyOperation(_currentOperation))
                {
                    _state = ExecutionState.Failed;
                    OnStateChanged?.Invoke(_state);
                    OnExecutionFailed?.Invoke("Недостаточно ресурсов для выполнения операции");
                    yield break;
                }

                _currentOperation = null;
                _currentOperationProgress = 0f;
            }

            _state = ExecutionState.Completed;
            OnStateChanged?.Invoke(_state);
            OnPlanCompleted?.Invoke(_currentPlan);

            ResetAfterExecution();
        }

        private void ResetAfterExecution()
        {
            _currentPlan = null;
            _operationQueue = null;
            _currentOperation = null;
            _currentOperationProgress = 0f;
        }

        private bool ApplyOperation(PlannedOperation operation)
        {
            foreach (var ingredient in operation.Recipe.Ingredients)
            {
                var needed = ingredient.Amount * operation.RepeatCount;
                if (!_inventory.RemoveResource(ingredient.Resource, needed))
                    return false;
            }

            var produced = operation.Recipe.ResultCount * operation.RepeatCount;
            _inventory.AddResource(operation.Recipe.Result, produced);

            return true;
        }

        public void Pause()
        {
            if (_state == ExecutionState.Running)
            {
                _state = ExecutionState.Paused;
                OnStateChanged?.Invoke(_state);
            }
        }

        public void Resume()
        {
            if (_state == ExecutionState.Paused)
            {
                _state = ExecutionState.Running;
                OnStateChanged?.Invoke(_state);
            }
        }

        public void Stop()
        {
            if (_executionCoroutine != null)
            {
                _coroutineHost.StopCoroutine(_executionCoroutine);
                _executionCoroutine = null;
            }

            _state = ExecutionState.Idle;
            _currentOperation = null;
            _currentOperationProgress = 0f;
            _currentPlan = null;
            _operationQueue = null;
            OnStateChanged?.Invoke(_state);
        }

        public bool CanStartPlan(ProductionPlan plan)
        {
            return (_state == ExecutionState.Idle || _state == ExecutionState.Completed)
                   && plan != null
                   && plan.IsValid
                   && IsPlanValid(plan);
        }

        public void Clear()
        {
            Stop();
            _currentPlan = null;
            _operationQueue?.Clear();
        }

        public void ResetToIdle()
        {
            if (_state == ExecutionState.Completed || _state == ExecutionState.Failed)
            {
                _state = ExecutionState.Idle;
                _currentPlan = null;
                _operationQueue = null;
                _currentOperation = null;
                _currentOperationProgress = 0f;
                OnStateChanged?.Invoke(_state);
            }
        }
    }
}