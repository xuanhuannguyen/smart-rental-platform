"""Post-processing for digit detections returned by the meter YOLO model."""
from __future__ import annotations

from dataclasses import dataclass
from statistics import median
from typing import Any, Iterable


class MeterReadError(ValueError):
    pass


@dataclass(frozen=True)
class Digit:
    value: str
    x: float
    y: float
    width: float
    height: float
    confidence: float


def _to_digit(item: dict[str, Any], min_confidence: float) -> Digit | None:
    label = str(item.get("class", "")).strip()
    confidence = float(item.get("confidence", 0))
    if label not in set("0123456789") or confidence < min_confidence:
        return None
    try:
        digit = Digit(label, float(item["x"]), float(item["y"]),
                      float(item["width"]), float(item["height"]), confidence)
    except (KeyError, TypeError, ValueError) as exc:
        raise MeterReadError("Model trả về tọa độ chữ số không hợp lệ.") from exc
    return digit if digit.width > 0 and digit.height > 0 else None


def _overlap_ratio(a: Digit, b: Digit) -> float:
    left = max(a.x - a.width / 2, b.x - b.width / 2)
    right = min(a.x + a.width / 2, b.x + b.width / 2)
    top = max(a.y - a.height / 2, b.y - b.height / 2)
    bottom = min(a.y + a.height / 2, b.y + b.height / 2)
    intersection = max(0.0, right - left) * max(0.0, bottom - top)
    return intersection / min(a.width * a.height, b.width * b.height)


def _remove_duplicates(digits: Iterable[Digit]) -> list[Digit]:
    kept: list[Digit] = []
    for digit in sorted(digits, key=lambda d: d.confidence, reverse=True):
        if not any(_overlap_ratio(digit, other) > 0.65 for other in kept):
            kept.append(digit)
    return kept


def _best_row(digits: list[Digit]) -> list[Digit]:
    """Pick the strongest horizontal row, ignoring serial numbers elsewhere."""
    typical_height = median(d.height for d in digits)
    tolerance = max(6.0, typical_height * 0.55)
    candidates = []
    for anchor in digits:
        row = [d for d in digits if abs(d.y - anchor.y) <= tolerance]
        score = (len(row), sum(d.confidence for d in row), -sum(abs(d.y - anchor.y) for d in row))
        candidates.append((score, row))
    return max(candidates, key=lambda item: item[0])[1]


def read_prediction(payload: dict[str, Any], min_confidence: float = 0.60) -> dict[str, Any]:
    predictions = payload.get("predictions")
    if not isinstance(predictions, list):
        raise MeterReadError("Model không trả về danh sách dự đoán.")

    digits = _remove_duplicates(filter(None, (_to_digit(p, min_confidence) for p in predictions)))
    if not digits:
        raise MeterReadError("Không phát hiện chữ số đủ tin cậy trong ảnh.")

    row = sorted(_best_row(digits), key=lambda d: d.x)
    raw_text = "".join(d.value for d in row)
    if not 1 <= len(raw_text) <= 12:
        raise MeterReadError("Số chữ số nhận diện được không hợp lệ.")

    return {
        "reading": int(raw_text),
        "raw_text": raw_text,
    }
