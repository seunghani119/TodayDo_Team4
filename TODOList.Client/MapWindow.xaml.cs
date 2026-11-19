using Microsoft.Web.WebView2.Core;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using TODOList.Client.Models;

namespace TODOList.Client
{
    public partial class MapWindow : Window
    {
        #region 01 지도 상태
        private const string MapOrigin = "https://todaydo.example";

        private bool isClosed;

        public MapLocationInfo? SelectedLocation { get; private set; }
        #endregion

        #region 02 지도 창 생성과 WebView2 초기화
        public MapWindow()
        {
            InitializeComponent();
        }

        private async void MapWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                string mapFolder = Path.Combine(AppContext.BaseDirectory, "MapAssets");

                string htmlPath = Path.Combine(mapFolder, "map.html");

                if (!File.Exists(htmlPath))
                {
                    MapMessage.Text = "map.html이 없습니다. 파일의 출력 복사 설정을 확인해 주세요.";

                    return;
                }

                // WebView2가 사용할 캐시 폴더입니다.
                string userDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TodayDo", "WebView2");

                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);

                if (isClosed)
                    return;

                await MapBrowser.EnsureCoreWebView2Async(environment);

                if (isClosed)
                    return;

                // 로컬 MapAssets 폴더를 지정한 주소로 연결합니다.
                MapBrowser.CoreWebView2
                    .SetVirtualHostNameToFolderMapping("todaydo.example", mapFolder, CoreWebView2HostResourceAccessKind.DenyCors);

                MapBrowser.CoreWebView2.WebMessageReceived += MapBrowser_WebMessageReceived;

                // 지도 창 안에서 다른 사이트로 이동하지 않게 합니다.
                MapBrowser.CoreWebView2.NavigationStarting
                    += (_, args) =>
                    {
                        if (!IsMapPage(args.Uri))
                            args.Cancel = true;
                    };

                MapBrowser.Source = new Uri(MapOrigin + "/map.html");
            }
            catch (Exception)
            {
                if (!isClosed)
                {
                    MapMessage.Text = "지도를 실행하지 못했습니다. WebView2 Runtime 설치 여부를 확인해 주세요.";
                }
            }
        }
        #endregion

        #region 03 지도 주소와 메시지 검증
        private static bool IsMapPage(string address)
        {
            return Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "todaydo.example"
                   && uri.IsDefaultPort
                   && uri.AbsolutePath == "/map.html";
        }

        private void MapBrowser_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (isClosed || !IsMapPage(e.Source))
                return;

            try
            {
                using JsonDocument document = JsonDocument.Parse(e.WebMessageAsJson);

                JsonElement root = document.RootElement;

                string? type = root.GetProperty("type").GetString();

                switch (type)
                {
                    case "ready":
                        MapMessage.Text = "지도에서 원하는 위치를 클릭해 주세요.";
                        break;

                    case "loading":
                        SelectedLocation = null;
                        ConfirmLocationButton.IsEnabled = false;

                        MapMessage.Text = "선택한 위치의 주소를 확인하고 있습니다.";
                        break;

                    case "error":
                        SelectedLocation = null;
                        ConfirmLocationButton.IsEnabled = false;

                        MapMessage.Text = root.GetProperty("message").GetString() ?? "위치를 확인하지 못했습니다.";
                        break;

                    case "selected":
                        double latitude = root.GetProperty("latitude").GetDouble();

                        double longitude = root.GetProperty("longitude").GetDouble();

                        string address = root.GetProperty("address").GetString() ?? "";

                        if (!double.IsFinite(latitude) || !double.IsFinite(longitude) || latitude < -90 || latitude > 90 || longitude < -180 || longitude > 180 ||
                            string.IsNullOrWhiteSpace(address))
                        {
                            throw new InvalidOperationException();
                        }

                        SelectedLocation = new MapLocationInfo
                        {
                            LocationName = root.TryGetProperty("locationName", out JsonElement name) && 
                            !string.IsNullOrWhiteSpace(name.GetString())
                            ? name.GetString()! : address,

                            Address = address,
                            Latitude = latitude,
                            Longitude = longitude
                        };

                        MapMessage.Text = SelectedLocation.LocationName == SelectedLocation.Address
                        ? SelectedLocation.Address : $"{SelectedLocation.LocationName}\n{SelectedLocation.Address}";
                        ConfirmLocationButton.IsEnabled = true;
                        break;
                }
            }
            catch (Exception)
            {
                SelectedLocation = null;
                ConfirmLocationButton.IsEnabled = false;

                MapMessage.Text = "위치 정보를 읽지 못했습니다. 다시 선택해 주세요.";
            }
        }
        #endregion

        #region 04 선택 확정 취소와 리소스 해제
        private void ConfirmLocationButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedLocation == null)
                return;

            // ShowDialog()를 호출한 메인 창에
            // 선택 완료 여부를 전달합니다.
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void MapWindow_Closed(object? sender, EventArgs e)
        {
            isClosed = true;
            MapBrowser.Dispose();
        }
        #endregion
    }
}
