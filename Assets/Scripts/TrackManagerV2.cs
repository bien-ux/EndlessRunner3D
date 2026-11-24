
using UnityEngine;
using System.Collections.Generic;

public class TrackManagerV2 : MonoBehaviour
{
    public static TrackManagerV2 Instance { get; private set; }

    [Header("INITIAL TRACKS")]
    public TrackSegment[] initialSegments;

    [Header("STRAIGHT TRACK PREFABS")]
    public TrackSegment[] straightPrefabs;

    [Header("TURN TRACK PREFABS")]
    public TrackSegment[] turnPrefabs;

    [Header("PLAYER")]
    public PlayerRunner playerRunner;

    [Header("GENERATION")]
    [Min(30f)]
    public float spawnAheadDistance = 150f;

    [Min(10f)]
    public float cleanupBehindDistance = 60f;

    [Min(1)]
    public int maxSpawnPerFrame = 20;

    [Header("TURN SETTINGS")]
    [Range(0f, 1f)]
    public float turnChance = 0.4f;

    [Min(1)]
    public int minStraightBetweenTurns = 2;

    [Header("CONNECTION CHECK")]
    public float maxConnectionError = 0.05f;

    [Header("DEBUG")]
    public bool showDebugLogs = true;

    private readonly List<TrackSegment> activeSegments =
        new List<TrackSegment>();

    private readonly HashSet<TrackSegment> runtimeSegments =
        new HashSet<TrackSegment>();

    private float totalLength;
    private int straightCount;
    private bool lastWasTurn;
    private bool initialized;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        InitializeTracks();
    }

    private void Update()
    {
        if (!initialized || playerRunner == null)
            return;

        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
            return;

        EnsureTrackAhead();
        CleanupOldTracks();
    }

    // =====================================================
    // INITIALIZE
    // =====================================================

    private void InitializeTracks()
    {
        initialized = false;

        activeSegments.Clear();
        runtimeSegments.Clear();

        totalLength = 0f;
        straightCount = 0;
        lastWasTurn = false;

        if (initialSegments == null ||
            initialSegments.Length == 0)
        {
            Debug.LogError(
                "[TRACK] Chua gan Initial Segments!"
            );
            return;
        }

        TrackSegment previous = null;

        foreach (TrackSegment source in initialSegments)
        {
            if (source == null)
                continue;

            bool isSceneObject =
                source.gameObject.scene.IsValid();

            TrackSegment segment;

            if (isSceneObject)
            {
                segment = source;
            }
            else
            {
                segment = Instantiate(source);

                segment.name = source.name + "_Runtime";

                segment.transform.SetParent(
                    transform,
                    true
                );

                runtimeSegments.Add(segment);

                if (previous != null)
                {
                    if (!AlignSegment(segment, previous))
                    {
                        DestroyRuntimeSegment(segment);
                        continue;
                    }
                }
            }

            segment.BuildPath();

            if (!IsSegmentValid(segment))
            {
                Debug.LogError(
                    "[TRACK] Initial Track khong hop le: " +
                    segment.name
                );

                if (runtimeSegments.Contains(segment))
                    DestroyRuntimeSegment(segment);

                continue;
            }

            segment.StartDistance = totalLength;

            activeSegments.Add(segment);

            totalLength += segment.Length;

            previous = segment;

            Log(
                "Initial: " + segment.name +
                " | Length: " + segment.Length.ToString("F2") +
                " | Total: " + totalLength.ToString("F2")
            );
        }

        if (activeSegments.Count == 0)
        {
            Debug.LogError(
                "[TRACK] Khong co Track hop le!"
            );
            return;
        }

        if (playerRunner == null)
        {
            Debug.LogError(
                "[TRACK] Chua gan Player Runner!"
            );
            return;
        }

        TrackPose startPose;

        if (TryGetPose(0f, out startPose))
        {
            playerRunner.transform.position =
                startPose.position +
                Vector3.up * playerRunner.playerHeightOffset;

            playerRunner.transform.rotation =
                startPose.rotation;

            playerRunner.InitializeTrackPosition();
        }

        initialized = true;

        Log(
            "Khoi tao thanh cong: " +
            activeSegments.Count +
            " Track | Tong chieu dai: " +
            totalLength.ToString("F2")
        );

        // Tao san duong phia truoc ngay khi khoi tao.
        EnsureTrackAhead();
    }

    // =====================================================
    // ENSURE TRACK AHEAD
    // =====================================================

    private void EnsureTrackAhead()
    {
        if (playerRunner == null)
            return;

        float requiredDistance =
            playerRunner.DistanceTravelled +
            Mathf.Max(30f, spawnAheadDistance);

        int spawned = 0;

        while (
            activeSegments.Count > 0 &&
            totalLength < requiredDistance &&
            spawned < maxSpawnPerFrame
        )
        {
            float oldLength = totalLength;

            bool success = SpawnNextSegment();

            if (!success ||
                totalLength <= oldLength + 0.001f)
            {
                Debug.LogError(
                    "[TRACK] Sinh Track that bai! " +
                    "Distance: " +
                    playerRunner.DistanceTravelled.ToString("F2") +
                    " | Total: " +
                    totalLength.ToString("F2") +
                    ". Kiem tra Prefab trong Inspector."
                );

                break;
            }

            spawned++;
        }

        if (spawned > 0)
        {
            Log(
                "Da sinh them " + spawned +
                " Track | Tong Track: " +
                activeSegments.Count +
                " | Tong chieu dai: " +
                totalLength.ToString("F2")
            );
        }
    }

    // =====================================================
    // SPAWN NEXT SEGMENT
    // =====================================================

    private bool SpawnNextSegment()
    {
        if (activeSegments.Count == 0)
            return false;

        TrackSegment previous =
            activeSegments[activeSegments.Count - 1];

        if (previous == null ||
            previous.exitPoint == null)
        {
            Debug.LogError(
                "[TRACK] Track cu thieu ExitPoint!"
            );
            return false;
        }

        bool mustSpawnStraight = lastWasTurn;

        bool shouldTurn =
            !mustSpawnStraight &&
            straightCount >= minStraightBetweenTurns &&
            HasValidPrefab(turnPrefabs) &&
            Random.value < turnChance;

        TrackSegment next = null;
        bool spawnedAsTurn = false;

        if (shouldTurn)
        {
            // Thu sinh khuc cua.
            next = TrySpawnFromArray(
                turnPrefabs,
                previous
            );

            if (next != null)
            {
                spawnedAsTurn = true;
            }
            else
            {
                Log(
                    "Khong tao duoc Turn. Thu Straight."
                );
            }
        }

        // Bat buoc sinh duong thang sau khuc cua.
        if (next == null)
        {
            next = TrySpawnFromArray(
                straightPrefabs,
                previous
            );

            spawnedAsTurn = false;
        }

        // Neu khong phai bat buoc duong thang,
        // thu cac Turn Prefab neu Straight bi loi.
        if (next == null &&
            !mustSpawnStraight &&
            !lastWasTurn)
        {
            Log(
                "Khong tao duoc Straight. Thu Turn thay the."
            );

            next = TrySpawnFromArray(
                turnPrefabs,
                previous
            );

            if (next != null)
                spawnedAsTurn = true;
        }

        if (next == null)
        {
            Debug.LogError(
                "[TRACK] Tat ca Prefab deu khong hop le!"
            );
            return false;
        }

        // Cap nhat thong tin Track.
        next.StartDistance = totalLength;

        activeSegments.Add(next);

        totalLength += next.Length;

        if (spawnedAsTurn)
        {
            lastWasTurn = true;
            straightCount = 0;

            Log(
                "TAO THANH CONG KHUC CUA: " +
                next.name
            );
        }
        else
        {
            lastWasTurn = false;
            straightCount++;

            Log(
                "TAO THANH CONG DUONG THANG: " +
                next.name +
                " | Straight count: " +
                straightCount
            );
        }

        return true;
    }

    // =====================================================
    // TRY SPAWN FROM ARRAY
    // =====================================================

    private TrackSegment TrySpawnFromArray(
        TrackSegment[] prefabs,
        TrackSegment previous)
    {
        if (prefabs == null || prefabs.Length == 0)
            return null;

        List<TrackSegment> candidates =
            new List<TrackSegment>();

        foreach (TrackSegment prefab in prefabs)
        {
            if (prefab == null)
                continue;

            if (prefab.entryPoint == null ||
                prefab.exitPoint == null)
            {
                Debug.LogWarning(
                    "[TRACK] Thieu EntryPoint/ExitPoint: " +
                    prefab.name
                );
                continue;
            }

            candidates.Add(prefab);
        }

        // Tron danh sach de chon ngau nhien.
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);

            TrackSegment temp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = temp;
        }

        foreach (TrackSegment prefab in candidates)
        {
            TrackSegment segment = Instantiate(prefab);

            segment.name = prefab.name + "_Runtime";

            segment.transform.SetParent(
                transform,
                true
            );

            runtimeSegments.Add(segment);

            if (!AlignSegment(segment, previous))
            {
                DestroyRuntimeSegment(segment);
                continue;
            }

            segment.BuildPath();

            if (!IsSegmentValid(segment))
            {
                Debug.LogWarning(
                    "[TRACK] Length khong hop le: " +
                    prefab.name
                );

                DestroyRuntimeSegment(segment);
                continue;
            }

            float positionError = Vector3.Distance(
                segment.entryPoint.position,
                previous.exitPoint.position
            );

            if (positionError > maxConnectionError)
            {
                Debug.LogWarning(
                    "[TRACK] Sai lech ket noi: " +
                    prefab.name +
                    " | Error: " +
                    positionError.ToString("F4")
                );

                DestroyRuntimeSegment(segment);
                continue;
            }

            Log(
                "Ket noi OK: " +
                previous.name +
                " -> " +
                segment.name
            );

            return segment;
        }

        return null;
    }

    // =====================================================
    // ALIGN SEGMENT
    // =====================================================

    private bool AlignSegment(
        TrackSegment segment,
        TrackSegment previous)
    {
        if (segment == null ||
            previous == null ||
            segment.entryPoint == null ||
            previous.exitPoint == null)
        {
            return false;
        }

        segment.AlignEntryTo(
            previous.exitPoint.position,
            previous.exitPoint.rotation
        );

        segment.BuildPath();

        return true;
    }

    // =====================================================
    // VALIDATE
    // =====================================================

    private bool IsSegmentValid(TrackSegment segment)
    {
        if (segment == null)
            return false;

        if (segment.entryPoint == null ||
            segment.exitPoint == null)
            return false;

        if (segment.Length <= 0.1f)
            return false;

        if (float.IsNaN(segment.Length) ||
            float.IsInfinity(segment.Length))
            return false;

        if (Vector3.Distance(
            segment.entryPoint.position,
            segment.exitPoint.position
        ) < 0.1f)
            return false;

        return true;
    }

    private bool HasValidPrefab(
        TrackSegment[] prefabs)
    {
        if (prefabs == null)
            return false;

        foreach (TrackSegment prefab in prefabs)
        {
            if (prefab != null &&
                prefab.entryPoint != null &&
                prefab.exitPoint != null)
                return true;
        }

        return false;
    }

    // =====================================================
    // GET TRACK POSE
    // =====================================================

    public bool TryGetPose(
        float distance,
        out TrackPose pose)
    {
        pose = default;

        if (activeSegments.Count == 0)
            return false;

        // Tim doan duong chua distance.
        for (int i = 0; i < activeSegments.Count; i++)
        {
            TrackSegment segment = activeSegments[i];

            if (segment == null)
                continue;

            if (distance >= segment.StartDistance &&
                distance <= segment.EndDistance)
            {
                return segment.TryGetPose(
                    distance - segment.StartDistance,
                    out pose
                );
            }
        }

        return false;
    }

    // =====================================================
    // CLEANUP OLD TRACKS
    // =====================================================

    private void CleanupOldTracks()
    {
        if (playerRunner == null)
            return;

        float removeBefore =
            playerRunner.DistanceTravelled -
            cleanupBehindDistance;

        for (int i = 0; i < activeSegments.Count - 1; i++)
        {
            TrackSegment segment = activeSegments[i];

            if (segment == null)
            {
                activeSegments.RemoveAt(i);
                i--;
                continue;
            }

            if (segment.EndDistance < removeBefore)
            {
                activeSegments.RemoveAt(i);

                DestroyRuntimeSegment(segment);

                i--;
            }
            else
            {
                break;
            }
        }
    }

    // =====================================================
    // DESTROY RUNTIME ONLY
    // =====================================================

    private void DestroyRuntimeSegment(
        TrackSegment segment)
    {
        if (segment == null)
            return;

        if (runtimeSegments.Remove(segment))
        {
            Destroy(segment.gameObject);
        }
    }

    // =====================================================
    // PUBLIC INFORMATION
    // =====================================================

    public float GetTotalTrackLength()
    {
        return totalLength;
    }

    public int GetActiveTrackCount()
    {
        return activeSegments.Count;
    }

    public bool IsInitialized()
    {
        return initialized;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log("[TRACK] " + message);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
