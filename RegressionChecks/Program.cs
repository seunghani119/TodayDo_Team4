using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TodayDo.Server.Controllers;
using TodayDo.Server.Data;
using TodayDo.Server.Models;
using TODOList.Client.Services;
using TodayDo.Server.Services;
using System.Text.Json.Nodes;
using System.Text.Json;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}

// FindAsync만 대체하여 실제 DB를 읽거나 수정하지 않습니다.
using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().Options);
db.Todos = new TodoSet();
var todos = new TodoController(db);
Check((await todos.Update(2, 1, new Todo())).Result is ObjectResult { StatusCode: 403 }, "다른 회원의 일정 수정: 403");
Check(await todos.Delete(2, 1) is ObjectResult { StatusCode: 403 }, "다른 회원의 일정 삭제: 403");

var checkResponse = typeof(ApiService).GetMethod("CheckResponseAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
async Task CheckError(string json, string expected)
{
    using var response = new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(json) };
    try
    {
        await (Task)checkResponse.Invoke(null, new object[] { response, CancellationToken.None })!;
        throw new Exception("오류 응답이 성공으로 처리됨");
    }
    catch (InvalidOperationException ex)
    {
        Check(ex.Message.Contains(expected), "오류 본문 처리: " + expected);
    }
}
await CheckError("{\"message\":\"장소를 확인해 주세요\"}", "장소를 확인해 주세요");
await CheckError("{\"title\":\"입력 오류\"}", "입력 오류");
await CheckError("<html>error</html>", "HTTP 400");
await CheckError("", "HTTP 400");
using (var canceled = new CancellationTokenSource())
using (var response = new HttpResponseMessage(HttpStatusCode.BadRequest))
{
    canceled.Cancel();
    try
    {
        await (Task)checkResponse.Invoke(null, new object[] { response, canceled.Token })!;
        Check(false, "오류 응답을 읽기 전에 취소 반영");
    }
    catch (OperationCanceledException) { Check(true, "오류 응답을 읽기 전에 취소 반영"); }
}
using (var response = new HttpResponseMessage(HttpStatusCode.NoContent))
{
    await (Task)checkResponse.Invoke(null, new object[] { response, CancellationToken.None })!;
    Check(true, "204 성공 응답은 본문 없이 처리");
}

var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["WeatherApi:ServiceKey"] = "test-key"
}).Build();
var forecasts = new KmaForecastService(new HttpClient(new FakeHandler()), config);
var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton(db);
services.AddHttpClient<KmaForecastService>();
services.AddTransient<WeatherController>();
using (var provider = services.BuildServiceProvider())
    Check(provider.GetRequiredService<WeatherController>() != null, "컨트롤러와 기상청 서비스 생성 확인");

try
{
    await forecasts.ReadAsync(60, 127, new DateTime(2026, 11, 12, 9, 0, 0),
        new DateTime(2026, 11, 12, 10, 0, 0), new DateTime(2026, 11, 12, 8, 0, 0), CancellationToken.None);
    throw new Exception("잘못된 예보 배열이 허용됨");
}
catch (InvalidOperationException ex)
{
    Check(ex.Message.Contains("목록 형식"), "잘못된 기상청 배열을 처리 가능한 예외로 변환");
}

// 페이지 경계에 나뉜 항목이 같은 시간의 예보로 합쳐지는지 확인합니다.
var pagingHandler = new PagingKmaHandler();
var paging = new KmaForecastService(new HttpClient(pagingHandler), config);
var hourly = await paging.ReadAsync(60, 127, new DateTime(2026, 11, 18, 10, 0, 0),
    new DateTime(2026, 11, 18, 10, 0, 0), new DateTime(2026, 11, 18, 8, 0, 0), CancellationToken.None);
Check(pagingHandler.Calls == 2, "기상청 다음 페이지 조회 유지");
Check(hourly.Count == 1 && hourly[0]?["temperature"]?.ToString() == "12"
    && hourly[0]?["rainProbability"]?.ToString() == "80", "시간별 항목 묶기와 범위 밖 예보 제외");

var todoMetadata = new Todo { TodoID = 42, Title = "원문 제목", Content = "원문 내용", TodoDate = new DateTime(2026, 11, 18, 10, 0, 0).AddMilliseconds(123) };
var metadata = (JsonObject)typeof(WeatherController).GetMethod("CreateForecast", BindingFlags.NonPublic | BindingFlags.Static)!
    .Invoke(null, new object?[] { todoMetadata, "원문 장소" })!;
Check(metadata["todoId"]?.GetValue<int>() == 42 && metadata["title"]?.ToString() == "원문 제목"
    && metadata["checkedAt"] != null && metadata["end"] == null, "예보 상태 공통 원문과 확인 시각 유지");
Check(DateTime.TryParse(metadata["start"]?.ToString(), out DateTime startTime) && startTime == todoMetadata.TodoDate
    && DateTimeOffset.TryParse(metadata["checkedAt"]?.ToString(), out _), "공통 날짜를 손실 없는 ISO 문자열로 유지");
JsonObject Forecast(string title, string status = "ready") => new()
{
    ["status"] = status, ["title"] = title, ["content"] = "사용자가 직접 입력한 일정 내용",
    ["locationName"] = "테스트 장소", ["forecastBase"] = "2026-11-18T08:00:00",
    ["weather"] = new JsonArray(new JsonObject
    {
        ["forecastTime"] = "2026-11-18T10:00:00", ["temperature"] = "12",
        ["rainProbability"] = "80", ["precipitationType"] = "1", ["precipitation"] = "2", ["sky"] = "3"
    })
};
var scenarios = new[]
{
    (Name: "비 오는 날 등산", Data: Forecast("등산")),
    (Name: "비 오는 날 영화관", Data: Forecast("영화관 관람"))
};
var readable = WeatherDisplayFormatter.ReadableWeather(scenarios[0].Data, (JsonArray)scenarios[0].Data["weather"]!);
Check(readable[0]?["sky"]?.ToString() == "구름 많음" && readable[0]?["precipitationType"]?.ToString() == "비", "시간별 날씨 표시 변환 유지");
var extendedForecast = Forecast("연장 예보");
extendedForecast["weather"]![0]!["forecastTime"] = "2026-11-21T10:00:00";
var extended = WeatherDisplayFormatter.ReadableWeather(extendedForecast, (JsonArray)extendedForecast["weather"]!);
Check(extended[0]?["precipitation"]?.ToString() == "보통 비", "연장 예보 강수량 코드 해석 유지");

// 외부 API 대신 응답을 대체하여 실제 전송 자료와 반환 답변을 검사합니다.
var aiConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
{
    ["VIRTUAL_KEY"] = "test-key", ["SCHOOL_LLM_BASE_URL"] = "https://example.invalid", ["AiApi:Model"] = "claude-sonnet-4-5"
}).Build();
AiAdviceService TestAi(HttpClient http) => (AiAdviceService)typeof(AiAdviceService)
    .GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(IConfiguration), typeof(HttpClient) }, null)!
    .Invoke(new object[] { aiConfig, http });
var aiHandler = new FakeAiHandler("""{"choices":[{"finish_reason":"stop","message":{"content":"기온은 12℃입니다. 입력한 활동에 맞춰 준비해 주세요."}}]}""");
using var aiHttp = new HttpClient(aiHandler);
var testAi = TestAi(aiHttp);
foreach (string title in new[] { "미술관 관람", "공원 걷기", "친구 만나기" })
{
    string answer = await testAi.SendMessage(Forecast(title));
    Check(answer.StartsWith("기온은 12℃"), title + ": AI 답변을 숫자 필터 없이 반환");
    var payload = aiHandler.LastRequest!;
    Check(payload["system"] != null && payload["model"]?.ToString() == "claude-sonnet-4-5", "Claude 요청 형식 유지");
    string message = payload["messages"]![0]!["content"]!.ToString();
    var input = JsonNode.Parse(message[(message.LastIndexOf("\n{") + 1)..])!;
    Check(input["title"]?.ToString() == title && input["content"]?.ToString() == "사용자가 직접 입력한 일정 내용", "일정 원문 전달: " + title);
    Check(input["context"] == null && input["weather"]?[0]?["temperatureC"]?.ToString() == "12", "사전 판단 없이 실제 예보 전달");
}
foreach (string status in new[] { "partial", "pending", "past", "location_required" })
{
    var forecast = Forecast("사용자 일정", status);
    forecast["message"] = "예보가 일부만 있거나 제공되지 않습니다.";
    if (status != "partial") forecast.Remove("weather");
    await testAi.SendMessage(forecast);
    string message = aiHandler.LastRequest!["messages"]![0]!["content"]!.ToString();
    var input = JsonNode.Parse(message[(message.LastIndexOf("\n{") + 1)..])!;
    Check(input["weatherStatus"]?.ToString() == status && input["weatherMessage"]?.ToString() == forecast["message"]?.ToString(), "예보 제한 상태를 AI에 전달: " + status);
}
var preparedForecast = Forecast("서버 예보 재사용");
preparedForecast["displayWeather"] = WeatherDisplayFormatter.ReadableWeather(preparedForecast, (JsonArray)preparedForecast["weather"]!);
string beforeRequest = preparedForecast.ToJsonString();
await testAi.SendMessage(preparedForecast);
Check(preparedForecast.ToJsonString() == beforeRequest, "AI 요청이 원본 예보 노드를 변경하지 않음");
using (var invalidHttp = new HttpClient(new FakeAiHandler("""{"choices":[{"finish_reason":"stop","message":{"content":""}}]}""")))
{
    try { await TestAi(invalidHttp).SendMessage(Forecast("산책")); Check(false, "빈 AI 답변 거부"); }
    catch (InvalidOperationException) { Check(true, "빈 AI 답변 거부"); }
}
using (var errorHttp = new HttpClient(new FakeAiHandler("{}", HttpStatusCode.TooManyRequests)))
{
    try { await TestAi(errorHttp).SendMessage(Forecast("산책")); Check(false, "AI 통신 실패를 기본 안내로 숨기지 않음"); }
    catch (AiRequestException ex) { Check(ex.Status == 429, "AI 오류 상태 유지"); }
}
Console.WriteLine($"검사 {passed}개 통과");
if (args.Contains("--live-ai"))
{
    string serverRoot = Path.GetFullPath("1103_4조TODOList.Server");
    string envFile = Path.Combine(serverRoot, ".env");
    if (File.Exists(envFile)) DotNetEnv.Env.NoClobber().Load(envFile);
    var liveConfig = new ConfigurationBuilder().SetBasePath(serverRoot)
        .AddJsonFile("appsettings.json").AddEnvironmentVariables().Build();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(50));
    try
    {
        var ai = new AiAdviceService(liveConfig);
        Console.WriteLine("LIVE MODEL: " + ai.Model);
        foreach (var scenario in scenarios)
        {
            string answer = await ai.SendMessage(scenario.Data, timeout.Token);
            Console.WriteLine("LIVE " + scenario.Name + ": " + answer);

        }
    }
    catch (AiRequestException ex)
    {
        Console.WriteLine($"LIVE FAILED: HTTP {ex.Status} (모델·학교 API 권한 확인 필요)");
        string diagnostic = ex.Message;
        foreach (string setting in new[] { "VIRTUAL_KEY", "SCHOOL_LLM_BASE_URL", "WeatherApi:ServiceKey" })
        {
            string? secret = liveConfig[setting];
            if (!string.IsNullOrEmpty(secret)) diagnostic = diagnostic.Replace(secret, "[redacted]");
        }
        Console.WriteLine(diagnostic.Length > 1500 ? diagnostic[..1500] : diagnostic);
        Environment.ExitCode = 2;
    }
    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException)
    {
        Console.WriteLine("LIVE FAILED: " + ex.GetType().Name);
        Environment.ExitCode = 2;
    }
}

sealed class TodoSet : DbSet<Todo>
{
    public override Microsoft.EntityFrameworkCore.Metadata.IEntityType EntityType => throw new NotSupportedException();
    public override ValueTask<Todo?> FindAsync(params object?[]? keys) =>
        ValueTask.FromResult<Todo?>(new Todo { TodoID = 1, MemberID = 1 });
}
sealed class FakeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"response":{"header":{"resultCode":"00"},"body":{"totalCount":1,"items":{"item":{}}}}}""")
        });
}

sealed class FakeAiHandler(string response, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public JsonObject? LastRequest { get; private set; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        LastRequest = JsonNode.Parse(await request.Content!.ReadAsStringAsync(token)) as JsonObject;
        return new HttpResponseMessage(status) { Content = new StringContent(response) };
    }
}

sealed class PagingKmaHandler : HttpMessageHandler
{
    public int Calls { get; private set; }
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
    {
        Calls++;
        var fields = Calls == 1 ? new[] { ("TMP", "12"), ("TMP", "99") }
            : new[] { ("POP", "80"), ("PCP", "2"), ("SKY", "3"), ("PTY", "1") };
        var items = fields.Select((field, index) => new
        {
            category = field.Item1, fcstValue = field.Item2, fcstDate = "20261118",
            fcstTime = Calls == 1 && index == 1 ? "1100" : "1000"
        });
        string json = JsonSerializer.Serialize(new { response = new { header = new { resultCode = "00" }, body = new { totalCount = 101, items = new { item = items } } } });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
