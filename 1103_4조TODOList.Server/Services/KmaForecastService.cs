using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TodayDo.Server.Services
{
    // 기상청 통신과 응답 정리만 담당합니다. 일정과 AI 판단은 하지 않습니다.
    public sealed class KmaForecastService(HttpClient client, IConfiguration configuration)
    {
        #region 01 기상청 항목 매핑
        // 기상청 항목명 → 우리 프로그램에서 사용할 이름
        private static readonly Dictionary<string, string> WeatherFields = new()
        {
            ["TMP"] = "temperature",
            ["POP"] = "rainProbability",
            ["PCP"] = "precipitation",
            ["SKY"] = "sky",
            ["PTY"] = "precipitationType"
        };
        #endregion

        #region 02 예보 조회와 시간별 항목 정리
        internal static bool HasAllFields(JsonNode? row) =>
            WeatherFields.Values.All(name => row?[name] != null);

        // 기상청 응답에서 필요한 시간과 항목만 추립니다.
        public async Task<JsonArray> ReadAsync(
            int nx,
            int ny,
            DateTime firstHour,
            DateTime lastHour,
            DateTime baseTime,
            CancellationToken cancellationToken)
        {
            string key =
                configuration["WeatherApi:ServiceKey"]?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException(
                    "appsettings.json의 WeatherApi:ServiceKey를 설정해 주세요.");
            }

            var selected = new Dictionary<DateTime, JsonObject>();

            int expected =
                (int)(lastHour - firstHour).TotalHours + 1;

            int page = 1;
            const int pageSize = 100;

            while (true)
            {
                string url =
                    "https://apis.data.go.kr/1360000/" +
                    "VilageFcstInfoService_2.0/getVilageFcst" +
                    $"?serviceKey={Uri.EscapeDataString(key)}" +
                    $"&dataType=JSON&pageNo={page}&numOfRows={pageSize}" +
                    $"&base_date={baseTime:yyyyMMdd}" +
                    $"&base_time={baseTime:HHmm}" +
                    $"&nx={nx}&ny={ny}";

                using var response =
                    await client.GetAsync(url, cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    throw new InvalidOperationException(
                        $"기상청 요청 실패: HTTP {(int)response.StatusCode}");
                }

                string json = await response.Content
                    .ReadAsStringAsync(cancellationToken);

                using var document = JsonDocument.Parse(json);

                JsonElement api =
                    ReadPart(document.RootElement, "response");

                string resultCode =
                    ReadText(ReadPart(api, "header"), "resultCode");

                if (resultCode != "00" && resultCode != "0")
                {
                    throw new InvalidOperationException(
                        $"기상청 결과 코드: {resultCode}");
                }

                JsonElement body = ReadPart(api, "body");

                if (!int.TryParse(
                        ReadText(body, "totalCount"), out int total)
                    || total < 0)
                {
                    throw new InvalidOperationException(
                        "기상청 전체 결과 수를 읽지 못했습니다.");
                }

                if (total == 0)
                    break;

                JsonElement items =
                    ReadPart(ReadPart(body, "items"), "item");

                if (items.ValueKind != JsonValueKind.Array)
                    throw new InvalidOperationException("기상청 시간별 예보 목록 형식이 올바르지 않습니다.");

                foreach (JsonElement item in items.EnumerateArray())
                {
                    string category = ReadText(item, "category");

                    if (!WeatherFields.TryGetValue(
                        category, out string? field))
                    {
                        continue;
                    }

                    string timeText =
                        ReadText(item, "fcstDate") +
                        ReadText(item, "fcstTime");

                    bool validTime = DateTime.TryParseExact(
                        timeText,
                        "yyyyMMddHHmm",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime time);

                    if (!validTime
                        || time < firstHour
                        || time > lastHour)
                    {
                        continue;
                    }

                    if (!selected.TryGetValue(
                        time, out JsonObject? row))
                    {
                        row = new JsonObject
                        {
                            ["forecastTime"] =
                                time.ToString("yyyy-MM-ddTHH:mm:ss")
                        };

                        foreach (string name in WeatherFields.Values)
                            row[name] = null;

                        selected[time] = row;
                    }

                    string value = ReadText(item, "fcstValue");

                    bool missing =
                        value == "" ||
                        (value == "null" && category != "PCP") ||
                        (
                            double.TryParse(
                                value,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out double number)
                            && (number >= 900 || number <= -900)
                        );

                    row[field] =
                        missing ? null : JsonValue.Create(value);
                }

                bool complete =
                    selected.Count == expected &&
                    selected.Values.All(HasAllFields);

                if (complete
                    || page * pageSize >= total
                    || items.GetArrayLength() == 0)
                {
                    break;
                }

                page++;
            }

            var result = new JsonArray();

            foreach (var pair in selected.OrderBy(pair => pair.Key))
                result.Add(pair.Value);

            return result;
        }
        #endregion

        #region 03 JSON 응답 읽기
        private static string ReadText(
            JsonElement element,
            string name)
        {
            return ReadPart(element, name).ToString();
        }

        private static JsonElement ReadPart(
            JsonElement element,
            string name)
        {
            if (element.ValueKind != JsonValueKind.Object
                || !element.TryGetProperty(
                    name, out JsonElement value))
            {
                throw new InvalidOperationException(
                    $"기상청 응답에 {name} 항목이 없습니다.");
            }

            return value;
        }
        #endregion
    }
}
