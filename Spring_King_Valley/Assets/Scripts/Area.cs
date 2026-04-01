using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Area : MonoBehaviour
{
    public GameObject bulletEffect;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.tag == "Player" && !other.gameObject.GetComponent<ITakenDamage>().isAttack)
        {
            Instantiate(bulletEffect, transform.position, Quaternion.identity);
            FindObjectOfType<CameraController>().CameraShake(0.5f);

            ITakenDamage damageable = other.gameObject.GetComponent<ITakenDamage>();
            damageable.TakenDamage(1);
        }
    }
}
