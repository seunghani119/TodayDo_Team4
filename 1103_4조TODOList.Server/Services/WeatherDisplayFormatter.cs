using System.Globalization;
using System.Text.Json.Nodes;

namespace TodayDo.Server.Services
{
    // 기상청 코드와 단위를 화면 및 AI 입력용 문구로 변환
    public static class WeatherDisplayFormatter
    {
        public static JsonArray ReadableWeather(JsonObject source, JsonArray rows)
        {
            var result = new JsonArray();
            DateTime.TryParse(source["forecastBase"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime baseTime);
            foreach (JsonObject row in rows.OfType<JsonObject>())
            {
                DateTime.TryParse(row["forecastTime"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time);
                bool extended = baseTime != default && time.Date >= baseTime.Date.AddDays(baseTime.Hour >= 17 ? 4 : 3);
                string rain = row["precipitation"]?.ToString()?.Trim() ?? "";
                if (rain.Length == 0) rain = "정보 없음";
                else if (rain is "null" or "-" or "0" or "강수없음" or "강수 없음") rain = "강수 없음";
                else if (extended && !rain.Contains("mm", StringComparison.OrdinalIgnoreCase))
                    rain = rain switch { "1" => "약한 비", "2" => "보통 비", "3" => "강한 비", _ => "정보 없음" };
                else if (double.TryParse(rain, NumberStyles.Float, CultureInfo.InvariantCulture, out double amount))
                    rain = amount >= 0 && amount < 900 ? amount.ToString("0.#", CultureInfo.InvariantCulture) + "mm" : "정보 없음";

                result.Add(new JsonObject
                {
                    ["time"] = row["forecastTime"]?.ToString(),
                    ["temperatureC"] = row["temperature"]?.ToString(),
                    ["rainProbabilityPercent"] = row["rainProbability"]?.ToString(),
                    ["precipitation"] = rain,
                    ["sky"] = row["sky"]?.ToString() switch { "1" => "맑음", "3" => "구름 많음", "4" => "흐림", _ => "정보 없음" },
                    ["precipitationType"] = row["precipitationType"]?.ToString() switch
                    {
                        "0" => "강수 없음", "1" => "비", "2" => "비/눈", "3" => "눈", "4" => "소나기", _ => "정보 없음"
                    }
                });
            }
            return result;
        }
    }
}
