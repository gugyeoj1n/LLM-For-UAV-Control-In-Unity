# 위성 인식 YOLO 모델 학습 가이드

## 1단계: 환경 설정
```bash
pip install ultralytics
pip install torch torchvision
```

## 2단계: 데이터셋 준비
### 폴더 구조:
```
satellite_dataset/
├── images/
│   ├── train/
│   └── val/
├── labels/
│   ├── train/
│   └── val/
└── dataset.yaml
```

### dataset.yaml 설정:
```yaml
path: ./satellite_dataset
train: images/train
val: images/val

nc: 1  # 클래스 개수
names: ['satellite']
```

## 3단계: 학습 코드
```python
from ultralytics import YOLO

# 기존 YOLO12s 모델 로드
model = YOLO('yolo12s.pt')

# 위성 감지용으로 Fine-tuning
results = model.train(
    data='satellite_dataset/dataset.yaml',
    epochs=100,
    imgsz=640,
    batch=16,
    lr0=0.001,
    name='satellite_yolo12s',
    patience=20
)

# ONNX 형식으로 내보내기
model.export(format='onnx', optimize=True, simplify=True)
```

## 4단계: Unity에 적용
1. 학습된 .onnx 파일을 `Assets/sentis-YOLOv8n/Models/` 폴더에 복사
2. Unity Inspector에서 modelAsset을 새 모델로 변경
3. classes.txt를 새 라벨에 맞게 수정

## 5단계: 성능 향상 팁
- **데이터 증강**: 회전, 크기 변경, 밝기 조절
- **Hard Negative Mining**: 오탐지 이미지 추가
- **Multi-scale Training**: 다양한 해상도로 학습
- **Ensemble Methods**: 여러 모델 조합

## 주의사항
- 최소 1000장 이상의 다양한 위성 이미지 필요
- 라벨링 품질이 성능에 큰 영향
- GPU 환경에서 학습 권장 (CUDA 설정 필요) 