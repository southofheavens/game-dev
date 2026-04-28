using UnityEngine;

namespace ProjectScale.Core
{
    /// <summary>
    /// Параметры одного уровня. Различия между уровнями сводятся к разным значениям
    /// этих полей: скорость стены смерти, конфигурация GD-секции (число платформ,
    /// их ширина, размер пропастей), наличие дополнительных препятствий.
    ///
    /// LevelGenerator читает текущий <see cref="LevelManager.Current"/> и собирает
    /// уровень в соответствии с конфигом. Все производные координаты (правый край
    /// уровня, позиция Wall_Right, max-X для шипов) считаются здесь же — другим
    /// модулям достаточно прочитать готовое значение.
    /// </summary>
    public class LevelConfig
    {
        public int Index;
        public string DisplayName;
        public string DifficultyLabel;
        public Color AccentColor = Color.white;

        // ---- Стена смерти (PursuingSpikes) ----
        public float SpikeStartX = -22f;
        public float SpikeSpeed = 1.3f;

        // ---- GD-секция (плавающие платформы над пропастью) ----
        public int   GdPlatformCount = 8;
        public float GdPlatformWidth = 2f;
        public float GdEdgeGap = 5f;     // расстояние от ПРАВОГО края одной платформы до ЛЕВОГО следующей
        public float GdStartX = 59f;     // X центра первой GD-платформы

        // ---- Доп. усложнения (выключены на L1, включаются на L2/L3) ----
        public bool ExtraBigBreakableWall;  // дополнительная Big-стена в основном секторе
        public bool ExtraSpikePillar;       // вертикальный столб шипов между Normal-зоной и финальной стеной

        // ---- Постоянные параметры мира (одинаковые на всех уровнях) ----
        public float MainGroundEndX  = 53f;
        public float GroundRightWidth = 14f;

        // ---- Производные координаты ----
        /// <summary>Расстояние между центрами соседних GD-платформ.</summary>
        public float GdPitch => GdPlatformWidth + GdEdgeGap;

        /// <summary>Правый край последней GD-платформы.</summary>
        public float LastGdPlatformRightEdge =>
            GdStartX + (GdPlatformCount - 1) * GdPitch + GdPlatformWidth * 0.5f;

        /// <summary>Левый край Ground_Right (финишной площадки).</summary>
        public float GroundRightLeftEdge => LastGdPlatformRightEdge + GdEdgeGap;

        /// <summary>X центра Ground_Right.</summary>
        public float GroundRightCenterX => GroundRightLeftEdge + GroundRightWidth * 0.5f;

        /// <summary>Правый край уровня (там стена и за ней ничего).</summary>
        public float LevelEndX => GroundRightLeftEdge + GroundRightWidth;

        /// <summary>X центра финиш-флага — ближе к правому краю Ground_Right.</summary>
        public float FinishX => GroundRightLeftEdge + GroundRightWidth - 3f;
    }
}
