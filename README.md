### FD Agent Satellite
멀티모달 AI(LLM + YOLO)을 결합해 위성 간 협업 감시·자동 보고를 검증하는 시뮬레이션 시스템
### TEAM
<table>
  <tr align="center">
    <td width="300px">
      <a href="https://github.com/gugyeoj1n" target="_blank">
        <img src="https://avatars.githubusercontent.com/gugyeoj1n" alt="gugyeoj1n" />
      </a>
    </td>
    <td width="300px">
      <a href="https://github.com/espada105" target="_blank">
        <img src="https://avatars.githubusercontent.com/espada105" alt="espada105" />
      </a>
    </td>
  </tr>

  <tr align="center">
    <td>
      곽우진
    </td>
    <td>
      홍성인
    </td>
  </tr>

  <tr align="center">
    <td>
      위성 UI<br>Flight Dynamics Agent<br>Satellite State
    </td>
    <td>
      Ollama<br>LLM Command Interpreter
    </td>
  </tr>  
  
</table>

### 사용 기술
```
Unity 6000.0.43f1
- 물리 기반 Flight Dynamics 시뮬레이션과 UI/코루틴을 모두 Unity에서 처리

Ollama (Local LLM, Llama 3:8B)
- 자연어 명령 → JSON 명령 변환, 현재 위성 상태를 함께 전달하여 컨텍스트 보존

Unity Sentis + YOLOv8n ONNX
- GPUCompute 백엔드로 ONNX 모델을 실시간 추론, NMS/Confidence 필터링 적용

Native Lidar Scanner
- 360° Raycast(36개)로 장애물까지의 거리 맵을 구성, Recon 모드의 회피 로직에 제공

Newtonsoft.Json, UnityWebRequest
- LLM 응답 JSON 파싱 및 `Resources/command.json` 저장, Ollama API 호출 담당
```

### FD Agent Satellite 개요
- Flight Dynamics Agent: `SatelliteController`가 Move/Hover/Altitude/Rotate/Return/Recon/Tracking 상태 머신을 운용하며 Rigidbody linearVelocity를 직접 제어합니다.
- LLM Command Interpreter: `Program` 스크립트가 시스템 프롬프트와 함께 Ollama(Llama 3 8B)를 호출해 JSON 명령을 수신한 뒤 규칙 기반 파서로 폴백합니다.
- 센서 융합: `SatelliteVisionAnalyzer`가 Sentis YOLO 출력과 Lidar 스캔 결과를 UI 및 제어 로직에 반영하여 추적·정찰 시나리오를 구성합니다.

### 연구 배경 및 목표
- New Space 시대 위성 군집 규모가 급격히 증가하면서 지상 텔레메트리 기반 감시에 통신 지연, 대역폭 제약, 인적 의존성 문제가 누적되었습니다.
- 이를 해결하기 위해 위성 온보드에서 자연어 명령 수신 → 임무 실행 → 이상 탐지 → 자연어 보고까지 자율 처리하는 FD-Agent 개념을 검증했습니다.
- Unity 시뮬레이터에 Flight Dynamics(위성 A), 감시 대상 위성 B, Sentis YOLO 12s/8n, 온보드 LLM(Ollama)을 통합해 위성 간 협업 감시 파이프라인을 재현했습니다.

### 시스템 플로우
1. **에이전트 1단계 – 자연어 명령 변환**: 관제 GUI에서 “위성 B의 움직임을 감시하라”와 같은 명령을 작성하면 위성 A 온보드 LLM(Ollama, Llama3 8B)이 명령 변환 지침과 함께 입력을 받아 JSON 명령(`action`, `altitude`, `direction`, `speed`, `trackingDistance`)으로 구조화합니다. 규칙 기반 파서가 폴백으로 동작해 온보드 환경에서도 안정적입니다.
2. **에이전트 2단계 – 임무 실행**: `SatelliteCommandHandler`가 명령 큐에 적재하고 `SatelliteController` 상태 머신이 Move/Hover/Altitude/Rotate/Return/Recon/Tracking 모드를 실행합니다. 위성 A 카메라는 0.5초 간격으로 프레임을 캡처하며 Sentis YOLO 12s/8n ONNX 모델이 GPUCompute 백엔드에서 실시간 추론합니다.
3. **에이전트 3단계 – 데이터 수집/분석**: 감시 로그 20개가 쌓이면 LLM에 전달하여 자연어 보고서 생성 명령을 실행합니다. 로컬 실험 환경(RTX 3060 Ti 8GB, Ryzen 5 5600) 기준 배치 크기를 20으로 설정했으며, 고성능 환경에서는 확장 가능합니다.
4. **에이전트 4단계 – 자연어 보고**: 요약된 보고서는 UI에 즉시 표시되어 지상 인력이 원시 데이터를 분석하지 않고도 상황을 파악할 수 있으며 통신 지연과 인적 의존도를 낮춥니다.

### 실제 Unity 구동 화면
<img width="1907" height="973" alt="image" src="https://github.com/user-attachments/assets/3781e743-b2f4-4203-93ca-336dd8eb3462" />

<img width="1690" height="765" alt="image" src="https://github.com/user-attachments/assets/91887f6f-1361-43da-84e2-a1be774a7789" />
