using System.Collections;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    private Transform target;
    [SerializeField] private bool clampEnabled = true;

    [Header("Follow (XY only; Z locked to -10)")]
    [SerializeField, Tooltip("Lower = snappier follow.")]
    private float followSmoothTime = 0.1f;
    [SerializeField, Tooltip("Caps camera speed so it can catch up when the player moves fast.")]
    private float followMaxSpeed = 180f;
    private Vector3 followVelocity;

    [Header("Clamp size (when clamp enabled)")]
    [SerializeField] private float minX, minY, maxX, maxY;

    [Header("Camera Shake")]
    private Vector3 shakeActive;
    private float shakeAmplify;
    [SerializeField, Range(0.1f, 1f)] private float shakeMultiplier = 0.4f;
    [SerializeField, Range(0.02f, 0.3f)] private float maxShakeAmplitude = 0.14f;
    [SerializeField, Range(1f, 6f)] private float shakeDecaySpeed = 2.1f;

    private void Start()
    {
        target = GameObject.FindGameObjectWithTag("Player").GetComponent<Transform>();
    }

    private void Update()
    {
        if (shakeAmplify > 0)
        {
            shakeActive = new Vector3(Random.Range(-shakeAmplify, shakeAmplify), Random.Range(-shakeAmplify, shakeAmplify), 0f);
            shakeAmplify -= Time.deltaTime * shakeDecaySpeed;
        }
        else
        {
            shakeActive = Vector3.zero;
        }

        transform.position += shakeActive;
    }

    private void LateUpdate()
    {
        // Only follow X/Y. Never lerp Z toward the player (player is usually z=0); doing so pulls the
        // orthographic camera onto the sprite plane and the world appears black while UI still renders.
        Vector3 t = target.position;
        Vector3 from = transform.position;
        Vector3 goal = new Vector3(t.x, t.y, from.z);
        transform.position = Vector3.SmoothDamp(from, goal, ref followVelocity, followSmoothTime, followMaxSpeed, Time.deltaTime);
        followVelocity.z = 0f;
        transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
        CameraClamp();
    }

    private void CameraClamp()
    {
        if (!clampEnabled)
            return;
        transform.position = new Vector3(Mathf.Clamp(transform.position.x, minX, maxX),
                                         Mathf.Clamp(transform.position.y, minY, maxY),
                                         -10f);
    }

    public void SetWorldBounds(float mnX, float mxX, float mnY, float mxY)
    {
        minX = mnX;
        maxX = mxX;
        minY = mnY;
        maxY = mxY;
    }

    public void SetClampEnabled(bool enabled)
    {
        clampEnabled = enabled;
    }

    //OPTIONAL_02 Camera Shake
    public void CameraShake(float _amount)
    {
        float scaled = Mathf.Abs(_amount) * shakeMultiplier;
        shakeAmplify = Mathf.Clamp(scaled, 0f, maxShakeAmplitude);
    }

    //OPTIONAL_01 Camera Shake 
    public IEnumerator CameraShakeCo(float _maxTime, float _amount)
    {
        Vector3 originalPos = transform.localPosition;
        float shakeTime = 0.0f;

        while(shakeTime < _maxTime)
        {
            float x = Random.Range(-1f, 1f) * _amount;
            float y = Random.Range(-1f, 1f) * _amount;

            transform.localPosition = new Vector3(x, y, originalPos.z);
            shakeTime += Time.deltaTime;

            yield return new WaitForSeconds(0f);
        }
    }
}
