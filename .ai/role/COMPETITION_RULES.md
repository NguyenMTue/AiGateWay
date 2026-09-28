# Danh sách Kiểm tra & Thể lệ Cuộc thi AI Gateway

## 1. Thông tin Chung & Dòng thời gian
- **Hạn chót nộp bài:** 23:59 ngày 01/10/2026.
- **Hình thức:** Cá nhân.
- **Quy định về AI:** Được phép sử dụng AI (ChatGPT, Claude, Gemini, Cursor...). Đánh giá dựa trên tư duy giải quyết vấn đề, KHÔNG phải số lượng code gõ bằng tay. Không được sao chép mù quáng, phải hiểu rõ mọi dòng code được sinh ra.

## 2. Quy tắc Ẩn danh (TỐI QUAN TRỌNG)
- [ ] Không chứa tên thật, username cá nhân, email trong toàn bộ mã nguồn.
- [ ] Không chứa logo cá nhân trên Video Demo hoặc UI (Swagger/Postman).
- [ ] Xóa thư mục `.git` trước khi nén file `.zip` để nộp (nếu không dùng Github ẩn danh).
- [ ] Tệp zip nộp bài không mang tên cá nhân (VD: dùng `AIGateway_Submission.zip`).

## 3. Yêu cầu Kỹ thuật Bắt buộc (Minimum Requirements)
Hệ thống phải có đủ các Endpoints và tính năng sau:
- [ ] `POST /auth`: Xác thực người dùng/thiết bị.
- [ ] `POST /ai/chat` hoặc `POST /ai/analyze`: API gọi LLM, yêu cầu trả về đầu ra có cấu trúc (Structured JSON).
- [ ] `GET /conversations`: Lấy lịch sử ngữ cảnh trò chuyện.
- [ ] `GET /usage`: Bảng thống kê (tổng request, token, latency trung bình, tỷ lệ lỗi).
- [ ] **Lưu trữ Log:** Mỗi request AI phải lưu đủ: `user`, `model`, `timestamp`, `latency`, `input tokens`, `output tokens`, `status`.
- [ ] **Độ tin cậy:** Có xử lý lỗi, cơ chế thử lại (Retry), xử lý hết thời gian chờ (Timeout) và Giới hạn tốc độ (Rate Limiting).

## 4. Danh sách Nộp bài (Deliverables Checklist)
- [ ] **Sản phẩm hoạt động:** Liên kết demo hoặc file mã nguồn nén (tệp `.zip` hoàn chỉnh, có sẵn `docker-compose.yml` để chạy DB).
- [ ] **Video giới thiệu:** Tối đa 5 phút (Quay màn hình chạy Unity Game song song với Postman/DB Log).
- [ ] **Tài liệu README.md:** Bao gồm sơ đồ kiến trúc, thiết kế API, Database, tích hợp AI, xử lý lỗi và hạn chế.
- [ ] **Tài liệu AI_WORKLOG.md:** Ghi chép rõ lỗi của AI và cách bạn khắc phục (Minh chứng cho Tư duy kiểm chứng).
- [ ] **Form nộp bài:** Chuẩn bị ít nhất 1 prompt xuất sắc và đoạn mô tả quy trình > 30 ký tự để điền vào hệ thống.

## 5. Tiêu chí Chấm điểm (Trọng số 1-4 điểm)
- **1. Chất lượng nhắc nhở:** Có bối cảnh, vai trò, yêu cầu rõ ràng (Đã xử lý qua `.cursorrules` và `SCHEMA_DESIGN.md`).
- **2. Kết quả chất lượng:** Ứng dụng chạy mượt, code chuẩn Clean Architecture, không có lỗi 500.
- **3. Tư duy kiểm chứng:** Can thiệp và sửa sai cho AI (Thể hiện rõ trong `AI_WORKLOG.md`).
- **4. Ứng dụng thực tế:** Giải quyết được bài toán bảo mật API Key và Fallback cho thiết bị vật lý/Game.
- **5. Trình bày & chia sẻ:** `README.md` rõ ràng, ai cũng có thể clone về và chạy thử thành công trong 5 phút.

## 6. Tính năng Thưởng (Bonus - Để đạt điểm tuyệt đối)
- [ ] **Định tuyến đa mô hình & Fallback:** Tự động chuyển từ OpenAI sang Gemini khi lỗi (Polly v8).
- [ ] **Bộ nhớ đệm (Caching):** (Tùy chọn) Lưu kết quả vào Redis nếu câu hỏi/tình huống lặp lại.