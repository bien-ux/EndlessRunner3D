
using System.Collections.Generic;
using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [Header("DEBUG")]
    public bool debug = true;
    [Header("KIEM TRA AN TOAN KHUC GO")]
    public float logHalfLength = 3.5f;
    public float logHalfWidth = 0.8f;
    public float edgeSafeDistance = 1.2f;
    public float groundCheckSpacing = 0.5f;
    public float groundCheckHeight = 4f;
    public float groundCheckDepth = 10f;

    // Chỉ chọn Layer mặt đường, không chọn Terrain bên dưới hố.
    public LayerMask roadMask;
    [Header("PREFABS")]
    public GameObject logPrefab;
    public GameObject stumpPrefab1;
    public GameObject stumpPrefab2;

    [Header("PLAYER")]
    public PlayerRunner playerRunner;

    [Header("SPAWN")]
    public float spawnDistance = 35f;
    public float spawnInterval = 18f;
    public float spawnYOffset = 0f;

    [Header("LANES")]
    public float laneDistance = 2.5f;
    public bool usePlayerLaneDistance = true;

    [Header("PROBABILITY")]
    [Range(0f, 1f)]
    public float logChance = 0.25f;

    [Header("ROTATION")]
    public Vector3 logRotationOffset =
        new Vector3(0f, 90f, 0f);

    public Vector3 stumpRotationOffset =
        Vector3.zero;

    [Header("TURN AVOIDANCE")]
    [Tooltip("Khoang cach kiem tra huong duong truoc va sau vi tri spawn")]
    public float turnCheckDistance = 8f;

    [Tooltip("Goc thay doi huong toi da cho phep spawn")]
    [Range(5f, 90f)]
    public float maxTurnAngle = 12f;

    [Tooltip("Khong spawn trong khoang cach nay gan khuc cua")]
    public float turnSafeDistance = 18f;

    [Header("GROUND / HOLE CHECK")]
    public LayerMask groundMask = ~0;

    public float groundRayStartHeight = 4f;
    public float groundRayDistance = 10f;

    [Tooltip("Chenh lech do cao mat duong toi da giua cac lan")]
    public float maxGroundHeightDifference = 0.5f;

    [Header("COLLIDER")]
    public bool automaticallyAddColliderIfMissing = true;

    [Tooltip("Them Rigidbody Kinematic neu prefab chua co")]
    public bool automaticallyAddRigidbody = false;

    [Header("CLEANUP")]
    public float cleanupDistance = 30f;

    [Header("DEBUG")]
    public bool showDebugLogs = true;

    private float nextSpawnDistance;

    private class SpawnedObstacle
    {
        public GameObject obj;
        public float distance;
    }

    private readonly List<SpawnedObstacle> spawned =
        new List<SpawnedObstacle>();

    private void Start()
    {
        if (playerRunner == null)
        {
            Debug.LogError(
                "[OBSTACLE] Chua gan PlayerRunner!"
            );
            enabled = false;
            return;
        }

        if (usePlayerLaneDistance)
            laneDistance = playerRunner.laneDistance;

        nextSpawnDistance =
            playerRunner.DistanceTravelled + spawnDistance;

        Log("ObstacleSpawner da khoi dong.");
    }

    private void Update()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
            return;

        if (playerRunner == null ||
            TrackManagerV2.Instance == null)
            return;

        int safety = 0;

        while (
            nextSpawnDistance <
            playerRunner.DistanceTravelled + spawnDistance &&
            safety < 20
        )
        {
            TrySpawnObstacle(nextSpawnDistance);

            // Luon tang khoang cach, ke ca khi vi tri bi bo qua.
            nextSpawnDistance += spawnInterval;
            safety++;
        }

        Cleanup();
    }

    // ==================================================
    // MAIN SPAWN
    // ==================================================

    private void TrySpawnObstacle(float distance)
    {
        TrackPose pose;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance, out pose))
        {
            Log("Bo qua: khong tim thay TrackPose.");
            return;
        }

        // Khong sinh vat can tren hoac gan khuc cua.
        if (IsNearTurn(distance))
        {
            Log(
                "Bo qua vat can: gan khuc cua tai " +
                distance.ToString("F1")
            );
            return;
        }

        bool spawnLog = Random.value < logChance;

        if (spawnLog)
        {
            // Khuc go dai co the chiem nhieu lan.
            // Kiem tra ca 3 lan truoc khi tao.
            if (!HasSafeGroundForLog(pose))
            {
                Log("Bo qua khuc go: mat duong khong an toan.");
                return;
            }

            SpawnLog(pose, distance);
        }
        else
        {
            int lane = Random.Range(0, 3);

            if (!HasSafeGroundInLane(pose, lane))
            {
                // Thu hai lan con lai neu lan duoc chon co ho.
                List<int> lanes = new List<int>();

                for (int i = 0; i < 3; i++)
                {
                    if (i != lane)
                        lanes.Add(i);
                }

                bool found = false;

                for (int i = lanes.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    int temp = lanes[i];
                    lanes[i] = lanes[j];
                    lanes[j] = temp;
                }

                foreach (int alternativeLane in lanes)
                {
                    if (HasSafeGroundInLane(
                        pose, alternativeLane))
                    {
                        lane = alternativeLane;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    Log("Bo qua goc cay: ca 3 lan khong an toan.");
                    return;
                }
            }

            SpawnStump(pose, distance, lane);
        }
    }

    // ==================================================
    // TURN DETECTION
    // ==================================================

    private bool IsNearTurn(float distance)
    {
        float checkDistance = Mathf.Max(
            turnCheckDistance,
            turnSafeDistance
        );

        TrackPose beforePose;
        TrackPose afterPose;
        TrackPose currentPose;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance - checkDistance,
            out beforePose))
            return true;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance + checkDistance,
            out afterPose))
            return true;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance,
            out currentPose))
            return true;

        Vector3 beforeForward =
            Vector3.ProjectOnPlane(
                beforePose.forward,
                Vector3.up
            ).normalized;

        Vector3 afterForward =
            Vector3.ProjectOnPlane(
                afterPose.forward,
                Vector3.up
            ).normalized;

        float totalAngle = Vector3.Angle(
            beforeForward,
            afterForward
        );

        if (totalAngle > maxTurnAngle)
            return true;

        // Kiem tra them tung nua khoang de khong bo sot khuc cua.
        TrackPose leftPose;
        TrackPose rightPose;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance - turnSafeDistance,
            out leftPose))
            return true;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance + turnSafeDistance,
            out rightPose))
            return true;

        float localAngle = Vector3.Angle(
            Vector3.ProjectOnPlane(
                leftPose.forward, Vector3.up).normalized,
            Vector3.ProjectOnPlane(
                rightPose.forward, Vector3.up).normalized
        );

        return localAngle > maxTurnAngle;
    }

    // ==================================================
    // GROUND CHECK
    // ==================================================

    private bool HasGroundAt(
        Vector3 position,
        out RaycastHit groundHit)
    {
        Vector3 origin =
            position +
            Vector3.up * groundRayStartHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundRayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        groundHit = default;

        float nearestDistance = float.MaxValue;
        bool found = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            // Bo qua Player.
            if (playerRunner != null &&
                (hit.collider.transform == playerRunner.transform ||
                 hit.collider.transform.IsChildOf(playerRunner.transform)))
                continue;

            // Khong nhan vat can la mat duong.
            if (IsObstacle(hit.collider))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                groundHit = hit;
                found = true;
            }
        }

        return found;
    }

    private bool HasSafeGroundInLane(
        TrackPose pose,
        int lane)
    {
        float laneX = (lane - 1) * laneDistance;

        Vector3 position =
            pose.position +
            pose.right * laneX;

        RaycastHit hit;

        return HasGroundAt(position, out hit);
    }

    private bool HasSafeGroundForLog(TrackPose pose)
    {
        RaycastHit leftHit;
        RaycastHit centerHit;
        RaycastHit rightHit;

        Vector3 leftPosition =
            pose.position - pose.right * laneDistance;

        Vector3 centerPosition =
            pose.position;

        Vector3 rightPosition =
            pose.position + pose.right * laneDistance;

        if (!HasGroundAt(leftPosition, out leftHit))
            return false;

        if (!HasGroundAt(centerPosition, out centerHit))
            return false;

        if (!HasGroundAt(rightPosition, out rightHit))
            return false;

        float heightDifference = Mathf.Max(
            Mathf.Abs(leftHit.point.y - centerHit.point.y),
            Mathf.Abs(rightHit.point.y - centerHit.point.y)
        );

        return heightDifference <= maxGroundHeightDifference;
    }

    // ==================================================
    // SPAWN LOG
    // ==================================================

    private void SpawnLog(TrackPose pose, float distance)
    {
        if (logPrefab == null)
            return;

        Quaternion rotation =
            Quaternion.LookRotation(pose.forward, Vector3.up) *
            Quaternion.Euler(logRotationOffset);

        // Không sinh khúc gỗ nếu bất kỳ điểm nào trong vùng đặt gỗ
        // không có mặt đường bên dưới.
        if (!IsLogAreaSafe(pose, rotation))
        {
            if (debug)
                Debug.Log("[ObstacleSpawner] Bo qua khuc go: gan mep ho.");
            return;
        }

        Vector3 position =
            pose.position + Vector3.up * spawnYOffset;

        GameObject obj = Instantiate(logPrefab, position, rotation);
        RegisterObstacle(obj, distance);
    }

    private bool IsLogAreaSafe(TrackPose pose, Quaternion rotation)
    {
        Vector3 right = rotation * Vector3.right;
        Vector3 forward = rotation * Vector3.forward;

        // Kiểm tra nhiều điểm trên toàn bộ vùng khúc gỗ,
        // bao gồm cả hai đầu và hai bên.
        for (float x = -logHalfLength;
             x <= logHalfLength + 0.001f;
             x += groundCheckSpacing)
        {
            for (float z = -logHalfWidth;
                 z <= logHalfWidth + 0.001f;
                 z += groundCheckSpacing)
            {
                Vector3 samplePosition =
                    pose.position +
                    right * x +
                    forward * z;

                if (!HasRoadAt(samplePosition))
                    return false;
            }
        }

        // Kiểm tra thêm vùng an toàn ở hai đầu khúc gỗ.
        for (int side = -1; side <= 1; side += 2)
        {
            Vector3 edgePosition =
                pose.position +
                right * (side * (logHalfLength + edgeSafeDistance));

            if (!HasRoadAt(edgePosition))
                return false;
        }

        return true;
    }

    private bool HasRoadAt(Vector3 position)
    {
        Vector3 origin =
            position + Vector3.up * groundCheckHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundCheckDepth,
            roadMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (playerRunner != null &&
                hit.collider.transform.IsChildOf(playerRunner.transform))
                continue;

            return true;
        }

        return false;
    }

    // ==================================================
    // SPAWN STUMP
    // ==================================================

    private void SpawnStump(
        TrackPose pose,
        float distance,
        int lane)
    {
        GameObject prefab =
            Random.value < 0.5f
            ? stumpPrefab1
            : stumpPrefab2;

        if (prefab == null)
        {
            prefab = stumpPrefab1 != null
                ? stumpPrefab1
                : stumpPrefab2;
        }

        if (prefab == null)
        {
            Log("Chua gan Stump Prefab.");
            return;
        }

        float laneX =
            (lane - 1) * laneDistance;

        Vector3 position =
            pose.position +
            pose.right * laneX +
            Vector3.up * spawnYOffset;

        Quaternion rotation =
            Quaternion.LookRotation(
                pose.forward,
                Vector3.up
            ) *
            Quaternion.Euler(stumpRotationOffset);

        GameObject obj = Instantiate(
            prefab,
            position,
            rotation
        );

        RegisterObstacle(obj, distance);

        Log("Da sinh goc cay tai lane " + lane);
    }

    // ==================================================
    // REGISTER OBSTACLE
    // ==================================================

    private void RegisterObstacle(
        GameObject obj,
        float distance)
    {
        if (obj == null)
            return;

        // Gan Tag cho root va toan bo children.
        SetObstacleTagRecursively(obj.transform);

        Collider[] colliders =
            obj.GetComponentsInChildren<Collider>(true);

        if (colliders.Length == 0 &&
            automaticallyAddColliderIfMissing)
        {
            // Collider tam cho prefab khong co Collider.
            BoxCollider box = obj.AddComponent<BoxCollider>();

            box.center = Vector3.zero;
            box.size = Vector3.one;

            Log(
                "Canh bao: " + obj.name +
                " khong co Collider. Da them BoxCollider tam."
            );

            colliders = obj.GetComponentsInChildren<Collider>(true);
        }

        if (colliders.Length == 0)
        {
            Debug.LogError(
                "[OBSTACLE] " + obj.name +
                " khong co Collider!"
            );
        }

        if (automaticallyAddRigidbody &&
            obj.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = obj.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        spawned.Add(new SpawnedObstacle
        {
            obj = obj,
            distance = distance
        });
    }

    private void SetObstacleTagRecursively(Transform target)
    {
        if (target == null)
            return;

        try
        {
            target.gameObject.tag = "Obstacle";
        }
        catch (UnityException)
        {
            Debug.LogWarning(
                "[OBSTACLE] Chua tao Tag Obstacle!"
            );
        }

        for (int i = 0; i < target.childCount; i++)
        {
            SetObstacleTagRecursively(
                target.GetChild(i)
            );
        }
    }

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
    // CLEANUP
    // ==================================================

    private void Cleanup()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            SpawnedObstacle item = spawned[i];

            if (item.obj == null ||
                playerRunner.DistanceTravelled >
                item.distance + cleanupDistance)
            {
                if (item.obj != null)
                    Destroy(item.obj);

                spawned.RemoveAt(i);
            }
        }
    }

    // ==================================================
    // DEBUG
    // ==================================================

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log("[OBSTACLE] " + message);
    }

    private void OnDrawGizmosSelected()
    {
        if (playerRunner == null)
            return;

        Gizmos.color = Color.yellow;

        Vector3 position = playerRunner.transform.position;

        Gizmos.DrawWireSphere(
            position + Vector3.forward * spawnDistance,
            0.5f
        );
    }
}
