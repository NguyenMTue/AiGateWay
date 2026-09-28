# Quy chuẩn Lập trình Backend Controller/Endpoints (Backend Controller Conventions) - Dự án AiGateway

Tài liệu này quy định cấu trúc code, quy chuẩn thiết kế và cách triển khai các Endpoint (Controller) ở phần **Backend** của dự án **AiGateway**. 
Các AI Agent (như Antigravity, Codex, Copilot, Gemini...) và lập trình viên **bắt buộc** phải tuân thủ tài liệu này khi đọc, bảo trì hoặc sinh mã nguồn mới cho backend.

---

## 🏗️ 1. Tổng quan Kiến trúc Backend (Backend Architecture)

Dự án **AiGateway Backend** được xây dựng trên nền tảng:
- **Framework**: .NET 10 / ASP.NET Core **Minimal APIs**.
- **Architectural Pattern**: **Clean Architecture** kết hợp với **CQRS** (Command Query Responsibility Segregation) thông qua thư viện **MediatR**.
- **Endpoint Pattern**: Thay vì dùng Controllers truyền thống (`[ApiController]`), dự án gom nhóm API theo từng module thông qua Interface `IEndpointGroup` đặt tại namespace `AiGateway.Web.Endpoints`.
- **Tự động đăng ký**: Tất cả class triển khai `IEndpointGroup` sẽ được quét và tự động đăng ký route qua `WebApplicationExtensions.MapEndpoints()`.

---

## 🧩 2. Quy chuẩn Cấu trúc Class Endpoint (`IEndpointGroup`)

Mỗi file Controller/Endpoint đại diện cho một nhóm tài nguyên (ví dụ: `AuthEndpoints`, `ProviderEndpoints`, `VirtualKeyEndpoints`, `ChatCompletionsEndpoints`) và bắt buộc triển khai `IEndpointGroup`.

### 📌 Cấu trúc chuẩn của một Endpoint Group Class:
```csharp
using AiGateway.Application.Common.Models;
using AiGateway.Application.Providers.Commands.CreateProvider;
using AiGateway.Application.Providers.Commands.UpdateProvider;
using AiGateway.Application.Providers.Commands.DeleteProvider;
using AiGateway.Application.Providers.Queries.GetProviderById;
using AiGateway.Web.Infrastructure;
using Microsoft.AspNetCore.Http.HttpResults;
using MediatR;

namespace AiGateway.Web.Endpoints;

public class ProviderEndpoints : IEndpointGroup
{
    // 1. Khai báo Route Prefix cố định cho nhóm API
    public static string RoutePrefix => "/api/providers";

    // 2. Hàm Map đăng ký các Route HTTP
    public static void Map(RouteGroupBuilder group)
    {
        group.MapGet("/id/{id:int}", GetById).AllowAnonymous();
        group.MapPost("/create", Create).RequireAuthorization();
        group.MapPut("/update", Update).RequireAuthorization();
        group.MapDelete("/delete/{id:int}", Delete).RequireAuthorization();
    }

    // 3. Khai báo các handler method tĩnh (static) bên dưới...
}
```

---

## 🛠️ 3. Quy chuẩn Chi tiết cho 4 Chức năng Cốt lõi: Login, Create, Update, Delete

### 🔑 1. Login (Xác thực người dùng & Cấp JWT Cookie)
- **Đặc điểm**: Cần kiểm tra Rate Limit theo IP/Email, gửi `LoginCommand` qua MediatR, thiết lập JWT cookie (`access_token`, `refresh_token`) an toàn (`HttpOnly`, `SameSite=Lax`), và trả về kết quả định dạng chuẩn.

```csharp
public static async Task<Results<Ok<AuthTokenResult>, UnauthorizedHttpResult, StatusCodeHttpResult>> Login(
    ISender sender,
    LoginCommand request,
    HttpContext httpContext,
    IRateLimitService rateLimitService)
{
    // 1. Kiểm tra Rate Limiting chống Brute Force Attack
    var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var allowed = await rateLimitService.IsAllowedAsync(
        $"login:{ip}:{request.Email.ToLowerInvariant()}",
        limit: 8,
        window: TimeSpan.FromMinutes(1),
        httpContext.RequestAborted);

    if (!allowed)
    {
        return TypedResults.StatusCode(StatusCodes.Status429TooManyRequests);
    }

    // 2. Gửi Command tới Application Layer
    var result = await sender.Send(request);

    if (result is null)
    {
        return TypedResults.Unauthorized();
    }

    // 3. Set Cookie chứa Token (Access Token & Refresh Token)
    SetTokenCookies(httpContext, result);

    // 4. Trả về kết quả TypedResults.Ok
    return TypedResults.Ok(result);
}
```

---

### ➕ 2. Create (Tạo mới tài nguyên)
- **Đặc điểm**: Yêu cầu xác thực (`RequireAuthorization()`), nhận `ISender` và `Create<Entity>Command`, thực hiện tạo mới và trả về `Result<int>` (ID của đối tượng mới tạo) hoặc `BadRequest` nếu validate thất bại.

```csharp
public static async Task<Results<Ok<Result<int>>, BadRequest<Result<int>>>> Create(
    ISender sender, 
    CreateProviderCommand request)
{
    var result = await sender.Send(request);
    
    return result.Succeeded 
        ? TypedResults.Ok(result) 
        : TypedResults.BadRequest(result);
}
```

---

### ✏️ 3. Update (Cập nhật tài nguyên)
- **Đặc điểm**: Nhận `Update<Entity>Command` chứa thông tin cập nhật, gọi CQRS Handler. Khi thành công trả về `TypedResults.NoContent()` (HTTP 204) hoặc `TypedResults.Ok(result)`, khi thất bại trả về `TypedResults.BadRequest(result)`.

```csharp
public static async Task<Results<NoContent, BadRequest<Result>>> Update(
    ISender sender, 
    UpdateProviderCommand request)
{
    var result = await sender.Send(request);

    return result.Succeeded 
        ? TypedResults.NoContent() 
        : TypedResults.BadRequest(result);
}
```

---

### 🗑️ 4. Delete (Xóa tài nguyên)
- **Đặc điểm**: Định tuyến qua HTTP DELETE với tham số id trên Route (ví dụ: `/delete/{id:int}`). Tạo mới `Delete<Entity>Command(id)` và truyền tới `ISender`.

```csharp
public static async Task<Results<NoContent, BadRequest<Result>>> Delete(
    ISender sender, 
    int id)
{
    var result = await sender.Send(new DeleteProviderCommand(id));

    return result.Succeeded 
        ? TypedResults.NoContent() 
        : TypedResults.BadRequest(result);
}
```

---

## ⚡ 4. Nguyên tắc Lập trình CQRS & Application Layer đi kèm

Để Endpoints hoạt động đúng chuẩn, các Command/Query ở tầng `Application` phải tuân theo cấu trúc gọn nhẹ sử dụng C# 12 Primary Constructors và Sealed Records:

### 📄 Cấu trúc Command & Handler mẫu (`CreateProviderCommand.cs`):
```csharp
namespace AiGateway.Application.Providers.Commands.CreateProvider;

// 1. Command Record
public sealed record CreateProviderCommand(string Name, string BaseUrl) : IRequest<Result<int>>;

// 2. Command Handler với Primary Constructor
public sealed class CreateProviderCommandHandler(
    IApplicationDbContext db, 
    ICacheService cache) 
    : IRequestHandler<CreateProviderCommand, Result<int>>
{
    public async Task<Result<int>> Handle(CreateProviderCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result<int>.Failure(new[] { "Provider name is required." });

        var entity = new AiProvider { Name = request.Name.Trim(), BaseUrl = request.BaseUrl.Trim() };
        db.AiProviders.Add(entity);
        await db.SaveChangesAsync(ct);
        
        // Invalidate Cache liên quan
        await cache.RemoveAsync("providers:all", ct);
        
        return Result<int>.Success(entity.Id);
    }
}
```

---

## ⚠️ 5. Quy tắc Bắt buộc dành cho AI Agents (AI Agent Directives)

Khi một AI Agent được giao nhiệm vụ viết Endpoint / Controller mới hoặc sửa đổi code backend trong dự án này:

1. **KHÔNG DÙNG `[ApiController]` hay `ControllerBase`**: Mọi Controller phải viết theo dạng **Minimal API Group** triển khai `IEndpointGroup`.
2. **LUÔN DÙNG `TypedResults`**: Không dùng `Results.Ok()` không định kiểu. Phải sử dụng `TypedResults.Ok(...)`, `TypedResults.BadRequest(...)`, `TypedResults.NoContent()`, `TypedResults.NotFound()`, `TypedResults.Unauthorized()` để OpenAPI/Swagger hiển thị đúng Schema.
3. **KHÔNG VIẾT LOGIC XỬ LÝ DỮ LIỆU TẠI ENDPOINT**: Endpoint chỉ làm nhiệm vụ:
   - Kiểm tra Rate Limit / Idempotency / Claims (nếu có).
   - Gọi `sender.Send(command/query)`.
   - Trả về HTTP Status Code tương ứng với kết quả của `Result`.
4. **KHAI BÁO PHƯƠNG THỨC TĨNH (`public static async Task<Results<...>> MethodName`)**: Tất cả handler method trong `IEndpointGroup` đều là static method.
5. **ĐẶT TÊN ROUTE NHẤT QUÁN**:
   - **Dành cho Admin Management APIs**:
     - Lấy tất cả: `GET /api/<entities>/all`
     - Lấy theo ID: `GET /api/<entities>/id/{id:int}`
     - Tạo mới: `POST /api/<entities>/create`
     - Cập nhật: `PUT /api/<entities>/update`
     - Xóa: `DELETE /api/<entities>/delete/{id:int}`
   - **Dành cho AI Proxy Endpoints (Chuẩn OpenAI Spec)**:
     - Chat Completions: `POST /v1/chat/completions`
     - List Models: `GET /v1/models`
     - Embeddings: `POST /v1/embeddings`
6. **ĐẶT FILE VÀ NAMESPACE**:
   - File Endpoint đặt tại: `src/Web/Endpoints/<Entity>Endpoints.cs`
   - Namespace: `namespace AiGateway.Web.Endpoints;`
