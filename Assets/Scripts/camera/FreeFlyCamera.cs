//===========================================================================//
//                       FreeFlyCamera (Version 1.2)                         //
//                        (c) 2019 Sergey Stafeyev                           //
//===========================================================================//

using UnityEngine;
using Leap;

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
    [Tooltip("Camera zooming in/out by Mouse Scroll Wheel is active")]
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
    [Tooltip("Speed of the quick camera movement when holding Left Shift")]
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
    [Tooltip("LOCK-ON: How quickly the camera swings to face the target when locking on.")]
    private float _lockSmoothSpeed = 5f;

    [Space]

    [SerializeField]
    [Tooltip("Camera collides with objects instead of passing through them")]
    private bool _enableCollision = true;

    [SerializeField]
    [Tooltip("Radius of the camera's collision bubble")]
    private float _collisionRadius = 0.3f;

    [SerializeField]
    [Tooltip("Layers the camera should collide with")]
    private LayerMask _collisionMask = ~0;

    [SerializeField]
    [Tooltip("Small buffer so the camera doesn't visually clip into surfaces")]
    private float _skinWidth = 0.05f;

    #endregion UI


    #region Leap Motion

    [Space]
    [Header("Leap Motion Movement")]

    [SerializeField]
    [Tooltip("Hand pose that moves the camera forward")]
    private HandPoseDetector _moveForwardPose;

    [SerializeField]
    [Tooltip("Hand pose that moves the camera backward")]
    private HandPoseDetector _moveBackPose;

    [SerializeField]
    [Tooltip("Hand pose that moves the camera left")]
    private HandPoseDetector _moveLeftPose;

    [SerializeField]
    [Tooltip("Hand pose that moves the camera right")]
    private HandPoseDetector _moveRightPose;

    [SerializeField]
    [Tooltip("Hand pose that moves the camera up")]
    private HandPoseDetector _moveUpPose;

    [SerializeField]
    [Tooltip("Hand pose that moves the camera down")]
    private HandPoseDetector _moveDownPose;

    [SerializeField]
    [Tooltip("Movement speed when using Leap Motion")]
    private float _leapMovementSpeed = 10f;

    #endregion Leap Motion


    private CursorLockMode _wantedMode;

    private float _currentIncrease = 1;
    private float _currentIncreaseMem = 0;

    private Vector3 _initPosition;
    private Vector3 _initRotation;

    private bool _isLocked = false;


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

        Cursor.lockState = _wantedMode;
        Cursor.visible = (CursorLockMode.Locked != _wantedMode);
    }


    private void CalculateCurrentIncrease(bool moving)
    {
        _currentIncrease = Time.deltaTime;

        if (!_enableSpeedAcceleration || !moving)
        {
            _currentIncreaseMem = 0;
            return;
        }

        _currentIncreaseMem += Time.deltaTime * (_speedAccelerationFactor - 1);
        _currentIncreaseMem = Mathf.Clamp(_currentIncreaseMem, 0f, 3f);

        _currentIncrease =
            Time.deltaTime +
            Mathf.Pow(_currentIncreaseMem, 3) * Time.deltaTime;
    }


    private void Update()
    {
        if (!_active)
            return;

        SetCursorState();

        if (Input.GetKeyDown(_lockToggleKey) && _lockTarget != null)
        {
            _isLocked = !_isLocked;

            Debug.Log(
                $"[FreeFlyCamera] Lock-on {(_isLocked ? "enabled" : "disabled")} on {_lockTarget.name}"
            );
        }

        if (Cursor.visible)
            return;


        //==============================================================//
        // Translation / Mouse Wheel
        //==============================================================//

        if (_enableTranslation)
        {
            Vector3 translateVector =
                Vector3.forward *
                Input.mouseScrollDelta.y *
                Time.deltaTime *
                _translationSpeed;

            Vector3 worldTranslate =
                transform.TransformDirection(translateVector);

            if (_enableCollision && worldTranslate != Vector3.zero)
                worldTranslate = GetCollisionSafeMove(worldTranslate);

            transform.position += worldTranslate;
        }


        //==============================================================//
        // Movement
        //==============================================================//

        if (_enableMovement)
        {
            Vector3 deltaPosition = Vector3.zero;

            float currentSpeed = _movementSpeed;

            if (Input.GetKey(_boostSpeed))
                currentSpeed = _boostedSpeed;


            //==========================================================//
            // Keyboard movement
            //==========================================================//

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


            //==========================================================//
            // Leap Motion movement
            //==========================================================//

            Vector3 leapMovement = Vector3.zero;


            if (_moveForwardPose != null &&
                _moveForwardPose.GetCurrentlyDetectedPose() != null)
            {
                leapMovement += transform.forward;

                Debug.Log("[FreeFlyCamera] Leap Motion: Forward");
            }


            if (_moveBackPose != null &&
                _moveBackPose.GetCurrentlyDetectedPose() != null)
            {
                leapMovement -= transform.forward;

                Debug.Log("[FreeFlyCamera] Leap Motion: Back");
            }


            if (_moveLeftPose != null &&
                _moveLeftPose.GetCurrentlyDetectedPose() != null)
            {
                leapMovement -= transform.right;

                Debug.Log("[FreeFlyCamera] Leap Motion: Left");
            }


            if (_moveRightPose != null &&
                _moveRightPose.GetCurrentlyDetectedPose() != null)
            {
                leapMovement += transform.right;

                Debug.Log("[FreeFlyCamera] Leap Motion: Right");
            }


            if (_moveUpPose != null &&
                _moveUpPose.GetCurrentlyDetectedPose() != null)
            {
                leapMovement += transform.up;

                Debug.Log("[FreeFlyCamera] Leap Motion: Up");
            }


            if (_moveDownPose != null &&
                _moveDownPose.GetCurrentlyDetectedPose() != null)
            {
                leapMovement -= transform.up;

                Debug.Log("[FreeFlyCamera] Leap Motion: Down");
            }


            // Apply Leap Motion movement
            deltaPosition += leapMovement * _leapMovementSpeed;


            //==========================================================//
            // Apply movement
            //==========================================================//

            CalculateCurrentIncrease(deltaPosition != Vector3.zero);

            Vector3 moveVector =
                deltaPosition *
                currentSpeed *
                _currentIncrease;

            if (_enableCollision && moveVector != Vector3.zero)
                moveVector = GetCollisionSafeMove(moveVector);

            transform.position += moveVector;
        }


        //==============================================================//
        // Camera rotation
        //==============================================================//

        if (_isLocked && _lockTarget != null)
        {
            // Rotation handled in LateUpdate
        }
        else if (_enableRotation)
        {
            transform.rotation *= Quaternion.AngleAxis(
                -Input.GetAxis("Mouse Y") * _mouseSense,
                Vector3.right
            );

            transform.rotation = Quaternion.Euler(
                transform.eulerAngles.x,
                transform.eulerAngles.y +
                Input.GetAxis("Mouse X") * _mouseSense,
                transform.eulerAngles.z
            );
        }


        //==============================================================//
        // Reset position
        //==============================================================//

        if (Input.GetKeyDown(_initPositonButton))
        {
            transform.position = _initPosition;
            transform.eulerAngles = _initRotation;
        }
    }


    private void LateUpdate()
    {
        if (!_active ||
            !_isLocked ||
            _lockTarget == null ||
            Cursor.visible)
            return;

        Vector3 dirToTarget =
            (_lockTarget.position - transform.position).normalized;

        Quaternion lookRot =
            Quaternion.LookRotation(dirToTarget, Vector3.up);

        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                lookRot,
                _lockSmoothSpeed * Time.deltaTime
            );

        Vector3 lockedEuler = transform.eulerAngles;

        transform.rotation =
            Quaternion.Euler(
                lockedEuler.x,
                lockedEuler.y,
                0f
            );
    }


    private Vector3 GetCollisionSafeMove(Vector3 desiredMove)
    {
        float distance = desiredMove.magnitude;

        if (distance <= 0f)
            return Vector3.zero;

        Vector3 direction = desiredMove / distance;

        if (Physics.SphereCast(
            transform.position,
            _collisionRadius,
            direction,
            out RaycastHit hit,
            distance + _skinWidth,
            _collisionMask,
            QueryTriggerInteraction.Ignore))
        {
            float allowedDistance =
                Mathf.Max(hit.distance - _skinWidth, 0f);

            return direction * allowedDistance;
        }

        return desiredMove;
    }
}