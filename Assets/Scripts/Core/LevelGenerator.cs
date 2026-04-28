using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Cinemachine;
using ProjectScale.Player;
using ProjectScale.CameraSystems;
using ProjectScale.UI;
using ProjectScale.Interactables;
using ProjectScale.Effects;

namespace ProjectScale.Core
{
    /// <summary>
    /// Сборщик уровня в рантайме. Создаёт спрайт, профили, игрока, камеру, окружение,
    /// головоломки и UI прямо во время Play. Можно вызвать из RuntimeBootstrap.Awake()
    /// или из любого Editor-скрипта.
    ///
    /// Отдельной сцены и assets не требуется — всё делается в памяти.
    ///
    /// Геометрия уровня рассчитана исходя из реальной баллистики прыжков:
    ///   Normal: jumpForce 13, gravityScale 3   → max apex ~2.87 (от центра)
    ///   Small : jumpForce 14, gravityScale 2.4 → max apex ~4.16
    ///   Big   : jumpForce 11.5, gravityScale 3.6 → max apex ~1.87
    /// и максимальной горизонтальной дальности прыжка:
    ///   Normal ~6.2,  Small ~10.1,  Big ~3.6 единиц.
    /// </summary>
    public static class LevelGenerator
    {
        // Используем статичный layer-индекс, чтобы не зависеть от именованных слоёв.
        // Слой 8 — первый пользовательский, всегда доступен.
        public const int GroundLayer = 8;

        private static Sprite _sharedSquare;

        /// <summary>
        /// Текущий конфиг уровня. Все Build*-функции читают его при необходимости.
        /// Заполняется в начале <see cref="BuildEverything"/>.
        /// </summary>
        private static LevelConfig _config;
        public static LevelConfig CurrentConfig => _config;

        public static void BuildEverything(Transform parent = null)
        {
            _config = LevelManager.Current;

            var sq = GetOrCreateSquareSprite();
            var profiles = CreateScaleProfiles();

            var respawn = BuildEnvironment(sq, parent);
            var player = BuildPlayer(profiles, sq, parent);
            BuildCamera(player.transform, parent);
            BuildPuzzles(sq, parent);
            BuildPursuingSpikes(sq, parent);
            BuildUI(parent);
            BuildGameManager(player.transform, respawn, parent);
        }

        // ---------------------------------------------------------------------
        // Sprites & profiles (in-memory)
        // ---------------------------------------------------------------------

        public static Sprite GetOrCreateSquareSprite()
        {
            if (_sharedSquare != null) return _sharedSquare;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "WhiteSquare"
            };
            var px = new Color[64 * 64];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            _sharedSquare = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64);
            _sharedSquare.name = "WhiteSquare";
            return _sharedSquare;
        }

        public static ScaleProfile[] CreateScaleProfiles()
        {
            var small = ScriptableObject.CreateInstance<ScaleProfile>();
            small.name = "Profile_Small";
            small.state = ScaleState.Small;
            small.scale = 0.55f;
            small.mass = 0.6f;
            small.linearDrag = 0.3f;
            small.gravityScale = 2.4f;
            small.moveSpeed = 8.5f;
            small.jumpForce = 14f;
            small.groundAcceleration = 70f;
            small.airAcceleration = 35f;
            small.transformCost = 18f;
            small.tintColor = new Color(0.45f, 0.85f, 1f);
            small.lightRadius = 2.2f;
            small.lightIntensity = 1.1f;
            small.resonanceFrequency = 3f;

            var normal = ScriptableObject.CreateInstance<ScaleProfile>();
            normal.name = "Profile_Normal";
            normal.state = ScaleState.Normal;
            normal.scale = 1f;
            normal.mass = 1.5f;
            normal.linearDrag = 0.25f;
            normal.gravityScale = 3f;
            normal.moveSpeed = 7f;
            normal.jumpForce = 13f;
            normal.groundAcceleration = 60f;
            normal.airAcceleration = 25f;
            normal.transformCost = 15f;
            normal.tintColor = Color.white;
            normal.lightRadius = 3f;
            normal.lightIntensity = 1f;
            normal.resonanceFrequency = 2f;

            var big = ScriptableObject.CreateInstance<ScaleProfile>();
            big.name = "Profile_Big";
            big.state = ScaleState.Big;
            big.scale = 1.7f;
            big.mass = 6f;
            big.linearDrag = 0.2f;
            big.gravityScale = 3.6f;
            big.moveSpeed = 5.5f;
            big.jumpForce = 11.5f;
            big.groundAcceleration = 45f;
            big.airAcceleration = 18f;
            big.transformCost = 25f;
            big.tintColor = new Color(1f, 0.55f, 0.2f);
            big.lightRadius = 4.5f;
            big.lightIntensity = 1.4f;
            big.resonanceFrequency = 1f;

            return new[] { small, normal, big };
        }

        // ---------------------------------------------------------------------
        // Environment
        // ---------------------------------------------------------------------

        public static Transform BuildEnvironment(Sprite sq, Transform parent)
        {
            var root = new GameObject("Environment");
            if (parent != null) root.transform.SetParent(parent, false);

            var cfg = _config;

            // Левая часть пола — сплошная от стартовой стены до края секции
            // обычных пазлов (cfg.MainGroundEndX). После него идёт пропасть GD-стиля.
            // Ground_Main: левый край фиксирован на x=-33 (рядом с Wall_Left=-34),
            // правый край — на cfg.MainGroundEndX.
            float mainLeft = -33f;
            float mainCenter = (mainLeft + cfg.MainGroundEndX) * 0.5f;
            float mainWidth = cfg.MainGroundEndX - mainLeft;
            CreatePlatform(root.transform, "Ground_Main",
                new Vector2(mainCenter, -3f), new Vector2(mainWidth, 1f),
                new Color(0.25f, 0.3f, 0.4f), sq);

            // Правая часть пола — за GD-секцией, до правой стены.
            CreatePlatform(root.transform, "Ground_Right",
                new Vector2(cfg.GroundRightCenterX, -3f),
                new Vector2(cfg.GroundRightWidth, 1f),
                new Color(0.25f, 0.3f, 0.4f), sq);

            CreatePlatform(root.transform, "Wall_Left",  new Vector2(-34f, 1f), new Vector2(1f, 14f),
                new Color(0.2f, 0.22f, 0.3f), sq);
            CreatePlatform(root.transform, "Wall_Right",
                new Vector2(cfg.LevelEndX + 1f, 1f), new Vector2(1f, 14f),
                new Color(0.2f, 0.22f, 0.3f), sq);

            // Низкий потолок: Big не пролезает (его макушка y=-0.8 выше нижней грани -1.2),
            // Normal/Small проходят свободно.
            CreatePlatform(root.transform, "Ceiling_NarrowGap",
                new Vector2(3f, -1.0f), new Vector2(5f, 0.4f),
                new Color(0.2f, 0.22f, 0.3f), sq);

            // Средняя полка: достижима в Normal с земли (top y=0.25, Normal max landable 0.37).
            // Промежуточная ступень для подъёма к HighShelf.
            CreatePlatform(root.transform, "MidShelf",
                new Vector2(12f, 0.0f), new Vector2(2f, 0.5f),
                new Color(0.3f, 0.4f, 0.55f), sq);

            // Высокая полка с финальным коллектаблом — достижима только в Small.
            // Дистанция от MidShelf (правый край x=13) до HighShelf (левый край x=23.5) = 10.5,
            // Normal max horizontal ~6.18 — НЕ хватает,
            // Small max horizontal ~10.1 — тоже впритык НЕ хватает напрямую.
            // Поэтому строго нужен HiddenBridge (виден только Small).
            CreatePlatform(root.transform, "HighShelf",
                new Vector2(25f, 0.5f), new Vector2(3f, 0.5f),
                new Color(0.3f, 0.4f, 0.55f), sq);

            // Декоративный второй уровень платформ в финальной зоне — для разнообразия
            // и опциональной "верхней дороги".
            CreatePlatform(root.transform, "FarShelf",
                new Vector2(45f, -0.2f), new Vector2(2.5f, 0.5f),
                new Color(0.3f, 0.4f, 0.55f), sq);

            // -------- GD-секция: плавающие платформы за основным полом --------
            // Параметризовано через LevelConfig:
            //   - GdPlatformCount: сколько платформ в ряду
            //   - GdPlatformWidth: ширина каждой плитки (Уже = тяжелее точное приземление)
            //   - GdEdgeGap:       пропасть между соседними плитками
            // Часть платформ на уровне земли (top y=-2.5), часть приподнята
            // (top y=-1.75) — даёт визуальный ритм "вверх-вниз" а-ля Geometry Dash.
            // PulseSprite со случайной фазой добавляет неоновое "дыхание".
            var gdBase = new Color(0.32f, 0.42f, 0.6f);
            var gdPeak = new Color(0.65f, 0.85f, 1f);
            // Уровни 2/3 подкрашиваем под их акцентный цвет, чтобы визуально читалось,
            // что это другой уровень (а не та же сцена).
            if (cfg.Index >= 1)
            {
                gdPeak = Color.Lerp(gdPeak, cfg.AccentColor, 0.55f);
            }
            for (int i = 0; i < cfg.GdPlatformCount; i++)
            {
                float x = cfg.GdStartX + i * cfg.GdPitch;
                float y = (i % 2 == 0) ? -2.75f : -2.0f;
                BuildGdPlatform(root.transform, $"GdPlatform_{i + 1}",
                    new Vector2(x, y), cfg.GdPlatformWidth, sq, gdBase, gdPeak);
            }

            // Фон/пропасть растягиваются под фактическую длину уровня.
            float bgCenter = (-34f + cfg.LevelEndX + 1f) * 0.5f;
            float bgWidth  = (cfg.LevelEndX + 1f - (-34f)) + 50f; // +50 запас по краям
            var bg = CreateRect(root.transform, "Background",
                new Vector2(bgCenter, 3f), new Vector2(bgWidth, 30f),
                new Color(0.08f, 0.09f, 0.13f), sq);
            Object.Destroy(bg.GetComponent<BoxCollider2D>());
            bg.GetComponent<SpriteRenderer>().sortingOrder = -10;

            // Звёздное поле — медленно дрейфующие точки на фоне. Даёт
            // ощущение глубины и "технологичности" без необходимости в URP/post-FX.
            BuildStarfield(root.transform, bgCenter, bgWidth);

            var spawn = new GameObject("RespawnPoint");
            spawn.transform.SetParent(root.transform);
            spawn.transform.position = new Vector3(-12f, -1.5f, 0f);

            // Смертельная пропасть. Поднята близко к низу платформ (top y=-3),
            // чтобы падение в GD-секции убивало быстро. Перекрывает всю длину уровня.
            var pit = CreateRect(root.transform, "Pit",
                new Vector2(bgCenter, -5f), new Vector2(bgWidth, 4f),
                new Color(0.55f, 0.07f, 0.1f, 0.9f), sq);
            var pitCol = pit.GetComponent<BoxCollider2D>();
            pitCol.isTrigger = true;
            pit.GetComponent<SpriteRenderer>().sortingOrder = -8;
            pit.AddComponent<Hazard>();

            return spawn.transform;
        }

        private static GameObject CreatePlatform(Transform parent, string name, Vector2 position, Vector2 size, Color color, Sprite sq)
        {
            var go = CreateRect(parent, name, position, size, color, sq);
            go.layer = GroundLayer;
            return go;
        }

        private static void BuildGdPlatform(Transform parent, string name, Vector2 position, float width, Sprite sq, Color baseColor, Color peakColor)
        {
            var p = CreatePlatform(parent, name, position, new Vector2(width, 0.5f), baseColor, sq);
            var pulse = p.AddComponent<Effects.PulseSprite>();
            pulse.Configure(baseColor, peakColor, 1.2f);
        }

        // ---------------------------------------------------------------------
        // Background starfield (ParticleSystem-based)
        // ---------------------------------------------------------------------

        public static void BuildStarfield(Transform parent, float centerX = 47f, float width = 220f, Color? accentOverride = null)
        {
            var go = new GameObject("Starfield");
            if (parent != null) go.transform.SetParent(parent, false);
            // Z=1 уводит частицы за обычные объекты (камера на z=-10 смотрит вперёд).
            go.transform.position = new Vector3(centerX, 4f, 1f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 30f;
            main.loop = true;
            main.startLifetime = 12f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.18f);
            // Тонируем точки в акцент уровня, чтобы фон визуально отличался
            // между уровнями (синева на L1, золото на L2, малиновый на L3).
            // Меню передаёт нейтральный голубой через accentOverride.
            var accent = accentOverride ?? (LevelManager.LevelSelected
                ? LevelManager.Current.AccentColor
                : new Color(0.55f, 0.75f, 1f));
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.55f, 0.75f, 1f, 0.85f),
                new Color(accent.r, accent.g, accent.b, 0.85f));
            main.maxParticles = 350;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 25f;

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(width, 26f, 0f);

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.x = -0.5f;
            velocity.y = 0.05f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = grad;

            // ParticleSystemRenderer должен иметь материал, иначе не отрисуется.
            // Используем тот же Sprites/Default: даёт мягкие точки.
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = -9; // прямо над фоном (-10), но за всем остальным
        }

        private static GameObject CreateRect(Transform parent, string name, Vector2 position, Vector2 size, Color color, Sprite sq)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sq;
            sr.color = color;
            sr.drawMode = SpriteDrawMode.Simple;
            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
            return go;
        }

        // ---------------------------------------------------------------------
        // Player
        // ---------------------------------------------------------------------

        public static GameObject BuildPlayer(ScaleProfile[] profiles, Sprite sq, Transform parent)
        {
            var player = new GameObject("Player");
            if (parent != null) player.transform.SetParent(parent, false);
            player.tag = "Player";
            player.transform.position = new Vector3(-12f, -1.5f, 0f);

            var rb = player.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            var col = player.AddComponent<CapsuleCollider2D>();
            col.size = new Vector2(0.7f, 1f);
            col.direction = CapsuleDirection2D.Vertical;
            var pmat = new PhysicsMaterial2D("PlayerMatRuntime") { friction = 0.05f, bounciness = 0f };
            col.sharedMaterial = pmat;

            // Спрайт ставим непосредственно на корне игрока. Так весь визуал
            // масштабируется вместе с коллайдером, а флип делается через flipX.
            var sr = player.AddComponent<SpriteRenderer>();
            sr.sprite = sq;
            sr.color = Color.white;
            sr.sortingOrder = 5;

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(player.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, -0.55f, 0f);

            var vfx = new GameObject("TransformVfx");
            vfx.transform.SetParent(player.transform, false);
            var ps = vfx.AddComponent<ParticleSystem>();
            var psMain = ps.main;
            psMain.playOnAwake = false;
            vfx.AddComponent<TransformVfxFallback>();

            var energy = player.AddComponent<EnergySystem>();
            var scale = player.AddComponent<PlayerScaleController>();
            var movement = player.AddComponent<PlayerMovement>();
            player.AddComponent<PlayerInput>();

            // Через рефлексию проставляем приватные сериализованные поля.
            SetField(scale, "smallProfile", profiles[0]);
            SetField(scale, "normalProfile", profiles[1]);
            SetField(scale, "bigProfile", profiles[2]);
            SetField(scale, "spriteRenderer", sr);
            SetField(scale, "transformParticles", ps);
            SetField(scale, "energy", energy);

            SetField(movement, "groundCheck", groundCheck.transform);
            SetField(movement, "groundMask", (LayerMask)(1 << GroundLayer));
            SetField(movement, "spriteRenderer", sr);

            // Хвост за игроком — отдельный root-объект, копирует позицию игрока в LateUpdate.
            BuildPlayerTrail(player.transform, parent);

            return player;
        }

        public static void BuildPlayerTrail(Transform target, Transform parent)
        {
            var trailGo = new GameObject("PlayerTrail");
            if (parent != null) trailGo.transform.SetParent(parent, false);
            trailGo.transform.position = target.position;

            var trail = trailGo.AddComponent<TrailRenderer>();
            trail.time = 0.35f;
            trail.minVertexDistance = 0.04f;
            trail.numCornerVertices = 2;
            trail.numCapVertices = 2;
            // Спрайтовый шейдер поддерживает прозрачность и работает в любом
            // рендер-пайплайне (Built-in / URP) без переключения материалов.
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.sortingOrder = 4; // прямо под игроком (тот сортируется на 5)

            var ptr = trailGo.AddComponent<Effects.PlayerTrail>();
            ptr.SetTarget(target);
            ptr.SetTrail(trail);
            ptr.ApplyState(ScaleState.Normal);
        }

        // ---------------------------------------------------------------------
        // Camera (Cinemachine)
        // ---------------------------------------------------------------------

        public static void BuildCamera(Transform target, Transform parent)
        {
            var camGo = new GameObject("Main Camera");
            if (parent != null) camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();
            var brain = camGo.AddComponent<CinemachineBrain>();
            // Явно фиксируем LateUpdate — стабильно работает с Rigidbody2D Interpolate
            // и не "дёргается" при изменении ортосайза.
            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
            brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;

            // Слушатель импульсов на самой Main Camera. Сидит независимо
            // от vcam — шейк работает на любой активной виртуальной камере.
            // m_ChannelMask=1 — тот же канал, что у источника по умолчанию.
            var listener = camGo.AddComponent<CinemachineIndependentImpulseListener>();
            listener.m_ChannelMask = 1;
            listener.m_Gain = 0.6f;
            listener.m_Use2DDistance = true;
            listener.m_UseLocalSpace = true;
            listener.m_ReactionSettings = new CinemachineImpulseListener.ImpulseReaction
            {
                m_AmplitudeGain = 1f,
                m_FrequencyGain = 1f,
                m_Duration = 1f
            };

            var vcamGo = new GameObject("CM vcam Player");
            if (parent != null) vcamGo.transform.SetParent(parent, false);
            var vcam = vcamGo.AddComponent<CinemachineVirtualCamera>();
            vcam.Follow = target;
            vcam.LookAt = target;
            vcam.m_Lens.Orthographic = true;
            vcam.m_Lens.OrthographicSize = 6.5f;

            var transposer = vcam.AddCinemachineComponent<CinemachineFramingTransposer>();
            transposer.m_DeadZoneWidth = 0.2f;
            transposer.m_DeadZoneHeight = 0.25f;
            transposer.m_SoftZoneWidth = 0.6f;
            transposer.m_SoftZoneHeight = 0.6f;
            transposer.m_XDamping = 0.5f;
            transposer.m_YDamping = 0.5f;

            vcamGo.AddComponent<AdaptiveCameraController>();

            // Источник импульсов — отдельный GO. Подвешивается компонент
            // ImpulseRouter, который слушает события игры и при необходимости
            // выпускает импульс GenerateImpulseWithForce(...).
            BuildImpulseRouter(parent);
        }

        public static void BuildImpulseRouter(Transform parent)
        {
            var go = new GameObject("ImpulseRouter");
            if (parent != null) go.transform.SetParent(parent, false);

            var src = go.AddComponent<CinemachineImpulseSource>();
            // m_ImpulseDefinition при AddComponent не вызывает Reset(), поэтому
            // прописываем явно. Bump = короткий отчётливый удар, который
            // хорошо ощущается в 2D-платформере.
            src.m_ImpulseDefinition = new CinemachineImpulseDefinition
            {
                m_ImpulseChannel = 1,
                m_ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump,
                m_CustomImpulseShape = new AnimationCurve(),
                m_ImpulseDuration = 0.25f,
                m_ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform,
                m_DissipationDistance = 100f,
                m_DissipationRate = 0.25f,
                m_PropagationSpeed = 343f
            };
            // Стандартное направление импульса — лёгкое смещение вниз и вбок,
            // получается естественная "встряска" в обоих осях.
            src.m_DefaultVelocity = new Vector3(0.3f, -1f, 0f);

            go.AddComponent<Effects.ImpulseRouter>();
        }

        // ---------------------------------------------------------------------
        // Puzzles
        // ---------------------------------------------------------------------

        public static void BuildPuzzles(Sprite sq, Transform parent)
        {
            var puzzles = new GameObject("Puzzles");
            if (parent != null) puzzles.transform.SetParent(parent, false);
            var cfg = _config;

            // ------------------------------------------------------------
            // 1) PRESSURE PLATE — нужен Big.
            // Плита-триггер на земле. Игрок становится Big и встаёт на неё —
            // ворота поднимаются и остаются открытыми (Close() намеренно не подключён).
            // Высокие ворота (top y=2.5), их нельзя перепрыгнуть ни в одном размере.
            // ------------------------------------------------------------
            var plate = new GameObject("PressurePlate");
            plate.transform.SetParent(puzzles.transform);
            plate.transform.position = new Vector2(-5f, -2.5f);
            plate.transform.localScale = new Vector3(2f, 0.3f, 1f);
            var plateSr = plate.AddComponent<SpriteRenderer>();
            plateSr.sprite = sq;
            plateSr.color = new Color(0.6f, 0.6f, 0.6f);
            plateSr.sortingOrder = 1;
            var plateCol = plate.AddComponent<BoxCollider2D>();
            plateCol.isTrigger = true;
            var pp = plate.AddComponent<PressurePlate>();
            SetField(pp, "plateRenderer", plateSr);
            SetField(pp, "requiredMass", 4f);

            // Делаем ворота заведомо непрыгуемыми (top y=9.5). С таким запасом
            // ни один игрок (даже Small с импульсным трюком от Big→Small) не клерит.
            var gateBlock = CreateRect(puzzles.transform, "MovableGate",
                new Vector2(-2f, 3.5f), new Vector2(0.8f, 12f),
                new Color(0.6f, 0.4f, 0.3f), sq);
            gateBlock.layer = GroundLayer;
            var movable = gateBlock.AddComponent<MovableGate>();
            SetField(movable, "openOffset", new Vector3(0f, 12f, 0f));

            // Только Open — ворота остаются открытыми, чтобы Big успел сойти с плиты
            // и сменить размер для прохода (Big не пролезет под низким потолком дальше).
            BindRuntimeListener(GetEvent(pp, "onActivated"), movable.Open);

            // ------------------------------------------------------------
            // 2) NARROW CEILING — Big не проходит, проход в Normal/Small.
            // Создан в Environment (Ceiling_NarrowGap).
            // ------------------------------------------------------------

            // ------------------------------------------------------------
            // 3) BIG-BREAKABLE — главная "палка" посередине уровня.
            // Толстая оранжевая стена от пола до потолка. Прыгнуть невозможно
            // (top y=2.5 — выше любого апекса), обойти негде. Игрок должен стать
            // Big и врезаться: BigBreakable уничтожает блок и разлетается осколками.
            //
            // Сразу перед стеной кладу маленький "учебный" оранжевый кубик с тем же
            // компонентом — игрок может пнуть его в Big и сразу понять механику.
            // ------------------------------------------------------------
            var hintBlock = CreateRect(puzzles.transform, "BigBreakable_Hint",
                new Vector2(8f, -2.25f), new Vector2(0.5f, 0.5f),
                new Color(1f, 0.55f, 0.2f, 0.95f), sq);
            hintBlock.layer = GroundLayer;
            hintBlock.AddComponent<BigBreakable>();

            // Стена сильно выше, чем апекс любого прыжка (даже Small с буст-импульсом
            // от Big→Small трансформации в воздухе ~2.45). top y = 9.5 — гарантировано
            // непрыгуема ни в каком сочетании размеров и трюков.
            var breakable = CreateRect(puzzles.transform, "BigBreakable_Wall",
                new Vector2(10f, 3.5f), new Vector2(0.9f, 12f),
                new Color(1f, 0.5f, 0.2f), sq);
            breakable.layer = GroundLayer;
            breakable.AddComponent<BigBreakable>();

            // ------------------------------------------------------------
            // 4) HIDDEN PATH — голубой мост виден ТОЛЬКО в Small. Без него
            // ни Normal, ни Small напрямую не дотягиваются от MidShelf
            // до HighShelf (gap 10.5 > Small range 10.1).
            // ------------------------------------------------------------
            var hidden = CreateRect(puzzles.transform, "HiddenBridge",
                new Vector2(18f, 0.0f), new Vector2(3f, 0.4f),
                new Color(0.45f, 0.85f, 1f, 0.25f), sq);
            hidden.layer = GroundLayer;
            var hp = hidden.AddComponent<HiddenPath>();
            SetField(hp, "revealAt", ScaleState.Small);

            // ------------------------------------------------------------
            // 5) RESONANCE (Small) — небольшой кристалл-«сувенир» в нише.
            // Активирует визуальный эффект (просто меняет цвет) — оставлен как
            // демонстрация механики резонанса без блокирующих ворот.
            // ------------------------------------------------------------
            var resSmall = CreateRect(puzzles.transform, "Resonant_SmallCrystal",
                new Vector2(20f, -1.7f), new Vector2(0.7f, 0.7f),
                new Color(0.3f, 0.85f, 1f, 0.85f), sq);
            Object.Destroy(resSmall.GetComponent<BoxCollider2D>());
            var resScriptS = resSmall.AddComponent<ResonantObject>();
            SetField(resScriptS, "targetFrequency", 3f);
            SetField(resScriptS, "spriteRenderer", resSmall.GetComponent<SpriteRenderer>());

            // ------------------------------------------------------------
            // 6) NORMAL-ONLY: жёлтая калиброванная плита.
            // Big (mass 6) — слишком тяжёлый, перегружает плиту → не активна.
            // Small (mass 0.6) — слишком лёгкий → не активна.
            // Только Normal (mass 1.5) попадает в диапазон [1.0, 3.0].
            // Плита открывает жёлтые ворота — высокие, не перепрыгнуть.
            // ------------------------------------------------------------
            var normPlate = new GameObject("NormalCalibratedPlate");
            normPlate.transform.SetParent(puzzles.transform);
            normPlate.transform.position = new Vector2(31f, -2.5f);
            normPlate.transform.localScale = new Vector3(2f, 0.3f, 1f);
            var normPlateSr = normPlate.AddComponent<SpriteRenderer>();
            normPlateSr.sprite = sq;
            normPlateSr.color = new Color(0.95f, 0.85f, 0.3f);
            normPlateSr.sortingOrder = 1;
            var normPlateCol = normPlate.AddComponent<BoxCollider2D>();
            normPlateCol.isTrigger = true;
            var normPp = normPlate.AddComponent<PressurePlate>();
            SetField(normPp, "plateRenderer", normPlateSr);
            SetField(normPp, "requiredMass", 1.0f);
            SetField(normPp, "maxMass", 3.0f);
            SetField(normPp, "idleColor", new Color(0.95f, 0.85f, 0.3f, 1f));
            SetField(normPp, "activeColor", new Color(0.4f, 1f, 0.5f, 1f));

            // Жёлтые ворота тоже делаем высокими: HighShelf (top y=0.75) на x=25
            // достаточно близко, что Small мог бы оттуда дотянуться через старую
            // версию (top y=2.5). Теперь top y=9.5 — гарантированно блокирует.
            var normGate = CreateRect(puzzles.transform, "NormalCalibratedGate",
                new Vector2(34f, 3.5f), new Vector2(0.8f, 12f),
                new Color(0.85f, 0.7f, 0.2f), sq);
            normGate.layer = GroundLayer;
            var normMovable = normGate.AddComponent<MovableGate>();
            SetField(normMovable, "openOffset", new Vector3(0f, 12f, 0f));
            BindRuntimeListener(GetEvent(normPp, "onActivated"), normMovable.Open);

            // ------------------------------------------------------------
            // 7) Visual hint: декоративный жёлтый кристалл рядом с плитой.
            // ResonantObject с frequency=2 (Normal) — пульсирует, когда игрок
            // в Normal-форме, давая визуальную подсказку.
            // ------------------------------------------------------------
            var resNormal = CreateRect(puzzles.transform, "Resonant_NormalCrystal",
                new Vector2(33f, -1.4f), new Vector2(0.7f, 0.7f),
                new Color(0.95f, 0.85f, 0.3f, 0.85f), sq);
            Object.Destroy(resNormal.GetComponent<BoxCollider2D>());
            var resScriptN = resNormal.AddComponent<ResonantObject>();
            SetField(resScriptN, "targetFrequency", 2f);
            SetField(resScriptN, "spriteRenderer", resNormal.GetComponent<SpriteRenderer>());
            SetField(resScriptN, "idleColor", new Color(0.95f, 0.85f, 0.3f, 0.45f));
            SetField(resScriptN, "resonatingColor", new Color(1f, 1f, 0.4f, 1f));

            // ------------------------------------------------------------
            // 8) ФИНАЛЬНАЯ Big-стена — последняя проверка корпусом перед финишем.
            // Перед ней — пара "учебных" блоков-осколков, чтобы было понятно,
            // что и здесь нужно перейти в Big.
            // ------------------------------------------------------------
            // Стопка из двух кубиков (нижний на полу, верхний — на нижнем),
            // чтобы перед финальной стеной ясно было: ломаем Big-формой.
            var hint2a = CreateRect(puzzles.transform, "BigBreakable_Hint2a",
                new Vector2(48.5f, -2.25f), new Vector2(0.5f, 0.5f),
                new Color(1f, 0.55f, 0.2f, 0.95f), sq);
            hint2a.layer = GroundLayer;
            hint2a.AddComponent<BigBreakable>();

            var hint2b = CreateRect(puzzles.transform, "BigBreakable_Hint2b",
                new Vector2(48.5f, -1.75f), new Vector2(0.5f, 0.5f),
                new Color(1f, 0.55f, 0.2f, 0.95f), sq);
            hint2b.layer = GroundLayer;
            hint2b.AddComponent<BigBreakable>();

            // Финальная стена тоже непрыгуема (top y = 9.5). Иначе Small,
            // запрыгнув на FarShelf (top 0.05), мог бы перепрыгнуть стену
            // (Small apex top от FarShelf ~4.76 > 2.5 старой высоты).
            var breakable2 = CreateRect(puzzles.transform, "BigBreakable_Wall_Final",
                new Vector2(51f, 3.5f), new Vector2(0.9f, 12f),
                new Color(1f, 0.5f, 0.2f), sq);
            breakable2.layer = GroundLayer;
            breakable2.AddComponent<BigBreakable>();

            // ------------------------------------------------------------
            // 9) Доп. усложнения для L2/L3.
            // ------------------------------------------------------------
            if (cfg.ExtraBigBreakableWall)
            {
                // Дополнительная Big-стена между Normal-зоной (x=34) и
                // финальной стеной (x=51). Заставляет ещё раз сменить размер
                // на Big после прохождения Normal-калибровки.
                var extraHint = CreateRect(puzzles.transform, "BigBreakable_HintExtra",
                    new Vector2(38.5f, -2.25f), new Vector2(0.5f, 0.5f),
                    new Color(1f, 0.55f, 0.2f, 0.95f), sq);
                extraHint.layer = GroundLayer;
                extraHint.AddComponent<BigBreakable>();

                var extraWall = CreateRect(puzzles.transform, "BigBreakable_Wall_Extra",
                    new Vector2(40f, 3.5f), new Vector2(0.9f, 12f),
                    new Color(1f, 0.5f, 0.2f), sq);
                extraWall.layer = GroundLayer;
                extraWall.AddComponent<BigBreakable>();
            }

            if (cfg.ExtraSpikePillar)
            {
                // Вертикальный столб шипов на L3. Стоит дном на полу (y=-2.5),
                // верх — y=-0.2 (высота 2.3). Чтобы игрок прошёл, его НИЖНЯЯ
                // точка капсулы в апексе должна быть выше top пиллара
                // (иначе во время полёта тело пересечёт hitbox).
                //
                // Apex bottom (центр в апексе минус половина высоты капсулы):
                //   Big    : 0.22 − 0.85 = −0.63 < −0.2 → УПИРАЕТСЯ (Big не пройдёт)
                //   Normal : 0.87 − 0.50 = +0.37 > −0.2 → проходит, запас 0.57
                //   Small  : 1.94 − 0.275 = +1.66 > −0.2 → проходит легко
                //
                // Big не может обойти через FarShelf (top y=0.05): apex top от
                // пола = 1.07, чего не хватает чтобы запрыгнуть на FarShelf.
                // → L3 без переключения в Normal/Small непроходим.
                var pillar = CreateRect(puzzles.transform, "SpikePillar",
                    new Vector2(43f, -1.35f), new Vector2(0.6f, 2.3f),
                    new Color(0.85f, 0.2f, 0.25f), sq);
                pillar.GetComponent<SpriteRenderer>().sortingOrder = 3;
                pillar.AddComponent<Hazard>();

                var pillarPulse = pillar.AddComponent<Effects.PulseSprite>();
                pillarPulse.Configure(new Color(0.85f, 0.2f, 0.25f),
                                       new Color(1f, 0.45f, 0.45f), 3f);

                // Декоративные "шипы-зубья" сверху столба (без коллайдера).
                // Делает препятствие визуально опаснее, не меняя физику.
                for (int i = 0; i < 3; i++)
                {
                    var tooth = new GameObject($"PillarTooth_{i}");
                    tooth.transform.SetParent(pillar.transform.parent, false);
                    tooth.transform.position = new Vector3(43f - 0.2f + i * 0.2f, -0.05f, 0f);
                    tooth.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    tooth.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
                    var tsr = tooth.AddComponent<SpriteRenderer>();
                    tsr.sprite = sq;
                    tsr.color = new Color(1f, 0.45f, 0.45f);
                    tsr.sortingOrder = 4;
                }
            }

            // ------------------------------------------------------------
            // 10) Коллектаблы (5 штук — пятый награждает за GD-секцию).
            // ------------------------------------------------------------
            CreateCollectible(puzzles.transform, new Vector2(-8f, -1.7f), sq);    // у старта
            CreateCollectible(puzzles.transform, new Vector2(3f, -1.7f), sq);     // под низким потолком
            CreateCollectible(puzzles.transform, new Vector2(25f, 1.3f), sq);     // на HighShelf — Small
            CreateCollectible(puzzles.transform, new Vector2(45f, 0.45f), sq);    // на FarShelf
            // Пятый — над одной из средних GD-платформ. Считаем X середины
            // секции, чтобы независимо от количества платформ кристалл оказался
            // на середине прыжкового маршрута и подталкивал не падать.
            int midIdx = cfg.GdPlatformCount / 2;
            float midX = cfg.GdStartX + midIdx * cfg.GdPitch;
            CreateCollectible(puzzles.transform, new Vector2(midX, -0.5f), sq);

            // ------------------------------------------------------------
            // 11) Финиш — триггер на Ground_Right, ближе к правой стене.
            // ------------------------------------------------------------
            var finish = CreateRect(puzzles.transform, "Finish",
                new Vector2(cfg.FinishX, -1f), new Vector2(0.6f, 3f),
                new Color(0.4f, 1f, 0.5f), sq);
            Object.Destroy(finish.GetComponent<BoxCollider2D>());
            var fcol = finish.AddComponent<BoxCollider2D>();
            fcol.isTrigger = true;
            finish.AddComponent<LevelFinish>();

            // Финиш-флаг должен бросаться в глаза. Сильная пульсация
            // между мягко-зелёным и ядовито-салатовым.
            var finishPulse = finish.AddComponent<Effects.PulseSprite>();
            finishPulse.Configure(new Color(0.4f, 1f, 0.5f), new Color(0.85f, 1f, 0.85f), 2f);
        }

        // ---------------------------------------------------------------------
        // Pursuing Spikes — стена смерти, ползущая слева направо
        // ---------------------------------------------------------------------

        public static void BuildPursuingSpikes(Sprite sq, Transform parent)
        {
            var cfg = _config;

            // Корень — позиционируем СНАЧАЛА, чтобы Awake-PursuingSpikes
            // правильно запомнил стартовую координату.
            //
            // Стандартный стартовый X=-22 выбран не случайно: спавн игрока на
            // x=-12, камера ортосайз 6.5 при 16:9 показывает ~11.5 ед. влево
            // → x=-23.5. Стена занимает примерно [-23, -21], правая кромка с
            // шипами попадает в кадр сразу после загрузки. Игрок видит угрозу.
            var wall = new GameObject("PursuingSpikes");
            if (parent != null) wall.transform.SetParent(parent, false);
            wall.transform.position = new Vector3(cfg.SpikeStartX, 1f, 0f);

            // Тёмное тело стены (фон).
            var body = new GameObject("Body");
            body.transform.SetParent(wall.transform, false);
            body.transform.localPosition = new Vector3(-0.6f, 0f, 0f);
            body.transform.localScale = new Vector3(1.2f, 14f, 1f);
            var bodySr = body.AddComponent<SpriteRenderer>();
            bodySr.sprite = sq;
            bodySr.color = new Color(0.4f, 0.05f, 0.08f);
            bodySr.sortingOrder = 3;

            // Светящаяся "горячая" грань на правой стороне стены.
            var edge = new GameObject("HotEdge");
            edge.transform.SetParent(wall.transform, false);
            edge.transform.localPosition = new Vector3(0.05f, 0f, 0f);
            edge.transform.localScale = new Vector3(0.18f, 14f, 1f);
            var edgeSr = edge.AddComponent<SpriteRenderer>();
            edgeSr.sprite = sq;
            edgeSr.color = new Color(1f, 0.35f, 0.35f);
            edgeSr.sortingOrder = 4;

            // Вертикальный ряд "шипов" — это квадраты, повёрнутые на 45°,
            // дополнительно сжатые по X — получаются ромбы-зубья.
            const int spikeCount = 14;
            const float spikeSpacing = 1f;
            float startY = -6.5f;
            for (int i = 0; i < spikeCount; i++)
            {
                var spike = new GameObject($"Spike_{i}");
                spike.transform.SetParent(wall.transform, false);
                spike.transform.localPosition = new Vector3(0.35f, startY + i * spikeSpacing, 0f);
                spike.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                spike.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
                var sr = spike.AddComponent<SpriteRenderer>();
                sr.sprite = sq;
                sr.color = new Color(1f, 0.5f, 0.5f);
                sr.sortingOrder = 5;
            }

            // Один общий триггер-коллайдер, покрывающий всю смертельную зону
            // (тело + торчащие шипы). Hazard уже знает, что нужно сделать
            // isTrigger=true и поднять PlayerDied.
            var col = wall.AddComponent<BoxCollider2D>();
            col.size = new Vector2(1.6f, 14f);
            col.offset = new Vector2(-0.2f, 0f);
            col.isTrigger = true;

            wall.AddComponent<Hazard>();
            // Дальше финиша стена не двигается — иначе на победном экране она
            // наедет на флаг и убьёт игрока случайно. maxX = LevelEndX − 1
            // (чуть-чуть до правой стены). Скорость и старт берутся из конфига.
            var pursuer = wall.AddComponent<PursuingSpikes>();
            SetField(pursuer, "speed", cfg.SpikeSpeed);
            SetField(pursuer, "maxX", cfg.LevelEndX - 1f);
        }

        private static void CreateCollectible(Transform parent, Vector2 pos, Sprite sq)
        {
            var c = new GameObject("Collectible");
            c.transform.SetParent(parent);
            c.transform.position = pos;
            c.transform.localScale = new Vector3(0.4f, 0.4f, 1f);
            var sr = c.AddComponent<SpriteRenderer>();
            sr.sprite = sq;
            sr.color = new Color(0.6f, 1f, 0.8f);
            sr.sortingOrder = 6;
            var col = c.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            c.AddComponent<Collectible>();
        }

        // ---------------------------------------------------------------------
        // UI
        // ---------------------------------------------------------------------

        public static void BuildUI(Transform parent)
        {
            var canvasGo = new GameObject("Canvas");
            if (parent != null) canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem");
            if (parent != null) es.transform.SetParent(parent, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var uiSquare = GetOrCreateSquareSprite();
            var font = GetDefaultUIFont();

            var energyBg = CreateUIRect(canvasGo.transform, "EnergyBG", new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -80), new Vector2(400, -40));
            var bgImg = energyBg.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.55f);
            bgImg.sprite = uiSquare;

            var energyFillGo = CreateUIRect(energyBg.transform, "EnergyFill", new Vector2(0, 0), new Vector2(1, 1), new Vector2(8, 8), new Vector2(-8, -8));
            var fillImg = energyFillGo.AddComponent<Image>();
            fillImg.color = new Color(0.4f, 0.85f, 1f);
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillAmount = 1f;
            fillImg.sprite = uiSquare;

            var energyLabel = CreateUIText(energyBg.transform, "EnergyLabel",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                "100/100", 28, TextAnchor.MiddleCenter, Color.white, font);

            var scaleLabel = CreateUIText(canvasGo.transform, "ScaleLabel",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200, -100), new Vector2(200, -40),
                "НОРМАЛЬНЫЙ", 36, TextAnchor.MiddleCenter, Color.white, font);
            scaleLabel.fontStyle = FontStyle.Bold;

            var scaleIconGo = CreateUIRect(canvasGo.transform, "ScaleIcon", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-30, -150), new Vector2(30, -90));
            var scaleIcon = scaleIconGo.AddComponent<Image>();
            scaleIcon.color = Color.white;
            scaleIcon.sprite = uiSquare;

            var collLabel = CreateUIText(canvasGo.transform, "CollLabel",
                new Vector2(1, 1), new Vector2(1, 1), new Vector2(-360, -60), new Vector2(-40, -20),
                "Кристаллы: 0/3", 26, TextAnchor.MiddleRight, new Color(0.8f, 1f, 0.85f), font);

            CreateUIText(canvasGo.transform, "Help",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 36),
                "A/D или ←/→ — движение     SPACE — прыжок     1/2/3 или Q/E — масштаб     ESC — меню",
                22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.7f), font);

            // Имя текущего уровня. Расположен слева сверху над энергобаром, тонированный
            // в акцентный цвет уровня — так же визуально отличаются разные уровни.
            var lvlConfig = LevelManager.Current;
            var levelLabel = CreateUIText(canvasGo.transform, "LevelLabel",
                new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -38), new Vector2(800, -8),
                lvlConfig.DisplayName + "  •  " + lvlConfig.DifficultyLabel,
                22, TextAnchor.MiddleLeft, lvlConfig.AccentColor, font);
            levelLabel.fontStyle = FontStyle.Bold;

            var winGo = CreateUIRect(canvasGo.transform, "WinPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var winBg = winGo.AddComponent<Image>();
            winBg.color = new Color(0f, 0f, 0f, 0.7f);
            winBg.sprite = uiSquare;
            winGo.SetActive(false);

            var winLabel = CreateUIText(winGo.transform, "WinText",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-500, -40), new Vector2(500, 130),
                "УРОВЕНЬ ПРОЙДЕН!", 84, TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.8f), font);
            winLabel.fontStyle = FontStyle.Bold;

            // Подзаголовок: "Дальше: Уровень 2..." или "Спасибо за игру" на последнем.
            var winSubtitle = CreateUIText(winGo.transform, "WinSubtitle",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-700, -150), new Vector2(700, -50),
                "", 36, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f), font);

            var hud = canvasGo.AddComponent<GameHUD>();
            SetField(hud, "energyFill", fillImg);
            SetField(hud, "energyLabel", energyLabel);
            SetField(hud, "scaleLabel", scaleLabel);
            SetField(hud, "scaleIcon", scaleIcon);
            SetField(hud, "collectibleLabel", collLabel);
            SetField(hud, "winPanel", winGo);
            SetField(hud, "winLabel", winLabel);
            SetField(hud, "winSubtitle", winSubtitle);

            // Полноэкранная вспышка — тонкий цветной слой поверх сцены, но
            // под WinPanel (важно: добавляем ДО win'а нельзя, иначе перекроет).
            // Создаём после, поэтому SiblingIndex выставляем ниже WinPanel.
            var flashGo = CreateUIRect(canvasGo.transform, "ScreenFlash",
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.sprite = uiSquare;
            flashImg.raycastTarget = false; // вспышка не должна перехватывать клики
            // WinPanel создан раньше (последний sibling). Перемещаем его в конец,
            // чтобы он остался поверх вспышки.
            winGo.transform.SetAsLastSibling();
            var flash = flashGo.AddComponent<Effects.ScreenFlash>();
            flash.SetImage(flashImg);
        }

        /// <summary>
        /// Возвращает встроенный шрифт Unity. В новых версиях редактора (2022+)
        /// "Arial.ttf" недоступен — у них поставляется LegacyRuntime.ttf;
        /// в более старых работает Arial. Пробуем оба, чтобы не зависеть от версии.
        /// </summary>
        private static Font GetDefaultUIFont()
        {
            Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        private static Text CreateUIText(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            string text, int fontSize, TextAnchor alignment, Color color, Font font)
        {
            var go = CreateUIRect(parent, name, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = go.AddComponent<Text>();
            t.text = text;
            t.font = font;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        private static GameObject CreateUIRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return go;
        }

        // ---------------------------------------------------------------------
        // Меню выбора уровня
        // ---------------------------------------------------------------------

        /// <summary>
        /// Собирает экран выбора уровня вместо игры. Вызывается из
        /// <see cref="ProjectScale.Core.RuntimeBootstrap"/>, когда
        /// <see cref="LevelManager.LevelSelected"/> = false.
        ///
        /// На экране строится фон со звёздным полем (чтобы и тут было красиво),
        /// заголовок и одна кнопка на каждый уровень из <see cref="LevelManager"/>.
        /// </summary>
        public static void BuildMenu(Transform parent = null)
        {
            var sq = GetOrCreateSquareSprite();

            // 1) Фон + звёзды + камера. Делаем без игрока — нужна простая
            // ортогональная камера, которая просто показывает цвет/частицы.
            var envRoot = new GameObject("MenuEnvironment");
            if (parent != null) envRoot.transform.SetParent(parent, false);

            var bg = CreateRect(envRoot.transform, "MenuBackground",
                new Vector2(0f, 0f), new Vector2(60f, 30f),
                new Color(0.05f, 0.06f, 0.09f), sq);
            Object.Destroy(bg.GetComponent<BoxCollider2D>());
            bg.GetComponent<SpriteRenderer>().sortingOrder = -10;

            // Звёзды по центру (использует Current.AccentColor — у меню это всегда L1).
            BuildStarfield(envRoot.transform, 0f, 60f);

            var camGo = new GameObject("Main Camera");
            if (parent != null) camGo.transform.SetParent(parent, false);
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<UnityEngine.Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6.5f;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.09f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<AudioListener>();

            // 2) UI — Canvas с заголовком, подзаголовком и кнопками.
            var canvasGo = new GameObject("MenuCanvas");
            if (parent != null) canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();

            var es = new GameObject("EventSystem");
            if (parent != null) es.transform.SetParent(parent, false);
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            var uiSquare = sq;
            var font = GetDefaultUIFont();

            // Заголовок и подзаголовок.
            var title = CreateUIText(canvasGo.transform, "Title",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-600, -260), new Vector2(600, -120),
                "PROJECT SCALE", 110, TextAnchor.MiddleCenter,
                new Color(0.85f, 0.95f, 1f), font);
            title.fontStyle = FontStyle.Bold;

            CreateUIText(canvasGo.transform, "Subtitle",
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-600, -340), new Vector2(600, -260),
                "Выберите уровень", 36, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.8f), font);

            // Контроллер меню — на канвасе.
            var menu = canvasGo.AddComponent<UI.LevelSelectMenu>();

            // Кнопки уровней — три большие плитки в ряд.
            int count = LevelManager.Count;
            float buttonWidth = 380f;
            float buttonHeight = 280f;
            float spacing = 60f;
            float totalWidth = count * buttonWidth + (count - 1) * spacing;
            float startX = -totalWidth * 0.5f;
            float buttonY = -100f; // относительно центра экрана

            for (int i = 0; i < count; i++)
            {
                var lvl = LevelManager.Get(i);
                float x = startX + i * (buttonWidth + spacing);
                BuildLevelButton(canvasGo.transform, menu, lvl,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, buttonY - buttonHeight * 0.5f),
                    new Vector2(x + buttonWidth, buttonY + buttonHeight * 0.5f),
                    uiSquare, font);
            }

            // Подсказка внизу.
            CreateUIText(canvasGo.transform, "Footer",
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 60),
                "ESC во время игры — вернуться в это меню",
                22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f), font);
        }

        private static void BuildLevelButton(Transform parent, UI.LevelSelectMenu menu,
            LevelConfig cfg, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Sprite sq, Font font)
        {
            var btnGo = CreateUIRect(parent, $"LevelButton_{cfg.Index + 1}",
                anchorMin, anchorMax, offsetMin, offsetMax);

            var img = btnGo.AddComponent<Image>();
            img.sprite = sq;
            // Прозрачная плитка с тонкой подкладкой акцентного цвета.
            img.color = new Color(cfg.AccentColor.r, cfg.AccentColor.g, cfg.AccentColor.b, 0.18f);

            var btn = btnGo.AddComponent<Button>();
            btn.targetGraphic = img;
            // Цвета по состояниям: при наведении — ярче, при клике — ещё ярче.
            var colors = btn.colors;
            colors.normalColor      = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor    = new Color(1.05f, 1.05f, 1.05f, 1f);
            btn.colors = colors;

            menu.RegisterButton(btn, cfg.Index);

            // Большая цифра уровня.
            CreateUIText(btnGo.transform, "Number",
                new Vector2(0, 0.55f), new Vector2(1, 1), new Vector2(0, 0), new Vector2(0, 0),
                (cfg.Index + 1).ToString(), 160, TextAnchor.MiddleCenter,
                cfg.AccentColor, font).fontStyle = FontStyle.Bold;

            // Имя.
            CreateUIText(btnGo.transform, "Name",
                new Vector2(0, 0.3f), new Vector2(1, 0.55f), new Vector2(20, 0), new Vector2(-20, 0),
                cfg.DisplayName, 28, TextAnchor.MiddleCenter,
                Color.white, font);

            // Сложность.
            CreateUIText(btnGo.transform, "Difficulty",
                new Vector2(0, 0.05f), new Vector2(1, 0.3f), new Vector2(20, 0), new Vector2(-20, 0),
                cfg.DifficultyLabel, 24, TextAnchor.MiddleCenter,
                new Color(cfg.AccentColor.r, cfg.AccentColor.g, cfg.AccentColor.b, 0.85f), font);
        }

        // ---------------------------------------------------------------------
        // GameManager
        // ---------------------------------------------------------------------

        public static void BuildGameManager(Transform player, Transform respawn, Transform parent)
        {
            var go = new GameObject("GameManager");
            if (parent != null) go.transform.SetParent(parent, false);
            var gm = go.AddComponent<GameManager>();
            gm.SetCollectiblesTotal(5);
            gm.SetRespawnPoint(respawn);
            gm.SetPlayer(player);
        }

        // ---------------------------------------------------------------------
        // Reflection helpers
        // ---------------------------------------------------------------------

        private static void SetField(object obj, string fieldName, object value)
        {
            var f = obj.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            f?.SetValue(obj, value);
        }

        private static UnityEvent GetEvent(MonoBehaviour mb, string fieldName)
        {
            var f = mb.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(mb) as UnityEvent;
        }

        private static void BindRuntimeListener(UnityEvent ev, UnityAction action)
        {
            // В рантайме можно использовать обычные runtime-листенеры; они не сохраняются
            // в сцене, но это и не нужно — мы пересобираем мир с нуля при каждом Play.
            if (ev == null)
            {
                Debug.LogWarning("[LevelGenerator] BindRuntimeListener: UnityEvent == null. " +
                                 "Поле должно быть инициализировано через `= new UnityEvent()` в классе-источнике.");
                return;
            }
            ev.AddListener(action);
        }
    }
}
