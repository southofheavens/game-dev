using UnityEngine;
using UnityEngine.UI;
using ProjectScale.Core;

namespace ProjectScale.UI
{
    /// <summary>
    /// Контроллер кнопок меню выбора уровня. Сами кнопки и канвас собираются
    /// в <see cref="ProjectScale.Core.LevelGenerator"/> при старте, когда
    /// <see cref="LevelManager.LevelSelected"/> = false.
    ///
    /// Этот компонент только подписывает обработчики на клики кнопок —
    /// делегирует выбор в <see cref="LevelManager.SelectLevel(int)"/>.
    /// </summary>
    public class LevelSelectMenu : MonoBehaviour
    {
        public void RegisterButton(Button button, int levelIndex)
        {
            if (button == null) return;
            int captured = levelIndex; // защита от модификации замыкания
            button.onClick.AddListener(() => LevelManager.SelectLevel(captured));
        }
    }
}
