# Hướng dẫn giả lập Client Vật lý (Mock Client Setup)

Tệp này chứa các quy tắc và kịch bản để tạo một ứng dụng giả lập thay thế cho Game Unity 2D. Mock Client này sẽ đóng vai trò như một thiết bị vật lý gửi request liên tục đến AI Gateway nhằm kiểm thử tính năng Rate Limiting, cơ chế Fallback, truy xuất ngữ cảnh và ghi log.

## 1. Tùy chọn 1: C# Console Application (Khuyến dùng)
Sử dụng C# Console App giúp bạn tận dụng lại các Data Models (DTOs) của backend và đồng bộ ngôn ngữ.

**Yêu cầu tính năng (Dành cho AI Agent tạo code):**
1. **Xác thực (`POST /auth`):** Khi ứng dụng khởi động, gửi thông tin đăng nhập tĩnh để nhận và lưu JWT Token.
2. **Vòng lặp sự kiện:** Tạo một vòng lặp bất đồng bộ `while (true)`.
3. **Sinh dữ liệu vật lý (Mock Data):** Khởi tạo ngẫu nhiên tọa độ (X, Y) của NPC, vận tốc, và khoảng cách tới chướng ngại vật dưới định dạng JSON.
4. **Gửi yêu cầu (`POST /ai/analyze`):** Dùng `HttpClient` đính kèm header `Authorization: Bearer <token>`, gửi payload vật lý lên Gateway. In kết quả `{"action": "..."}` ra Terminal.
5. **Kiểm tra ngữ cảnh (`GET /conversations`):** Cứ sau mỗi 5 lần gửi tọa độ, Console App tự động gọi `GET /conversations` để lấy và in ra màn hình 3 hành động gần nhất của NPC. Điều này chứng minh API lưu trữ lịch sử hoạt động trơn tru.
6. **Stress Test & Rate Limiting:** Thiết lập một phím cứng (ví dụ: nhấn Enter) để giảm `Task.Delay` từ 2000ms xuống 100ms, cố tình gọi API liên tục để kích hoạt HTTP 429 - Rate Limit.

## 2. Kịch bản ghi hình Video Demo (Tối đa 5 phút)
Quay màn hình chia đôi để chứng minh toàn bộ API hoạt động thực tế:
- **Bên trái (Mock Client):** Chạy Console App. Giám khảo sẽ thấy text nhảy liên tục báo cáo trạng thái NPC, lâu lâu sẽ in ra "Lịch sử 5 bước gần nhất..." (Kiểm chứng `/conversations`).
- **Bên phải (Swagger/Postman):** Gọi định kỳ endpoint `GET /usage`. Giám khảo sẽ thấy tổng số Request, số Token và Độ trễ trung bình tăng lên theo thời gian thực.
- **Điểm nhấn:** Kích hoạt tính năng gọi nhanh để màn hình Mock Client văng lỗi `429 Too Many Requests`, chứng minh tính năng bảo vệ hệ thống.

## 3. Checklist Kiểm chứng Database (Bắt buộc trước khi nộp bài)
Mở Database (PgAdmin/DBeaver/SQL Server Management Studio) và kiểm tra bảng `ApiUsageLog`. Đảm bảo MỖI request từ Mock Client đều lưu đầy đủ 7 trường dữ liệu sau:
- [ ] `user_id`: ID của thiết bị/người dùng.
- [ ] `model`: Mô hình LLM đã dùng (VD: "gpt-4o-mini").
- [ ] `timestamp` / `created_at`: Thời gian chính xác của request.
- [ ] `latency_ms`: Độ trễ tính bằng mili-giây (phải là số thực, VD: 1250ms).
- [ ] `input_tokens`: Số lượng token của prompt gửi đi.
- [ ] `output_tokens`: Số lượng token của JSON trả về.
- [ ] `status`: Trạng thái ("Success", "RateLimited", "Timeout", "Error").