#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ProjectScale.EditorTools
{
    /// <summary>
    /// При первом открытии проекта автоматически создаёт Bootstrap-сцену и открывает её.
    /// Дальше пользователю достаточно нажать Play. Срабатывает один раз — отслеживается
    /// маркер-файл в Library/, который не индексируется AssetDatabase.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoFirstOpen
    {
        private const string MarkerFile = "Library/ProjectScale_Bootstrapped";
        private const int MaxAttempts = 60;
        private static int _attempts;

        static AutoFirstOpen()
        {
            // EditorApplication.update — единственный надёжный способ дождаться,
            // когда AssetDatabase, пакеты и компиляция полностью готовы.
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            // Не пытаемся запускаться, пока что-то компилируется или импортируется.
            if (EditorApplication.isCompiling) return;
            if (EditorApplication.isUpdating) return;
            if (BuildPipeline.isBuildingPlayer) return;

            _attempts++;
            if (_attempts > MaxAttempts)
            {
                EditorApplication.update -= OnUpdate;
                return;
            }

            if (File.Exists(MarkerFile))
            {
                EditorApplication.update -= OnUpdate;
                return;
            }

            // Если сцена уже существует (например, сгенерирована вручную) — просто откроем её.
            if (File.Exists(LevelBuilder.ScenePath))
            {
                if (SceneManager.GetActiveScene().path != LevelBuilder.ScenePath)
                    EditorSceneManager.OpenScene(LevelBuilder.ScenePath, OpenSceneMode.Single);
                File.WriteAllText(MarkerFile, "ok");
                EditorApplication.update -= OnUpdate;
                return;
            }

            // Создаём сцену с Bootstrap-объектом.
            try
            {
                LevelBuilder.BuildBootstrapScene();
                File.WriteAllText(MarkerFile, "ok");
                Debug.Log("<color=#5cc4ff>[ProjectScale]</color> Проект готов. Нажмите Play.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ProjectScale] AutoFirstOpen: попытка " + _attempts + " не удалась: " + ex.Message);
                // Не помечаем как готовое, попробуем снова на следующем тике.
                return;
            }

            EditorApplication.update -= OnUpdate;
        }
    }
}
#endif
