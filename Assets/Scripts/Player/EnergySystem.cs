using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Player
{
    /// <summary>
    /// Система энергии. Энергия тратится на трансформации и регенерируется со временем.
    /// Это вынуждает игрока стратегически планировать маршрут.
    /// </summary>
    public class EnergySystem : MonoBehaviour
    {
        [Header("Параметры энергии")]
        [Tooltip("Максимальный запас энергии")]
        [SerializeField] private float maxEnergy = 100f;
        [Tooltip("Восстановление энергии в секунду")]
        [SerializeField] private float regenPerSecond = 12f;
        [Tooltip("Задержка перед началом регенерации после траты, в секундах")]
        [SerializeField] private float regenDelay = 0.5f;

        private float _current;
        private float _regenTimer;

        public float Current => _current;
        public float Max => maxEnergy;
        public float Normalized => maxEnergy > 0f ? _current / maxEnergy : 0f;

        private void Awake()
        {
            _current = maxEnergy;
        }

        private void Start()
        {
            // Сообщаем UI начальное значение, чтобы он не показывал 0 на первом кадре.
            GameEvents.RaiseEnergyChanged(_current, maxEnergy);
        }

        private void Update()
        {
            if (_regenTimer > 0f)
            {
                _regenTimer -= Time.deltaTime;
                return;
            }

            if (_current < maxEnergy)
            {
                _current = Mathf.Min(maxEnergy, _current + regenPerSecond * Time.deltaTime);
                GameEvents.RaiseEnergyChanged(_current, maxEnergy);
            }
        }

        /// <summary>Хватает ли энергии на действие?</summary>
        public bool HasEnergy(float amount) => _current >= amount;

        /// <summary>Списывает энергию. Возвращает true, если списание прошло успешно.</summary>
        public bool Spend(float amount)
        {
            if (!HasEnergy(amount)) return false;
            _current -= amount;
            _regenTimer = regenDelay;
            GameEvents.RaiseEnergyChanged(_current, maxEnergy);
            return true;
        }

        /// <summary>Мгновенно возвращает запас энергии — полезно для пикапов или чекпоинтов.</summary>
        public void Restore(float amount)
        {
            _current = Mathf.Min(maxEnergy, _current + amount);
            GameEvents.RaiseEnergyChanged(_current, maxEnergy);
        }
    }
}
