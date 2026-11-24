
using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("COIN SETTINGS")]
    public int value = 10;
    public float rotateSpeed = 120f;
    public float bobSpeed = 2f;
    public float bobHeight = 0.15f;

    [Header("SOUND")]
    public AudioClip pickupSound;

    [Range(0f, 1f)]
    public float soundVolume = 0.8f;

    private Vector3 startPosition;
    private bool collected;

    private void Start()
    {
        startPosition = transform.position;

        // Dam bao cac Renderer cua coin dang bat.
        Renderer[] renderers =
            GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
            r.enabled = true;
    }

    private void Update()
    {
        if (collected)
            return;

        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
            return;

        // Xoay dong xu.
        transform.Rotate(
            Vector3.up,
            rotateSpeed * Time.deltaTime,
            Space.World
        );

        // Nhay len xuong nhe.
        transform.position = startPosition +
            Vector3.up *
            (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected)
            return;

        // Ho tro ca collider nam tren object con cua Player.
        PlayerRunner player =
            other.GetComponentInParent<PlayerRunner>();

        if (player == null &&
            !other.CompareTag("Player") &&
            !other.transform.root.CompareTag("Player"))
            return;

        if (GameManager.Instance == null ||
            !GameManager.Instance.IsPlaying)
            return;

        collected = true;

        // Cong tien va diem.
        GameManager.Instance.AddCoin(1);
        GameManager.Instance.AddScore(value);

        // Phat am thanh.
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(
                pickupSound,
                transform.position,
                soundVolume
            );
        }

        Destroy(gameObject);
    }
}
