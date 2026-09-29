using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace MockClient;

public class Program
{
    private static readonly string GatewayBaseUrl = "https://localhost:7144";
    private static readonly string AuthEmail = "npcclient@localhost";
    private static readonly string AuthPassword = "NpcClient123!";
    private static readonly string DevVirtualKey = "gw-live-devtestkey1234567890abcdef";

    private static string? _accessToken;
    private static bool _stressTestMode = false;
    private static int _requestCounter = 0;

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "AI Gateway - Physical Client Simulator (Mock Client)";

        PrintBanner();

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri(GatewayBaseUrl) };

        // 1. Authenticate with Gateway
        await AuthenticateAsync(client);

        Console.WriteLine("\n[CTRL+C to exit | Press 'S' to toggle Stress Test Mode (Rate Limiting Trigger)]");
        Console.WriteLine("----------------------------------------------------------------------------------");

        // Start background key listener thread
        _ = Task.Run(ListenForKeyPresses);

        var random = new Random();

        // 2. Main Simulation Loop
        while (true)
        {
            _requestCounter++;
            var posX = Math.Round(random.NextDouble() * 100, 2);
            var posY = Math.Round(random.NextDouble() * 100, 2);
            var playerDist = Math.Round(random.NextDouble() * 20, 2);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[REQ #{_requestCounter}] [{DateTime.Now:HH:mm:ss.fff}] NPC Telemetry -> Pos: ({posX}, {posY}) | Player Dist: {playerDist}m");
            Console.ResetColor();

            // Send Chat Completion Request
            await SendAiAnalyzeRequestAsync(client, posX, posY, playerDist);

            // Every 5 requests, check conversation context history (GET /conversations)
            if (_requestCounter % 5 == 0)
            {
                await GetConversationContextAsync(client);
            }

            var delayMs = _stressTestMode ? 100 : 2000;
            await Task.Delay(delayMs);
        }
    }

    private static void PrintBanner()
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine(@"
================================================================================
          AI GATEWAY - PHYSICAL CLIENT SIMULATOR (MOCK CLIENT)
================================================================================
  Simulates an autonomous NPC Unity 2D / Physical Device sending live telemetry
  to the AI Gateway for Real-time LLM Routing, Rate Limiting & Audit Logging.
================================================================================");
        Console.ResetColor();
    }

    private static async Task AuthenticateAsync(HttpClient client)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"\n[STEP 1] Authenticating client device via POST /auth ({AuthEmail})...");
        Console.ResetColor();

        try
        {
            var authPayload = new { email = AuthEmail, password = AuthPassword };
            var response = await client.PostAsJsonAsync("/auth", authPayload);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                if (json.TryGetProperty("accessToken", out var tokenProp))
                {
                    _accessToken = tokenProp.GetString();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[AUTH SUCCESS] Device Authenticated! JWT Bearer Token acquired.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[AUTH WARN] Defaulting to Virtual Key Authorization header ({DevVirtualKey}).");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[AUTH ERROR] Could not reach Gateway: {ex.Message}");
            Console.WriteLine($"[FALLBACK] Using Dev Virtual Key ({DevVirtualKey}).");
            Console.ResetColor();
        }
    }

    private static async Task SendAiAnalyzeRequestAsync(HttpClient client, double posX, double posY, double playerDist)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, "/v1/chat/completions");

        // Attach Authorization (Bearer Token or Virtual Key)
        if (!string.IsNullOrEmpty(_accessToken))
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
        }
        else
        {
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", DevVirtualKey);
        }

        var payload = new
        {
            model = "smart-model",
            messages = new[]
            {
                new { role = "system", content = "You are an NPC Guard. Analyze telemetry and output JSON response: {\"action\": \"...\", \"dialogue\": \"...\"}" },
                new { role = "user", content = $"Telemetry: Pos=({posX},{posY}), PlayerDistance={playerDist}m" }
            },
            max_tokens = 150
        };

        requestMessage.Content = JsonContent.Create(payload);

        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var response = await client.SendAsync(requestMessage);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($" -> [200 OK] Latency: {stopwatch.ElapsedMilliseconds}ms | Response: {Truncate(responseBody, 120)}");
                Console.ResetColor();
            }
            else if ((int)response.StatusCode == 429)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.BackgroundColor = ConsoleColor.DarkRed;
                Console.WriteLine($" -> [RATE LIMITED 429] TOO MANY REQUESTS! Gateway protection active. Limit enforced.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($" -> [HTTP {(int)response.StatusCode}] {response.ReasonPhrase}");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($" -> [CONNECTION ERROR] {ex.Message}");
            Console.ResetColor();
        }
    }

    private static async Task GetConversationContextAsync(HttpClient client)
    {
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("\n ---> [CHECK CONTEXT] Fetching recent NPC history via GET /conversations?top=3 ...");

        try
        {
            var response = await client.GetAsync("/conversations?top=3");
            if (response.IsSuccessStatusCode)
            {
                var conversations = await response.Content.ReadFromJsonAsync<JsonElement>();
                Console.WriteLine($" ---> [CONTEXT RECEIVED] Last 3 Actions in DB:");
                if (conversations.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in conversations.EnumerateArray())
                    {
                        var role = item.TryGetProperty("role", out var r) ? r.GetString() : "unknown";
                        var content = item.TryGetProperty("content", out var c) ? c.GetString() : "";
                        var time = item.TryGetProperty("createdAt", out var t) ? t.GetDateTimeOffset().ToString("HH:mm:ss") : "";
                        Console.WriteLine($"      • [{time}] ({role}): {Truncate(content, 70)}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($" ---> [CONTEXT WARN] Failed to fetch history: {ex.Message}");
        }

        Console.ResetColor();
    }

    private static void ListenForKeyPresses()
    {
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.S)
            {
                _stressTestMode = !_stressTestMode;
                Console.ForegroundColor = _stressTestMode ? ConsoleColor.Red : ConsoleColor.Green;
                Console.WriteLine($"\n>>> STRESS TEST MODE TOGGLED: {(_stressTestMode ? "ON (100ms Interval - Rapid Fire!)" : "OFF (2000ms Normal Interval)")} <<<\n");
                Console.ResetColor();
            }
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
    }
}
