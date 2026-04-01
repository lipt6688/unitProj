using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//TODO Object Pooling System
public class Echo : MonoBehaviour
{
    private PlayerMovement playerMovement;
    public GameObject echo;
    public Transform footTrans_0;
    public Transform footTrans_1;

    private float timeBtSpawn;
    [SerializeField] private float spawnRate;

    private void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
    }

    //TODO Use Game Pooling REPLACE LATER
    private void Update()
    {
        if (playerMovement.GetComponent<Rigidbody2D>().velocity.x != 0 ||
            playerMovement.GetComponent<Rigidbody2D>().velocity.y != 0)
        {
            if (timeBtSpawn >= spawnRate)
            {
                GameObject instance_0 = Instantiate(echo, footTrans_0.position, Quaternion.identity);
                GameObject instance_1 = Instantiate(echo, footTrans_1.position, Quaternion.identity);

                Destroy(instance_0, 0.3f);
                Destroy(instance_1, 0.3f);
                timeBtSpawn = 0;
            }
            else
            {
                timeBtSpawn += Time.deltaTime;
            }
        }
    }
}
