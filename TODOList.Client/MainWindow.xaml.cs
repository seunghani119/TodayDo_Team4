using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TODOList.Client.Models;
using TODOList.Client.Services;

namespace TODOList.Client
{
    public partial class MainWindow : Window
    {
        #region 01 화면 상태와 로그인 회원
        // 서버에서 받은 전체 일정과 지역 목록
        private List<TodoInfo> allTodos = new();
        private List<LocationInfo> locations = new();

        // 지도에서 선택한 위치
        private MapLocationInfo? selectedMapLocation;

        // 현재 목록에 적용 중인 검색 날짜
        private DateTime? activeSearchDate;

        /// all: 전체 일정 / past: 지난 일정
        private string todoView = "all";

        // 중복 요청과 닫힌 창의 갱신 방지
        private bool isBusy;
        private bool isClosed;

        private int CurrentMemberId => UserSession.CurrentMember?.MemberID ?? throw new InvalidOperationException("다시 로그인해 주세요.");
        #endregion

        #region 02 창 생성과 최초 데이터 로딩
        public MainWindow()
        {
            InitializeComponent();
            InitializeTimeSelectors();

            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
            Closed += (_, _) => isClosed = true;
            Closed += (_, _) => CancelWeatherRequest();
            Closed += (_, _) => StopAutoWeather();     // -> 창이 닫히거나 로그아웃하면 자동조회 중단 (타이머나 요청등)
        }

        // 창이 처음 열리면 지역과 일정을 불러옵니다.
        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= MainWindow_Loaded;

            ResetEditor();

            await ExecuteAsync(async () =>
            {
                await ReloadLocationsAsync();
                await ReloadTodosAsync();

                SetStatus("지역과 일정을 불러왔습니다.");
            });

            StartAutoWeather();
            await RefreshWeatherAsync();
        }
        #endregion

        #region 03 서버 목록 조회와 필터링
        // 지역 목록을 받아 콤보박스에 넣습니다.
        private async Task ReloadLocationsAsync()
        {
            var result = await ApiService.GetLocationsAsync();

            if (isClosed)
                return;

            locations = result;
            UpdateSelectedLocationText();
        }

        // 서버에서 최신 일정을 받아 목록을 갱신합니다.
        private async Task ReloadTodosAsync()
        {
            var result = await ApiService.GetTodosAsync(CurrentMemberId);

            if (isClosed)
                return;

            allTodos = result;

            foreach (var todo in allTodos)
            {
                todo.LocationName =
                    todo.LocationID == null ? "지역 미선택" : locations.FirstOrDefault(l => l.LocationID == todo.LocationID)?.LocationName ?? "지역 정보 없음";
            }

            ApplyFilter();
        }

        // 전체 목록 또는 선택 날짜에 해당하는 목록 표시
        private void ApplyFilter()
        {
            DateTime today = DateTime.Today;

            IEnumerable<TodoInfo> result = allTodos;

            if (activeSearchDate is DateTime date)
            {
                result = result.Where(t =>
                    t.TodoDate.Date <= date.Date &&
                    (t.DueDate ?? t.TodoDate).Date >= date.Date);
            }
            else if (todoView == "past")
            {
                result = result.Where(t =>
                    (t.DueDate ?? t.TodoDate).Date < today);
            }

            bool showPastFirst =
                activeSearchDate == null && todoView == "past";

            TodoGrid.ItemsSource = showPastFirst
                ? result.OrderByDescending(t => t.DueDate ?? t.TodoDate).ToList()
                : result.OrderBy(t => t.TodoDate).ToList();
        }
        #endregion

        #region 04 새로고침과 날짜 검색
        // 일정 새로고침
        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBusy || !CanDiscardEditor("새로고침"))
                return;

            await ExecuteAsync(async () =>
            {
                int? selectedId = editorTodo?.TodoID;
                ResetEditor();

                await ReloadLocationsAsync();
                await ReloadTodosAsync();

                if (isClosed)
                    return;

                if (selectedId.HasValue)
                    TodoGrid.SelectedItem = allTodos.FirstOrDefault(t => t.TodoID == selectedId.Value);

                SetStatus("최신 일정 목록을 불러왔습니다. 날짜 검색 조건은 유지됩니다.");
            });
            await RefreshWeatherAsync();
        }

        // 선택한 날짜 검색
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (SearchDatePicker.SelectedDate is not DateTime date)
            {
                SetStatus("검색할 날짜를 선택해 주세요.", true);

                return;
            }

            if (!CanDiscardEditor("검색한 목록으로 이동"))
                return;

            ResetEditor();
            activeSearchDate = date.Date;

            ApplyFilter();

            SetStatus($"{date:yyyy-MM-dd}에 해당하는 일정 " + $"{TodoGrid.Items.Count}개입니다.");
        }

        // 날짜 입력과 검색 조건 모두 초기화
        private void ResetSearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanDiscardEditor("검색 날짜를 초기화"))
                return;

            ResetEditor();
            SearchDatePicker.SelectedDate = null;
            SearchDatePicker.Text = "";

            activeSearchDate = null;

            ApplyFilter();

            SetStatus("검색 날짜를 초기화했습니다. 현재 선택한 목록을 표시합니다.");
        }

        // 날짜 입력은 남겨 두고 전체 목록 표시
        private void ShowAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanDiscardEditor("전체 목록으로 이동"))
                return;

            ResetEditor();
            todoView = "all";
            activeSearchDate = null;

            ApplyFilter();

            SetStatus("예정 일정과 지난 일정을 모두 표시했습니다.");
        }
        #endregion

        #region 05 일정 선택과 편집 화면 연결
        // 선택한 일정을 입력란에 표시하고 날씨를 조회합니다.
        private async void TodoGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingEditor || isClosed)
                return;

            TodoInfo? requested = TodoGrid.SelectedItem as TodoInfo;

            if (HasUnsavedEditorChanges())
            {
                RestoreEditorSelection();

                if (!CanDiscardEditor("다른 일정으로 이동"))
                    return;

                isUpdatingEditor = true;
                try
                {
                    TodoGrid.SelectedItem = requested;
                }
                finally
                {
                    isUpdatingEditor = false;
                }
            }

            CancelWeatherRequest();

            if (requested == null)
            {
                ResetEditor();
                return;
            }

            FillEditorFromTodo(requested);
            SetStatus($"'{requested.Title}' 일정을 선택했습니다. 수정 후 [수정]을 눌러 주세요.");
            await LoadSelectedWeatherAsync(requested);
        }
        #endregion

        #region 06 입력값과 날짜 시간 검증
        // 입력값 검사 후 서버에 보낼 일정 객체 생성
        private TodoInfo ReadEditor()
        {
            string title = TitleBox.Text.Trim();

            if (title.Length == 0)
                throw new InvalidOperationException("제목을 입력해 주세요.");

            DateTime start = ReadDateTime(StartDatePicker, StartPeriodComboBox, StartHourComboBox, StartMinuteComboBox, "활동 시작");

            DateTime? end = null;

            if (HasEndDateCheckBox.IsChecked == true)
            {
                end = ReadDateTime(EndDatePicker, EndPeriodComboBox, EndHourComboBox, EndMinuteComboBox, "활동 종료");

                if (end < start)
                    throw new InvalidOperationException("종료 시간은 시작 시간보다 빠를 수 없습니다.");
            }

            if (selectedMapLocation == null &&
                selectedLocationId.HasValue &&
                !locations.Any(l => l.LocationID == selectedLocationId.Value))
            {
                throw new InvalidOperationException("기존 장소 정보를 찾지 못했습니다. 장소를 다시 선택해 주세요.");
            }

            return new TodoInfo
            {
                MemberID = CurrentMemberId,
                Title = title,
                Content = ContentBox.Text.Trim(),
                TodoDate = start,
                DueDate = end,
                IsCompleted = IsCompletedCheckBox.IsChecked == true,
                LocationID = selectedLocationId
            };
        }

        // 오전/오후와 01~12시 선택 목록을 준비합니다.
        private void InitializeTimeSelectors()
        {
            foreach (ComboBox box in new[] { StartPeriodComboBox, EndPeriodComboBox })
                box.ItemsSource = new[] { "오전", "오후" };

            foreach (ComboBox box in new[] { StartHourComboBox, EndHourComboBox })
                box.ItemsSource = Enumerable.Range(1, 12).ToArray();
        }

        // 저장된 24시간제 시간을 화면의 오전/오후 선택값으로 표시합니다.
        private static void SetTimeSelection(ComboBox periodBox, ComboBox hourBox, ComboBox minuteBox, DateTime time)
        {
            int[] minutes = Enumerable.Range(0, 6).Select(n => n * 10).ToArray();

            // 기존 일정이 10분 단위가 아니어도 임의로 반올림하지 않습니다.
            if (!minutes.Contains(time.Minute))
                minutes = minutes.Append(time.Minute).OrderBy(n => n).ToArray();

            minuteBox.ItemsSource = minutes;
            periodBox.SelectedItem = time.Hour < 12 ? "오전" : "오후";
            hourBox.SelectedItem = time.Hour % 12 == 0 ? 12 : time.Hour % 12;
            minuteBox.SelectedItem = time.Minute;
        }

        // 선택한 날짜와 오전/오후·시·분을 서버에 보낼 DateTime으로 합칩니다.
        private static DateTime ReadDateTime(DatePicker picker, ComboBox periodBox, ComboBox hourBox, ComboBox minuteBox, string label)
        {
            if (picker.SelectedDate is not DateTime date)
                throw new InvalidOperationException($"{label} 날짜를 선택해 주세요.");

            if (periodBox.SelectedItem is not string period || (period != "오전" && period != "오후") ||
                hourBox.SelectedItem is not int hour || hour < 1 || hour > 12 ||
                minuteBox.SelectedItem is not int minute || minute < 0 || minute > 59)
            {
                throw new InvalidOperationException($"{label}의 오전/오후, 시, 분을 모두 선택해 주세요.");
            }

            int hour24 = hour % 12 + (period == "오후" ? 12 : 0);
            return date.Date.AddHours(hour24).AddMinutes(minute);
        }
        #endregion

        #region 07 일정 등록 수정 삭제와 재조회
        // 새 일정 등록  입력값 검사 → 새 장소 저장 → 장소 번호를 넣은 일정 저장
        private async void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteAsync(async () =>
            {
                var todo = ReadEditor();

                await ResolveTodoLocationAsync(todo);

                if (isClosed)
                    return;

                await SaveAndRefreshAsync(
                    () => ApiService.CreateTodoAsync(CurrentMemberId, todo),
                    "일정을 등록했습니다.");
            });
        }

        // 선택한 일정 수정
        // 장소를 변경할 때는 해당 일정의 LocationID를 변경
        // 기존 장소 행을 수정해서 다른 일정의 장소까지 바뀌는 방식으로 처리XXX
        private async void UpdateButton_Click(object sender, RoutedEventArgs e)
        {
            await ExecuteAsync(async () =>
            {
                if (TodoGrid.SelectedItem is not TodoInfo selected)
                {
                    throw new InvalidOperationException("수정할 일정을 목록에서 선택해 주세요.");
                }

                var todo = ReadEditor();
                await ResolveTodoLocationAsync(todo);
                if (isClosed) return;

                await SaveAndRefreshAsync(() => ApiService.UpdateTodoAsync(CurrentMemberId, selected.TodoID, todo), "일정을 수정했습니다.");
            });
        }

        // 선택한 일정 삭제
        private async void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBusy)
                return;

            if (TodoGrid.SelectedItem is not TodoInfo selected)
            {
                SetStatus("삭제할 일정을 목록에서 선택해 주세요.", true);

                return;
            }

            string question = $"'{selected.Title}' 일정을 삭제하시겠습니까?";

            if (HasUnsavedEditorChanges())
                question += "\n작성 중인 변경 내용도 함께 버려집니다.";

            var answer = MessageBox.Show(this, question, "일정 삭제", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer != MessageBoxResult.Yes)
                return;

            await ExecuteAsync(() => SaveAndRefreshAsync(() => ApiService.DeleteTodoAsync(CurrentMemberId, selected.TodoID), "일정을 삭제했습니다."));
        }

        // 저장 성공과 저장 후 재조회 실패를 구분합니다.
        private async Task SaveAndRefreshAsync(Func<Task> save, string message)
        {
            await save();

            if (isClosed)
                return;

            ResetEditor();

            try
            {
                await ReloadTodosAsync();

                SetStatus(message + " 검색 조건에 맞는 목록을 표시합니다.");
            }
            catch (Exception ex) when (
                ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                SetStatus(message + " 목록 재조회는 실패했습니다. " + "[일정 새로고침]을 눌러 주세요.", true);
            }
        }
        #endregion

        #region 08 작성란 초기화
        // 작성란 초기화 버튼
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanDiscardEditor("입력란을 초기화"))
                return;

            ResetEditor();

            SetStatus("작성란을 초기화했습니다. " + "저장된 일정은 삭제되지 않습니다.");
        }

        // 오른쪽 입력란을 새 일정 작성 상태로 되돌립니다.
        private void ResetEditor()
        {
            bool previous = isUpdatingEditor;
            isUpdatingEditor = true;

            try
            {
                CancelWeatherRequest();
                TodoGrid.SelectedItem = null;
                TitleBox.Clear();
                ContentBox.Clear();
                StartDatePicker.SelectedDate = null;
                StartDatePicker.Text = "";
                ClearTimeSelection(StartPeriodComboBox, StartHourComboBox, StartMinuteComboBox);
                HasEndDateCheckBox.IsChecked = true;
                EndDatePicker.SelectedDate = null;
                EndDatePicker.Text = "";
                ClearTimeSelection(EndPeriodComboBox, EndHourComboBox, EndMinuteComboBox);
                IsCompletedCheckBox.IsChecked = false;
                selectedLocationId = null;
                selectedMapLocation = null;
                UpdateSelectedLocationText();
                WeatherUpdateText.Text = "날씨를 확인할 일정을 선택해 주세요.";
                RememberEditorState(null);
            }
            finally
            {
                isUpdatingEditor = previous;
            }
        }
        #endregion

        #region 09 비동기 작업과 공통 오류 안내
        // 공통 비동기 처리: 중복 클릭 방지와 오류 안내
        private async Task ExecuteAsync(Func<Task> action)
        {
            if (isBusy || isClosed)
                return;

            isBusy = true;

            ((UIElement)Content).IsEnabled = false;

            SetStatus("처리 중입니다.");

            try
            {
                await action();
            }
            catch (HttpRequestException)
            {
                SetStatus("서버와 통신하지 못했습니다. " + "서버 실행 상태와 주소를 확인해 주세요.", true);
            }
            catch (TaskCanceledException)
            {
                SetStatus("응답 시간이 초과되었습니다. " + "저장 요청이었다면 새로고침으로 " + "결과를 먼저 확인해 주세요.", true);
            }
            catch (JsonException)
            {
                SetStatus("서버 응답을 읽지 못했습니다.", true);
            }
            catch (InvalidOperationException ex)
            {
                SetStatus(ex.Message, true);
            }
            finally
            {
                isBusy = false;

                if (!isClosed)
                {
                    ((UIElement)Content).IsEnabled = true;
                }
            }
        }

        // 하단 상태 문구 표시
        private void SetStatus(string message, bool error = false)
        {
            if (isClosed)
                return;

            StatusMessage.Foreground = Brushes.DarkSlateGray;
            if (error)
            {
                StatusMessage.Text = "안내창의 내용을 확인해 주세요.";

                MessageBox.Show(this, message, "TodayDo 안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            StatusMessage.Text = message;
        }
        #endregion

        #region 10 로그아웃과 알림 지도 목록 전환
        // 로그아웃
        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBusy)
                return;

            if (!CanDiscardEditor("로그아웃"))
                return;

            editorBaseline = null;
            UserSession.CurrentMember = null;

            LoginWindow loginWindow = new();

            Application.Current.MainWindow = loginWindow;

            loginWindow.Show();
            Close();
        }

        // 이후 실제 날씨 변경을 확인했을 때 호출
        private void ShowWeatherNotification(string message)
        {
            WeatherNotificationText.Text = message;

            WeatherNotificationPanel.Visibility = Visibility.Visible;
        }
        
        //알림창 닫기 버튼
        private void DismissWeatherNotification_Click(object sender, RoutedEventArgs e)
        {
            weatherNotices.Clear();

            WeatherNotificationPanel.Visibility = Visibility.Collapsed;
        }

        // 지도 선택 기능 유지
        private void SelectMapLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var mapWindow = new MapWindow
            {
                Owner = this
            };

            if (mapWindow.ShowDialog() != true || mapWindow.SelectedLocation == null)
                return;

            selectedMapLocation = mapWindow.SelectedLocation;
            selectedLocationId = null;

            UpdateSelectedLocationText();

            SetStatus("장소를 선택했습니다. 일정 등록 또는 수정을 눌러 저장해 주세요.");
        }

        //지난 일정으로 이동
        private void PastTodosButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanDiscardEditor("지난 일정 목록으로 이동"))
                return;

            ResetEditor();

            activeSearchDate = null;
            todoView = "past";

            ApplyFilter();

            SetStatus($"지난 일정 {TodoGrid.Items.Count}개를 표시했습니다.");
        }
        #endregion
    }
}
