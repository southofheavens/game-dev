using System.Collections;
using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Player
{
    /// <summary>
    /// Центральная система трансформации персонажа. Хранит профили трёх состояний,
    /// применяет их физические/визуальные параметры, сохраняет импульс при переходе
    /// и взаимодействует с системой энергии.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlayerScaleController : MonoBehaviour
    {
        [Header("Профили состояний (Small / Normal / Big)")]
        [SerializeField] private ScaleProfile smallProfile;
        [SerializeField] private ScaleProfile normalProfile;
        [SerializeField] private ScaleProfile bigProfile;

        [Header("Стартовое состояние")]
        [SerializeField] private ScaleState startState = ScaleState.Normal;

        [Header("Анимация перехода")]
        [Tooltip("Длительность интерполяции масштаба при трансформации, сек. " +
                 "0 = мгновенная смена размера (рекомендуется — иначе физика не " +
                 "успевает корректно сместить ноги, и спрайт зависает над/под полом).")]
        [SerializeField] private float transitionDuration = 0f;

        [Header("Сохранение импульса")]
        [Tooltip("Базовый коэффициент сохранения горизонтального импульса")]
        [SerializeField] private float momentumXFactor = 1f;
        [Tooltip("Базовый коэффициент сохранения вертикального импульса")]
        [SerializeField] private float momentumYFactor = 1f;
        [Tooltip("Бонус к вертикальному импульсу при уменьшении (Big->Normal->Small) — эффект 'катапульты'")]
        [SerializeField] private float shrinkUpwardBoost = 1.25f;
        [Tooltip("Бонус к горизонтальному импульсу при увеличении в воздухе — длинные прыжки")]
        [SerializeField] private float growHorizontalBoost = 1.15f;

        [Header("Связи")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("ParticleSystem для трансформации — встроенный fallback")]
        [SerializeField] private ParticleSystem transformParticles;

        [Header("Зависимости")]
        [SerializeField] private EnergySystem energy;

        private Rigidbody2D _rb;
        private ScaleState _current;
        private ScaleProfile _currentProfile;
        private Coroutine _transition;

        public ScaleState CurrentState => _current;
        public ScaleProfile CurrentProfile => _currentProfile;
        public float CurrentResonance => _currentProfile != null ? _currentProfile.resonanceFrequency : 1f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            if (energy == null) energy = GetComponent<EnergySystem>();
        }

        private void Start()
        {
            // Применяем стартовое состояние мгновенно, без расхода энергии и без VFX.
            ApplyProfile(GetProfile(startState), instant: true);
            _current = startState;
            GameEvents.RaiseScaleChanged(startState, startState);
        }

        /// <summary>
        /// Пытается перейти в указанное состояние. Возвращает true при успехе.
        /// Тратит энергию из EnergySystem согласно профилю целевого состояния.
        /// </summary>
        public bool TryTransform(ScaleState target)
        {
            if (target == _current) return false;
            var targetProfile = GetProfile(target);
            if (targetProfile == null) return false;

            float cost = targetProfile.transformCost;
            if (energy != null && !energy.Spend(cost)) return false;

            // Сохраняем импульс с учётом направления изменения размера.
            Vector2 preservedVelocity = ComputePreservedVelocity(_current, target, _rb.velocity);

            var oldState = _current;
            _current = target;
            ApplyProfile(targetProfile, instant: false);
            _rb.velocity = preservedVelocity;

            PlayTransformVfx();
            GameEvents.RaiseScaleChanged(oldState, target);
            return true;
        }

        public void ForceState(ScaleState target)
        {
            var profile = GetProfile(target);
            if (profile == null) return;
            var oldState = _current;
            _current = target;
            ApplyProfile(profile, instant: true);
            GameEvents.RaiseScaleChanged(oldState, target);
        }

        /// <summary>
        /// Расчёт сохранённой скорости при трансформации. Идея: при сжатии импульс концентрируется,
        /// при расширении — растягивается. Это даёт игроку "трюки": сжаться в воздухе для буста.
        /// </summary>
        private Vector2 ComputePreservedVelocity(ScaleState from, ScaleState to, Vector2 v)
        {
            float xFactor = momentumXFactor;
            float yFactor = momentumYFactor;

            int delta = (int)to - (int)from;
            if (delta < 0)
            {
                // Уменьшение — катапультируемся вверх.
                yFactor *= shrinkUpwardBoost;
            }
            else if (delta > 0)
            {
                // Увеличение — длинный горизонтальный полёт.
                xFactor *= growHorizontalBoost;
            }

            return new Vector2(v.x * xFactor, v.y * yFactor);
        }

        private ScaleProfile GetProfile(ScaleState state)
        {
            switch (state)
            {
                case ScaleState.Small:  return smallProfile;
                case ScaleState.Normal: return normalProfile;
                case ScaleState.Big:    return bigProfile;
                default:                return normalProfile;
            }
        }

        private void ApplyProfile(ScaleProfile profile, bool instant)
        {
            if (profile == null) return;
            float oldScale = _currentProfile != null ? _currentProfile.scale : transform.localScale.y;
            _currentProfile = profile;

            _rb.mass         = profile.mass;
            _rb.drag         = profile.linearDrag;
            _rb.gravityScale = profile.gravityScale;

            if (spriteRenderer != null) spriteRenderer.color = profile.tintColor;

            // Корректируем Y, чтобы "ноги" остались на прежней высоте после смены размера.
            // Иначе при росте Big-коллайдер уходит в землю (а с маленьким игроком наоборот
            // образуется зазор), и физика грубо выталкивает или роняет тело.
            // Базовая высота коллайдера принята равной 1 (см. CapsuleCollider2D.size = (0.7, 1)).
            float halfHeightOld = oldScale * 0.5f;
            float halfHeightNew = profile.scale * 0.5f;
            float deltaY = halfHeightNew - halfHeightOld;
            if (Mathf.Abs(deltaY) > Mathf.Epsilon)
            {
                Vector2 newPos = (Vector2)transform.position + new Vector2(0f, deltaY);
                // Явно обновляем и transform, и rigidbody — иначе Rigidbody2D с
                // Interpolate ещё кадр рендерит старую позицию.
                _rb.position = newPos;
                transform.position = newPos;
            }

            // Масштабируем весь корень игрока вместе с коллайдером и GroundCheck-ом.
            // Знак x сохраняем — флип делается через SpriteRenderer.flipX,
            // а не через инверсию scale.
            float sign = Mathf.Sign(transform.localScale.x);
            if (sign == 0f) sign = 1f;
            Vector3 targetScale = new Vector3(sign * profile.scale, profile.scale, 1f);

            if (instant || transitionDuration <= 0f)
            {
                transform.localScale = targetScale;
            }
            else
            {
                if (_transition != null) StopCoroutine(_transition);
                _transition = StartCoroutine(LerpScale(transform.localScale, targetScale));
            }
        }

        private IEnumerator LerpScale(Vector3 from, Vector3 to)
        {
            float t = 0f;
            while (t < transitionDuration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, t / transitionDuration);
                transform.localScale = Vector3.LerpUnclamped(from, to, k);
                yield return null;
            }
            transform.localScale = to;
            _transition = null;
        }

        private void PlayTransformVfx()
        {
            if (transformParticles != null)
            {
                var main = transformParticles.main;
                main.startColor = _currentProfile.tintColor;
                transformParticles.Play();
            }
        }
    }
}
