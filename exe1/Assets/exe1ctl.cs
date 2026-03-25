using UnityEngine;

public class exe1ctl : MonoBehaviour
{
    void Update()
    {
        // 1. 获取输入 (Up/Down 箭头)
        float vInput = Input.GetAxis("Vertical");

        // 2. Q 键退出 (Quits the application)
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.Quit();
        }

        if (vInput != 0)
        {
            // --- (30%) Position: 45-degree trajectory ---
            Vector3 pos = transform.position;

            // 根据当前位置的符号决定移动方向 (away from or towards origin)
            // Mathf.Sign(pos.x) 在第一象限是 1，在第二象限是 -1
            float dx = Mathf.Sign(pos.x) * vInput;
            float dy = Mathf.Sign(pos.y) * vInput;

            Vector3 newPos = new Vector3(pos.x + dx, pos.y + dy, 0);

            // (10%) Object’s positions between +-1 and +-50
            if (pos.x > 0) newPos.x = Mathf.Clamp(newPos.x, 1f, 50f);
            else newPos.x = Mathf.Clamp(newPos.x, -50f, -1f);

            if (pos.y > 0) newPos.y = Mathf.Clamp(newPos.y, 1f, 50f);
            else newPos.y = Mathf.Clamp(newPos.y, -50f, -1f);

            transform.position = newPos;

            // --- (30%) Scale: Size between 1 and 6 ---
            // (10%) 使用老师给的公式：size += Input.GetAxis("Vertical") * 0.1f
            float s = vInput * 0.1f;
            Vector3 currentScale = transform.localScale;
            Vector3 newScale = currentScale + new Vector3(s, s, s);

            // (10%) 限制 Scale 在 1 到 6 之间 (对应 Size 1-6)
            float clampedS = Mathf.Clamp(newScale.x, 1f, 6f);
            transform.localScale = new Vector3(clampedS, clampedS, clampedS);
        }
    }
}