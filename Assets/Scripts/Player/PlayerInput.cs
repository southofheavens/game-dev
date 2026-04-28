using UnityEngine;
using ProjectScale.Core;

namespace ProjectScale.Player
{
    /// <summary>
    /// Биндинги клавиш для системы трансформации:
    ///   1 — Small
    ///   2 — Normal
    ///   3 — Big
    ///   Q — циклически уменьшать масштаб
    ///   E — циклически увеличивать масштаб
    /// </summary>
    [RequireComponent(typeof(PlayerScaleController))]
    public class PlayerInput : MonoBehaviour
    {
        private PlayerScaleController _scale;

        private void Awake()
        {
            _scale = GetComponent<PlayerScaleController>();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                _scale.TryTransform(ScaleState.Small);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                _scale.TryTransform(ScaleState.Normal);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
                _scale.TryTransform(ScaleState.Big);

            if (Input.GetKeyDown(KeyCode.Q))
                CycleScale(-1);
            else if (Input.GetKeyDown(KeyCode.E))
                CycleScale(1);
        }

        private void CycleScale(int delta)
        {
            int idx = (int)_scale.CurrentState + delta;
            idx = Mathf.Clamp(idx, 0, 2);
            _scale.TryTransform((ScaleState)idx);
        }
    }
}
