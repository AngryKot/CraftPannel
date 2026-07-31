using CraftPlanner.Controllers;
using CraftPlanner.Domain.Executor;
using CraftPlanner.Domain.Planner;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CraftPlanner.UI
{
    public class ExecutionUI : MonoBehaviour
    {
        [SerializeField] private Slider _progressSlider;
        [SerializeField] private TextMeshProUGUI _operationNameText;
        [SerializeField] private TextMeshProUGUI _progressText;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _stopButton;

        private CraftController _controller;

        public void Initialize(CraftController controller)
        {
            _controller = controller;

            _pauseButton.onClick.AddListener(() => _controller.PauseExecution());
            _resumeButton.onClick.AddListener(() => _controller.ResumeExecution());
            _stopButton.onClick.AddListener(() => _controller.StopExecution());

            _controller.OnExecutionProgress += UpdateProgress;
            _controller.OnExecutionStateChanged += OnStateChanged;
            _controller.OnPlanBuilt += OnPlanBuilt;

            _pauseButton.interactable = false;
            _resumeButton.interactable = false;
            _progressSlider.value = 0;
            _operationNameText.text = "Ожидание...";
            _progressText.text = "0%";
        }

        private void UpdateProgress(PlannedOperation operation, float progress)
        {
            _progressSlider.value = progress;
            _operationNameText.text = operation?.DisplayName ?? "Выполняется...";
            _progressText.text = $"{progress:P0}";
        }

        private void OnStateChanged(ExecutionState state)
        {
            _pauseButton.interactable = state == ExecutionState.Running;
            _resumeButton.interactable = state == ExecutionState.Paused;
            _stopButton.interactable = state == ExecutionState.Running || state == ExecutionState.Paused;

            switch (state)
            {
                case ExecutionState.Running:
                    _operationNameText.text = "Выполняется...";
                    break;
                case ExecutionState.Paused:
                    _operationNameText.text = "На паузе";
                    break;
                case ExecutionState.Completed:
                    _progressSlider.value = 1f;
                    _operationNameText.text = "Выполнено!";
                    _progressText.text = "100%";
                    break;
                case ExecutionState.Failed:
                    _progressSlider.value = 0f;
                    _operationNameText.text = "Ошибка";
                    _progressText.text = "0%";
                    break;
                case ExecutionState.Idle:
                    _operationNameText.text = "Ожидание...";
                    _progressText.text = "0%";
                    break;
            }
        }

        private void OnPlanBuilt(ProductionPlan plan)
        {
            if (plan == null || !plan.IsValid)
            {
                _progressSlider.value = 0;
                _operationNameText.text = "План не построен";
                _progressText.text = "0%";
            }
            else
            {
                _progressSlider.value = 0;
                _operationNameText.text = "План готов";
                _progressText.text = "0%";
            }
        }
    }
}