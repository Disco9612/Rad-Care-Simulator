using UnityEngine;
using Oculus.Interaction;
using System.Collections;
using System;

public class HandJoystickLocomotion : MonoBehaviour
{
    public OVRHand hand;
    public float moveSpeed = 1.0f;

    private Vector3 _neutralPosition;
    private Coroutine _moveCoroutine;

    // 핀치 상태 변화를 감지하기 위한 Action
    private event Action<bool> OnPinchStateChanged;
    [Header("Collision Settings")]
    public LayerMask obstacleLayer; // 충돌을 막을 레이어 지정
    public float collisionRadius = 0.3f; // 플레이어의 충돌 크기 반경
    void Awake()
    {
        // 핀치 상태가 변경될 때 실행될 로직 구독
        OnPinchStateChanged += HandlePinchStateChanged;
    }

    void OnDestroy()
    {
        OnPinchStateChanged -= HandlePinchStateChanged;
    }

    // OVRHand 데이터가 갱신될 때마다 호출되는 메서드
    // (OVRHand는 내부적으로 데이터를 갱신하므로 이를 활용합니다)
    void LateUpdate()
    {
        // Update 대신 LateUpdate를 사용하여 핸드 포즈가 완벽히 계산된 후 체크
        bool isPinching = hand.GetFingerIsPinching(OVRHand.HandFinger.Index);

        // 상태가 변경되었을 때만 Action 발동
        if (isPinching != (_moveCoroutine != null))
        {
            OnPinchStateChanged?.Invoke(isPinching);
        }
    }

    private void HandlePinchStateChanged(bool isPinching)
    {
        if (isPinching)
        {
            _neutralPosition = hand.PointerPose.position;
            _moveCoroutine = StartCoroutine(MoveRoutine());
        }
        else
        {
            StopCoroutine(_moveCoroutine);
            _moveCoroutine = null;
        }
    }

    private IEnumerator MoveRoutine()
    {
        while (true)
        {
            Vector3 offset = hand.PointerPose.position - _neutralPosition;
            Vector3 moveDirection = new Vector3(offset.x, 0, offset.z);

            Vector3 nextMotion = moveDirection * moveSpeed * Time.deltaTime;

            // 이동하려는 방향에 장애물이 있는지 구형 레이캐스트(SphereCast)로 검사
            if (nextMotion.sqrMagnitude > 0.0001f)
            {
                Vector3 moveDirNormalized = nextMotion.normalized;
                float moveDistance = nextMotion.magnitude;

                // 이동 경로상에 obstacleLayer에 속한 오브젝트가 있다면 이동 차단
                if (!Physics.SphereCast(transform.position, collisionRadius, moveDirNormalized, out RaycastHit hit, moveDistance, obstacleLayer))
                {
                    transform.position += nextMotion;
                }
            }

            yield return null;
        }
    }
}