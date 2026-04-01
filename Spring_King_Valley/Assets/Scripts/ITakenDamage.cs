using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ITakenDamage 
{
    bool isAttack { get; set; }
    void TakenDamage(int _amount);
}
