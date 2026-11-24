
using System.Collections.Generic;
using UnityEngine;

public class CoinSpawner : MonoBehaviour
{
    [Header("COIN PREFAB")]
    public GameObject coinPrefab;

    [Header("PLAYER")]
    public PlayerRunner playerRunner;

    [Header("SPAWN SETTINGS")]
    public float firstSpawnDistance = 25f;
    public float spawnInterval = 22f;
    public int coinsPerRow = 5;
    public float coinSpacing = 2.5f;

    [Header("LANES")]
    public float laneDistance = 2.5f;
    public bool usePlayerLaneDistance = true;

    [Header("COIN POSITION")]
    public float coinHeight = 1.3f;
    public float coinForwardOffset = 0f;

    [Header("ROAD CHECK")]
    public LayerMask roadMask;
    public float rayStartHeight = 4f;
    public float rayDistance = 10f;

    [Header("TURN AVOIDANCE")]
    public float turnCheckDistance = 8f;
    public float turnSafeDistance = 15f;
    public float maxTurnAngle = 12f;

    [Header("CLEANUP")]
    public float cleanupDistance = 35f;

    [Header("DEBUG")]
    public bool showDebugLogs = true;

    private float nextSpawnDistance;

    private class SpawnedCoin
    {
        public GameObject obj;
        public float distance;
    }

    private readonly List<SpawnedCoin> spawnedCoins =
        new List<SpawnedCoin>();

    private void Start()
    {
        if (coinPrefab == null)
        {
            UnityEngine.Debug.LogError("[COIN] Chua gan Coin Prefab!");
            enabled = false;
            return;
        }

        if (playerRunner == null)
        {
            UnityEngine.Debug.LogError("[COIN] Chua gan PlayerRunner!");
            enabled = false;
            return;
        }

        if (usePlayerLaneDistance)
            laneDistance = playerRunner.laneDistance;

        // Không sinh coin tại vị trí bắt đầu của Player.
        nextSpawnDistance =
            playerRunner.DistanceTravelled +
            Mathf.Max(firstSpawnDistance, 40f);

        UnityEngine.Debug.Log(
            "[COIN] Bat dau sinh coin tai distance = " +
            nextSpawnDistance
        );
    }

    private void Update()
    {
        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
            return;

        if (TrackManagerV2.Instance == null ||
            playerRunner == null)
            return;

        int safety = 0;

        while (
            nextSpawnDistance <
            playerRunner.DistanceTravelled + firstSpawnDistance
            && safety < 20
        )
        {
            SpawnCoinRow(nextSpawnDistance);

            nextSpawnDistance += spawnInterval;
            safety++;
        }

        CleanupCoins();
    }

    // =========================================
    // SPAWN COIN ROW
    // =========================================

    private void SpawnCoinRow(float startDistance)
    {
        TrackPose startPose;

        if (!TrackManagerV2.Instance.TryGetPose(
            startDistance, out startPose))
            return;

        if (IsNearTurn(startDistance))
        {
            Log("Bo qua coin gan khuc cua.");
            return;
        }

        // Chon mot trong ba lan.
        int lane = Random.Range(0, 3);

        int spawned = 0;

        for (int i = 0; i < coinsPerRow; i++)
        {
            float distance =
                startDistance + i * coinSpacing;

            if (IsNearTurn(distance))
                break;

            TrackPose pose;

            if (!TrackManagerV2.Instance.TryGetPose(
                distance, out pose))
                break;

            float laneOffset =
                (lane - 1) * laneDistance;
            Vector3 position =
                pose.position +
                pose.right * laneOffset +
                pose.forward * coinForwardOffset +
                Vector3.up * coinHeight;

            // Khong cho coin sinh qua gan Player.
            if (Vector3.Distance(
                position,
                playerRunner.transform.position
            ) < 15f)
            {
                continue;
            }

            if (!HasRoadAt(
                pose.position + pose.right * laneOffset))
            {
                Log("Bo qua coin: khong co mat duong.");
                break;
            }

            Quaternion rotation =
                Quaternion.LookRotation(
                    pose.forward,
                    Vector3.up
                );

            GameObject coin = Instantiate(
                coinPrefab,
                position,
                rotation
            );

            spawnedCoins.Add(new SpawnedCoin
            {
                obj = coin,
                distance = distance
            });

            spawned++;
        }

        Log("Da tao " + spawned + " coin tai lane " + lane);
    }

    // =========================================
    // TURN CHECK
    // =========================================

    private bool IsNearTurn(float distance)
    {
        TrackPose beforePose;
        TrackPose afterPose;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance - turnSafeDistance,
            out beforePose))
            return true;

        if (!TrackManagerV2.Instance.TryGetPose(
            distance + turnSafeDistance,
            out afterPose))
            return true;

        Vector3 before =
            Vector3.ProjectOnPlane(
                beforePose.forward,
                Vector3.up
            ).normalized;

        Vector3 after =
            Vector3.ProjectOnPlane(
                afterPose.forward,
                Vector3.up
            ).normalized;

        float angle = Vector3.Angle(before, after);

        return angle > maxTurnAngle;
    }

    // =========================================
    // ROAD CHECK
    // =========================================

    private bool HasRoadAt(Vector3 position)
    {
        Vector3 origin =
            position + Vector3.up * rayStartHeight;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            rayDistance,
            roadMask,
            QueryTriggerInteraction.Ignore
        );

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (playerRunner != null &&
                hit.collider.transform.IsChildOf(
                    playerRunner.transform))
                continue;

            if (hit.collider.CompareTag("Obstacle") ||
                hit.collider.transform.root.CompareTag("Obstacle"))
                continue;

            return true;
        }

        return false;
    }

    // =========================================
    // CLEANUP
    // =========================================

    private void CleanupCoins()
    {
        for (int i = spawnedCoins.Count - 1; i >= 0; i--)
        {
            SpawnedCoin item = spawnedCoins[i];

            if (item.obj == null ||
                playerRunner.DistanceTravelled >
                item.distance + cleanupDistance)
            {
                if (item.obj != null)
                    Destroy(item.obj);

                spawnedCoins.RemoveAt(i);
            }
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            UnityEngine.Debug.Log("[COIN] " + message);
    }
}
