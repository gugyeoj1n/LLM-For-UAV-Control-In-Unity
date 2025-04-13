### LLM을 활용한 무인 항공기 제어 시스템 시뮬레이션

    1. 유니티 환경에서 커맨드 기반으로 작동하는 드론 시스템을 구현합니다.

    2. UnityWebRequest로 로컬 환경에 실행된 Ollama 서버에 요청을 전송 및 응답을 반환받습니다.

    2 - 1. 사전에 준비한 프롬프트와 자연어 명령을 전송합니다.

    2 - 2. LLM 모델이 프롬프트를 기반으로 명령을 분석해 그와 일치하는 드론 커맨드를 JSON 형식으로 반환합니다.

    3. DroneCommandHandler에서 반환받은 JSON 파일을 분석해 DroneController로 전달합니다.

    4. DroneController는 전달받은 명령을 수행합니다.
