# Database Schema Design (EF Core)

## 1. User (Thiết bị vật lý / Unity Client)
- Id: Guid (PK)
- Username: string (MaxLength: 50, Required)
- Role: string (MaxLength: 20, Default: "NpcClient")
- CreatedAt: DateTimeOffset (Default: UtcNow)

## 2. ApiUsageLog (Lưu trữ token & metrics)
- Id: Guid (PK)
- UserId: Guid (FK -> User)
- Provider: string (MaxLength: 50) // "OpenAI" or "Gemini"
- Model: string (MaxLength: 50) // "gpt-4o-mini", "gemini-1.5-flash"
- InputTokens: int
- OutputTokens: int
- LatencyMs: long // Thời gian phản hồi của LLM
- Status: string (MaxLength: 20) // "Success", "Timeout", "RateLimited", "Failed"
- CreatedAt: DateTimeOffset (Default: UtcNow)

## 3. ChatHistory (Ngữ cảnh của NPC - Tuỳ chọn)
- Id: Guid (PK)
- UserId: Guid (FK -> User)
- Role: string (MaxLength: 20) // "System", "User", "Assistant"
- Content: string (Required)
- CreatedAt: DateTimeOffset