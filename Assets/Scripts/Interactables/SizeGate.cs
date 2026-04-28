using UnityEngine;
using ProjectScale.Core;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Узкий проход: коллайдер активен только тогда, когда игрок в одном из разрешённых
    /// размеров. Используется для физических ограничений ("Big не пролезет").
    ///
    /// Это работает через отключение коллайдера, а не через триггер. Так получаются
    /// правдоподобные стены, которые остаются твёрдыми для нежелательных размеров.
    /// </summary>
    public class SizeGate : MonoBehaviour
    {
        [Header("Разрешённые размеры")]
        [SerializeField] private bool allowSmall = true;
        [SerializeField] private bool allowNormal = false;
        [SerializeField] private bool allowBig = false;

        [Header("Визуал")]
        [SerializeField] private SpriteRenderer[] renderers;
        [SerializeField, Range(0f, 1f)] private float passableAlpha = 0.25f;
        [SerializeField, Range(0f, 1f)] private float blockedAlpha = 1f;

        private Collider2D[] _colliders;

        private void Awake()
        {
            _colliders = GetComponentsInChildren<Collider2D>(true);
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void Start()
        {
            GameEvents.OnScaleChanged += HandleScaleChanged;
            var p = GameObject.FindWithTag("Player");
            var sc = p != null ? p.GetComponent<PlayerScaleController>() : null;
            ApplyState(sc != null ? sc.CurrentState : ScaleState.Normal);
        }

        private void OnDestroy()
        {
            GameEvents.OnScaleChanged -= HandleScaleChanged;
        }

        private void HandleScaleChanged(ScaleState _, ScaleState newState) => ApplyState(newState);

        private void ApplyState(ScaleState s)
        {
            bool passable = (s == ScaleState.Small  && allowSmall)
                         || (s == ScaleState.Normal && allowNormal)
                         || (s == ScaleState.Big    && allowBig);

            foreach (var c in _colliders)
                if (c != null) c.enabled = !passable;

            float alpha = passable ? passableAlpha : blockedAlpha;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var col = r.color;
                col.a = alpha;
                r.color = col;
            }
        }
    }
}
