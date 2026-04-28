using System.Collections;
using UnityEngine;

namespace ProjectScale.Interactables
{
    /// <summary>
    /// Ворота/дверь, перемещающаяся между двумя позициями. Подключается к плите давления
    /// или резонансному объекту через UnityEvent.
    /// </summary>
    public class MovableGate : MonoBehaviour
    {
        [Header("Позиции")]
        [SerializeField] private Vector3 closedOffset = Vector3.zero;
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 4f, 0f);

        [Header("Параметры")]
        [SerializeField] private float moveDuration = 0.7f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private Vector3 _basePosition;
        private Coroutine _moveRoutine;
        private bool _isOpen;

        private void Awake()
        {
            _basePosition = transform.position;
            transform.position = _basePosition + closedOffset;
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            StartMove(_basePosition + openOffset);
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            StartMove(_basePosition + closedOffset);
        }

        public void Toggle()
        {
            if (_isOpen) Close();
            else Open();
        }

        private void StartMove(Vector3 target)
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = StartCoroutine(MoveTo(target));
        }

        private IEnumerator MoveTo(Vector3 target)
        {
            Vector3 start = transform.position;
            float t = 0f;
            while (t < moveDuration)
            {
                t += Time.deltaTime;
                float k = curve.Evaluate(Mathf.Clamp01(t / moveDuration));
                transform.position = Vector3.LerpUnclamped(start, target, k);
                yield return null;
            }
            transform.position = target;
            _moveRoutine = null;
        }
    }
}
