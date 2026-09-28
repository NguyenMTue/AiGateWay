# Nhật ký sử dụng AI (AI Worklog)

## 1. Công cụ đã sử dụng
- Gemenu/ Antigravity (Ghi rõ công cụ bạn dùng).

## 2. Lời nhắc (Prompts) tiêu biểu
**Prompt 1 (Thiết kế Logging Pipeline):**
"Hãy tạo một `IPipelineBehavior` trong MediatR để tự động đo thời gian bằng Stopwatch, sau đó lấy thông tin token và lưu vào `ApiUsageLog` qua EF Core mỗi khi có request gửi lên AI Gateway."

**Prompt 2 (Thiết lập Fallback Polly):**
"Viết cấu hình `IHttpClientBuilder` sử dụng Polly v8. Nếu gọi OpenAI bị timeout quá 5 giây, tự động retry 1 lần. Nếu vẫn lỗi, kích hoạt Fallback Policy trả về một JSON an toàn cho Unity Client."

## 3. Kết quả đầu ra không chính xác của AI & Cách khắc phục (Tư duy kiểm chứng)
- **Vấn đề:** Khi tôi yêu cầu AI viết API `/usage` tính tổng token, AI đã sử dụng LINQ `_dbContext.ApiUsageLogs.ToList().Sum(...)`. Điều này kéo toàn bộ dữ liệu về RAM của server gây rò rỉ bộ nhớ nếu có hàng triệu logs.
- **Cách khắc phục:** Tôi đã yêu cầu AI (hoặc tự sửa) bằng cách đẩy phép tính xuống Database: `await _dbContext.ApiUsageLogs.SumAsync(x => x.InputTokens, ct);`.

## 4. Dự định tối ưu trong 7 ngày tới
- Tích hợp Redis để làm bộ nhớ đệm (Cache) cho các ngữ cảnh lặp lại của NPC.
- ...