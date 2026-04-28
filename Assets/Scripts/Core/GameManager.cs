using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectScale.Core
{
    /// <summary>
    /// Менеджер уровня. Считает собранные предметы, обрабатывает завершение и смерть игрока.
    /// Singleton — в сцене всегда один экземпляр.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Цели уровня")]
        [SerializeField] private int collectiblesTotal = 3;

        [Header("Точка возрождения")]
        [SerializeField] private Transform respawnPoint;

        [Header("Игрок (для удобства Editor-скрипта)")]
        [SerializeField] private Transform player;

        private int _collected;

        public int CollectiblesTotal => collectiblesTotal;
        public int CollectiblesCollected => _collected;
        public Transform Player => player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // ESC во время игры — возврат к меню выбора уровня. Не делаем при
            // открытом WinPanel или умирающем игроке, чтобы случайный нажим
            // не путал автопереход / экран смерти.
            if (Input.GetKeyDown(KeyCode.Escape) && !_dying && !_finishing)
            {
                LevelManager.ReturnToMenu();
            }
        }

        private void OnDestroy()
        {
            // Подчищаем глобальные события, чтобы избежать утечек между перезапусками сцены.
            GameEvents.Clear();
        }

        public void RegisterCollectible()
        {
            _collected++;
            GameEvents.RaiseCollectibleGathered(_collected, collectiblesTotal);
            // Победа теперь происходит только через LevelFinish-триггер.
            // Кристаллы — это бонус (энергия + счётчик), а не альтернативное
            // условие выигрыша — это убирает путаницу для новых игроков.
        }

        public void SetCollectiblesTotal(int total) => collectiblesTotal = total;
        public void SetRespawnPoint(Transform t) => respawnPoint = t;
        public void SetPlayer(Transform t) => player = t;

        private bool _dying;
        private bool _finishing;

        private void HandlePlayerDied()
        {
            if (_dying || _finishing) return;
            _dying = true;
            StartCoroutine(DeathSequence());
        }

        /// <summary>
        /// Уровень пройден. Если есть ещё — через короткую задержку запускаем
        /// следующий (LevelFinish уже показал WinPanel). Если это последний
        /// уровень — даём чуть больше времени порадоваться и возвращаем в меню.
        /// </summary>
        private void HandleLevelCompleted()
        {
            if (_finishing || _dying) return;
            _finishing = true;
            StartCoroutine(LevelCompletedSequence());
        }

        private IEnumerator LevelCompletedSequence()
        {
            // Время на восприятие WinPanel и анимацию её появления.
            yield return new WaitForSecondsRealtime(2.0f);

            if (LevelManager.IsLast)
            {
                // Последний уровень — финал игры. Чуть подольше, потом меню.
                yield return new WaitForSecondsRealtime(2.5f);
                LevelManager.ReturnToMenu();
            }
            else
            {
                LevelManager.TryAdvance();
            }
        }

        /// <summary>
        /// Короткое слоумо + ожидание, чтобы Cinemachine-импульс и красная
        /// вспышка успели проиграться до перезагрузки сцены. Без задержки
        /// смерть была бы "мгновенной" и не ощущалась бы кинематографично.
        /// Time.unscaledDeltaTime в WaitForSecondsRealtime сохраняет реальное
        /// время ожидания, не зависящее от Time.timeScale.
        /// </summary>
        private IEnumerator DeathSequence()
        {
            Time.timeScale = 0.25f;
            yield return new WaitForSecondsRealtime(0.45f);
            Time.timeScale = 1f;
            ReloadCurrentScene();
        }

        /// <summary>
        /// Полный сброс уровня — перезагружаем сцену. Так гарантированно
        /// возвращаются в исходное состояние:
        ///   - открытые ворота снова закрыты,
        ///   - сломанные Big-стены восстановлены,
        ///   - подобранные кристаллы возвращаются,
        ///   - стена шипов уезжает на стартовую позицию,
        ///   - энергия и масштаб сбрасываются.
        ///
        /// RuntimeBootstrap при загрузке сцены пересобирает уровень с нуля
        /// через LevelGenerator.BuildEverything().
        ///
        /// Используем имя сцены, а не buildIndex: AutoFirstOpen регистрирует
        /// её в Build Settings, но если этого не случилось (например, при
        /// запуске из произвольной сцены), buildIndex=-1 и LoadScene упадёт.
        /// Имя работает в обоих случаях.
        /// </summary>
        private static void ReloadCurrentScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }
    }
}
