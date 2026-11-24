
using UnityEngine;

public class TrackManager : MonoBehaviour
{
    public Transform player;
    public Transform[] trackTiles;

    public float tileLength = 40f;

    void Update()
    {
        if (player == null || trackTiles == null ||
            trackTiles.Length == 0)
        {
            return;
        }

        Transform firstTile = trackTiles[0];
        Transform lastTile = trackTiles[0];

        // Tim doan duong gan nhat va xa nhat
        foreach (Transform tile in trackTiles)
        {
            if (tile.position.z < firstTile.position.z)
                firstTile = tile;

            if (tile.position.z > lastTile.position.z)
                lastTile = tile;
        }

        // Neu nguoi choi da di qua doan duong,
        // dua doan do ra phia truoc
        if (player.position.z >
            firstTile.position.z + tileLength)
        {
            firstTile.position = new Vector3(
                firstTile.position.x,
                firstTile.position.y,
                lastTile.position.z + tileLength
            );
        }
    }
}
