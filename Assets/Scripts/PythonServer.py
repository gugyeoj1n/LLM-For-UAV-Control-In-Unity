from fastapi import FastAPI, WebSocket
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

# 분석 결과를 텍스트로 변환하는 함수
def generate_description(detections, frame_shape):
    height, width = frame_shape[:2]
    center_x = width // 2
    
    objects = {}
    for det in detections:
        cls_id = int(det.boxes.cls.cpu().numpy()[0])
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

    # 설명 생성
    if not objects:
        return "특별한 물체가 감지되지 않았습니다."
    
    descriptions = []
    for obj_name, instances in objects.items():
        for instance in instances:
            descriptions.append(f"{instance['position']}쪽 {instance['distance']} {obj_name}가 있습니다.")
    
    return " ".join(descriptions)

@app.websocket("/analyze")
async def analyze_frame(websocket: WebSocket):
    await websocket.accept()
    
    while True:
        try:
            # 이미지 데이터 수신
            data = await websocket.receive_text()
            img_data = base64.b64decode(data.split(',')[1] if ',' in data else data)
            
            # 이미지 디코딩
            nparr = np.frombuffer(img_data, np.uint8)
            frame = cv2.imdecode(nparr, cv2.IMREAD_COLOR)
            
            # YOLOv8-nano로 객체 검출
            results = model(frame)
            
            # 결과 처리 및 설명 생성
            description = generate_description(results, frame.shape)
            
            # 결과 반환
            await websocket.send_text(json.dumps({
                "description": description,
                "objects": [
                    {
                        "class": model.names[int(det.boxes.cls.cpu().numpy()[0])],
                        "confidence": float(det.boxes.conf.cpu().numpy()[0]),
                        "bbox": det.boxes.xyxy.cpu().numpy()[0].tolist()
                    } for det in results if len(det)
                ]
            }))
            
        except Exception as e:
            print(f"Error: {e}")
            await websocket.close()
            break

if __name__ == "__main__":
    uvicorn.run(app, host="0.0.0.0", port=8000)