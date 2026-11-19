using System.ComponentModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using TODOList.Client.Models;

namespace TODOList.Client
{
    public partial class MainWindow
    {
        #region 01 편집 상태
        private string? editorBaseline;
        private TodoInfo? editorTodo;
        private bool isUpdatingEditor;
        private Func<string, bool>? discardEditorConfirmation = null;
        #endregion

        #region 02 변경 감지와 저장 전 확인
        // 입력란의 원래 상태와 현재 상태를 비교합니다. DB에는 전송하지 않습니다.
        private string GetEditorSnapshot()
        {
            return JsonSerializer.Serialize(new
            {
                title = TitleBox.Text,
                content = ContentBox.Text,
                startDate = StartDatePicker.SelectedDate,
                startDateText = StartDatePicker.Text,
                startPeriod = StartPeriodComboBox.SelectedItem,
                startHour = StartHourComboBox.SelectedItem,
                startMinute = StartMinuteComboBox.SelectedItem,
                hasEnd = HasEndDateCheckBox.IsChecked,
                endDate = EndDatePicker.SelectedDate,
                endDateText = EndDatePicker.Text,
                endPeriod = EndPeriodComboBox.SelectedItem,
                endHour = EndHourComboBox.SelectedItem,
                endMinute = EndMinuteComboBox.SelectedItem,
                locationId = selectedLocationId,
                mapName = selectedMapLocation?.LocationName,
                mapAddress = selectedMapLocation?.Address,
                mapLatitude = selectedMapLocation?.Latitude,
                mapLongitude = selectedMapLocation?.Longitude,
                completed = IsCompletedCheckBox.IsChecked
            });
        }

        private void RememberEditorState(TodoInfo? todo)
        {
            editorTodo = todo;
            editorBaseline = GetEditorSnapshot();
        }

        private bool HasUnsavedEditorChanges()
        {
            return editorBaseline != null && editorBaseline != GetEditorSnapshot();
        }

        private bool CanDiscardEditor(string action)
        {
            if (!HasUnsavedEditorChanges())
                return true;

            return (discardEditorConfirmation ?? ShowDiscardEditorConfirmation)(action);
        }

        private bool ShowDiscardEditorConfirmation(string action)
        {
            string message = "작성하거나 수정한 내용이 아직 저장되지 않았습니다.\n\n"
                + $"진행할 작업: {action}\n계속하면 현재 작성 중인 내용이 사라집니다.\n\n"
                + "예: 작성 중인 내용을 버리고 진행\n아니요: 현재 내용을 계속 작성";

            return MessageBox.Show(this, message, "작성 중인 일정 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        }
        #endregion

        #region 03 선택 복원과 편집 값 채우기
        // 선택 변경을 취소할 때 입력란을 다시 채우지 않고 선택 표시만 복원합니다.
        private void RestoreEditorSelection()
        {
            bool previous = isUpdatingEditor;
            isUpdatingEditor = true;

            try
            {
                TodoGrid.SelectedItem = editorTodo;
            }
            finally
            {
                isUpdatingEditor = previous;
            }
        }

        private void FillEditorFromTodo(TodoInfo todo)
        {
            bool previous = isUpdatingEditor;
            isUpdatingEditor = true;

            try
            {
                TitleBox.Text = todo.Title;
                ContentBox.Text = todo.Content ?? "";
                StartDatePicker.SelectedDate = todo.TodoDate.Date;
                SetTimeSelection(StartPeriodComboBox, StartHourComboBox, StartMinuteComboBox, todo.TodoDate);
                HasEndDateCheckBox.IsChecked = todo.DueDate.HasValue;
                EndDatePicker.SelectedDate = todo.DueDate?.Date;

                if (todo.DueDate is DateTime end)
                    SetTimeSelection(EndPeriodComboBox, EndHourComboBox, EndMinuteComboBox, end);
                else
                    ClearTimeSelection(EndPeriodComboBox, EndHourComboBox, EndMinuteComboBox);

                selectedLocationId = todo.LocationID;
                selectedMapLocation = null;
                UpdateSelectedLocationText();
                IsCompletedCheckBox.IsChecked = todo.IsCompleted;
                RememberEditorState(todo);
            }
            finally
            {
                isUpdatingEditor = previous;
            }
        }

        private static void ClearTimeSelection(ComboBox periodBox, ComboBox hourBox, ComboBox minuteBox)
        {
            minuteBox.ItemsSource = Enumerable.Range(0, 6).Select(n => n * 10).ToArray();
            periodBox.SelectedIndex = -1;
            hourBox.SelectedIndex = -1;
            minuteBox.SelectedIndex = -1;
        }
        #endregion

        #region 04 창 닫기 전 확인
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!CanDiscardEditor("창 닫기"))
                e.Cancel = true;
        }
        #endregion
    }
}
