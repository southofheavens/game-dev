using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Effects
{
    /// <summary>
    /// Цветной хвост, тянущийся за игроком. GameObject живёт в корне сцены и в LateUpdate
    /// копирует позицию игрока — так избегаем влияния transform.localScale игрока
    /// (который меняется при трансформациях) на видимую толщину хвоста.
    ///
    /// Цвет и ширина обновляются на событие <see cref="GameEvents.OnScaleChanged"/>:
    ///   Small  → голубой, тонкий
    ///   Normal → белый, средний
    ///   Big    → оранжевый, толстый
    /// </summary>
    public class PlayerTrail : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private TrailRenderer trail;

        public void SetTarget(Transform t) => target = t;
        public void SetTrail(TrailRenderer tr) => trail = tr;

        private void OnEnable()
        {
            GameEvents.OnScaleChanged += HandleScale;
        }

        private void OnDisable()
        {
            GameEvents.OnScaleChanged -= HandleScale;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position;
        }

        private void HandleScale(ScaleState _, ScaleState newState)
        {
            ApplyState(newState);
        }

        public void ApplyState(ScaleState s)
        {
            if (trail == null) return;
            Color c = ColorForState(s);
            trail.startColor = new Color(c.r, c.g, c.b, 0.75f);
            trail.endColor   = new Color(c.r, c.g, c.b, 0f);
            trail.startWidth = WidthForState(s);
            trail.endWidth   = 0.04f;
        }

        private static Color ColorForState(ScaleState s) => s switch
        {
            ScaleState.Small  => new Color(0.45f, 0.85f, 1f),
            ScaleState.Normal => new Color(1f, 1f, 1f),
            ScaleState.Big    => new Color(1f, 0.55f, 0.15f),
            _ => Color.white
        };

        private static float WidthForState(ScaleState s) => s switch
        {
            ScaleState.Small  => 0.32f,
            ScaleState.Normal => 0.55f,
            ScaleState.Big    => 0.9f,
            _ => 0.55f
        };
    }
}
