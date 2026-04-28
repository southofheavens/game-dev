using UnityEngine;

namespace ProjectScale.Player
{
    /// <summary>
    /// Движение персонажа с учётом текущего ScaleProfile.
    /// Используем Mathf.MoveTowards для скорости — без осцилляций, как у AddForce + drag.
    /// Флип реализован через SpriteRenderer.flipX, чтобы не инвертировать scale корня
    /// (иначе ломается коллайдер и появляется микро-дрожание у стен).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerScaleController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Земля")]
        [SerializeField] private Transform groundCheck;
        [SerializeField] private float groundCheckRadius = 0.18f;
        [SerializeField] private LayerMask groundMask;

        [Header("Coyote / Buffer")]
        [Tooltip("Время после ухода с края, в течение которого ещё можно прыгнуть")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("Время предвосхищения прыжка перед приземлением")]
        [SerializeField] private float jumpBuffer = 0.12f;

        [Header("Гашение прыжка")]
        [Tooltip("Если игрок отпустил прыжок, восходящая скорость гасится в этот раз")]
        [SerializeField] private float jumpCutMultiplier = 0.5f;

        [Header("Визуал")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        private Rigidbody2D _rb;
        private PlayerScaleController _scale;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _isGrounded;
        private bool _facingRight = true;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _scale = GetComponent<PlayerScaleController>();
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        private void Update()
        {
            _coyoteTimer -= Time.deltaTime;
            _jumpBufferTimer -= Time.deltaTime;

            if (Input.GetButtonDown("Jump")
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.W)
                || Input.GetKeyDown(KeyCode.UpArrow))
            {
                _jumpBufferTimer = jumpBuffer;
            }

            if ((Input.GetButtonUp("Jump")
                 || Input.GetKeyUp(KeyCode.Space)
                 || Input.GetKeyUp(KeyCode.W)
                 || Input.GetKeyUp(KeyCode.UpArrow)) && _rb.velocity.y > 0f)
            {
                _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y * jumpCutMultiplier);
            }
        }

        private void FixedUpdate()
        {
            UpdateGrounded();

            var profile = _scale.CurrentProfile;
            if (profile == null) return;

            float input = Input.GetAxisRaw("Horizontal");
            float targetSpeed = input * profile.moveSpeed;
            float accel = _isGrounded ? profile.groundAcceleration : profile.airAcceleration;
            float maxDelta = accel * Time.fixedDeltaTime;
            float newSpeed = Mathf.MoveTowards(_rb.velocity.x, targetSpeed, maxDelta);
            _rb.velocity = new Vector2(newSpeed, _rb.velocity.y);

            if (input > 0.05f && !_facingRight) Flip();
            else if (input < -0.05f && _facingRight) Flip();

            if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
            {
                _rb.velocity = new Vector2(_rb.velocity.x, profile.jumpForce);
                _jumpBufferTimer = 0f;
                _coyoteTimer = 0f;
            }
        }

        private void UpdateGrounded()
        {
            if (groundCheck == null)
            {
                _isGrounded = false;
                return;
            }
            _isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundMask);
            if (_isGrounded) _coyoteTimer = coyoteTime;
        }

        private void Flip()
        {
            _facingRight = !_facingRight;
            if (spriteRenderer != null)
                spriteRenderer.flipX = !_facingRight;
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheck == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
        }
    }
}
