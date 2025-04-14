### LLM-For-UAV-Control-In-Unity
Large Language Model(LLM)을 활용하여 자연어 명령을 드론 제어로 변환하는 Unity 시뮬레이션 시스템
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
      UI<br>Drone Control Protocol (DCP) <br> Drone State
    </td>
    <td>
      Ollama<br>LLM-Based Command Interpreter
    </td>
  </tr>  
  
</table>

### 사용 기술
    Unity 6000.0.43f1
    Ollama (Local LLM)
    LLM Model: Llama 3:8B


### LLM을 활용한 무인 항공기 제어 시스템 시뮬레이션

    1. 유니티 환경에서 커맨드 기반으로 작동하는 드론 시스템을 구현합니다.

    2. UnityWebRequest로 로컬 환경에 실행된 Ollama 서버에 요청을 전송 및 응답을 반환받습니다.

    2 - 1. 사전에 준비한 프롬프트와 자연어 명령을 전송합니다.

    2 - 2. LLM 모델이 프롬프트를 기반으로 명령을 분석해 그와 일치하는 드론 커맨드를 JSON 형식으로 반환합니다.

    3. DroneCommandHandler에서 반환받은 JSON 파일을 분석해 DroneController로 전달합니다.

    4. DroneController는 전달받은 명령을 수행합니다.

### 실제 Unity 사용 및 Ollama 통신 화면
![UAV_Result-ezgif com-video-to-gif-converter](https://github.com/user-attachments/assets/0d3e5833-1e68-4548-b393-72141424f74a)
