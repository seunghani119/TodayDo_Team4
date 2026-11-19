using System.Windows;
using TODOList.Client.Models;

namespace TODOList.Client
{
    public partial class MainWindow
    {
        #region 01 일정 상세 창 열기
        private void ViewTodoButton_Click(object sender, RoutedEventArgs e)
        {
            if (isBusy || isClosed)
                return;

            if (TodoGrid.SelectedItem is not TodoInfo todo)
            {
                SetStatus("자세히 볼 일정을 목록에서 선택해 주세요.");
                return;
            }

            var detailWindow = new TodoDetailWindow(todo) { Owner = this };

            if (detailWindow.ShowDialog() == true)
                FocusTodoEditor(todo);
        }

        #endregion

        #region 02 상세 창에서 편집 화면으로 이동
        private bool FocusTodoEditor(TodoInfo todo)
        {
            if (isBusy || isClosed)
                return false;

            if (!ReferenceEquals(TodoGrid.SelectedItem, todo) ||
                !ReferenceEquals(editorTodo, todo))
            {
                SetStatus("선택한 일정이 바뀌었습니다. 수정할 일정을 목록에서 다시 선택해 주세요.");
                return false;
            }

            TitleBox.Focus();
            TitleBox.CaretIndex = TitleBox.Text.Length;

            SetStatus($"'{todo.Title}'의 입력란으로 돌아왔습니다. 변경 후 [수정]을 눌러 저장해 주세요.");

            return true;
        }
        #endregion
    }
}
