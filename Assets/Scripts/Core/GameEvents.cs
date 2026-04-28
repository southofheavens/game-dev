using System;
using UnityEngine;

namespace ProjectScale.Core
{
    /// <summary>
    /// Глобальная статическая шина событий. Помогает разным системам общаться без жёстких ссылок.
    /// Любой компонент может подписаться на интересующее событие, не зная, кто его вызывает.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Игрок сменил состояние масштаба. (старое, новое)</summary>
        public static event Action<ScaleState, ScaleState> OnScaleChanged;

        /// <summary>Энергия игрока изменилась. (текущее, максимум)</summary>
        public static event Action<float, float> OnEnergyChanged;

        /// <summary>Игрок собрал коллектабл. (всего собрано, цель)</summary>
        public static event Action<int, int> OnCollectibleGathered;

        /// <summary>Уровень пройден.</summary>
        public static event Action OnLevelCompleted;

        /// <summary>Игрок умер / провалился.</summary>
        public static event Action OnPlayerDied;

        public static void RaiseScaleChanged(ScaleState oldState, ScaleState newState)
            => OnScaleChanged?.Invoke(oldState, newState);

        public static void RaiseEnergyChanged(float current, float max)
            => OnEnergyChanged?.Invoke(current, max);

        public static void RaiseCollectibleGathered(int collected, int total)
            => OnCollectibleGathered?.Invoke(collected, total);

        public static void RaiseLevelCompleted() => OnLevelCompleted?.Invoke();

        public static void RaisePlayerDied() => OnPlayerDied?.Invoke();

        /// <summary>
        /// Сбрасывает все подписки. Вызывается при перезагрузке сцены, чтобы избежать утечек,
        /// когда статические события держат ссылки на уничтоженные объекты.
        /// </summary>
        public static void Clear()
        {
            OnScaleChanged = null;
            OnEnergyChanged = null;
            OnCollectibleGathered = null;
            OnLevelCompleted = null;
            OnPlayerDied = null;
        }
    }
}
