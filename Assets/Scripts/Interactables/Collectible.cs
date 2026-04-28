using UnityEngine;
using ProjectScale.Core;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Сборный предмет — кристалл "энергии". При сборе:
    ///  * увеличивает счётчик в GameManager (для UI);
    ///  * мгновенно восстанавливает игроку часть энергии — становится осмысленным
    ///    тактическим бонусом для тех, кто планирует переключения размера.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Collectible : MonoBehaviour
    {
        [SerializeField] private float bobAmplitude = 0.15f;
        [SerializeField] private float bobSpeed = 2f;
        [SerializeField] private float rotationSpeed = 60f;

        [Header("Бонус")]
        [Tooltip("Сколько энергии возвращает игроку при сборе")]
        [SerializeField] private float energyReward = 30f;

        private Vector3 _basePosition;

        private void Awake()
        {
            _basePosition = transform.position;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        private void Update()
        {
            transform.position = _basePosition + Vector3.up * Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            if (energyReward > 0f)
            {
                var energy = other.GetComponentInParent<EnergySystem>();
                if (energy != null) energy.Restore(energyReward);
            }

            if (GameManager.Instance != null) GameManager.Instance.RegisterCollectible();
            Destroy(gameObject);
        }
    }
}
