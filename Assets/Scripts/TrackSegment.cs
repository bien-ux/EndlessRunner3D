
using UnityEngine;
using System.Collections.Generic;

public struct TrackPose
{
    public Vector3 position;
    public Vector3 forward;
    public Vector3 right;
    public Quaternion rotation;

    public TrackPose(Vector3 p, Vector3 f)
    {
        position = p;

        if (f.sqrMagnitude < 0.0001f)
            f = Vector3.forward;

        forward = f.normalized;

        right = Vector3.Cross(
            Vector3.up, forward
        ).normalized;

        if (right.sqrMagnitude < 0.0001f)
            right = Vector3.right;

        Vector3 up = Vector3.Cross(
            forward, right
        ).normalized;

        rotation = Quaternion.LookRotation(
            forward, up
        );
    }
}

public class TrackSegment : MonoBehaviour
{
    [Header("Connection Points")]
    public Transform entryPoint;
    public Transform exitPoint;

    [Header("Road Waypoints")]
    public Transform[] pathPoints;

    [Header("Track Settings")]
    public float roadWidth = 7.5f;

    private readonly List<Vector3> sampledPoints =
        new List<Vector3>();

    private readonly List<float> cumulativeLengths =
        new List<float>();

    public float Length { get; private set; }

    public float StartDistance { get; set; }

    public float EndDistance => StartDistance + Length;

    public void BuildPath()
    {
        sampledPoints.Clear();
        cumulativeLengths.Clear();
        Length = 0f;

        if (entryPoint == null || exitPoint == null)
        {
            Debug.LogError(
                name + ": Chua gan EntryPoint hoac ExitPoint!"
            );
            return;
        }

        List<Vector3> source = new List<Vector3>();

        source.Add(entryPoint.position);

        if (pathPoints != null)
        {
            foreach (Transform point in pathPoints)
            {
                if (point != null)
                    source.Add(point.position);
            }
        }

        source.Add(exitPoint.position);

        foreach (Vector3 point in source)
        {
            if (sampledPoints.Count == 0 ||
                Vector3.Distance(
                    sampledPoints[sampledPoints.Count - 1],
                    point
                ) > 0.01f)
            {
                sampledPoints.Add(point);
            }
        }

        if (sampledPoints.Count < 2)
            return;

        cumulativeLengths.Add(0f);

        for (int i = 1; i < sampledPoints.Count; i++)
        {
            float distance = Vector3.Distance(
                sampledPoints[i - 1],
                sampledPoints[i]
            );

            Length += distance;
            cumulativeLengths.Add(Length);
        }
    }

    public bool TryGetPose(
        float localDistance,
        out TrackPose pose)
    {
        pose = default;

        if (sampledPoints.Count < 2 || Length <= 0f)
            return false;

        localDistance = Mathf.Clamp(
            localDistance, 0f, Length
        );

        int index = 0;

        for (int i = 0; i < cumulativeLengths.Count - 1; i++)
        {
            if (localDistance <= cumulativeLengths[i + 1])
            {
                index = i;
                break;
            }

            index = i;
        }

        float segmentLength =
            cumulativeLengths[index + 1] -
            cumulativeLengths[index];

        float t = segmentLength > 0.0001f
            ? (localDistance - cumulativeLengths[index]) /
              segmentLength
            : 0f;

        Vector3 position = Vector3.Lerp(
            sampledPoints[index],
            sampledPoints[index + 1],
            t
        );

        Vector3 forward =
            sampledPoints[index + 1] -
            sampledPoints[index];

        if (forward.sqrMagnitude < 0.0001f)
            forward = exitPoint.forward;

        pose = new TrackPose(position, forward);

        return true;
    }

    public void AlignEntryTo(
        Vector3 targetPosition,
        Quaternion targetRotation)
    {
        if (entryPoint == null)
        {
            Debug.LogError(name + ": Thieu EntryPoint!");
            return;
        }

        // Tinh chenh lech goc xoay trong world space.
        Quaternion rotationDelta =
            targetRotation *
            Quaternion.Inverse(entryPoint.rotation);

        // Xoay toan bo Prefab, khong sua Mesh.
        transform.rotation =
            rotationDelta * transform.rotation;

        // Dua EntryPoint den dung vi tri dich.
        Vector3 positionDelta =
            targetPosition - entryPoint.position;

        transform.position += positionDelta;

        BuildPath();
    }

    private void OnValidate()
    {
        if (entryPoint != null && exitPoint != null)
            BuildPath();
    }

    private void OnDrawGizmos()
    {
        if (entryPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(entryPoint.position, 0.2f);
            Gizmos.DrawRay(
                entryPoint.position,
                entryPoint.forward * 1.5f
            );
        }

        if (exitPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(exitPoint.position, 0.2f);
            Gizmos.DrawRay(
                exitPoint.position,
                exitPoint.forward * 1.5f
            );
        }

        if (pathPoints != null)
        {
            Gizmos.color = Color.yellow;

            Vector3 previous = entryPoint != null
                ? entryPoint.position
                : transform.position;

            foreach (Transform point in pathPoints)
            {
                if (point == null)
                    continue;

                Gizmos.DrawLine(previous, point.position);
                Gizmos.DrawSphere(point.position, 0.12f);

                previous = point.position;
            }

            if (exitPoint != null)
                Gizmos.DrawLine(previous, exitPoint.position);
        }
    }
}
