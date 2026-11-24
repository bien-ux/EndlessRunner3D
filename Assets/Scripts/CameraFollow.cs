
using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public PlayerRunner player;

    [Header("Camera Position")]
    public Vector3 offset =
        new Vector3(0f, 4.5f, -7f);

    [Header("Follow")]
    public float positionSmooth = 6f;
    public float rotationSmooth = 5f;

    [Header("Look Ahead")]
    public float lookAheadDistance = 9f;
    public float lookHeight = 1f;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 forward = target.forward;

        if (player != null &&
            TrackManagerV2.Instance != null)
        {
            TrackPose pose;

            if (TrackManagerV2.Instance.TryGetPose(
                player.DistanceTravelled +
                lookAheadDistance, out pose))
            {
                forward = pose.forward;
            }
            else
            {
                forward = player.CurrentForward;
            }
        }

        if (forward.sqrMagnitude < 0.001f)
            forward = target.forward;

        Quaternion roadRotation =
            Quaternion.LookRotation(
                forward,
                Vector3.up
            );

        Vector3 desiredPosition =
            target.position +
            roadRotation * offset;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmooth * Time.deltaTime
        );

        Vector3 lookTarget =
            target.position +
            forward * lookAheadDistance +
            Vector3.up * lookHeight;

        Quaternion desiredRotation =
            Quaternion.LookRotation(
                lookTarget - transform.position,
                Vector3.up
            );

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            rotationSmooth * Time.deltaTime
        );
    }
}
