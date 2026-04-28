using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Плита давления: активируется, только если суммарная масса лежащих объектов
    /// (включая игрока) превышает порог. Тяжёлый Big-режим — единственный способ
    /// активировать "тяжёлые" механизмы.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PressurePlate : MonoBehaviour
    {
        [Header("Условия")]
        [Tooltip("Минимальная масса (Rigidbody2D.mass) для активации")]
        [SerializeField] private float requiredMass = 4f;
        [Tooltip("Максимальная масса. Если суммарная масса ВЫШЕ — плита не активируется. " +
                 "Используется для 'калиброванных' плит: например, при requiredMass=1.0 и " +
                 "maxMass=3.0 плита срабатывает только под Normal — Big слишком тяжёлый, " +
                 "Small слишком лёгкий.")]
        [SerializeField] private float maxMass = Mathf.Infinity;

        [Header("Визуал")]
        [SerializeField] private SpriteRenderer plateRenderer;
        [SerializeField] private Color idleColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.4f, 1f);
        [SerializeField] private float pressDownOffset = 0.08f;

        [Header("Сигналы")]
        // ВАЖНО: инициализируем экземпляр UnityEvent в декларации.
        // При AddComponent<>() в рантайме Unity не вызывает свой десериализатор,
        // и без явного new эти поля остаются null — слушатели тогда не подцепляются,
        // а Invoke() уходит в no-op. Из-за этого, например, плита визуально активируется,
        // но связанные ворота не открываются.
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

        private bool _isActive;
        private float _accumulatedMass;
        private Vector3 _originalPosition;
        private readonly HashSet<Rigidbody2D> _contacts = new HashSet<Rigidbody2D>();

        private void Awake()
        {
            _originalPosition = transform.position;
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
            UpdateVisual();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            _contacts.Add(rb);
            RecalculateMass();
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var rb = other.attachedRigidbody;
            if (rb == null) return;
            _contacts.Remove(rb);
            RecalculateMass();
        }

        private void FixedUpdate()
        {
            // Масса контактов может меняться (например, игрок трансформировался,
            // стоя на плите). Пересчитываем каждый кадр.
            RecalculateMass();
        }

        private void RecalculateMass()
        {
            float sum = 0f;
            // Чистим разрушенные ссылки, попутно суммируем массу.
            _contacts.RemoveWhere(r => r == null);
            foreach (var rb in _contacts) sum += rb.mass;
            _accumulatedMass = sum;
            ReevaluateState();
        }

        private void ReevaluateState()
        {
            // Активна, если масса попадает в диапазон [requiredMass, maxMass].
            // Это даёт три типа плит:
            //   - "Big-only":  required=4,   max=∞   — нужна большая масса.
            //   - "Normal-only": required=1, max=3    — Big перевешивает, Small легче.
            //   - "Любой":     required=0.1, max=∞   — нажимается чем угодно.
            bool active = _accumulatedMass >= requiredMass && _accumulatedMass <= maxMass;
            if (active == _isActive) { UpdateVisual(); return; }
            _isActive = active;
            UpdateVisual();
            if (_isActive) onActivated?.Invoke();
            else onDeactivated?.Invoke();
        }

        private void UpdateVisual()
        {
            if (plateRenderer != null)
                plateRenderer.color = _isActive ? activeColor : idleColor;
            transform.position = _originalPosition + (_isActive ? Vector3.down * pressDownOffset : Vector3.zero);
        }
    }
}
