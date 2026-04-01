using UnityEngine;

/// <summary>玩家进入后通关（活着离开墓室）。</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LevelExit : MonoBehaviour
{
    private bool _cleared;

    private void Reset()
    {
        var b = GetComponent<BoxCollider2D>();
        b.isTrigger = true;
    }

    private void Start()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_cleared || !other.CompareTag("Player"))
            return;
        _cleared = true;
        if (UIManager.instance != null)
            UIManager.instance.ShowVictory();
    }
}
