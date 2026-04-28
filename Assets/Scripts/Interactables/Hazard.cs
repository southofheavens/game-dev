using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Опасная зона: при касании уничтожает игрока (отправляет на чекпоинт).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
                GameEvents.RaisePlayerDied();
        }
    }
}
