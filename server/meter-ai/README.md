# Meter AI service

Service Python nhận ảnh từ API .NET, gọi model phát hiện chữ số và trả về chỉ số đã sắp xếp từ trái sang phải. Service này không dùng OCR.

```powershell
cd server/meter-ai
.\start.ps1 -ApiKey "your-key"
```

Kiểm tra thuật toán hậu xử lý mà không gọi model:

```powershell
python -m unittest test_meter_reader.py
```

Không commit API key. Có thể cấu hình thêm `ROBOFLOW_MODEL_ID`, `METER_MIN_CONFIDENCE` và `METER_MAX_IMAGE_BYTES` bằng biến môi trường.

Service AI trả về toàn bộ dãy số nhận diện được. Backend áp dụng quy tắc nghiệp vụ trước khi trả kết quả cho web:

- Điện: bỏ một chữ số cuối và làm tròn (`289` thành `29`, `284` thành `28`).
- Nước: bỏ ba chữ số cuối và làm tròn (`278902` thành `279`, `278499` thành `278`).
