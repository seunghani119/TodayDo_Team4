using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using TODOList.Client.Models;
using TODOList.Client.Services;

namespace TODOList.Client
{
    public partial class MainWindow
    {
        #region 01 조회 취소와 요청 상태
        private CancellationTokenSource? weatherCancellation;

        private void CancelWeatherRequest()
        {
            weatherCancellation?.Cancel();
        }
        #endregion

        #region 02 선택 일정의 날씨 조회
        // 선택한 일정의 날씨를 조회하고 화면에 표시합니다.
        private async Task LoadSelectedWeatherAsync(TodoInfo todo)
        {
            var request = new CancellationTokenSource();

            weatherCancellation = request;

            todo.WeatherSummary = "날씨 확인 중…";

            WeatherUpdateText.Text = $"'{todo.Title}'의 날씨를 확인하고 있습니다.";

            try
            {
                JsonObject result = await ApiService.GetTodoWeatherAsync(CurrentMemberId, todo.TodoID, request.Token);

                request.Token.ThrowIfCancellationRequested();

                if (isClosed || !ReferenceEquals(TodoGrid.SelectedItem, todo))
                    return;

                var display = FormatWeather(result);
                
                if (!RememberWeather(todo, result))     //역할: 자동 조회뿐 아니라, 일정을 선택해서 다시 조회했을 때도 이전 날씨와 비교
                    return;

                todo.WeatherSummary = display.Summary;

                WeatherUpdateText.Text = $"[{todo.Title}]\n{display.Details}";
            }
            catch (OperationCanceledException)
                when (request.IsCancellationRequested)
            {
                if (!ReferenceEquals(TodoGrid.SelectedItem, todo))
                    todo.WeatherSummary = "날씨 확인 전";
            }
            catch (Exception ex) when (
                ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
            {
                if (isClosed || request.IsCancellationRequested || !ReferenceEquals(TodoGrid.SelectedItem, todo))
                {
                    return;
                }

                todo.WeatherSummary = "날씨 조회 실패";

                WeatherUpdateText.Text = "다른 일정을 선택한 뒤 다시 선택하면 재조회합니다.";

                string message = ex is OperationCanceledException
                    ? "날씨 조회 시간이 초과되었습니다. 잠시 후 다시 시도해 주세요."
                    : ex is HttpRequestException
                        ? "날씨 서버와 통신하지 못했습니다. 서버 실행 상태를 확인해 주세요."
                        : ex is JsonException ? "날씨 응답을 읽지 못했습니다. 서버 응답 형식을 확인해 주세요." : ex.Message;

                SetStatus(message, true);
            }
            finally
            {
                if (ReferenceEquals(weatherCancellation, request))
                    weatherCancellation = null;

                request.Dispose();
            }
        }
        #endregion

        #region 03 목록 요약과 상세 문구 생성
        // 서버 응답을 목록 문구와 상세 문구로 변환합니다.
        private static (string Summary, string Details) FormatWeather(JsonObject result)
        {
            string status = result["status"]?.ToString() ?? "";

            string label = status switch
            {
                "pending" => "예보 대기",
                "past" => "과거 예보 이력 필요",
                "location_required" => "지역 선택 필요",
                "ready" or "partial" => "",

                _ => throw new InvalidOperationException("알 수 없는 날씨 응답 상태입니다.")
            };

            if (label != "")
            {
                return (label, result["message"]?.ToString() ?? label);
            }

            if (result["weather"] is not JsonArray rows || rows.Count == 0)
            {
                throw new InvalidOperationException("날씨 응답에 시간별 예보가 없습니다.");
            }

            DateTime.TryParse(result["forecastBase"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime baseTime);

            var lines = new List<string>();
            var summaries = new List<string>();

            foreach (JsonNode? row in rows)
            {
                if (row is not JsonObject item)
                {
                    throw new InvalidOperationException("시간별 날씨 형식이 올바르지 않습니다.");
                }

                bool hasTime = DateTime.TryParse(item["forecastTime"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time);

                string timeLabel = hasTime ? time.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture) : "예보 시각 정보 없음";

                // 단기예보 연장 구간의 강수량은 정성 코드일 수 있습니다.
                bool extended = baseTime != default && hasTime && time.Date >= baseTime.Date.AddDays(baseTime.Hour >= 17 ? 4 : 3);

                string sky = SkyText(item["sky"]?.ToString());

                string type = PrecipitationTypeText(item["precipitationType"]?.ToString());

                string temperature = NumberWithUnit(item["temperature"]?.ToString(), "℃");

                string probability = NumberWithUnit(item["rainProbability"]?.ToString(), "%");

                string rain = RainText(item["precipitation"]?.ToString(), extended);

                // 일정 목록에는 핵심 정보만 표시합니다.
                summaries.Add($"{type} · {temperature}\n" + $"강수확률 {probability}");

                // 하단에는 시간별 정보를 구분해서 표시합니다.
                lines.Add($"[{timeLabel}]\n" + $"날씨  {sky} / {type}    기온  {temperature}\n" + $"강수확률  {probability}    강수량  {rain}");
            }

            string summary = rows.Count == 1 ? summaries[0] : $"시간별 예보 {rows.Count}개\n하단 상세 확인";

            string heading = status == "partial" ? "일부 시간·항목만 제공됩니다. · 단기예보" : "예보 확인 완료 · 단기예보";

            if (status == "partial")
                summary = "일부 예보\n" + summary;

            if (baseTime != default)
            {
                heading += " · 발표 " + baseTime.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
            }

            if (DateTimeOffset.TryParse(result["checkedAt"]?.ToString(), out DateTimeOffset checkedAt))
            {
                heading += " · 확인 " + checkedAt.ToOffset(TimeSpan.FromHours(9)).ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
            }

            return (summary, heading + "\n\n" + string.Join("\n\n", lines));
        }
        #endregion

        #region 04 날씨 코드와 단위 표시
        private static string SkyText(string? value)
        {
            return value switch
            {
                "1" => "맑음",
                "3" => "구름 많음",
                "4" => "흐림",
                _ => "하늘 정보 없음"
            };
        }

        private static string PrecipitationTypeText(string? value)
        {
            return value switch
            {
                "0" => "강수 없음",
                "1" => "비",
                "2" => "비/눈",
                "3" => "눈",
                "4" => "소나기",
                "5" => "빗방울",
                "6" => "빗방울/눈날림",
                "7" => "눈날림",
                _ => "강수 형태 정보 없음"
            };
        }

        private static string NumberWithUnit(string? value, string unit)
        {
            bool valid = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number);

            if (!valid || !double.IsFinite(number) || number >= 900 || number <= -900)
            {
                return "정보 없음";
            }

            return number.ToString("0.#", CultureInfo.InvariantCulture) + unit;
        }

        private static string RainText(string? value, bool extended)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "정보 없음";

            value = value.Trim();

            if (value is "-" or "null" or "0" or "강수없음" or "강수 없음")
            {
                return "강수 없음";
            }

            if (value.Contains("mm", StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            if (extended)
            {
                return value switch
                {
                    "1" => "약한 비",
                    "2" => "보통 비",
                    "3" => "강한 비",
                    _ => "강수량 정보 확인 필요"
                };
            }

            bool valid = double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double rain);

            if (!valid || !double.IsFinite(rain) || rain < 0 || rain >= 900)
            {
                return "정보 없음";
            }

            if (rain == 0)
                return "강수 없음";

            if (rain < 1)
                return "1mm 미만";

            if (rain < 30)
            {
                return rain.ToString("0.#", CultureInfo.InvariantCulture) + "mm";
            }

            return rain < 50 ? "30~50mm" : "50mm 이상";
        }
        #endregion
    }
}
