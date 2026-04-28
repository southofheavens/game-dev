using UnityEngine;
using Cinemachine;
using ProjectScale.Core;

namespace ProjectScale.Effects
{
    /// <summary>
    /// Один источник Cinemachine-импульсов на сцену. Подписан на ключевые события
    /// (трансформация, смерть) и автоматически "пинает" камеру через
    /// <see cref="CinemachineImpulseSource"/>. Чтобы вызвать импульс из других мест
    /// (например, при разрушении BigBreakable-стены), используйте статический
    /// метод <see cref="Shake"/>.
    ///
    /// Парный слушатель — <see cref="CinemachineIndependentImpulseListener"/>
    /// на Main Camera, его LevelGenerator настраивает на тот же канал (1).
    /// </summary>
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class ImpulseRouter : MonoBehaviour
    {
        public static ImpulseRouter Instance { get; private set; }

        private CinemachineImpulseSource _source;

        private void Awake()
        {
            Instance = this;
            _source = GetComponent<CinemachineImpulseSource>();
        }

        private void OnEnable()
        {
            GameEvents.OnScaleChanged += HandleScale;
            GameEvents.OnPlayerDied   += HandleDie;
        }

        private void OnDisable()
        {
            GameEvents.OnScaleChanged -= HandleScale;
            GameEvents.OnPlayerDied   -= HandleDie;
            if (Instance == this) Instance = null;
        }

        private void HandleScale(ScaleState _, ScaleState newState)
        {
            // Сила тряски подобрана так, чтобы Big был ощутимо "тяжелее":
            //   Big    — мощный удар (сжимаем большую массу)
            //   Normal — средний толчок
            //   Small  — мягкий хлопок
            float force = newState switch
            {
                ScaleState.Big    => 0.55f,
                ScaleState.Normal => 0.35f,
                ScaleState.Small  => 0.3f,
                _ => 0.3f
            };
            Shake(force);
        }

        private void HandleDie() => Shake(2.0f);

        public static void Shake(float force)
        {
            if (Instance == null || Instance._source == null) return;
            Instance._source.GenerateImpulseWithForce(force);
        }
    }
}
