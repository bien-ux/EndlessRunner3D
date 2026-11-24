
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerRunner : MonoBehaviour
{
    [Header("MOVEMENT")]
    public float forwardSpeed = 8f;
    public float laneDistance = 2.5f;
    public float laneChangeSpeed = 10f;

    [Header("JUMP")]
    public float jumpHeight = 2.5f;
    public float gravity = -20f;

    [Header("TRACK")]
    public float playerHeightOffset = 0f;
    public float fallHeight = -15f;
    public float rotationSpeed = 12f;

    [Header("GROUND DETECTION")]
    public LayerMask groundMask = ~0;
    public float groundCheckStartHeight = 2f;
    public float groundCheckDistance = 6f;
    public float groundCheckRadius = 0.2f;
    public float groundSnapDistance = 0.15f;

    [Header("OBSTACLE DETECTION")]
    public LayerMask obstacleMask = ~0;
    public float obstacleCheckDistance = 0.15f;

    [Header("ANIMATION")]
    public Animator animator;

    [Header("DEBUG")]
    public bool debugJump = true;
    public bool debugGround = false;

    private CharacterController controller;

    private int currentLane = 1;

    private float laneOffset;
    private float laneVelocity;

    private float verticalVelocity;

    private bool isGrounded;
    private bool isJumping;
    private bool initialized;
    private bool gameOverTriggered;

    public float DistanceTravelled { get; private set; }

    public Vector3 CurrentForward { get; private set; }
    public Vector3 CurrentRight { get; private set; }

    public bool IsJumping => isJumping;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        controller.minMoveDistance = 0f;
    }

    // ==================================================
    // INITIALIZE
    // ==================================================

    public void InitializeTrackPosition()
    {
        DistanceTravelled = 0f;

        currentLane = 1;

        laneOffset = 0f;
        laneVelocity = 0f;

        verticalVelocity = 0f;

        isGrounded = false;
        isJumping = false;

        initialized = true;
        gameOverTriggered = false;
    }

    // ==================================================
    // UPDATE
    // ==================================================

    private void Update()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
        {
            SetRunningAnimation(false);
            return;
        }

        if (TrackManagerV2.Instance == null)
            return;

        if (!initialized)
            InitializeTrackPosition();

        HandleInput();

        float speed = GameManager.Instance.CurrentSpeed;

        if (speed <= 0f)
            speed = forwardSpeed;

        DistanceTravelled += speed * Time.deltaTime;

        TrackPose pose;

        if (!TrackManagerV2.Instance.TryGetPose(
            DistanceTravelled, out pose))
        {
            TriggerGameOver();
            return;
        }

        UpdateDirection(pose);

        MoveOnTrack(pose);

        if (transform.position.y < fallHeight)
        {
            if (debugGround)
                Debug.Log("[PLAYER] Roi xuong ho - GAME OVER");

            TriggerGameOver();
            return;
        }

        SetRunningAnimation(true);
    }

    // ==================================================
    // INPUT
    // ==================================================

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.A) ||
            Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentLane = Mathf.Max(0, currentLane - 1);
        }

        if (Input.GetKeyDown(KeyCode.D) ||
            Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentLane = Mathf.Min(2, currentLane + 1);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
    }

    // ==================================================
    // JUMP
    // ==================================================

    private void Jump()
    {
        if (!isGrounded || isJumping)
            return;

        verticalVelocity = Mathf.Sqrt(
            jumpHeight * -2f * gravity
        );

        isJumping = true;
        isGrounded = false;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJump();

        if (debugJump)
            Debug.Log("[PLAYER] JUMP");

    }


    // ==================================================
    // TRACK DIRECTION
    // ==================================================

    private void UpdateDirection(TrackPose pose)
    {
        CurrentForward = Vector3.ProjectOnPlane(
            pose.forward,
            Vector3.up
        ).normalized;

        CurrentRight = Vector3.ProjectOnPlane(
            pose.right,
            Vector3.up
        ).normalized;

        if (CurrentForward.sqrMagnitude < 0.001f)
            CurrentForward = transform.forward;

        if (CurrentRight.sqrMagnitude < 0.001f)
            CurrentRight = transform.right;
    }

    // ==================================================
    // MOVEMENT
    // ==================================================

    private void MoveOnTrack(TrackPose pose)
    {
        float targetLane =
            (currentLane - 1) * laneDistance;

        laneOffset = Mathf.SmoothDamp(
            laneOffset,
            targetLane,
            ref laneVelocity,
            1f / Mathf.Max(laneChangeSpeed, 0.01f)
        );

        // Vi tri muc tieu theo duong chay.
        Vector3 groundPosition =
            pose.position +
            CurrentRight * laneOffset;

        // Kiem tra mat duong thuc te.
        float groundY;

        bool hasGround = CheckGround(
            groundPosition,
            out groundY
        );

        float targetY = groundY + playerHeightOffset;

        // ------------------------------------------
        // XAC DINH NHAN VAT CO DANG TREN MAT DUONG
        // ------------------------------------------

        if (hasGround &&
            !isJumping &&
            verticalVelocity <= 0f &&
            transform.position.y <= targetY + groundSnapDistance)
        {
            isGrounded = true;
            verticalVelocity = -2f;

            // Bam theo do cao mat duong.
            if (transform.position.y < targetY)
            {
                verticalVelocity = 0f;
            }
        }
        else
        {
            isGrounded = false;
        }

        // ------------------------------------------
        // TINH CHUYEN DONG DOC
        // ------------------------------------------

        float verticalMovement;

        if (isGrounded)
        {
            verticalMovement = targetY - transform.position.y;

            if (verticalMovement < 0f)
                verticalMovement = 0f;

            if (debugGround && !isJumping)
                Debug.Log("[PLAYER] Ground detected");
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;

            verticalMovement =
                verticalVelocity * Time.deltaTime;
        }

        // Khi roi xuong va cham lai mat duong.
        if (!isGrounded &&
            !isJumping &&
            hasGround &&
            verticalVelocity < 0f &&
            transform.position.y <= targetY + groundSnapDistance)
        {
            verticalMovement = targetY - transform.position.y;

            verticalVelocity = 0f;
            isGrounded = true;

            if (debugGround)
                Debug.Log("[PLAYER] Landed on ground");
        }

        // ------------------------------------------
        // TINH CHUYEN DONG NGANG
        // ------------------------------------------

        Vector3 targetHorizontal = new Vector3(
            groundPosition.x,
            transform.position.y,
            groundPosition.z
        );

        Vector3 horizontalMovement =
            targetHorizontal - transform.position;

        horizontalMovement = Vector3.ProjectOnPlane(
            horizontalMovement,
            Vector3.up
        );

        Vector3 movement =
            horizontalMovement +
            Vector3.up * verticalMovement;

        // ------------------------------------------
        // KIEM TRA VAT CAN TRUOC KHI DI CHUYEN
        // ------------------------------------------

        if (CheckObstacleSweep(movement))
        {
            TriggerGameOver();
            return;
        }

        // Di chuyen bang CharacterController.
        controller.Move(movement);

        // ------------------------------------------
        // XOAY THEO HUONG DUONG
        // ------------------------------------------

        if (CurrentForward.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    CurrentForward,
                    Vector3.up
                );

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
        }

        // Kiem tra trang thai nhay sau khi di chuyen.
        if (isJumping &&
            !isGrounded &&
            hasGround &&
            verticalVelocity <= 0f &&
            transform.position.y <= targetY + groundSnapDistance)
        {
            transform.position = new Vector3(
                transform.position.x,
                targetY,
                transform.position.z
            );

            verticalVelocity = 0f;
            isJumping = false;
            isGrounded = true;

            if (debugJump)
                Debug.Log("[PLAYER] LANDED");
        }

        // Neu mat duong khong ton tai thi nhan vat tiep tuc roi.
        if (!hasGround && debugGround)
        {
            Debug.Log("[PLAYER] KHONG CO MAT DUONG BEN DUOI!");
        }
    }

    // ==================================================
    // GROUND CHECK
    // ==================================================

    private bool CheckGround(
        Vector3 groundPosition,
        out float groundY)
    {
        groundY = 0f;

        Vector3 origin =
            groundPosition +
            Vector3.up * groundCheckStartHeight;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            groundCheckRadius,
            Vector3.down,
            groundCheckDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        float nearestDistance = float.MaxValue;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // Bo qua collider cua chinh Player.
            if (hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform))
                continue;

            // Khong coi vat can la mat duong.
            if (IsObstacle(hit.collider))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                groundY = hit.point.y;
                found = true;
            }
        }

        return found;
    }

    // ==================================================
    // OBSTACLE SWEEP
    // ==================================================

    private bool CheckObstacleSweep(Vector3 movement)
    {
        float distance = movement.magnitude;

        if (distance < 0.001f)
            return false;

        Bounds bounds = controller.bounds;

        float scaleX = transform.lossyScale.x;
        float scaleY = transform.lossyScale.y;
        float scaleZ = transform.lossyScale.z;

        float radius = controller.radius *
            Mathf.Max(Mathf.Abs(scaleX), Mathf.Abs(scaleZ));

        float height = Mathf.Max(
            controller.height * Mathf.Abs(scaleY),
            radius * 2f
        );

        Vector3 center = bounds.center;

        float halfSegment = Mathf.Max(
            0f,
            height * 0.5f - radius
        );

        Vector3 bottom = center -
            Vector3.up * halfSegment;

        Vector3 top = center +
            Vector3.up * halfSegment;

        Vector3 direction = movement.normalized;

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            radius * 0.9f,
            direction,
            distance + obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Collide
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform))
                continue;

            if (IsObstacle(hit.collider))
            {
                if (debugGround)
                    Debug.Log(
                        "[PLAYER] Phat hien vat can: " +
                        hit.collider.name
                    );

                return true;
            }
        }

        // Kiem tra truong hop dang nam trong Trigger.
        Collider[] overlaps = Physics.OverlapCapsule(
            bottom,
            top,
            radius * 0.9f,
            obstacleMask,
            QueryTriggerInteraction.Collide
        );

        foreach (Collider col in overlaps)
        {
            if (col == null)
                continue;

            if (col.transform == transform ||
                col.transform.IsChildOf(transform))
                continue;

            if (IsObstacle(col))
                return true;
        }

        return false;
    }

    // ==================================================
    // OBSTACLE TAG CHECK
    // ==================================================

    private bool IsObstacle(Collider col)
    {
        if (col == null)
            return false;

        Transform current = col.transform;

        while (current != null)
        {
            if (current.CompareTag("Obstacle"))
                return true;

            current = current.parent;
        }

        return false;
    }

    // ==================================================
    // COLLISION CALLBACKS
    // ==================================================

    private void OnControllerColliderHit(
        ControllerColliderHit hit)
    {
        if (IsObstacle(hit.collider))
        {
            TriggerGameOver();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsObstacle(other))
        {
            TriggerGameOver();
        }
    }

    // ==================================================
    // ANIMATION
    // ==================================================

    private void SetRunningAnimation(bool running)
    {
        if (animator == null)
            return;

        foreach (AnimatorControllerParameter p
                 in animator.parameters)
        {
            if (p.name == "Running" &&
                p.type == AnimatorControllerParameterType.Bool)
            {
                animator.SetBool("Running", running);
                break;
            }
        }
    }

    // ==================================================
    // GAME OVER
    // ==================================================

    private void TriggerGameOver()
    {
        if (gameOverTriggered)
            return;

        gameOverTriggered = true;

        isGrounded = false;
        isJumping = false;

        SetRunningAnimation(false);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHit();

        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
        if (GameManager.Instance != null)
            GameManager.Instance.GameOver();
    }
}
