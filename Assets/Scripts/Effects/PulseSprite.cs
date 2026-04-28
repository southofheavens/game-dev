using UnityEngine;

namespace ProjectScale.Effects
{
    /// <summary>
    /// Лёгкая пульсация цвета у <see cref="SpriteRenderer"/>: синусоидальный
    /// лерп между baseColor и peakColor. Подходит для GD-платформ, кристаллов,
    /// финиш-флага — всё, что должно "дышать", не двигаясь физически.
    ///
    /// Случайная фаза присваивается в <see cref="Awake"/>, чтобы группа однотипных
    /// объектов не пульсировала синхронно (читалось бы как глитч).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class PulseSprite : MonoBehaviour
    {
        [SerializeField] private float frequency = 1.5f;
        [SerializeField] private Color baseColor = new Color(0.35f, 0.45f, 0.6f);
        [SerializeField] private Color peakColor = new Color(0.55f, 0.7f, 1f);
        [SerializeField] private float phase;

        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (Mathf.Approximately(phase, 0f))
                phase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (_sr == null) return;
            float t = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f + phase) + 1f) * 0.5f;
            _sr.color = Color.Lerp(baseColor, peakColor, t);
        }

        public void Configure(Color baseC, Color peakC, float freq)
        {
            baseColor = baseC;
            peakColor = peakC;
            frequency = freq;
            if (_sr == null) _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _sr.color = baseColor;
        }
    }
}
