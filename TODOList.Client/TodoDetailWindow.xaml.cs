using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using TODOList.Client.Models;
using TODOList.Client.Services;

namespace TODOList.Client
{
    public partial class TodoDetailWindow : Window
    {
        #region 01 일정 식별자와 요청 상태
        private readonly int memberId;
        private readonly int todoId;
        private CancellationTokenSource? adviceCancellation;
        private CancellationTokenSource? weatherCancellation;
        private bool detailClosed;
        #endregion

        #region 02 창 생성과 일정 표시
        public TodoDetailWindow()
        {
            InitializeComponent();
            // 작은 화면에서도 상세창이 작업 영역보다 커지지 않도록 조정합니다.
            MinWidth = Math.Min(MinWidth, SystemParameters.WorkArea.Width);
            MinHeight = Math.Min(MinHeight, SystemParameters.WorkArea.Height);
            Width = Math.Min(Width, SystemParameters.WorkArea.Width);
            Height = Math.Min(Height, SystemParameters.WorkArea.Height);
            Loaded += DetailWindow_Loaded;
        }

        public TodoDetailWindow(TodoInfo todo) : this()
        {
            memberId = UserSession.CurrentMember?.MemberID ?? 0;
            todoId = todo.TodoID;
            TitleText.Text = todo.Title;
            ContentText.Text = string.IsNullOrWhiteSpace(todo.Content) ? "작성된 내용이 없습니다." : todo.Content;
            StartText.Text = FormatDateTime(todo.TodoDate);
            EndText.Text = todo.DueDate is DateTime end ? FormatDateTime(end) : "설정하지 않음";
            LocationText.Text = string.IsNullOrWhiteSpace(todo.LocationName) ? "지역 미선택" : todo.LocationName;
            CompletionText.Text = todo.IsCompleted ? "완료한 일정" : "미완료 일정";
            HeaderSummaryText.Text = todo.TodoDate.ToString("MM월 dd일 tt hh:mm", CultureInfo.GetCultureInfo("ko-KR")) + " · " + LocationText.Text;
            CheckAiButton.IsEnabled = memberId > 0 && todoId > 0;
            RefreshWeatherButton.IsEnabled = CheckAiButton.IsEnabled;
        }
        #endregion

        #region 03 상세 날씨 조회와 새로고침
        private async void DetailWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= DetailWindow_Loaded;
            if (memberId > 0 && todoId > 0) await LoadWeatherAsync();
        }

        private async void RefreshWeatherButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadWeatherAsync();
        }

        private async Task LoadWeatherAsync()
        {
            if (detailClosed || adviceCancellation != null || weatherCancellation != null || memberId <= 0 || todoId <= 0) return;
            var request = new CancellationTokenSource();
            weatherCancellation = request;
            RefreshWeatherButton.IsEnabled = false;
            WeatherStatusText.Text = "최신 예보를 확인하고 있습니다.";
            try
            {
                JsonObject forecast = await ApiService.GetTodoWeatherAsync(memberId, todoId, request.Token);
                if (detailClosed || request.IsCancellationRequested) return;
                DisplayWeather(forecast, "방금 조회한 예보");
                // 예보를 새로 조회하면 기존 조언을 최신 조언으로 오해하지 않도록 표시합니다.
                if (!string.IsNullOrEmpty(AiCheckedText.Text))
                    AiStatusText.Text = "이전 조회의 조언입니다. 새 예보에 맞추려면 다시 확인해 주세요.";
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
            {
                if (!detailClosed && !request.IsCancellationRequested)
                    WeatherStatusText.Text = "새 예보 조회 실패 · " + ErrorMessage(ex);
            }
            finally
            {
                if (ReferenceEquals(weatherCancellation, request)) weatherCancellation = null;
                request.Dispose();
                if (!detailClosed) RefreshWeatherButton.IsEnabled = adviceCancellation == null;
            }
        }
        #endregion

        #region 04 AI 조언 요청
        private async void CheckAiButton_Click(object sender, RoutedEventArgs e)
        {
            if (detailClosed || adviceCancellation != null || memberId <= 0 || todoId <= 0) return;
            // AI 응답에 포함된 예보가 도착한 뒤 이전 날씨 요청이 덮어쓰지 않게 취소합니다.
            weatherCancellation?.Cancel();
            var request = new CancellationTokenSource();
            adviceCancellation = request;
            CheckAiButton.IsEnabled = false;
            RefreshWeatherButton.IsEnabled = false;
            CheckAiButton.Content = "일정에 맞는 조언을 준비하고 있어요…";
            AiStatusText.Foreground = Brushes.SlateBlue;
            AiStatusText.Text = "저장된 일정과 예보를 살펴보고 있어요.";
            AiAdviceText.Text = "잠시만 기다려 주세요. 창을 닫으면 요청을 취소합니다.";
            AiCheckedText.Text = "";
            try
            {
                JsonObject result = await ApiService.GetTodoAdviceAsync(memberId, todoId, request.Token);
                if (!detailClosed && !request.IsCancellationRequested) DisplayAdvice(result);
            }
            catch (OperationCanceledException) when (request.IsCancellationRequested) { }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
            {
                if (detailClosed) return;
                AiStatusText.Foreground = Brushes.IndianRed;
                AiStatusText.Text = "조언을 확인하지 못했습니다.";
                AiAdviceText.Text = ErrorMessage(ex);
                WeatherStatusText.Text = "AI용 새 예보를 확인하지 못했습니다. 날씨 새로고침으로 다시 확인해 주세요.";
            }
            finally
            {
                adviceCancellation = null;
                request.Dispose();
                if (!detailClosed)
                {
                    CheckAiButton.IsEnabled = true;
                    RefreshWeatherButton.IsEnabled = weatherCancellation == null;
                    CheckAiButton.Content = "최신 예보로 AI 조언 다시 확인";
                }
            }
        }
        #endregion

        #region 05 조언과 시간별 날씨 표시
        private void DisplayAdvice(JsonObject result)
        {
            string message = result["message"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(message)) throw new InvalidOperationException("서버에서 AI 조언을 받지 못했습니다.");
            AiAdviceText.Text = message;
            AiStatusText.Foreground = Brushes.DarkSlateBlue;
            AiStatusText.Text = "입력한 일정과 예보를 바탕으로 작성한 AI 조언";
            AiCheckedText.Text = Metadata(result);
            if (!string.IsNullOrWhiteSpace(result["model"]?.ToString())) AiCheckedText.Text += " · 설정 모델 " + result["model"];
            if (result["forecast"] is JsonObject forecast) DisplayWeather(forecast, "이번 안내에 사용한 예보");
        }

        private void DisplayWeather(JsonObject forecast, string source)
        {
            string status = forecast["status"]?.ToString() ?? "";
            WeatherStatusText.Text = status switch
            {
                "ready" => source,
                "partial" => source + " · 일부 시간·항목만 제공됩니다.",
                _ => forecast["message"]?.ToString() ?? "아직 활동 시간의 예보가 제공되지 않습니다."
            };
            WeatherCheckedText.Text = Metadata(forecast);
            var items = new List<object>();
            if (forecast["displayWeather"] is JsonArray rows)
            {
                foreach (JsonObject row in rows.OfType<JsonObject>())
                {
                    DateTime.TryParse(row["time"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime time);
                    items.Add(new
                    {
                        Time = time == default ? "시간 정보 없음" : time.ToString("MM/dd\nHH:mm"),
                        Sky = row["sky"]?.ToString() ?? "정보 없음",
                        Temperature = NumberWithUnit(row["temperatureC"], "℃"),
                        Type = row["precipitationType"]?.ToString() ?? "정보 없음",
                        Probability = NumberWithUnit(row["rainProbabilityPercent"], "%"),
                        Rain = row["precipitation"]?.ToString() ?? "정보 없음"
                    });
                }
            }
            else if (status is "ready" or "partial")
            {
                WeatherStatusText.Text = "서버를 다시 실행해 주세요. 새 시간별 날씨 형식이 필요합니다.";
            }
            WeatherGrid.ItemsSource = items;
        }
        #endregion

        #region 06 단위 날짜 오류 문구 변환
        private static string NumberWithUnit(JsonNode? node, string unit)
        {
            return double.TryParse(node?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && double.IsFinite(value) && value > -900 && value < 900
                ? value.ToString("0.#", CultureInfo.InvariantCulture) + unit : "정보 없음";
        }

        private static string Metadata(JsonObject result)
        {
            var parts = new List<string>();
            if (DateTime.TryParse(result["forecastBase"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime forecastBase)) parts.Add($"예보 발표 {forecastBase:MM/dd HH:mm}");
            if (DateTimeOffset.TryParse(result["checkedAt"]?.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset checkedAt)) parts.Add("확인 " + checkedAt.ToOffset(TimeSpan.FromHours(9)).ToString("MM/dd HH:mm"));
            return string.Join(" · ", parts);
        }

        private static string ErrorMessage(Exception ex)
        {
            return ex is OperationCanceledException ? "응답 시간이 초과되었습니다. 잠시 후 다시 확인해 주세요."
                : ex is HttpRequestException ? "서버와 연결하지 못했습니다. 서버 실행 상태를 확인해 주세요."
                : ex is JsonException ? "서버 응답 형식을 확인해 주세요." : ex.Message;
        }

        private static string FormatDateTime(DateTime time)
        {
            return time.ToString("yyyy년 MM월 dd일 (ddd)\ntt hh시 mm분", CultureInfo.GetCultureInfo("ko-KR"));
        }
        #endregion

        #region 07 편집 이동과 창 종료 요청 취소
        private void EditTodoButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CloseDetailButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            detailClosed = true;
            weatherCancellation?.Cancel();
            adviceCancellation?.Cancel();
            base.OnClosed(e);
        }
        #endregion
    }
}
