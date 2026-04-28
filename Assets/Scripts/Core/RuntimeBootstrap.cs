using UnityEngine;

namespace ProjectScale.Core
{
    /// <summary>
    /// Точка входа в игру. Достаточно положить один экземпляр этого MonoBehaviour
    /// в сцену — при нажатии Play он соберёт игрока, камеру, окружение, головоломки
    /// и UI с нуля. Никаких ручных настроек в Editor-е не требуется.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class RuntimeBootstrap : MonoBehaviour
    {
        [Tooltip("Уничтожить этот объект после сборки уровня")]
        [SerializeField] private bool selfDestructAfterBuild = true;

        private void Awake()
        {
            // Гарантируем, что разные системы не зависят от старых событий из предыдущих сессий.
            GameEvents.Clear();
            // Если предыдущая сессия успела включить слоумо и упасть/перезагрузиться
            // до восстановления Time.timeScale = 1, новый уровень начнётся в
            // "замороженном" виде. Сбрасываем явно.
            Time.timeScale = 1f;

            // Если игрок ещё не выбрал уровень из меню — собираем меню вместо игры.
            // После клика по кнопке LevelManager.SelectLevel() выставит флаг и
            // перезагрузит сцену, и мы попадём в обычный путь сборки уровня.
            if (LevelManager.LevelSelected)
            {
                LevelGenerator.BuildEverything();
            }
            else
            {
                LevelGenerator.BuildMenu();
            }

            if (selfDestructAfterBuild) Destroy(gameObject);
        }
    }
}
