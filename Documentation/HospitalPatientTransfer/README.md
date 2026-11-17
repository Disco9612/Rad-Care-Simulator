# Hospital 환자 이식 — Emergency_Decon 씬 3개

Hospital_Evaluation의 `Patient_Supine`에 사용된 환자와 `Simulated_Wound_Target/Studio_Model`의 상처 외형을 복제하여 `Assets/EmergencyDeconHospitalPatient/Hospital_Patient_Visual.prefab`으로 저장했다. Hospital 원본 씬은 수정하지 않았다.

`Emergency_Decon_Evaluation`, `Emergency_Decon_Tutorial`, `Emergency_Decon_Tutorial_Hand`의 기존 `Patient` 아래에 새 외형을 배치했다. 기존 환자 외형과 이전에 만든 환자복은 비활성화했다. 환자의 몸통과 옷, 상처 외형은 Hospital 씬의 에셋을 그대로 사용한다.

기존 `Blood` 오브젝트의 게임 오브젝트와 Forceps의 `targetObject` 참조는 유지했다. 기존 Blood 캡슐 렌더만 끄고, 트리거를 Hospital 상처 외형에 맞춰 이동했다. 측정기는 기존처럼 `Blood` 이름으로 오염을 판단하고, 집게 처치 완료 시 Blood 트리거가 비활성화된다. 별도 `Patient_WoundScanCollider`가 같은 부위의 재측정을 맡으므로 처치 후에도 상처 외형과 재측정 영역이 남는다. 새 환자 몸에는 `Patient_ScanCollider_*` 물리 표면을 생성했다.

`verified.txt`: 세 씬을 다시 열고 외형 활성 상태, Blood/집게 참조, 상처·트리거 정렬, 실제 Physics 겹침, 처치 완료를 모사한 Blood 비활성화 후 재측정 콜라이더, 재질을 검사했다. `preservation-check.txt`: 기존 스크립트 설정과 원본 오브젝트가 보존됐음을 확인했다. `Patient-after.png`: 실제 Evaluation 씬에서 새 환자와 상처만 분리해 렌더한 검토 이미지다. XR 기기 입력을 통한 전체 처치 흐름은 별도로 시험하지 않았다.

`Before`에는 작업 전 세 씬의 백업을 보관했다. 에디터 자동화 코드는 `Assets/Editor/HospitalPatientTransfer*.cs`에 있다.

## 오른팔 상처와 침대 자세 (2026-11-17)

세 Emergency_Decon 씬 모두 Hospital 환자의 팔 메시를 침대 매트리스 방향으로 변형하고, 상처 외형과 기존 `Blood` 트리거를 환자 오른팔 피부 표면으로 옮겼다. 처치 후 재측정 콜라이더도 함께 옮겼다. 원본 Hospital FBX의 읽기 설정은 작업 후 복원했다. 변경 전 씬과 환자 외형 프리팹은 `BeforeArmPose`에 보관했다.

`Patient-on-bed-top.png`와 `Patient-on-bed-side.png`는 Evaluation 씬의 환자와 실제 침대를 함께 렌더한 이미지다. 오른팔 피부 메시 최저점은 매트리스 윗면보다 약 1.5cm 높다(`arm-contact.txt`). `arm-pose.txt`와 갱신된 `verified.txt`에서 세 씬의 오른팔 상처 위치 및 상호작용 참조 검증을 확인할 수 있다. XR 기기에서의 손 자세와 도구 조작은 아직 별도로 확인하지 않았다.

## 팔을 편 자세 (2026-11-17 후속)

에셋 스튜디오의 `radcare_patient` 원본 작업(`D:/AssetAgentData/jobs/1dabbeb6-be99-4488-91a4-54991ee48444`)을 확인했다. 원본 FBX에서 몸과 옷 메시를 다시 복제하고 팔꿈치부터 손끝까지의 높이를 보정했다. 손끝이 위로 꺾이는 부분도 낮췄다. 환자 외형 프리팹과 세 Emergency_Decon 씬에 적용했고, 상처 외형·`Blood`·재측정 영역의 Transform은 이번 작업에서 변경하지 않았다. 수정 전 씬과 메시 에셋은 `BeforeArmPose`에 추가 보관했다.

갱신된 `Patient-on-bed-side.png`와 `Patient-on-bed-top.png`로 침대 위 자세를 확인했다. 오른팔 피부 메시 최저점은 매트리스 윗면보다 약 1.2cm 높다. `arm-pose.txt`는 세 씬 적용 기록이다. 손가락의 원본 형태와 소매 실루엣은 그대로 남아 있어, 완전히 새로운 리깅 모델 수준의 자세 표현은 아니다.

## 2026-11-17 뼈대 기반 재생성 완료

Asset Studio radcare_patient 원본 생성 레시피와 MakeHuman 뼈대로 다시 생성했다. 양측 upperarm/lowerarm/wrist의 방향을 맞춘 뒤 정적 메시로 내보냈다. 오른팔 소매는 불규칙 단면으로 제거했고 해당 팔의 숨겨졌던 피부를 복원했다. 적용 에셋은 Assets/EmergencyDeconHospitalPatient/Regenerated/v3_*.asset이다. 이전 단순 정점 이동 메시를 대체했다.

세 Emergency_Decon 씬 모두 Blood, Hospital_Wound_Visual, Patient_WoundScanCollider를 비활성화했다. 복구용 오브젝트와 참조는 보존되어 있다. 상처 처치 기능은 의도적으로 중단된 상태다. 위/옆 실제 씬 렌더를 확인했으며 오른팔 메시 최저점은 매트리스보다 약 2.2cm 위다. XR 기기 플레이 테스트는 하지 않았다. Regenerated 폴더에 Blender 원본과 생성 스크립트를 보관했다.

## 소매 절단 범위 수정
몸통까지 잘리던 좌표 기반 삭제를 제거했다. 오른팔 뼈대 가중치로 소매를 구분한 뒤 실제 면을 절단하고 1~3mm 크기의 불규칙 가장자리와 1.2mm 천 두께를 적용했다. 몸통 옆면을 복원했다. v4 메시를 세 씬에 적용하고 난간 없는 환자 렌더와 침대 측면 렌더에서 몸통 구멍이 사라진 것을 확인했다.

## 팔 표면 상처와 기존 처치 연결

기존 Blood_01.png를 팔 곡면에 투영한 메시로 표시한다. 셰이더에서 분홍색 배경을 투명하게 처리하고 가장자리를 부드럽게 제거했다. 표면에서 1.2mm 띄워 겹침 깜빡임을 방지한다. 기존 Blood 오브젝트 ID/이름, 집게 targetObject와 처치 시간·단계·평가 코드는 유지했다. 식염수 파티클 Trigger는 새 위치의 기존 Blood BoxCollider로 연결했다.

DeconWoundAppearance는 기존 GameManager/EvaluationGameManager의 IsWashingComplete를 읽어 혈흔을 옅게 만든다. 집게의 솜 처치 완료 시 기존 targetObject.SetActive(false)에 따라 혈흔과 오염 감지 트리거가 함께 사라진다. 별도의 Patient_WoundScanCollider가 처치 후 재측정 영역을 유지한다. 이전 Hospital 상처 외형은 꺼져 있다.

세 씬의 적용 및 물리 충돌·참조·처치 완료 상태 모사 검사는 wound-validation.txt에 기록했다. Wound-closeup.png는 실제 Evaluation 씬 확대 렌더다. XR 기기에서 식염수 붓기부터 솜 접촉까지의 전체 플레이는 별도 검증하지 않았다. BeforeWound는 적용 전 씬 백업이다. 과거의 validate/straight/regenerated 편집기 명령은 이전 상태용이므로 현재 상처 검증에는 사용하지 않는다.

## 세척 후 혈흔 가시성 조정
사용자 VR 플레이 피드백에 따라 세척 완료 시 _Opacity를 0.55에서 0.85로 변경했다. 세척 전은 1.0이며, 솜 처치 완료 시 Blood 비활성화 동작은 유지한다. 사용자가 기존 전체 인터랙션을 직접 플레이해 확인했고, 이번 수정은 시각적 옅어짐 정도만 변경한다.
