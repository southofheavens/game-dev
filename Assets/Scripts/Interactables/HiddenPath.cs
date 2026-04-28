using System.Collections;
using UnityEngine;
using ProjectScale.Core;
using ProjectScale.Player;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Платформа/проход, существующий только в определённом состоянии масштаба игрока.
    /// При несовпадении состояния объект становится прозрачным и отключает коллизию,
    /// эмулируя "скрытое измерение" доступное конкретному размеру.
    /// </summary>
    public class HiddenPath : MonoBehaviour
    {
        [Header("Условие проявления")]
        [SerializeField] private ScaleState revealAt = ScaleState.Small;

        [Header("Поведение")]
        [Tooltip("Отключать коллайдер, когда объект скрыт")]
        [SerializeField] private bool toggleCollider = true;
        [Tooltip("Полностью скрывать SpriteRenderer (false — оставлять полупрозрачным)")]
        [SerializeField] private bool fullyHidden = false;

        [Header("Визуал")]
        [SerializeField] private float fadeDuration = 0.25f;
        [SerializeField, Range(0f, 1f)] private float hiddenAlpha = 0.08f;
        [SerializeField, Range(0f, 1f)] private float visibleAlpha = 1f;

        private SpriteRenderer[] _renderers;
        private Collider2D[] _colliders;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
            _colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void Start()
        {
            GameEvents.OnScaleChanged += HandleScaleChanged;
            // Инициализация — мгновенно применяем текущее состояние.
            var player = GameObject.FindWithTag("Player");
            var sc = player != null ? player.GetComponent<PlayerScaleController>() : null;
            ApplyVisibility(sc != null && sc.CurrentState == revealAt, instant: true);
        }

        private void OnDestroy()
        {
            GameEvents.OnScaleChanged -= HandleScaleChanged;
        }

        private void HandleScaleChanged(ScaleState _, ScaleState newState)
        {
            ApplyVisibility(newState == revealAt, instant: false);
        }

        private void ApplyVisibility(bool visible, bool instant)
        {
            if (toggleCollider)
            {
                foreach (var c in _colliders)
                    if (c != null) c.enabled = visible;
            }

            float target = visible ? visibleAlpha : (fullyHidden ? 0f : hiddenAlpha);

            if (instant)
            {
                foreach (var r in _renderers)
                    if (r != null)
                    {
                        var col = r.color;
                        col.a = target;
                        r.color = col;
                    }
                return;
            }

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeTo(target));
        }

        private IEnumerator FadeTo(float targetAlpha)
        {
            if (_renderers == null || _renderers.Length == 0) yield break;
            float t = 0f;
            float startAlpha = _renderers[0] != null ? _renderers[0].color.a : 1f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(startAlpha, targetAlpha, t / fadeDuration);
                foreach (var r in _renderers)
                {
                    if (r == null) continue;
                    var col = r.color;
                    col.a = a;
                    r.color = col;
                }
                yield return null;
            }
        }
    }
}
