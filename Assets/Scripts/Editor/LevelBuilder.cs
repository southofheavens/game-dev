#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ProjectScale.Core;

namespace ProjectScale.EditorTools
{
    /// <summary>
    /// Создаёт минимальную сцену MainLevel.unity, содержащую один объект «Bootstrap»
    /// со скриптом RuntimeBootstrap. При нажатии Play этот скрипт собирает весь
    /// уровень в рантайме (см. LevelGenerator).
    ///
    /// Меню: ProjectScale ▸ Build Bootstrap Scene
    /// </summary>
    public static class LevelBuilder
    {
        public const string ScenePath = "Assets/Scenes/MainLevel.unity";

        [MenuItem("ProjectScale/Build Bootstrap Scene", priority = 0)]
        public static void BuildBootstrapScene()
        {
            EnsureFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("Bootstrap");
            go.AddComponent<RuntimeBootstrap>();

            // Подсказка-плейсхолдер на пустой сцене (в рантайме её перекроет UI).
            var hintGo = new GameObject("EditorHint");
            var sr = hintGo.AddComponent<SpriteRenderer>();
            sr.color = new Color(0.5f, 0.7f, 1f, 0.0f);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.MarkSceneDirty(scene);

            // Регистрируем сцену в Build Settings.
            var listed = false;
            var settings = EditorBuildSettings.scenes;
            foreach (var s in settings) if (s.path == ScenePath) listed = true;
            if (!listed)
            {
                var newList = new EditorBuildSettingsScene[settings.Length + 1];
                System.Array.Copy(settings, newList, settings.Length);
                newList[settings.Length] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = newList;
            }

            // Делаем эту сцену активной в Editor — пользователь увидит её при следующем открытии.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Debug.Log("<color=#5cc4ff>[ProjectScale]</color> Bootstrap-сцена создана: " + ScenePath +
                      ". Просто нажмите Play — уровень соберётся в рантайме.");
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name))
                AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
