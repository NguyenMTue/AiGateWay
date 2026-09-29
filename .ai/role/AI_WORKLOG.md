# Nhật ký sử dụng AI (AI Worklog) - Dự án AI Gateway

## 1. Công cụ đã sử dụng
- **Công cụ AI chính**: Google Antigravity (Model: Gemini 3.6 Flash High).
- **Môi trường phát triển**: Visual Studio 2022 / VS Code, .NET 10 SDK, EF Core PostgreSQL.

---

## 2. Các Lời nhắc (Prompts) tiêu biểu

### **Prompt 1 (Kiến trúc Nền tảng & Clean Architecture):**
> "Hãy thiết lập cấu trúc dự án ASP.NET Core Minimal APIs theo Clean Architecture kết hợp MediatR CQRS, Entity Framework Core PostgreSQL, hỗ trợ đa môi trường và .NET Aspire."

### **Prompt 2 (Bộ định tuyến Mô hình & Strategy Fallback):**
> "Viết bộ định tuyến `IntelligentRouter` và thực thể `RouteRule` hỗ trợ chiến lược Priority Routing. Nếu mô hình chính (PrimaryModel) bị lỗi hoặc quá tải, tự động chuyển tiếp tới mô hình dự phòng (FallbackModel)."

### **Prompt 3 (API Báo cáo & Thống kê Analytics):**
> "Triển khai module API Analytics tại `/api/analytics` gồm 3 báo cáo: Báo cáo chi phí/token theo thời gian (`/cost-usage`), Thống kê theo chìa khóa ảo (`/virtual-keys`), và Giám sát sức khỏe/độ trễ nhà cung cấp (`/provider-health`)."

### **Prompt 4 (Xác thực & Lịch sử Ngữ cảnh NPC):**
> "Triển khai endpoint xác thực `POST /auth` cấp JWT Bearer Token cho thiết bị/người dùng và API `GET /conversations` để truy xuất lịch sử ngữ cảnh 3 hành động gần nhất của NPC."

### **Prompt 5 (Mock Client Simulator & Stress Test):**
> "Tạo một C# Console Application `MockClient` đóng vai trò client vật lý gửi dữ liệu tọa độ NPC liên tục, tự động gọi `GET /conversations` và có chế độ Stress Test (nhấn phím S) kích hoạt lỗi HTTP 429 Rate Limit."

---

## 3. Kết quả đầu ra chưa chính xác của AI & Cách khắc phục (Tư duy kiểm chứng)

### ❌ **Vấn đề 1: Kéo toàn bộ Log về RAM để tính toán Thống kê (Memory Leak)**
* **Mô tả lỗi**: Trong lần sinh mã ban đầu cho API báo cáo token, AI đã viết `_context.RequestLogs.ToList().Sum(x => x.PromptTokens)`. Việc sử dụng `.ToList()` bắt EF Core nạp toàn bộ hàng triệu dòng log từ PostgreSQL về bộ nhớ RAM của Server trước khi tính tổng, gây tràn bộ nhớ (Out of Memory).
* **Cách khắc phục**: Tôi đã can thiệp tái cấu trúc lại câu lệnh LINQ, đẩy toàn bộ phép gom nhóm (`GroupBy`) và tính tổng (`Sum`) xuống thẳng câu lệnh SQL trên Database Engine:
  ```csharp
  var totalPromptTokens = await query.SumAsync(x => (long)x.PromptTokens, ct);
  ```

### ❌ **Vấn đề 2: Lỗi EF Core Async Provider khi chạy Unit Tests (`IAsyncEnumerable`)**
* **Mô tả lỗi**: Khi giả lập `Mock<DbSet<ChatHistory>>` trong bộ kiểm thử NUnit cho `GET /conversations`, AI sinh code mock truyền thống làm EF Core tung ngoại lệ `InvalidOperationException: The source IQueryable doesn't implement IAsyncEnumerable`.
* **Cách khắc phục**: Tôi đã tự thiết kế class trợ giúp `TestAsyncQueryProvider` và `TestAsyncEnumerable` triển khai `IAsyncQueryProvider` và `IAsyncEnumerable<T>`, giúp Moq giả lập chính xác EF Core Provider bất đồng bộ mà không cần phụ thuộc thư viện bên ngoài.

### ❌ **Vấn đề 3: Cảnh báo Biên dịch C# Nullability (`CS8604`)**
* **Mô tả lỗi**: AI tạo hàm cắt chuỗi hiển thị console `Truncate(string value, int maxLength)` nhưng khi nhận property JSON có thể null (`string?`), trình biên dịch C# 12 đưa ra cảnh báo `CS8604: Possible null reference argument`.
* **Cách khắc phục**: Sửa chữ ký hàm thành `Truncate(string? value, int maxLength)` và xử lý kiểm tra `string.IsNullOrEmpty(value)` trả về `string.Empty` an toàn tuyệt đối.

---

## 4. Kế hoạch Phát triển & Tối ưu tiếp theo
- Tích hợp Redis Cache cho danh sách `RouteRule` và kiểm tra Rate Limit `VirtualKey` để đạt độ trễ ngắt mạch < 1ms.
- Tích hợp bộ ngắt mạch tự động (Circuit Breaker via Polly v8) khi tỷ lệ lỗi HTTP 429/5xx của nhà cung cấp vượt quá 20% trong 5 phút.