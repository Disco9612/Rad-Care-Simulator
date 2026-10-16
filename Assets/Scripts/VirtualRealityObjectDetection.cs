using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Meta.XR.BuildingBlocks.AIBlocks
{
    public class VirtualRealityObjectDetection : MonoBehaviour
    {
        [Header("AI Provider & Settings")]
        [Tooltip("Provider asset that implements IObjectDetectionTask (예: UnityInferenceEngineProvider).")]
        [SerializeField] internal AIProviderBase providerAsset;

        [Range(0, 1)] public float minConfidence = 0.5f;

        [Tooltip("VR 환경에서 AI 추론을 실행할 프레임 주파수 (예: 30프레임마다 실행)")]
        [SerializeField, Range(1, 300)] private int detectEveryNFrames = 30;

        [Header("Visualization")]
        [Tooltip("VR 공간에 띄울 바운딩 박스 프리팹 (내부에 UI Text 포함)")]
        [SerializeField] private GameObject boundingBoxPrefab;
        [SerializeField] private bool showBoundingBoxes = true;
        [Range(0f, 1f)][SerializeField] private float labelScale = 0.5f;

        [Header("VR Raycast Settings")]
        [Tooltip("가상 공간 탐지에 사용할 레이캐스트 레이어 마스크")]
        [SerializeField] private LayerMask detectionLayerMask = -1;
        [Tooltip("레이캐스트 최대 거리")]
        [SerializeField] private float maxRayDistance = 10f;

        [Header("Stabilization & Filtering")]
        [SerializeField] private int requiredConsecutiveDetections = 2;
        [SerializeField] private float retentionTimeMs = 300f;
        [SerializeField] private float trackingDistanceThreshold = 0.5f;

        private Camera _vrCamera;
        private IObjectDetectionTask _detector;
        private UnityInferenceEngineProvider _unityProvider;
        private bool _busy;

        // 내부 트래킹 및 풀링 구조
        private class TrackedVRBox
        {
            public string Label;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public int ConsecutiveCount;
            public float TimeSinceLastSeen;
            public GameObject BoxGameObject;
        }

        private readonly List<TrackedVRBox> _trackedBoxes = new List<TrackedVRBox>();
        private readonly Queue<GameObject> _pool = new Queue<GameObject>();
        private readonly List<GameObject> _liveObjects = new List<GameObject>();

        private async void Awake()
        {
            // VR 메인 카메라 자동 탐색
            _vrCamera = Camera.main;
            if (_vrCamera == null)
            {
                _vrCamera = FindAnyObjectByType<Camera>();
            }

            _detector = providerAsset as IObjectDetectionTask;
            _unityProvider = providerAsset as UnityInferenceEngineProvider;

            if (_detector == null && _unityProvider == null)
            {
                Debug.LogError("[VirtualRealityObjectDetection] AI Provider가 설정되지 않았습니다.");
            }

            if (_unityProvider != null)
            {
#if UNITY_INFERENCE_INSTALLED
                await _unityProvider.WarmUp();
#endif
            }
        }

        private void Update()
        {
            // 1. 유지 시간(retentionTimeMs) 경과된 박스 제거 타이머 처리
            float dt = Time.deltaTime;
            for (int i = _trackedBoxes.Count - 1; i >= 0; i--)
            {
                var tracked = _trackedBoxes[i];
                tracked.TimeSinceLastSeen += dt;

                if (tracked.TimeSinceLastSeen * 1000f > retentionTimeMs)
                {
                    if (tracked.BoxGameObject)
                    {
                        tracked.BoxGameObject.SetActive(false);
                        _pool.Enqueue(tracked.BoxGameObject);
                    }
                    _trackedBoxes.RemoveAt(i);
                }
            }

            // 2. 주기적인 AI 추론 실행
            if (_busy || _vrCamera == null) return;

            if (Time.frameCount % detectEveryNFrames == 0)
            {
                _ = RunVRObjectDetection();
            }
        }

        private async Task RunVRObjectDetection()
        {
            _busy = true;
            try
            {
                // [1단계: VR 가상 카메라 화면 캡처]
                // 렌더 텍스처를 생성해 VR 카메라 뷰를 캡처
                int width = 640;
                int height = 640;
                RenderTexture rt = RenderTexture.GetTemporary(width, height, 24);
                RenderTexture previousRT = _vrCamera.targetTexture;

                _vrCamera.targetTexture = rt;
                _vrCamera.Render();

                Texture2D virtualScreenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                RenderTexture.active = rt;
                virtualScreenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                virtualScreenTex.Apply();

                _vrCamera.targetTexture = previousRT;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);

                // [2단계: AI 모델 추론]
                AIProviderBase.ObjectDetectionPrediction[] predictions = null;
                if (_unityProvider != null)
                {
#if UNITY_INFERENCE_INSTALLED
                    predictions = await _unityProvider.DetectAsync(virtualScreenTex);
#endif
                }
                else if (_detector != null)
                {
                    byte[] bytes = virtualScreenTex.EncodeToJPG();
                    string json = await _detector.DetectAsync(bytes);
                    if (!string.IsNullOrEmpty(json))
                    {
                        try
                        {
                            json = json.Trim();
                            if (json.StartsWith("["))
                            {
                                var arr = JsonUtility.FromJson<DetectionArray>("{\"items\":" + json + "}");
                                predictions = arr?.items;
                            }
                            else
                            {
                                var wrapper = JsonUtility.FromJson<DetectionWrapper>(json);
                                predictions = wrapper?.predictions;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"JSON 파싱 실패: {ex.Message}");
                        }
                    }
                }

                Destroy(virtualScreenTex);

                if (predictions == null || predictions.Length == 0) return;

                var currentFrameDetections = new List<TrackedVRBox>();

                foreach (var p in predictions)
                {
                    if (p.score < minConfidence || p.box == null || p.box.Length < 4) continue;

                    float xmin = p.box[0];
                    float ymin = p.box[1];
                    float xmax = p.box[0] + p.box[2];
                    float ymax = p.box[1] + p.box[3];

                    float centerX = (xmin + xmax) * 0.5f;
                    float centerY = (ymin + ymax) * 0.5f;

                    Vector3 screenPoint = new Vector3(centerX, height - centerY, 0);
                    Ray ray = _vrCamera.ScreenPointToRay(screenPoint);


                    if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, detectionLayerMask, QueryTriggerInteraction.Collide))
                    {
                        Vector3 worldPos = hit.point;
                        Quaternion worldRot = Quaternion.LookRotation(_vrCamera.transform.forward);
                        Vector3 worldScale = new Vector3(0.3f, 0.3f, 0.3f); // 기본 크기

                        currentFrameDetections.Add(new TrackedVRBox
                        {
                            Label = p.label,
                            Position = worldPos,
                            Rotation = worldRot,
                            Scale = worldScale,
                            ConsecutiveCount = 1,
                            TimeSinceLastSeen = 0f
                        });
                    }
                }

                // [4단계: 안정화 및 시각화 반영]
                UpdateVdTracking(currentFrameDetections);
            }
            finally
            {
                _busy = false;
            }
        }

        private void UpdateVdTracking(List<TrackedVRBox> detections)
        {
            foreach (var detection in detections)
            {
                TrackedVRBox bestMatch = null;
                float minDistance = float.MaxValue;

                foreach (var tracked in _trackedBoxes)
                {
                    if (tracked.Label == detection.Label)
                    {
                        float dist = Vector3.Distance(tracked.Position, detection.Position);
                        if (dist < trackingDistanceThreshold && dist < minDistance)
                        {
                            minDistance = dist;
                            bestMatch = tracked;
                        }
                    }
                }

                if (bestMatch != null)
                {
                    bestMatch.Position = Vector3.Lerp(bestMatch.Position, detection.Position, 0.5f);
                    bestMatch.Rotation = detection.Rotation;
                    bestMatch.Scale = Vector3.Lerp(bestMatch.Scale, detection.Scale, 0.5f);
                    bestMatch.TimeSinceLastSeen = 0f;
                    bestMatch.ConsecutiveCount++;

                    if (bestMatch.ConsecutiveCount >= requiredConsecutiveDetections)
                    {
                        RenderBox(bestMatch);
                    }
                }
                else
                {
                    _trackedBoxes.Add(detection);
                }
            }
        }

        private void RenderBox(TrackedVRBox tracked)
        {
            if (!showBoundingBoxes || boundingBoxPrefab == null) return;

            if (tracked.BoxGameObject == null)
            {
                tracked.BoxGameObject = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(boundingBoxPrefab);
                tracked.BoxGameObject.SetActive(true);
            }

            tracked.BoxGameObject.transform.SetPositionAndRotation(tracked.Position, tracked.Rotation);
            tracked.BoxGameObject.transform.localScale = tracked.Scale;

            
            var labelText = tracked.BoxGameObject.GetComponentInChildren<Text>();
            if (labelText != null)
            {
                labelText.text = tracked.Label;
            }
        }

    }
    [Serializable]
    public class DetectionWrapper { public AIProviderBase.ObjectDetectionPrediction[] predictions; }

    [Serializable]
    public class DetectionArray { public AIProviderBase.ObjectDetectionPrediction[] items; }
}