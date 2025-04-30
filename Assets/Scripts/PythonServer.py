from fastapi import FastAPI, WebSocket, WebSocketDisconnect
import uvicorn
import numpy as np
import cv2
import base64
from ultralytics import YOLO
import json
import asyncio

app = FastAPI()

# YOLOv8-nano 모델 로드
model = YOLO("yolov8n.pt")  # 약 6MB 크기의 경량 모델


# generate_description 함수 수정
def generate_description(detections, frame_shape):
    height, width = frame_shape[:2]
    center_x = width // 2
    
    objects = {}
    
    # 감지된 객체가 없는 경우 처리
    if not detections or len(detections) == 0:
        return "특별한 물체가 감지되지 않았습니다."
    
    for det in detections:
        # boxes가 비어있는지 확인
        if len(det) == 0 or len(det.boxes) == 0:
            continue
        
        try:
            # 안전하게 값에 접근
            boxes_cls = det.boxes.cls
            if len(boxes_cls) == 0:
                continue
                
            cls_id = int(boxes_cls.cpu().numpy()[0])
            cls_name = model.names[cls_id]
            
            # 신뢰도
            conf = float(det.boxes.conf.cpu().numpy()[0])
            if conf < 0.5:  # 낮은 신뢰도 객체 무시
                continue
                
            # 바운딩 박스 중심 계산
            box = det.boxes.xyxy.cpu().numpy()[0]
            box_center_x = (box[0] + box[2]) / 2
            
            # 객체 방향 결정 (왼쪽/중앙/오른쪽)
            position = "왼쪽" if box_center_x < center_x - width*0.2 else \
                      "오른쪽" if box_center_x > center_x + width*0.2 else \
                      "앞"
            
            # 상대적 크기 계산 (가까움/중간/멈)
            box_area = (box[2] - box[0]) * (box[3] - box[1])
            frame_area = width * height
            size_ratio = box_area / frame_area
            
            distance = "가까이" if size_ratio > 0.2 else \
                      "멀리" if size_ratio < 0.05 else \
                      "중간 거리에"
            
            if cls_name not in objects:
                objects[cls_name] = []
            objects[cls_name].append({"position": position, "distance": distance})
        except (IndexError, ValueError) as e:
            print(f"객체 처리 중 오류: {e}")
            continue

    # 설명 생성
    if not objects:
        return "특별한 물체가 감지되지 않았습니다."
    
    descriptions = []
    for obj_name, instances in objects.items():
        for instance in instances:
            descriptions.append(f"{instance['position']}쪽 {instance['distance']} {obj_name}가 있습니다.")
    
    return " ".join(descriptions)

# analyze_frame 함수의 objects 배열 처리 부분 수정
# 결과 반환 부분 수정
response = {
    "description": description,
    "objects": []
}

# 감지된 객체가 있을 경우에만 처리
for det in results:
    if len(det) > 0 and len(det.boxes) > 0 and len(det.boxes.cls) > 0:
        try:
            obj = {
                "class_name": model.names[int(det.boxes.cls.cpu().numpy()[0])],
                "confidence": float(det.boxes.conf.cpu().numpy()[0]),
                "bbox": det.boxes.xyxy.cpu().numpy()[0].tolist()
            }
            response["objects"].append(obj)
        except (IndexError, ValueError) as e:
            print(f"객체 JSON 변환 중 오류: {e}")
            continue

# 객체 수 출력 (디버깅용)
print(f"감지된 객체 수: {len(response['objects'])}")

@app.websocket("/analyze")
async def analyze_frame(websocket: WebSocket):
    await websocket.accept()
    print("클라이언트 연결 성공")
    
    # 연결 상태 추적
    is_connection_active = True
    
    try:
        while is_connection_active:
            try:
                # 이미지 데이터 수신
                data = await websocket.receive_text()
                print("이미지 데이터 수신 완료")
                
                # 데이터 디코딩
                try:
                    img_data = base64.b64decode(data.split(',')[1] if ',' in data else data)
                    
                    # 이미지 디코딩
                    nparr = np.frombuffer(img_data, np.uint8)
                    frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
                    
                    if frame is None:
                        print("이미지 디코딩 실패")
                        await websocket.send_text(json.dumps({
                            "description": "이미지 처리 중 오류가 발생했습니다.",
                            "objects": []
                        }))
                        continue
                    
                    # 이미지 크기 출력 (디버깅용)
                    print(f"이미지 크기: {frame.shape}")
                    
                    # YOLOv8-nano로 객체 검출
                    results = model(frame)
                    
                    # 결과 처리 및 설명 생성
                    description = generate_description(results, frame.shape)
                    print(f"생성된 설명: {description}")
                    
                    # 결과 반환
                    response = {
                        "description": description,
                        "objects": [
                            {
                                "class_name": model.names[int(det.boxes.cls.cpu().numpy()[0])],
                                "confidence": float(det.boxes.conf.cpu().numpy()[0]),
                                "bbox": det.boxes.xyxy.cpu().numpy()[0].tolist()
                            } for det in results if len(det)
                        ]
                    }
                    
                    # 객체 수 출력 (디버깅용)
                    print(f"감지된 객체 수: {len(response['objects'])}")
                    
                    await websocket.send_text(json.dumps(response))
                    print("분석 결과 전송 완료")
                    
                except Exception as e:
                    print(f"이미지 처리 오류: {e}")
                    await websocket.send_text(json.dumps({
                        "description": f"이미지 처리 중 오류가 발생했습니다: {str(e)}",
                        "objects": []
                    }))
            
            except WebSocketDisconnect:
                print("WebSocket 연결이 끊어졌습니다.")
                is_connection_active = False
                break
                
    except Exception as e:
        print(f"연결 처리 중 오류 발생: {e}")
    
    print("WebSocket 핸들러 종료")

@app.get("/")
async def root():
    return {"message": "드론 비전 분석 서버가 실행 중입니다. WebSocket 엔드포인트: /analyze"}

if __name__ == "__main__":
    print("드론 비전 분석 서버를 시작합니다...")
    uvicorn.run(app, host="0.0.0.0", port=8000)