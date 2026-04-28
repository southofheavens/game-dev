using UnityEngine;

namespace ProjectScale.Core
{
    /// <summary>
    /// ScriptableObject, описывающий физические и визуальные свойства одного состояния масштаба.
    /// Используется системой масштабирования для применения параметров к персонажу.
    /// </summary>
    [CreateAssetMenu(menuName = "ProjectScale/Scale Profile", fileName = "ScaleProfile")]
    public class ScaleProfile : ScriptableObject
    {
        [Header("Идентификатор")]
        public ScaleState state = ScaleState.Normal;

        [Header("Геометрия")]
        [Tooltip("Множитель размера графики и коллайдера")]
        public float scale = 1f;

        [Header("Физика")]
        [Tooltip("Масса Rigidbody2D. Влияет на инерцию и активацию тяжёлых механизмов")]
        public float mass = 1f;
        [Tooltip("Линейное затухание (drag) — чем выше, тем быстрее персонаж останавливается")]
        public float linearDrag = 0.5f;
        [Tooltip("Гравитационный множитель")]
        public float gravityScale = 3f;

        [Header("Управление")]
        [Tooltip("Максимальная скорость горизонтального движения")]
        public float moveSpeed = 7f;
        [Tooltip("Сила прыжка (импульс по Y)")]
        public float jumpForce = 13f;
        [Tooltip("Множитель ускорения на земле")]
        public float groundAcceleration = 60f;
        [Tooltip("Множитель ускорения в воздухе")]
        public float airAcceleration = 25f;

        [Header("Энергия")]
        [Tooltip("Стоимость перехода в это состояние (единицы энергии)")]
        public float transformCost = 25f;

        [Header("Визуал")]
        [Tooltip("Цвет персонажа в этом состоянии (вспомогательный визуальный маркер)")]
        public Color tintColor = Color.white;
        [Tooltip("Радиус источника света 2D, прикреплённого к персонажу")]
        public float lightRadius = 3f;
        [Tooltip("Интенсивность света")]
        public float lightIntensity = 1f;

        [Header("Резонанс")]
        [Tooltip("Частота резонанса. Объекты с такой же частотой реагируют на этот размер")]
        public float resonanceFrequency = 1f;
    }
}
