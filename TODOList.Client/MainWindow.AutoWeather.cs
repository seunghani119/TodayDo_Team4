using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows.Threading;
using TODOList.Client.Models;
using TODOList.Client.Services;

namespace TODOList.Client
{
    public partial class MainWindow
    {
        #region 01 자동 갱신 상태
        // ***********날씨를 30분마다 자동 조회***********
        private readonly DispatcherTimer weatherTimer = new()
        {
            Interval = TimeSpan.FromMinutes(30)
        };

        // 창을 닫을 때 진행 중인 자동 조회를 중단합니다.
        private readonly CancellationTokenSource autoWeatherCancellation = new();

        // 일정별로 마지막에 확인한 날씨를 기억합니다.
        private readonly Dictionary<string, JsonObject> previousWeather = new();

        // 화면에 표시할 최근 변경 알림입니다. 순서대로 기억
        private readonly Queue<string> weatherNotices = new();

        // 자동 조회가 중복 실행되는 것을 막습니다.
        private bool isRefreshingWeather;
        #endregion

        #region 02 타이머 시작 중지와 실행
        private void StartAutoWeather()
        {
            if (isClosed)
                return;

            weatherTimer.Tick += WeatherTimer_Tick;
            weatherTimer.Start();
        }

        private void StopAutoWeather()
        {
            weatherTimer.Stop();
            weatherTimer.Tick -= WeatherTimer_Tick;

            autoWeatherCancellation.Cancel();

            previousWeather.Clear();
            weatherNotices.Clear();
        }

        private async void WeatherTimer_Tick(object? sender, EventArgs e)
        {
            await RefreshWeatherAsync();
        }
        #endregion

        #region 03 일정 식별과 자동 날씨 조회
        // 일정의 지역이나 시간이 바뀌면 새 비교 기준을 사용합니다.
        private static string WeatherKey(TodoInfo todo)
        {
            return $"{todo.TodoID}|{todo.LocationID}|{todo.TodoDate.Ticks}|{todo.DueDate?.Ticks}";
        }
        // 대상 일정의 날씨를 하나씩 받아 목록과 상세 날씨를 갱신
        private async Task RefreshWeatherAsync()
        {
            if (isClosed || isBusy || isRefreshingWeather)
                return;

            isRefreshingWeather = true;

            try
            {
                int memberId = CurrentMemberId;

                DateTime now = DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(9))
                    .DateTime;

                var targets = allTodos
                    .Where(t =>
                        !t.IsCompleted &&
                        t.LocationID.HasValue &&
                        (t.DueDate ?? t.TodoDate) >= now)
                    .ToList();

                var activeKeys = allTodos
                    .Select(WeatherKey)
                    .ToHashSet();

                // 삭제되거나 지역·시간이 변경된 일정의 이전 기록을 정리합니다.
                foreach (string key in previousWeather.Keys.Where(k => !activeKeys.Contains(k)).ToList())
                {
                    previousWeather.Remove(key);
                }

                // 동시에 몰아서 요청하지 않고 하나씩 조회합니다.
                foreach (TodoInfo target in targets)
                {
                    if (isClosed || isBusy || autoWeatherCancellation.IsCancellationRequested)
                        break;

                    string key = WeatherKey(target);

                    try
                    {
                        JsonObject result = await ApiService.GetTodoWeatherAsync(
                            memberId,
                            target.TodoID,
                            autoWeatherCancellation.Token);

                        if (isClosed || autoWeatherCancellation.IsCancellationRequested)
                            break;

                        TodoInfo? current = allTodos
                            .FirstOrDefault(t => t.TodoID == target.TodoID);

                        // 기다리는 동안 삭제·수정된 일정의 결과는 반영하지 않습니다.
                        if (current == null || current.IsCompleted || WeatherKey(current) != key)
                            continue;

                        var display = FormatWeather(result);

                        if (!RememberWeather(current, result))
                            continue;

                        // 목록의 날씨 문구만 갱신합니다.
                        current.WeatherSummary = display.Summary;

                        // 현재 선택된 일정이면 하단 상세 날씨도 갱신합니다.
                        if (TodoGrid.SelectedItem is TodoInfo selected && selected.TodoID == current.TodoID)
                        {
                            WeatherUpdateText.Text = $"[{current.Title}]\n{display.Details}";
                        }
                    }
                    catch (OperationCanceledException) when (autoWeatherCancellation.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
                    {
                        // 자동 조회 실패로 작업 중인 사용자에게 팝업을 띄우지 않습니다.
                        if (!isClosed)
                        {
                            StatusMessage.Text =
                                "일부 일정의 날씨 자동 갱신에 실패했습니다. 다음 갱신 때 다시 확인합니다.";
                        }
                    }
                }
            }
            finally
            {
                isRefreshingWeather = false;
            }
        }
        #endregion

        #region 04 이전 예보 비교와 변경 알림
        // 첫 조회는 기억만 하고, 이후 조회부터 이전 값과 비교합니다.
        private bool RememberWeather(TodoInfo todo, JsonObject result)
        {
            string status = result["status"]?.ToString() ?? "";

            if (status != "ready" && status != "partial")
                return true;

            string key = WeatherKey(todo);

            if (previousWeather.TryGetValue(key, out JsonObject? previous))
            {
                DateTime.TryParse(
                    previous["forecastBase"]?.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime oldBase);

                DateTime.TryParse(
                    result["forecastBase"]?.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out DateTime newBase);

                // 늦게 도착한 오래된 발표 자료는 반영하지 않습니다.
                if (oldBase != default && newBase != default && newBase < oldBase)
                    return false;

                var before = WeatherValues(previous);
                var after = WeatherValues(result);

                var changes = new List<string>();

                foreach (var item in after)
                {
                    if (before.TryGetValue(item.Key, out string? oldValue) && oldValue != item.Value)
                    {
                        changes.Add($"{item.Key}: {oldValue} → {item.Value}");
                    }
                }

                if (changes.Count > 0)
                {
                    weatherNotices.Enqueue(
                        $"[{todo.Title}] 날씨 변경\n" +
                        string.Join("\n", changes));

                    // 최근 10개 알림만 유지합니다.
                    while (weatherNotices.Count > 10)
                    {
                        weatherNotices.Dequeue();
                    }

                    ShowWeatherNotification(
                        string.Join("\n\n", weatherNotices.Reverse()));
                }
            }

            // 다음 조회에서 비교할 기준으로 기억합니다.
            previousWeather[key] = (JsonObject)result.DeepClone();

            return true;
        }
        #endregion

        #region 05 날씨 비교용 값 변환
        // 코드값을 사람이 읽는 문구로 바꾸어 비교합니다.
        private static Dictionary<string, string> WeatherValues(JsonObject result)
        {
            var values = new Dictionary<string, string>();

            if (result["weather"] is not JsonArray rows)
                return values;

            DateTime.TryParse(result["forecastBase"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime baseTime);

            foreach (JsonNode? row in rows)
            {
                if (row is not JsonObject item ||
                    !DateTime.TryParse(
                        item["forecastTime"]?.ToString(),
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out DateTime time))
                {
                    continue;
                }

                bool extended =
                    baseTime != default &&
                    time.Date >= baseTime.Date.AddDays(baseTime.Hour >= 17 ? 4 : 3);

                void Add(string label, string value)
                {
                    // 누락값을 실제 날씨 변화로 알리지 않습니다.
                    if (!value.Contains("정보") && !value.Contains("확인 필요"))
                    {
                        values[$"{time:yyyy-MM-dd HH:mm} · {label}"] = value;
                    }
                }

                Add("기온", NumberWithUnit(item["temperature"]?.ToString(), "℃"));
                Add("강수확률", NumberWithUnit(item["rainProbability"]?.ToString(), "%"));
                Add("강수량", RainText(item["precipitation"]?.ToString(), extended));
                Add("하늘 상태", SkyText(item["sky"]?.ToString()));
                Add("강수 형태", PrecipitationTypeText(item["precipitationType"]?.ToString()));
            }

            return values;
        }
        #endregion
    }
}