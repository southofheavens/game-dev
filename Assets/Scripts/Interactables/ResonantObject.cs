using UnityEngine;
using UnityEngine.Events;
using ProjectScale.Core;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Объект, реагирующий только на определённую частоту резонанса. Игрок должен быть
    /// в состоянии с совпадающей частотой (заданной в ScaleProfile), чтобы активировать его.
    /// При совпадении объект "оживает": воспроизводит событие, опционально удаляется или
    /// меняет визуал.
    /// </summary>
    public class ResonantObject : MonoBehaviour
    {
        [Header("Резонанс")]
        [Tooltip("Частота, при которой объект активируется. Сравнивается с ScaleProfile.resonanceFrequency игрока")]
        [SerializeField] private float targetFrequency = 1f;
        [Tooltip("Допустимое отклонение частоты")]
        [SerializeField] private float tolerance = 0.05f;

        [Header("Визуал")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.5f);
        [SerializeField] private Color resonatingColor = Color.cyan;

        [Header("Реакция")]
        [Tooltip("Активировать объект только один раз")]
        [SerializeField] private bool oneShot = true;
        [Tooltip("Уничтожить объект при активации")]
        [SerializeField] private bool destroyOnActivate = false;
        // См. комментарий в PressurePlate — UnityEvent в рантайм-AddComponent
        // обязательно инициализируем явно, иначе слушатели не подцепляются.
        [SerializeField] private UnityEvent onActivated = new UnityEvent();
        [SerializeField] private UnityEvent onDeactivated = new UnityEvent();

        [Header("Колебания (визуальный фидбек)")]
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float pulseAmplitude = 0.08f;

        private Vector3 _baseScale;
        private bool _wasResonating;
        private bool _hasFired;
        private Transform _player;
        private PlayerScaleController _playerScale;

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (spriteRenderer != null) spriteRenderer.color = idleColor;
        }

        private void Start()
        {
            FindPlayer();
            GameEvents.OnScaleChanged += HandleScaleChanged;
        }

        private void OnDestroy()
        {
            GameEvents.OnScaleChanged -= HandleScaleChanged;
        }

        private void FindPlayer()
        {
            if (_playerScale != null) return;
            var go = GameObject.FindWithTag("Player");
            if (go == null) return;
            _player = go.transform;
            _playerScale = go.GetComponent<PlayerScaleController>();
            EvaluateResonance();
        }

        private void HandleScaleChanged(ScaleState _, ScaleState __)
        {
            if (_playerScale == null) FindPlayer();
            EvaluateResonance();
        }

        private void Update()
        {
            if (_playerScale == null) FindPlayer();
            if (_wasResonating)
            {
                float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
                transform.localScale = _baseScale * pulse;
            }
        }

        private void EvaluateResonance()
        {
            if (_playerScale == null) return;
            bool inResonance = Mathf.Abs(_playerScale.CurrentResonance - targetFrequency) <= tolerance;

            if (inResonance && !_wasResonating)
            {
                _wasResonating = true;
                if (spriteRenderer != null) spriteRenderer.color = resonatingColor;
                if (!_hasFired)
                {
                    onActivated?.Invoke();
                    _hasFired = oneShot;
                    if (destroyOnActivate) Destroy(gameObject, 0.05f);
                }
            }
            else if (!inResonance && _wasResonating)
            {
                _wasResonating = false;
                transform.localScale = _baseScale;
                if (spriteRenderer != null) spriteRenderer.color = idleColor;
                onDeactivated?.Invoke();
            }
        }
    }
}
