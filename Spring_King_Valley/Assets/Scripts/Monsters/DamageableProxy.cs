using UnityEngine;

namespace Vampire
{
    public class DamageableProxy : MonoBehaviour, ITakenDamage
    {
        public ITakenDamage target;

        public bool isAttack 
        { 
            get => target != null && target.isAttack; 
            set { if (target != null) target.isAttack = value; }
        }

        public void TakenDamage(int _amount)
        {
            if (target != null)
                target.TakenDamage(_amount);
        }
    }
}
