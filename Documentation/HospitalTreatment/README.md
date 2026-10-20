# 병원 처치실 에셋 및 시나리오 씬 정리

Unity 6000.3.0f1 / URP 17.3.0에서 제작·검증한 병원 처치실용 정적 에셋입니다.

## 씬

- `Assets/Scenes/MainLobby.unity`: 기존 튜토리얼/평가 선택 로비.
- `Assets/Scenes/Emergency_Decon_Tutorial.unity`: 기존 `MainScean_Controller`의 새 이름.
- `Assets/Scenes/Emergency_Decon_Evaluation.unity`: 기존 `MainScean_Evaluation`의 새 이름.
- `Assets/Scenes/Emergency_Decon_Tutorial_Hand.unity`: 기존 `MainScean`의 새 이름. 빌드에서는 비활성 상태를 유지합니다.
- `Assets/Scenes/Hospital_Tutorial.unity`: 작업 중인 병원 교육 씬 스냅샷. 로비 카탈로그와 빌드 목록에는 아직 등록하지 않았습니다.
- `Assets/RadCareTreatmentRoom_Demo.unity`: 환자·간호사·침상·카트·처치등·받이를 배치한 정적 예시.
- `Assets/RadCareHospitalPreview.unity`: 기존 소품 7종의 배치 미리보기.

기존 제염 스크립트는 `Assets/Scripts/Emergency_Decon`으로 이동했습니다. 씬과 스크립트의 `.meta` GUID를 유지하고 카탈로그의 씬 경로도 새 이름으로 변경했습니다.

## 에셋

`Assets/RadCareHospital`에는 처치 카트, 처치등, 모니터, 차트 태블릿, 드레싱 키트, 세척 주사기, 세척액 받이 7종이 있습니다. 카트와 처치등에는 빈 림, 타이어 홈, 분리된 축과 얇은 포크를 적용한 바퀴 수정본이 들어 있습니다.

`Assets/RadCareExpansion`에는 다음 5종과 누운 환자 프리팹이 있습니다.

| 모델 | 구성 |
| --- | --- |
| 처치실 | 약 4.8 × 4.8 × 2.8 m. 벽 두 면과 천장이 열린 구조, 수납장·커튼·벽면 패널·소독기·휴지통 |
| 침상 | 약 1.04 × 2.185 × 1.14 m. 캐스터·브레이크·열린 난간·매트리스·베개 |
| 환자 | 밝은 임상용 상하의, 서 있는 자세와 누운 배치 |
| 보조 간호사 | 청록색 상하의와 밝은 신발 |
| 콩팥형 받이 | 약 35 × 24 × 7 cm. 실제 빈 내부, 바닥과 연결된 둥근 가장자리 |

인물 복장은 MakeHuman의 맞춤 셔츠·바지 형상에 단색 임상용 재질을 적용한 단순화 모델입니다. 인물 출처와 CC0 문서는 이 폴더의 `human-source-provenance.json`, `LICENSE.MakeHuman-CC0.md`에 보관했습니다.

총 23개 프리팹(기존 14개 + 확장 9개)이며, 소품에는 일반 프리팹과 3단계 LOD 프리팹을 제공합니다. 인물 프리팹은 정적 스냅샷이며 인물 LOD는 없습니다.

## 검증과 제한

- 별도의 Unity 검증 프로젝트에서 원본 프리팹의 메시·URP/Lit·텍스처 연결을 검사했습니다.
- 소품 LOD0/1/2의 크기, 재질 연결, 삼각형 수 감소를 확인했습니다.
- 실제 Unity 렌더로 바퀴, 받이 내부, 인물 재질과 예시 배치를 검토했습니다.
- 생성본을 가져온 뒤 프로젝트에서 수정한 두 인물의 `MAT_eyes.mat` 투명 설정을 유지했습니다. 이 두 재질은 기존 생성본 검증 시점과 다르며, 이번 PR에서는 설정 차이와 참조 연결을 정적으로 확인했습니다.
- `hospital-props-verification.json`과 `expansion-verification.json`은 해당 에셋 검증 결과입니다. 전체 XR 프로젝트의 빌드 성공을 뜻하지 않습니다.
- 충돌용 FBX는 소품에 제공하지만 Collider는 프리팹에 부착하지 않았습니다. 받이 내부와 난간 틈을 고려하여 별도 구성해야 합니다.
- 병원 처치 판정, XR 상호작용, 캐스터·난간·조명 관절 동작, 인물 애니메이션, 물 표현은 이 에셋에 구현되어 있지 않습니다.
- Quest 성능, LOD 거리 전환, 인물 접촉 자세 및 임상 정확성은 별도 검수 대상입니다.

Git에는 Unity에서 사용하는 FBX·재질·텍스처·프리팹·씬과 메타데이터를 포함합니다. 생성 작업의 중복 `.unitypackage`, Blender 백업과 임시 검증 프로젝트는 포함하지 않습니다.
