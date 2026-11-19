using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodayDo.Server.Data;
using TodayDo.Server.Models;
using TodayDo.Server.Services;

namespace TodayDo.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WeatherController : ControllerBase
    {
        #region 01 DB와 기상청 조회 서비스
        private readonly AppDbContext db;
        private readonly KmaForecastService forecasts;

        #endregion

        #region 02 컨트롤러 생성
        public WeatherController(
            AppDbContext db,
            KmaForecastService forecasts)
        {
            this.db = db;
            this.forecasts = forecasts;

        }
        #endregion

        #region 03 일정별 기상청 날씨 조회 API
        // 해당 회원의 일정에 필요한 날씨 조회
        [HttpGet("todos/{memberId:int}/{todoId:int}")]
        public async Task<IActionResult> GetTodoWeather(
            int memberId,
            int todoId,
            CancellationToken cancellationToken)
        {
            var todo = await db.Todos
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.TodoID == todoId && t.MemberID == memberId,
                    cancellationToken);

            if (todo == null)
            {
                return NotFound(new
                {
                    message = "해당 회원의 일정을 찾을 수 없습니다."
                });
            }

            if (todo.LocationID == null)
            {
                return ForecastStatus(CreateForecast(todo), "location_required",
                    "일정의 활동 지역을 먼저 선택해 주세요.");
            }

            var location = await db.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.LocationID == todo.LocationID,
                    cancellationToken);

            if (location == null || location.Nx <= 0 || location.Ny <= 0)
            {
                return BadRequest(new
                {
                    message = "일정의 지역과 격자 좌표를 확인해 주세요."
                });
            }

            DateTime end = todo.DueDate ?? todo.TodoDate;
            JsonObject forecast = CreateForecast(todo, location.LocationName);

            if (end < todo.TodoDate)
            {
                return BadRequest(new
                {
                    message = "일정 종료 시간이 시작 시간보다 빠릅니다."
                });
            }

            // 현재 프로젝트의 일정 시간은 한국 시간으로 취급
            DateTime now = DateTimeOffset.UtcNow
                .ToOffset(TimeSpan.FromHours(9))
                .DateTime;

            DateTime firstHour = FloorHour(todo.TodoDate);

            // 09~11시 일정이면 09시, 10시 예보를 확인합니다.
            DateTime lastHour = FloorHour(
                end > todo.TodoDate ? end.AddTicks(-1) : end);

            DateTime baseTime = GetBaseTime(now);

            DateTime forecastLimit = baseTime.Date
                .AddDays(baseTime.Hour >= 17 ? 5 : 4)
                .AddHours(-1);

            if (firstHour.Date < now.Date)
            {
                return ForecastStatus(forecast, "past", "과거 활동의 날씨는 저장된 예보 이력이 필요합니다.");
            }

            if (firstHour > forecastLimit)
            {
                return ForecastStatus(forecast, "pending", "아직 일정 날짜의 예보가 제공되지 않습니다.");
            }

            DateTime scanEnd =
                lastHour < forecastLimit ? lastHour : forecastLimit;

            try
            {
                JsonArray weather = await forecasts.ReadAsync(
                    location.Nx,
                    location.Ny,
                    firstHour,
                    scanEnd,
                    baseTime,
                    cancellationToken);

                int expectedHours =
                    (int)(lastHour - firstHour).TotalHours + 1;

                int completeHours = weather.Count(KmaForecastService.HasAllFields);

                string status = weather.Count == 0
                    ? "pending"
                    : completeHours == expectedHours
                        ? "ready"
                        : "partial";

                forecast["status"] = status;
                forecast["forecastBase"] = baseTime.ToString("s", CultureInfo.InvariantCulture);
                forecast["expectedHours"] = expectedHours;
                forecast["completeHours"] = completeHours;
                forecast["displayWeather"] = WeatherDisplayFormatter.ReadableWeather(forecast, weather);
                forecast["weather"] = weather;
                return Ok(forecast);
            }
            catch (TaskCanceledException)
                when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(504, new
                {
                    message = "기상청 응답 시간이 초과되었습니다."
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new
                {
                    message = "기상청 서버와 통신하지 못했습니다."
                });
            }
            catch (JsonException)
            {
                return StatusCode(502, new
                {
                    message = "기상청 JSON 응답을 읽지 못했습니다. 인증키를 확인해 주세요."
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, new
                {
                    message = ex.Message
                });
            }
        }
        #endregion

        #region 04 날씨 기반 AI 조언 API
        [HttpPost("todos/{memberId:int}/{todoId:int}/advice")]
        public async Task<IActionResult> GetTodoAdvice(int memberId, int todoId, [FromServices] AiAdviceService ai, CancellationToken cancellationToken)
        {
            try
            {
                // 저장된 일정과 실제 기상청 예보를 다시 확인합니다.
                IActionResult weatherResult =
                    await GetTodoWeather(memberId, todoId, cancellationToken);

                if (weatherResult is not OkObjectResult ok)
                    return weatherResult;

                JsonObject forecast = ok.Value as JsonObject
                    ?? throw new InvalidOperationException(
                        "일정의 날씨 정보를 읽지 못했습니다.");

                string advice =
                    await ai.SendMessage(forecast, cancellationToken);

                return Ok(new
                {
                    message = advice,
                    forecast,
                    model = ai.Model,
                    forecastBase = forecast["forecastBase"]?.ToString(),
                    checkedAt = DateTimeOffset.UtcNow
                        .ToOffset(TimeSpan.FromHours(9))
                });
            }
            catch (AiRequestException ex)
            {
                return StatusCode(502, new
                {
                    message = $"학교 AI 요청에 실패했습니다. HTTP {ex.Status}. 모델 권한과 요청 주소를 확인해 주세요."
                });
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(504, new
                {
                    message = "AI 응답 시간이 초과되었습니다."
                });
            }
            catch (HttpRequestException)
            {
                return StatusCode(502, new
                {
                    message = "외부 서버와 통신하지 못했습니다."
                });
            }
            catch (JsonException)
            {
                return StatusCode(502, new
                {
                    message = "일정 또는 날씨 응답을 읽지 못했습니다."
                });
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(502, new
                {
                    message = ex.Message
                });
            }
        }
        #endregion

        #region 05 공통 응답과 예보 시각 계산
        private static JsonObject CreateForecast(Todo todo, string? locationName = null) => new()
        {
            ["todoId"] = todo.TodoID,
            ["title"] = todo.Title,
            ["content"] = todo.Content,
            ["start"] = todo.TodoDate.ToString("O", CultureInfo.InvariantCulture),
            ["end"] = todo.DueDate?.ToString("O", CultureInfo.InvariantCulture),
            ["locationName"] = locationName,
            ["checkedAt"] = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(9)).ToString("o", CultureInfo.InvariantCulture)
        };

        private OkObjectResult ForecastStatus(JsonObject forecast, string status, string message)
        {
            forecast["status"] = status;
            forecast["message"] = message;
            return Ok(forecast);
        }

        // 분·초를 제외하고 정각으로 맞춥니다.
        private static DateTime FloorHour(DateTime time)
        {
            return time.Date.AddHours(time.Hour);
        }

        // 제공 시각을 고려해 사용할 발표 시각을 구합니다.
        private static DateTime GetBaseTime(DateTime now)
        {
            DateTime available = now.AddMinutes(-10);

            foreach (int hour in new[] { 23, 20, 17, 14, 11, 8, 5, 2 })
            {
                if (available.Hour >= hour)
                    return available.Date.AddHours(hour);
            }

            return available.Date.AddDays(-1).AddHours(23);
        }

        #endregion
    }
}
