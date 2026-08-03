using System.Collections.Generic;
using CraftPlanner.Controllers;
using CraftPlanner.Data;
using CraftPlanner.Domain.Executor;
using CraftPlanner.Domain.Planner;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftPlanner.UI
{
    public class CraftRequestUI : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown _resourceDropdown;
        [SerializeField] private TMP_InputField _amountInput;
        [SerializeField] private Button _buildPlanButton;
        [SerializeField] private Button _startExecutionButton;
        [SerializeField] private TextMeshProUGUI _planDetailsText;
        [SerializeField] private TextMeshProUGUI _statusText;

        private CraftController _controller;
        private List<ResourceSO> _producibleResources = new();
        private ResourceSO _selectedTarget;

        public void Initialize(CraftController controller)
        {
            _controller = controller;

            _buildPlanButton.onClick.AddListener(BuildPlan);
            _startExecutionButton.onClick.AddListener(StartExecution);

            _controller.OnPlanBuilt += DisplayPlan;
            _controller.OnExecutionStateChanged += OnExecutionStateChanged;

            PopulateDropdown();
        }

        private void PopulateDropdown()
        {
            if (_controller == null) return;

            _producibleResources = _controller.GetProducibleResources();

            _resourceDropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>();

            foreach (var resource in _producibleResources)
            {
                options.Add(new TMP_Dropdown.OptionData(resource.DisplayName));
            }

            _resourceDropdown.AddOptions(options);
            _resourceDropdown.onValueChanged.AddListener(index =>
            {
                if (index >= 0 && index < _producibleResources.Count)
                    _selectedTarget = _producibleResources[index];
            });

            if (_producibleResources.Count > 0)
            {
                _selectedTarget = _producibleResources[0];
                _resourceDropdown.value = 0;
            }
        }

        private void BuildPlan()
        {
            if (_selectedTarget == null)
            {
                _statusText.text = "Выберите ресурс";
                return;
            }

            if (!int.TryParse(_amountInput.text, out var amount) || amount <= 0)
            {
                _statusText.text = "Введите корректное количество";
                return;
            }

            _controller.RequestProduction(_selectedTarget, amount);
        }

        private void StartExecution()
        {
            if (_controller == null) return;

            if (_controller.IsPlanValid())
            {
                _controller.StartExecution();
            }
            else
            {
                _statusText.text = "Невозможный план";
            }
        }

        private void DisplayPlan(ProductionPlan plan)
        {
            if (!plan.IsValid)
            {
                _planDetailsText.text = $"{plan.ErrorMessage}";
                _statusText.text = "План не может быть построен";
                return;
            }

            var details = $"План построен!\n";
            details += $"Цель: {plan.TargetResource?.DisplayName} x{plan.RequiredAmount}\n";
            details += $"Операций: {plan.TotalOperations}\n";
            details += $"Общая длительность: {plan.TotalDuration:F1} сек\n\n";

            details += "Операции (порядок выполнения):\n";
            for (int i = 0; i < plan.Operations.Count; i++)
            {
                var op = plan.Operations[i];
                details += $"  {i + 1}. {op.DisplayName} x{op.RepeatCount} ";
                details += $"({op.TotalDuration:F1} сек)\n";
                details += $"     → ";
                var ingredients = new List<string>();
                foreach (var ing in op.Recipe.Ingredients)
                {
                    ingredients.Add($"{ing.Resource.DisplayName} x{ing.Amount * op.RepeatCount}");
                }
                details += string.Join(", ", ingredients);
                details += $"\n     ← {op.DisplayName} x{op.OutputAmount}\n";
            }

            details += "\nИтоговое изменение инвентаря (дельта):\n";
            foreach (var kvp in plan.Delta)
            {
                var resource = _controller.RecipeDatabase?.GetResourceById(kvp.Key);
                var name = resource?.DisplayName ?? kvp.Key;
                var sign = kvp.Value > 0 ? "+" : "";
                var color = kvp.Value > 0 ? "#4CAF50" : "#f44336";
                details += $"  <color={color}>{sign}{kvp.Value}</color> {name}\n";
            }

            if (plan.HasMissingResources)
            {
                details += "\nНедостающие базовые ресурсы:\n";
                foreach (var missing in plan.MissingBaseResources)
                {
                    details += $"  • {missing}\n";
                }
            }

            _planDetailsText.text = details;
            _statusText.text = plan.HasMissingResources ?
                "Недостаточно базовых ресурсов для выполнения" :
                "План готов к выполнению";
        }

        private void OnExecutionStateChanged(ExecutionState state)
        {
            _statusText.text = state switch
            {
                ExecutionState.Running => "В процессе...",
                ExecutionState.Paused => "Пауза",
                ExecutionState.Completed => "Выполнено!",
                ExecutionState.Failed => "Ошщибка изготовки",
                _ => _statusText.text
            };

            _buildPlanButton.interactable = state != ExecutionState.Running && state != ExecutionState.Paused;
            _startExecutionButton.interactable = state == ExecutionState.Idle || state == ExecutionState.Completed;
        }
    }
}