using UnityEngine;
using ProjectScale.Core;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Триггер финиша уровня. Любой контакт игрока поднимает событие
    /// OnLevelCompleted — UI сразу показывает экран победы. Не зависит от
    /// числа собранных кристаллов: кристаллы — бонус, а финиш — основная цель.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class LevelFinish : MonoBehaviour
    {
        private bool _completed;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_completed) return;
            if (other.GetComponentInParent<PlayerScaleController>() == null) return;
            _completed = true;
            GameEvents.RaiseLevelCompleted();
        }
    }
}
