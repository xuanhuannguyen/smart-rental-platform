import unittest
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from meter_reader import MeterReadError, read_prediction


class MeterReaderTests(unittest.TestCase):
    def test_sorts_digits_and_preserves_leading_zeroes(self):
        predictions = [
            {"class": value, "x": x, "y": 20, "width": 10, "height": 14, "confidence": .99}
            for value, x in zip("0000032", [10, 20, 30, 40, 50, 60, 70])
        ]
        result = read_prediction({"predictions": list(reversed(predictions))})
        self.assertEqual(32, result["reading"])
        self.assertEqual("0000032", result["raw_text"])

    def test_ignores_low_confidence_and_other_rows(self):
        predictions = [
            {"class": "1", "x": 10, "y": 100, "width": 10, "height": 20, "confidence": .98},
            {"class": "2", "x": 20, "y": 101, "width": 10, "height": 20, "confidence": .97},
            {"class": "9", "x": 10, "y": 10, "width": 10, "height": 20, "confidence": .99},
            {"class": "8", "x": 30, "y": 100, "width": 10, "height": 20, "confidence": .2},
        ]
        self.assertEqual("12", read_prediction({"predictions": predictions})["raw_text"])

    def test_rejects_empty_detection(self):
        with self.assertRaises(MeterReadError):
            read_prediction({"predictions": []})


if __name__ == "__main__":
    unittest.main()
