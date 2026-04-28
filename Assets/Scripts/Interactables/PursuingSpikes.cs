using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// "Стена смерти", постоянно ползущая слева направо. Создаёт давление времени:
    /// игрок не может стоять и думать слишком долго, иначе шипы догонят и убьют.
    ///
    /// Логика убийства живёт в стандартном <see cref="Hazard"/> на том же
    /// GameObject — этот компонент только двигает стену и сбрасывает её на
    /// стартовую позицию при респавне игрока.
    /// </summary>
    public class PursuingSpikes : MonoBehaviour
    {
        [Header("Движение")]
        [Tooltip("Скорость движения вправо, единиц/сек")]
        [SerializeField] private float speed = 1.5f;

        [Header("Поведение")]
        [Tooltip("Возвращать стену на стартовую позицию при смерти игрока")]
        [SerializeField] private bool resetOnPlayerDeath = true;
        [Tooltip("Останавливать стену при завершении уровня")]
        [SerializeField] private bool stopOnLevelComplete = true;
        [Tooltip("X-координата, дальше которой стена не двигается. " +
                 "По умолчанию — без ограничения. Полезно, чтобы стена не " +
                 "наехала на финиш-флаг во время финального экрана.")]
        [SerializeField] private float maxX = float.PositiveInfinity;

        private Vector3 _startPosition;
        private bool _stopped;

        private void Awake()
        {
            _startPosition = transform.position;
        }

        private void OnEnable()
        {
            GameEvents.OnPlayerDied += HandlePlayerDied;
            GameEvents.OnLevelCompleted += HandleLevelCompleted;
        }

        private void OnDisable()
        {
            GameEvents.OnPlayerDied -= HandlePlayerDied;
            GameEvents.OnLevelCompleted -= HandleLevelCompleted;
        }

        private void Update()
        {
            if (_stopped) return;
            if (transform.position.x >= maxX) return;
            transform.position += Vector3.right * (speed * Time.deltaTime);
        }

        private void HandlePlayerDied()
        {
            if (!resetOnPlayerDeath) return;
            transform.position = _startPosition;
        }

        private void HandleLevelCompleted()
        {
            if (stopOnLevelComplete) _stopped = true;
        }

        public void SetSpeed(float v) => speed = v;
        public void SetMaxX(float x) => maxX = x;
    }
}
