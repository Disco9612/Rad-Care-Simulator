# Emergency Decon lighting revision

2026-11-16. Evaluation, Tutorial, Tutorial_Hand 세 씬에 적용 및 실제 Unity Progressive GPU 베이크 완료.

## 오류 대응
- 베이크 입력 중 0 스케일을 가진 XR Ring 시각 오브젝트를 확인하여 Contribute GI에서 제외했다. 해당 입력을 제외한 진단 베이크가 성공했다.
- 이전 로그에서 Patient_Body와 Gloves의 invalid UV 경고도 확인했다. 환자, XR 시각 요소, 이동 도구, 투명 오브젝트는 정적 라이트맵 대신 Light Probe를 사용하도록 정리했다.
- Blood의 Capsule 시각 오브젝트는 0인 두께를 0.0001로 보정했다. 기존 콜라이더와 처치 스크립트는 유지했다.
- 이전 베이크 데이터를 지우고 세 씬을 새로 베이크했다. 특정 한 입력만이 오류의 유일한 원인이었다고 단정하지는 않는다.

## 조명 구성
각 씬의 EmergencyDecon_ClinicalLighting 아래에 공통 구성을 배치했다.
- 천장 Rectangle Baked Light 6개: 5500K, intensity 7.5, 면적 1.6 x 0.4m. LED 하우징과 발광면 포함.
- Mixed Point 보조등 2개: 5500K, intensity 1, range 5.5m, 실시간 그림자 없음.
- 실내 Light Probe 75개. Evaluation은 기존 8개를 보존하여 총 83개.
- 128 해상도 HDR Reflection Probe, box projection, 각 씬별 저장된 cubemap.
- 기존 조명은 삭제하지 않고 비활성화했다.

공유 설정: Assets/EmergencyDeconLighting/EmergencyDecon_Clinical.lighting
Progressive GPU, Directional, Baked Indirect, 24 texels/m, atlas 1024, padding 4, samples 64/256/128, bounces 3, AO off.

## 검증
세 씬 각각 lightmap 2개. 최종 color/directionality 텍스처 655360 픽셀과 모든 SH 계수의 유한성을 검사하여 통과했다. 이번 각 베이크에서 Non finite / invalid UV 메시지 0건.
원본 오브젝트 삭제 없음. 기존 스크립트/물리 컴포넌트 변경 없음. PC/Mobile 렌더 파이프라인 에셋 변경 없음. 상세 결과는 이 폴더의 verified, numeric, preservation-check 파일 참조.
에디터 렌더로 외관 확인. Android 빌드와 실제 HMD 성능/색상은 검증하지 않았다. 어두운 재질 자체의 색과 반사 특성은 유지했다.

## 이후 수정
- 개별 광량/위치는 Hierarchy의 EmergencyDecon_ClinicalLighting에서 조정한다. Baked 조명 수정 후 Lighting 창에서 Generate Lighting을 실행한다.
- Tools > RadCare > Emergency Lighting > Rebuild and Bake All Three Scenes는 세 씬에 현재 코드의 기본 구성을 다시 만들고 베이크한다. 수동으로 변경한 조명 리그 값은 이 작업에서 초기화된다.
- Validate Current Baked Scene 메뉴로 현재 씬의 라이트맵/프로브 유한성을 검사할 수 있다.
- 코드: Assets/Editor/EmergencyLighting. 생성 에셋: Assets/EmergencyDeconLighting. 베이크 결과: Assets/Scenes/<씬 이름>.
- Before 폴더에는 이번 재개 작업 직전의 씬 사본이 있다. 씬 파일만 되돌리면 새 베이크 에셋까지 함께 복원되는 것은 아니다.
