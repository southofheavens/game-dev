using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectScale.Core
{
    /// <summary>
    /// Состояние сессии: какой уровень сейчас выбран и показывать ли меню.
    /// Хранится в статических полях — этого достаточно, потому что они переживают
    /// перезагрузку сцены (которую мы используем для смены уровня), но при выходе
    /// из Play Unity всё обнуляет, и игра честно стартует с меню.
    ///
    /// Уровни задаются в коде ниже. Если нужно добавить четвёртый — просто пиши
    /// ещё один <see cref="LevelConfig"/> в список <c>_levels</c>.
    /// </summary>
    public static class LevelManager
    {
        public static int CurrentLevel { get; private set; }
        public static bool LevelSelected { get; private set; }

        // 0..N-1. Index в LevelConfig дублирует позицию для удобства, но
        // фактический "адрес" уровня — это позиция в этом списке.
        private static readonly List<LevelConfig> _levels = new List<LevelConfig>
        {
            new LevelConfig
            {
                Index = 0,
                DisplayName     = "Уровень 1: Знакомство",
                DifficultyLabel = "Легко",
                AccentColor     = new Color(0.45f, 0.85f, 1f),
                SpikeSpeed      = 1.2f,
                GdPlatformCount = 8,
                GdPlatformWidth = 2f,
                GdEdgeGap       = 5f,
            },
            new LevelConfig
            {
                Index = 1,
                DisplayName     = "Уровень 2: Усложнение",
                DifficultyLabel = "Средне",
                AccentColor     = new Color(1f, 0.85f, 0.3f),
                SpikeSpeed      = 1.7f,
                GdPlatformCount = 9,
                GdPlatformWidth = 1.5f,
                GdEdgeGap       = 5f,
                ExtraBigBreakableWall = true,
            },
            new LevelConfig
            {
                Index = 2,
                DisplayName     = "Уровень 3: Мастер",
                DifficultyLabel = "Жёстко",
                AccentColor     = new Color(1f, 0.4f, 0.45f),
                SpikeSpeed      = 2.2f,
                GdPlatformCount = 10,
                GdPlatformWidth = 1.2f,
                GdEdgeGap       = 5.5f,
                ExtraBigBreakableWall = true,
                ExtraSpikePillar      = true,
            },
        };

        public static int Count => _levels.Count;
        public static LevelConfig Get(int index) => _levels[Mathf.Clamp(index, 0, _levels.Count - 1)];
        public static LevelConfig Current => Get(CurrentLevel);

        public static bool IsLast => CurrentLevel >= _levels.Count - 1;

        /// <summary>Игрок выбрал уровень из меню — стартуем игру.</summary>
        public static void SelectLevel(int index)
        {
            CurrentLevel = Mathf.Clamp(index, 0, _levels.Count - 1);
            LevelSelected = true;
            ReloadScene();
        }

        /// <summary>Уровень пройден — переходим к следующему. Возвращает false, если был последний.</summary>
        public static bool TryAdvance()
        {
            if (IsLast) return false;
            CurrentLevel++;
            LevelSelected = true;
            ReloadScene();
            return true;
        }

        /// <summary>Возврат в меню выбора уровней.</summary>
        public static void ReturnToMenu()
        {
            LevelSelected = false;
            ReloadScene();
        }

        private static void ReloadScene()
        {
            // Перед перезагрузкой сбрасываем timeScale на случай, если осталось
            // слоумо смерти, и чистим события, чтобы статика не держала ссылок
            // на уничтоженные объекты.
            Time.timeScale = 1f;
            GameEvents.Clear();
            var scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0)
                SceneManager.LoadScene(scene.buildIndex);
            else
                SceneManager.LoadScene(scene.name);
        }
    }
}
