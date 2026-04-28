using UnityEngine;
using UnityEngine.UI;
using ProjectScale.Core;

namespace ProjectScale.UI
{
    /// <summary>
    /// Главный HUD: полоса энергии, индикатор состояния масштаба, счётчик коллектаблов
    /// и сообщение о победе. Подписывается на глобальные события.
    /// Используем встроенный UnityEngine.UI.Text (а не TMP), чтобы не требовать
    /// импорта пакета TextMeshPro Essentials при первом запуске.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        [Header("Энергия")]
        [SerializeField] private Image energyFill;
        [SerializeField] private Text energyLabel;

        [Header("Состояние масштаба")]
        [SerializeField] private Text scaleLabel;
        [SerializeField] private Image scaleIcon;
        [SerializeField] private Color smallColor  = new Color(0.4f, 0.8f, 1f);
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color bigColor    = new Color(1f, 0.6f, 0.2f);

        [Header("Цели")]
        [SerializeField] private Text collectibleLabel;

        [Header("Победа")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private Text winLabel;
        [SerializeField] private Text winSubtitle;

        private void OnEnable()
        {
            GameEvents.OnEnergyChanged += UpdateEnergy;
            GameEvents.OnScaleChanged += UpdateScale;
            GameEvents.OnCollectibleGathered += UpdateCollectibles;
            GameEvents.OnLevelCompleted += ShowWin;
        }

        private void OnDisable()
        {
            GameEvents.OnEnergyChanged -= UpdateEnergy;
            GameEvents.OnScaleChanged -= UpdateScale;
            GameEvents.OnCollectibleGathered -= UpdateCollectibles;
            GameEvents.OnLevelCompleted -= ShowWin;
        }

        private void Start()
        {
            if (winPanel != null) winPanel.SetActive(false);
            int total = GameManager.Instance != null ? GameManager.Instance.CollectiblesTotal : 0;
            UpdateCollectibles(0, total);
        }

        private void UpdateEnergy(float current, float max)
        {
            if (energyFill != null) energyFill.fillAmount = max > 0f ? current / max : 0f;
            if (energyLabel != null) energyLabel.text = $"{Mathf.RoundToInt(current)}/{Mathf.RoundToInt(max)}";
        }

        private void UpdateScale(ScaleState _, ScaleState newState)
        {
            if (scaleLabel != null)
            {
                scaleLabel.text = newState switch
                {
                    ScaleState.Small  => "МАЛЫЙ",
                    ScaleState.Normal => "НОРМАЛЬНЫЙ",
                    ScaleState.Big    => "БОЛЬШОЙ",
                    _ => "?"
                };
            }
            if (scaleIcon != null)
            {
                scaleIcon.color = newState switch
                {
                    ScaleState.Small  => smallColor,
                    ScaleState.Normal => normalColor,
                    ScaleState.Big    => bigColor,
                    _ => Color.white
                };
            }
        }

        private void UpdateCollectibles(int collected, int total)
        {
            if (collectibleLabel != null)
                collectibleLabel.text = $"Кристаллы: {collected}/{total}";
        }

        private void ShowWin()
        {
            if (winPanel != null) winPanel.SetActive(true);

            // Если это последний уровень — показываем "ИГРА ПРОЙДЕНА", иначе
            // подсказку про автопереход. GameManager после задержки сам перейдёт.
            if (winLabel != null)
            {
                winLabel.text = LevelManager.IsLast ? "ИГРА ПРОЙДЕНА!" : "УРОВЕНЬ ПРОЙДЕН!";
            }
            if (winSubtitle != null)
            {
                winSubtitle.text = LevelManager.IsLast
                    ? "Спасибо за игру. Через мгновение — меню."
                    : $"Дальше: {LevelManager.Get(LevelManager.CurrentLevel + 1).DisplayName}";
            }
        }
    }
}
