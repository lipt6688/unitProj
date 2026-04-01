using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    private Rigidbody2D rb;
    private PlayerRuntimeStats stats;
    [HideInInspector] public float moveH, moveV;
    [SerializeField] private float moveSpeed;
    [Min(1f)][SerializeField] private float sprintMultiplier = 2f;
    private float externalSpeedMultiplier = 1f;
    private bool movementLocked;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<PlayerRuntimeStats>();
    }

    private void Update()
    {
        if (Time.timeScale <= 0f || GamePause.IsShopOpen || movementLocked)
        {
            moveH = moveV = 0f;
            return;
        }

        bool isSprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        float mult = stats != null ? stats.moveSpeedMultiplier : 1f;
        float sprintMult = isSprinting ? sprintMultiplier : 1f;
        float totalMultiplier = Mathf.Max(0f, mult) * Mathf.Clamp01(externalSpeedMultiplier);
        moveH = Input.GetAxis("Horizontal") * moveSpeed * totalMultiplier * sprintMult;
        moveV = Input.GetAxis("Vertical") * moveSpeed * totalMultiplier * sprintMult;
        Flip();
    }

    private void FixedUpdate()
    {
        if (Time.timeScale <= 0f || GamePause.IsShopOpen || movementLocked)
        {
            rb.velocity = Vector2.zero;
            return;
        }
        rb.velocity = new Vector2(moveH, moveV);
    }

    private void Flip()
    {
        const float deadZone = 0.01f;
        if (moveH > deadZone)
            transform.eulerAngles = new Vector3(0, 0, 0);
        else if (moveH < -deadZone)
            transform.eulerAngles = new Vector3(0, 180, 0);
    }

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        externalSpeedMultiplier = Mathf.Clamp01(multiplier);
    }

    public void SetMovementLocked(bool locked)
    {
        movementLocked = locked;
        if (locked && rb != null)
        {
            moveH = 0f;
            moveV = 0f;
            rb.velocity = Vector2.zero;
        }
    }
}
