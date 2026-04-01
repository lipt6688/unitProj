using UnityEngine;

public class Slash : MonoBehaviour
{
    [SerializeField] private int minAttack, maxAttack;
    private int attackDamage;

    public GameObject damageCanvas;

    public GameObject hitEffect;
    public Transform hitTrans;

    public void EndAttack()//MARKER This Function is Called inside Animation End Frame
    {
        gameObject.SetActive(false);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if(other.gameObject.tag == "Enemy")
        {
            int bMin = 0, bMax = 0;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                PlayerRuntimeStats st = player.GetComponent<PlayerRuntimeStats>();
                if (st != null)
                {
                    bMin = st.damageBonusMin;
                    bMax = st.damageBonusMax;
                }
            }
            int lo = minAttack + bMin;
            int hi = maxAttack + bMax;
            if (hi <= lo)
                hi = lo + 1;
            attackDamage = Random.Range(lo, hi);

            Debug.Log("We have Hitted the Enemy!");

            //Enemy enemy = other.gameObject.GetComponent<Enemy>();
            ITakenDamage enemy = other.gameObject.GetComponent<ITakenDamage>();

            if(!enemy.isAttack)
            {
                enemy.TakenDamage(attackDamage);
                FindObjectOfType<CameraController>().CameraShake(0.4f);
                Instantiate(hitEffect, hitTrans.position, Quaternion.identity);

                //MARKER SHOW damage Number 显示伤害数字
                DamageNum damagable = Instantiate(damageCanvas, other.transform.position, Quaternion.identity).GetComponent<DamageNum>();
                damagable.ShowDamage(attackDamage);

                //MARKER KnockBack Effect 击退效果 反方向移动，从角色中心点「当前位置」向敌人位置方向「目标点」移动
                Vector2 difference = other.transform.position - transform.position;
                difference.Normalize();
                other.transform.position = new Vector2(other.transform.position.x + difference.x / 2,
                                                       other.transform.position.y + difference.y / 2);
            }
        }

        //If Our Sword cut hit wall「如果我们的刀砍到了墙壁，则产生一个特效并且震动」
        if(other.gameObject.tag == "Wall")
        {
            FindObjectOfType<CameraController>().CameraShake(0.5f);
            Instantiate(hitEffect, hitTrans.position, Quaternion.identity);
        }
    }
}
