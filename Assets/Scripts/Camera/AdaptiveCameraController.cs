using UnityEngine;
using Cinemachine;
using ProjectScale.Core;

namespace ProjectScale.CameraSystems
{
    /// <summary>
    /// Адаптивная камера на Cinemachine: меняет ортографический размер в зависимости
    /// от состояния игрока. Маленький игрок — близкий обзор, большой — дальний.
    /// Заметка: «встряску» выполняли через transform vcam, но Cinemachine
    /// FramingTransposer пересчитывает позицию каждый LateUpdate и перетирает её,
    /// поэтому раньше эффект не работал и иногда давал микро-дёргания.
    /// Сейчас оставляем только плавный зум.
    /// </summary>
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public class AdaptiveCameraController : MonoBehaviour
    {
        [Header("Размеры обзора (ортографические)")]
        [SerializeField] private float smallSize  = 5.0f;
        [SerializeField] private float normalSize = 6.5f;
        [SerializeField] private float bigSize    = 8.5f;

        [Header("Параметры")]
        [SerializeField] private float lerpSpeed = 4f;

        private CinemachineVirtualCamera _vcam;
        private float _targetSize;

        private void Awake()
        {
            _vcam = GetComponent<CinemachineVirtualCamera>();
            _targetSize = normalSize;
            _vcam.m_Lens.OrthographicSize = normalSize;
        }

        private void OnEnable()
        {
            GameEvents.OnScaleChanged += HandleScaleChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnScaleChanged -= HandleScaleChanged;
        }

        private void HandleScaleChanged(ScaleState _, ScaleState newState)
        {
            switch (newState)
            {
                case ScaleState.Small:  _targetSize = smallSize;  break;
                case ScaleState.Normal: _targetSize = normalSize; break;
                case ScaleState.Big:    _targetSize = bigSize;    break;
            }
        }

        private void LateUpdate()
        {
            float current = _vcam.m_Lens.OrthographicSize;
            _vcam.m_Lens.OrthographicSize = Mathf.Lerp(current, _targetSize, Time.deltaTime * lerpSpeed);
        }
    }
}
