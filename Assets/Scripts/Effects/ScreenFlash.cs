using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ProjectScale.Core;

namespace ProjectScale.Effects
{
    /// <summary>
    /// Полноэкранная цветная вспышка. Подвешивается на полноразмерный
    /// <see cref="Image"/>, прозрачный по умолчанию. На события трансформации
    /// и смерти быстро поднимает альфу и плавно гасит.
    ///
    /// Анимация считается через <see cref="Time.unscaledDeltaTime"/> — слоумо
    /// (если есть) не растягивает вспышку.
    /// </summary>
    public class ScreenFlash : MonoBehaviour
    {
        [SerializeField] private Image flashImage;
        [SerializeField] private float duration = 0.25f;
        [SerializeField] private float peakAlpha = 0.35f;

        private Coroutine _coroutine;

        public void SetImage(Image img) => flashImage = img;

        private void OnEnable()
        {
            GameEvents.OnScaleChanged += HandleScale;
            GameEvents.OnPlayerDied   += HandleDie;
        }

        private void OnDisable()
        {
            GameEvents.OnScaleChanged -= HandleScale;
            GameEvents.OnPlayerDied   -= HandleDie;
        }

        private void HandleScale(ScaleState _, ScaleState newState)
        {
            Color c = newState switch
            {
                ScaleState.Small  => new Color(0.45f, 0.85f, 1f),
                ScaleState.Normal => Color.white,
                ScaleState.Big    => new Color(1f, 0.55f, 0.15f),
                _ => Color.white
            };
            Flash(c, duration, peakAlpha);
        }

        private void HandleDie()
        {
            // Смерть = насыщенно-красный, дольше и ярче, чтобы момент гибели
            // успел "почувствоваться" до перезагрузки сцены.
            Flash(new Color(1f, 0.15f, 0.15f), 0.45f, 0.7f);
        }

        public void Flash(Color color, float dur, float peak)
        {
            if (flashImage == null) return;
            if (_coroutine != null) StopCoroutine(_coroutine);
            _coroutine = StartCoroutine(Animate(color, dur, peak));
        }

        private IEnumerator Animate(Color color, float dur, float peak)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / dur);
                color.a = peak * (1f - k);
                flashImage.color = color;
                yield return null;
            }
            color.a = 0f;
            flashImage.color = color;
        }
    }
}
