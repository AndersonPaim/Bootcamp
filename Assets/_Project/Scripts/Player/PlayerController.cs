using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class PlayerController : MonoBehaviour, IDamageable
{
    public delegate void SetCheckpointHandler();
    public SetCheckpointHandler OnSetCheckpoint;

    public delegate void PlayerInputHandler(PlayerData playerData);
    public PlayerInputHandler OnPlayerInput;

    public delegate void DeathHandler();
    public DeathHandler OnDeath;

    public delegate void TakeDamageHandler();
    public TakeDamageHandler OnTakeDamage;

    [SerializeField] private CinemachineVirtualCamera _camera;

    [SerializeField] private PlayerBalancer _playerBalancer;

    [SerializeField] private GameObject _dashCollider;

    private float _jumpsCount = 2;
    private float _movementX = 0;
    private float _movementY = 0;
    private float _velocity = 0;
    private float _lastDirection;

    private bool _isGrounded = true;
    private bool _canDash = false;
    private bool _isDashing = false;
    private bool _isAttacking = false;
    private bool _isWalking = false;
    private bool _isRunning = false;
    private bool _isJumping = false;
    private bool _isDoubleJumping = false;
    private bool _isPaused = false;
    private bool _isVulnerable = true;

    private Rigidbody _rb;

    private Quaternion _playerRotationLeft;
    private Quaternion _playerRotationRight;

    private PlayerData _playerData;

    private CinemachineBasicMultiChannelPerlin _cameraNoise;

    public void TakeDamage(float damage, GameObject attacker)
    {
        if(!_isVulnerable)
        {
            return;
        }

        Vector3 knockbackDirection = new Vector3(0, 0, gameObject.transform.position.z - attacker.transform.position.z);
        _rb.velocity = new Vector3(0, 0, knockbackDirection.z) * 40;

        StartCoroutine(VurnerabilityDelay());
        OnTakeDamage?.Invoke();
    }

    private void Start()
    {
        Initialize();

        Gravity();
    }

    private void Update()
    {
        CreatePlayerStruct();
        GroundCheck();
        OnPlayerInput?.Invoke(_playerData);
        _playerData = new PlayerData();
    }

    private void OnDestroy()
    {
        RemoveDelegates();
    }

    private void SetupDelegates()
    {
        GameManager.sInstance.GetInputListener().OnInput += ReceiveInputs;
        GameManager.sInstance.OnGetRespawnPosition += DeathMovementDelay;
        GameManager.sInstance.GetInGameMenu().OnPause += PauseInputs;
    }

    private void RemoveDelegates()
    {
        GameManager.sInstance.GetInputListener().OnInput -= ReceiveInputs;
        GameManager.sInstance.OnGetRespawnPosition -= DeathMovementDelay;
        GameManager.sInstance.GetInGameMenu().OnPause -= PauseInputs;
    }

    private void Initialize()
    {
        Cursor.lockState = CursorLockMode.Locked;
        _rb = GetComponent<Rigidbody>();
        _playerRotationLeft = Quaternion.Euler(0, 180, 0);
        _playerRotationRight = Quaternion.Euler(0, 0, 0);
        _playerData = new PlayerData();
        _cameraNoise = _camera.GetCinemachineComponent<Cinemachine.CinemachineBasicMultiChannelPerlin>();

        SetupDelegates();
    }

    private void PauseInputs(bool isPaused)
    {
        _isPaused = isPaused;
    }

    private void DeathMovementDelay()
    {
        _isPaused = true;
        _velocity = 0;
        _movementX = 0;
        ResetForces();
        StartCoroutine(MovementDelay());
    }

    private IEnumerator MovementDelay()
    {
        yield return new WaitForSeconds(0.5f);
        _isPaused = false;
    }

    private void ReceiveInputs(InputData inputData)
    {
        if (!_isPaused)
        {
            Movement(inputData.MovementX, inputData.MovementY);
            Jump(inputData.Jump);
            Dash(inputData.Dash);
            Run(inputData.Run);
            Attack(inputData.Attack);
        }
    }

    private void CreatePlayerStruct()
    {
        _playerData.Walk = _isWalking;
        _playerData.OnGround = _isGrounded;
        _playerData.Jump = _isJumping;
        _playerData.Dash = _isDashing;
        _playerData.DoubleJump = _isDoubleJumping;
        _playerData.Movement = _movementX;
        _playerData.Velocity = _velocity;
        _playerData.Attack = _isAttacking;
    }


    private void Movement(float directionX, float directionY)
    {
        _movementX = directionX;
        _movementY = directionY;

        Vector3 vel = _rb.velocity;

        PlayerRotation(directionX);
        SetVelocity(directionX);

        if (directionX != 0 && _isDashing == false)
        {
            vel.z = directionX * _playerBalancer.speedMultiplier * _velocity;
            _rb.velocity = vel;

            _lastDirection = directionX;
        }
        else if(directionX == 0 && _isDashing == false)
        {
            vel.z = _lastDirection * _playerBalancer.speedMultiplier * _velocity;

            _rb.velocity = vel;
        }
    }

    private void SetVelocity(float direction)
    {
        if (direction != 0 && _isDashing == false)
        {
            if (_isRunning)
            {
                if (_velocity < _playerBalancer.runSpeed)
                {
                    _velocity += Time.deltaTime * _playerBalancer.acceleration;
                }
            }
            else
            {
                if (_velocity <= _playerBalancer.walkSpeed)
                {
                    _velocity += Time.deltaTime * _playerBalancer.acceleration;
                    if (_velocity > _playerBalancer.walkSpeed)
                    {
                        _velocity = _playerBalancer.walkSpeed;
                    }
                }
                else
                {
                    _velocity -= Time.deltaTime * _playerBalancer.deceleration;
                }
            }
        }
        else if (direction == 0 && _isDashing == false)
        {
            if (_velocity > 0)
            {
                _velocity -= Time.deltaTime * _playerBalancer.deceleration;
            }
            else if (_velocity <= 0)
            {
                _velocity = 0;
            }
        }
    }

    private void PlayerRotation(float direction)
    {
        if(direction < 0)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _playerRotationLeft, 1f);
            _isWalking = true;
        }
        else if(direction > 0)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, _playerRotationRight, 1f);
            _isWalking = true;
        }
        else
        {
            _isWalking = false;
        }
    }

    private void Run(bool isRunning)
    {
        if (isRunning)
        {
            if (_isWalking)
            {
                _isRunning = true;
            }
        }
        else
        {
            _isRunning = false;
        }
    }

    private void ResetForces()
    {
        _rb.velocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    private void Jump(bool isJumping)
    {
        if (isJumping)
        {
            if (_jumpsCount > 0)
            {
                if(_jumpsCount == 1)
                {
                    _isDoubleJumping = true;
                }
                ResetForces();
                _rb.AddForce(transform.up * _playerBalancer.jumpForce, ForceMode.Impulse);
                StartCoroutine(JumpCount());
            }
        }
    }

    private void Attack(bool isAttacking)
    {
        _isAttacking = isAttacking;
    }

    private IEnumerator JumpCount()
    {
        yield return new WaitForFixedUpdate();
        _jumpsCount--; //a verifica��o de ch�o anulava esse comando | arrumar depois
        _isJumping = true;
    }

    private void Dash(bool isDashing)
    {
        if (isDashing && _canDash)
        {
            Physics.IgnoreLayerCollision(8, 10, true);
            _isVulnerable = false;
            _dashCollider.SetActive(true);
            _canDash = false;
            _isDashing = true;
            ResetForces();
            CameraShake(3, 1);

            Vector3 dir = new Vector3(0, 0, 0);

            if(_movementY > 0 && _movementX != 0)
            {
                dir.y = Mathf.Abs(_movementY * 0.5f);
                dir.z = Mathf.Abs(_movementX * 0.5f);
            }
            else if(_movementY > 0)
            {
                dir.y = _movementY * 0.7f;
            }
            else
            {
                dir.z = 1;
            }

            _rb.AddRelativeForce(dir * _playerBalancer.dashForce, ForceMode.Impulse);
            StartCoroutine(StopDash(_playerBalancer.dashTime));
        }
    }

    public IEnumerator StopDash(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);
        Physics.IgnoreLayerCollision(8, 10, false);
        _isVulnerable = true;
        _isDashing = false;
        _dashCollider.SetActive(false);
        CameraShake(0, 0);
        ResetForces();
    }

    private IEnumerator VurnerabilityDelay()
    {
        _isVulnerable = false;
        yield return new WaitForSeconds(1);
        _isVulnerable = true;
    }

    private void CameraShake(float amplitude, float frequency)
    {
        _cameraNoise.m_AmplitudeGain = amplitude;
        _cameraNoise.m_FrequencyGain = frequency;
    }

    private void Gravity()
    {
        Physics.gravity = _playerBalancer.gravity;
    }

    private void GroundCheck()
    {
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.1f);

        if (_isGrounded)
        {
            _canDash = true;
            _jumpsCount = 2;
            _isJumping = false;
            _isDoubleJumping = false;
        }
    }

    private void Death()
    {
        if(!_isVulnerable)
        {
            return;
        }

        StartCoroutine(VurnerabilityDelay());
        OnDeath?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        LayerMask checkpointLayer = LayerMask.NameToLayer("Checkpoints");

        if (other.gameObject.layer == checkpointLayer)
        {
            OnSetCheckpoint?.Invoke();
            other.gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        LayerMask trapsLayer = LayerMask.NameToLayer("Traps");

        if (collision.collider.gameObject.layer == trapsLayer)
        {
            Death();
        }
    }
}
