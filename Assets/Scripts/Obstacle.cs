
using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public float destroyDistance = 15f;

    private Transform player;

    void Start()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    void Update()
    {
        if (player == null)
            return;

        // Huy vat can da nam sau nguoi choi
        if (transform.position.z <
            player.position.z - destroyDistance)
        {
            Destroy(gameObject);
        }
    }
}
