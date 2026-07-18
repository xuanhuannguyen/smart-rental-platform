import os
import tempfile
from pathlib import Path

from fastapi import FastAPI, File, HTTPException, UploadFile
from inference_sdk import InferenceHTTPClient
from starlette.concurrency import run_in_threadpool

from meter_reader import MeterReadError, read_prediction

ENV_PATH = Path(__file__).with_name(".env")


def _load_local_env() -> None:
    if not ENV_PATH.exists():
        return

    for line in ENV_PATH.read_text(encoding="utf-8").splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#") or "=" not in stripped:
            continue
        key, value = stripped.split("=", 1)
        key = key.strip()
        value = value.strip().strip('"').strip("'")
        if key and key not in os.environ:
            os.environ[key] = value


_load_local_env()

API_KEY = os.environ.get("ROBOFLOW_API_KEY", "").strip()
MODEL_ID = os.environ.get(
    "ROBOFLOW_MODEL_ID", "utility-meter-reading-dataset-for-automatic-reading-yolo/1"
)
MIN_CONFIDENCE = float(os.environ.get("METER_MIN_CONFIDENCE", "0.60"))
MAX_IMAGE_BYTES = int(os.environ.get("METER_MAX_IMAGE_BYTES", str(10 * 1024 * 1024)))

app = FastAPI(title="Smart Rental Meter AI", version="1.0.0")


def _infer(image_path: str) -> dict:
    client = InferenceHTTPClient(api_url="https://serverless.roboflow.com", api_key=API_KEY)
    return client.infer(image_path, model_id=MODEL_ID)


@app.get("/health")
def health() -> dict[str, str]:
    return {"status": "ok" if API_KEY else "missing_api_key"}


@app.post("/predict")
async def predict(file: UploadFile = File(...)) -> dict:
    if not API_KEY:
        raise HTTPException(503, "ROBOFLOW_API_KEY chưa được cấu hình.")
    if file.content_type not in {"image/jpeg", "image/png", "image/webp"}:
        raise HTTPException(415, "Chỉ hỗ trợ ảnh JPEG, PNG hoặc WebP.")

    content = await file.read(MAX_IMAGE_BYTES + 1)
    if not content or len(content) > MAX_IMAGE_BYTES:
        raise HTTPException(413, "Ảnh rỗng hoặc vượt quá giới hạn dung lượng.")

    try:
        suffix = {"image/jpeg": ".jpg", "image/png": ".png", "image/webp": ".webp"}[file.content_type]
        # Windows locks an open NamedTemporaryFile. Close it before the SDK
        # opens the path, then always remove it after inference.
        image_path: str | None = None
        try:
            with tempfile.NamedTemporaryFile(suffix=suffix, delete=False) as image:
                image.write(content)
                image_path = image.name
            result = await run_in_threadpool(_infer, image_path)
        finally:
            if image_path:
                Path(image_path).unlink(missing_ok=True)
        return read_prediction(result, MIN_CONFIDENCE)
    except MeterReadError as exc:
        raise HTTPException(422, str(exc)) from exc
    except Exception as exc:
        raise HTTPException(502, "Không thể gọi model nhận diện đồng hồ.") from exc
