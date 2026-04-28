using UnityEngine;
using ProjectScale.Core;
using ProjectScale.Effects;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Преграда, которую разбивает только игрок в форме Big. Любой контакт коллайдера
    /// (collision или trigger) с PlayerScaleController в состоянии Big уничтожает блок.
    /// Это даёт массе игрока осмысленную роль: Big не просто "тяжёлый", а ещё и
    /// "пробивной".
    /// </summary>
    public class BigBreakable : MonoBehaviour
    {
        [Header("Условие")]
        [Tooltip("Какое состояние игрока разрушает блок")]
        [SerializeField] private ScaleState requiredState = ScaleState.Big;

        [Header("Эффект")]
        [Tooltip("Количество частиц при разрушении")]
        [SerializeField] private int debrisCount = 8;
        [SerializeField] private Color debrisColor = new Color(1f, 0.55f, 0.2f);
        [Tooltip("Сила разлёта осколков")]
        [SerializeField] private float debrisForce = 6f;
        [Tooltip("Время жизни осколков")]
        [SerializeField] private float debrisLifetime = 1.2f;

        private bool _broken;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryBreak(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryBreak(other);
        }

        private void TryBreak(Collider2D other)
        {
            if (_broken || other == null) return;
            var scale = other.GetComponentInParent<PlayerScaleController>();
            if (scale == null || scale.CurrentState != requiredState) return;

            _broken = true;
            SpawnDebris();
            // Разрушение крупного блока должно ощущаться удар-в-удар: дёргаем
            // камеру через ImpulseRouter (сила пропорциональна размеру блока,
            // чтобы маленький "учебный" кубик не тряс экран на полную).
            float volume = Mathf.Max(0.01f, transform.lossyScale.x * transform.lossyScale.y);
            ImpulseRouter.Shake(Mathf.Clamp(volume * 0.18f, 0.2f, 1.6f));
            Destroy(gameObject);
        }

        /// <summary>
        /// Генерирует короткоживущие "осколки" — простые SpriteRenderer-кубики
        /// без коллайдеров, разлетающиеся в случайных направлениях.
        /// Используем тот же общий white-square sprite, который генерирует LevelGenerator.
        /// </summary>
        private void SpawnDebris()
        {
            var ownSr = GetComponent<SpriteRenderer>();
            var sprite = ownSr != null ? ownSr.sprite : null;
            if (sprite == null) return;

            // Берём fixed worldScale для осколков на основе текущего масштаба блока.
            float baseSize = Mathf.Min(transform.lossyScale.x, transform.lossyScale.y) * 0.25f;

            for (int i = 0; i < debrisCount; i++)
            {
                var go = new GameObject("Debris");
                go.transform.position = transform.position;
                go.transform.localScale = Vector3.one * baseSize;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = debrisColor;
                sr.sortingOrder = 4;

                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 2f;
                Vector2 dir = Random.insideUnitCircle.normalized;
                rb.velocity = dir * debrisForce;
                rb.angularVelocity = Random.Range(-360f, 360f);

                Object.Destroy(go, debrisLifetime);
            }
        }
    }
}
