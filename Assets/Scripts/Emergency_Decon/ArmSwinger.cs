using UnityEngine;

public class ArmSwinger : MonoBehaviour
{
    public float speed = 5.0f;     // 흔드는 속도
    public float amplitude = 30.0f; // 흔드는 각도

    private Quaternion initialRotation;

    void Start()
    {
        // 초기 회전값 저장
        initialRotation = transform.localRotation;
    }

    void Update()
    {
        // 사인파를 이용해 좌우로 부드럽게 회전
        float angle = Mathf.Sin(Time.time * speed) * amplitude;
        transform.localRotation = initialRotation * Quaternion.Euler(0, 0, angle);
    }
}