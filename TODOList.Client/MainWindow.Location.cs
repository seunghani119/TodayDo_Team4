using TODOList.Client.Models;
using TODOList.Client.Services;


//장소 처리
namespace TODOList.Client
{
    public partial class MainWindow
    {
        #region 01 선택한 장소 상태
        private int? selectedLocationId;
        #endregion

        #region 02 장소 표시 문구
        //받은 정보	                표시 결과
        //이름과 주소가 같음        한 번만 표시
        //이름과 주소가 다름        두 줄로 표시
        //주소가 없음                이름만 표시
        private static string FormatLocationText(string name, string? address)
        {
            name = name.Trim();
            address = address?.Trim() ?? "";

            if (address.Length == 0 || name == address)
                return name;

            if (name.Length == 0)
                return address;

            return $"{name}\n{address}";
        }

        private void UpdateSelectedLocationText()
        {
            if (selectedMapLocation != null)
            {
                SelectedMapLocationText.Text = FormatLocationText(selectedMapLocation.LocationName, selectedMapLocation.Address);

                return;
            }

            var location = locations.FirstOrDefault(l => l.LocationID == selectedLocationId);

            if (location != null)
            {
                SelectedMapLocationText.Text = FormatLocationText(location.LocationName, location.Address);

                return;
            }

            SelectedMapLocationText.Text = selectedLocationId.HasValue
                ? "저장된 장소 정보를 찾지 못했습니다. 장소를 다시 선택해 주세요."
                : "아직 선택한 장소가 없습니다.";
        }
        #endregion

        #region 03 선택 장소 저장과 일정 연결
        private async Task ResolveTodoLocationAsync(TodoInfo todo)
        {
            if (selectedMapLocation == null)
                return;

            LocationInfo location =
                await ApiService.CreateMapLocationAsync(selectedMapLocation);

            todo.LocationID = location.LocationID;

            locations.RemoveAll(l => l.LocationID == location.LocationID);
            locations.Add(location);
        }
        #endregion
    }
}