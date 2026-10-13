//===========================================================================//
//                       FreeFlyCamera (Version 1.2)                         //
//                        (c) 2019 Sergey Stafeyev                           //
//===========================================================================//

using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FreeFlyCamera : MonoBehaviour
{
    #region UI

    [Space]

    [SerializeField]
    [Tooltip("The script is currently active")]
    private bool _active = true;

    [Space]

    [SerializeField]
    [Tooltip("Camera rotation by mouse movement is active")]
    private bool _enableRotation = true;

    [SerializeField]
    [Tooltip("Sensitivity of mouse rotation")]
    private float _mouseSense = 1.8f;

    [Space]

    [SerializeField]
    [Tooltip("Camera zooming in/out by 'Mouse Scroll Wheel' is active")]
    private bool _enableTranslation = true;

    [SerializeField]
    [Tooltip("Velocity of camera zooming in/out")]
    private float _translationSpeed = 55f;

    [Space]

    [SerializeField]
    [Tooltip("Camera movement by keys is active")]
    private bool _enableMovement = true;

    [SerializeField]
    [Tooltip("Camera movement speed")]
    private float _movementSpeed = 10f;

    [SerializeField]
    [Tooltip("Speed of the quick camera movement when holding the 'Left Shift' key")]
    private float _boostedSpeed = 50f;

    [SerializeField]
    [Tooltip("Boost speed")]
    private KeyCode _boostSpeed = KeyCode.LeftShift;



    [Space]

    [SerializeField]
    [Tooltip("Acceleration at camera movement is active")]
    private bool _enableSpeedAcceleration = true;

    [SerializeField]
    [Tooltip("Rate which is applied during camera movement")]
    private float _speedAccelerationFactor = 2f;

    [Space]

    [SerializeField]
    [Tooltip("This keypress will move the camera to initialization position")]
    private KeyCode _initPositonButton = KeyCode.R;

    [Space]

    [SerializeField]
    [Tooltip("LOCK-ON: The transform the camera should lock its rotation onto.")]
    private Transform _lockTarget;

    [SerializeField]
    [Tooltip("LOCK-ON: Key that toggles lock-on rotation to _lockTarget.")]
    private KeyCode _lockToggleKey = KeyCode.L;

    [SerializeField]
    [Tooltip("LOCK-ON: How quickly the camera swings to face the target when locking on. Higher = snappier.")]
    private float _lockSmoothSpeed = 5f;

    [Space]

    [SerializeField]
    [Tooltip("Camera collides with objects instead of passing through them")]
    private bool _enableCollision = true;

    [SerializeField]
    [Tooltip("Radius of the camera's collision 'bubble'")]
    private float _collisionRadius = 0.3f;

    [SerializeField]
    [Tooltip("Layers the camera should collide with")]
    private LayerMask _collisionMask = ~0; // defaults to "everything"

    [SerializeField]
    [Tooltip("Small buffer so the camera doesn't visually clip into surfaces")]
    private float _skinWidth = 0.05f;

    #endregion UI

    private CursorLockMode _wantedMode;

    private float _currentIncrease = 1;
    private float _currentIncreaseMem = 0;

    private Vector3 _initPosition;
    private Vector3 _initRotation;

    private bool _isLocked = false; // LOCK-ON

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_boostedSpeed < _movementSpeed)
            _boostedSpeed = _movementSpeed;
    }
#endif


    private void Start()
    {
        _initPosition = transform.position;
        _initRotation = transform.eulerAngles;
    }

    private void OnEnable()
    {
        if (_active)
            _wantedMode = CursorLockMode.Locked;
    }

    // Apply requested cursor state
    private void SetCursorState()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = _wantedMode = CursorLockMode.None;
        }

        if (Input.GetMouseButtonDown(0))
        {
            _wantedMode = CursorLockMode.Locked;
        }

        // Apply cursor state
        Cursor.lockState = _wantedMode;
        // Hide cursor when locking
        Cursor.visible = (CursorLockMode.Locked != _wantedMode);
    }

    private void CalculateCurrentIncrease(bool moving)
    {
        _currentIncrease = Time.deltaTime;

        if (!_enableSpeedAcceleration || _enableSpeedAcceleration && !moving)
        {
            _currentIncreaseMem = 0;
            return;
        }

        _currentIncreaseMem += Time.deltaTime * (_speedAccelerationFactor - 1);
        _currentIncreaseMem = Mathf.Clamp(_currentIncreaseMem, 0f, 3f);

        _currentIncrease = Time.deltaTime + Mathf.Pow(_currentIncreaseMem, 3) * Time.deltaTime;
    }

    private void Update()
    {
        if (!_active)
            return;

        SetCursorState();

        // LOCK-ON: toggle
        if (Input.GetKeyDown(_lockToggleKey) && _lockTarget != null)
        {
            _isLocked = !_isLocked;
            Debug.Log($"[FreeFlyCamera] Lock-on {(_isLocked ? "enabled" : "disabled")} on {_lockTarget.name}");
        }

        if (Cursor.visible)
            return;

        // Translation
        if (_enableTranslation)
        {
            Vector3 translateVector = Vector3.forward * Input.mouseScrollDelta.y * Time.deltaTime * _translationSpeed;
            Vector3 worldTranslate = transform.TransformDirection(translateVector);

            if (_enableCollision && worldTranslate != Vector3.zero)
                worldTranslate = GetCollisionSafeMove(worldTranslate);

            transform.position += worldTranslate;
        }

        // Movement
        if (_enableMovement)
        {
            Vector3 deltaPosition = Vector3.zero;
            float currentSpeed = _movementSpeed;

            if (Input.GetKey(_boostSpeed))
                currentSpeed = _boostedSpeed;

            if (Input.GetKey(Lotusim.KeyBindings.Forward))
                deltaPosition += transform.forward;

            if (Input.GetKey(Lotusim.KeyBindings.Backward))
                deltaPosition -= transform.forward;

            if (Input.GetKey(Lotusim.KeyBindings.Left))
                deltaPosition -= transform.right;

            if (Input.GetKey(Lotusim.KeyBindings.Right))
                deltaPosition += transform.right;

            if (Input.GetKey(Lotusim.KeyBindings.Up))
                deltaPosition += transform.up;

            if (Input.GetKey(Lotusim.KeyBindings.Down))
                deltaPosition -= transform.up;

            CalculateCurrentIncrease(deltaPosition != Vector3.zero);

            Vector3 moveVector = deltaPosition * currentSpeed * _currentIncrease;

            if (_enableCollision && moveVector != Vector3.zero)
                moveVector = GetCollisionSafeMove(moveVector);

            transform.position += moveVector;
        }

        if (_isLocked && _lockTarget != null) // LOCK-ON: rotation itself now happens in LateUpdate (see below)
        {
            // Intentionally left empty — see LateUpdate for why.
        }
        else if (_enableRotation)
        {
            transform.rotation *= Quaternion.AngleAxis(
                -Input.GetAxis("Mouse Y") * _mouseSense,
                Vector3.right
            );

            // Paw
            transform.rotation = Quaternion.Euler(
                transform.eulerAngles.x,
                transform.eulerAngles.y + Input.GetAxis("Mouse X") * _mouseSense,
                transform.eulerAngles.z
            );
        }

        if (Input.GetKeyDown(_initPositonButton))
        {
            transform.position = _initPosition;
            transform.eulerAngles = _initRotation;
        }
    }


    private void LateUpdate()
    {
        if (!_active || !_isLocked || _lockTarget == null || Cursor.visible)
            return;

        Vector3 dirToTarget = (_lockTarget.position - transform.position).normalized;
        Quaternion lookRot = Quaternion.LookRotation(dirToTarget, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, _lockSmoothSpeed * Time.deltaTime);

        Vector3 lockedEuler = transform.eulerAngles;
        transform.rotation = Quaternion.Euler(lockedEuler.x, lockedEuler.y, 0f);
    }

    private Vector3 GetCollisionSafeMove(Vector3 desiredMove)
    {
        float distance = desiredMove.magnitude;
        Vector3 direction = desiredMove / distance;

        if (Physics.SphereCast(transform.position, _collisionRadius, direction,
            out RaycastHit hit, distance + _skinWidth, _collisionMask, QueryTriggerInteraction.Ignore))
        {
            float allowedDistance = Mathf.Max(hit.distance - _skinWidth, 0f);
            return direction * allowedDistance;
        }

        return desiredMove;
    }
}