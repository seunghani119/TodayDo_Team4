//**지도 좌표를 날씨 API가 사용하는 좌표로 바꾸는 클래스**

namespace TodayDo.Server.Services
{
    public static class KmaGridConverter
    {
        #region 01 위경도 검증과 기상청 격자 좌표 변환
        public static (int Nx, int Ny) ToGrid(double latitude, double longitude)
        {
            if (!double.IsFinite(latitude) || !double.IsFinite(longitude) ||

                latitude <= -90 || latitude >= 90 ||
                longitude < -180 || longitude > 180)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude));
            }

            const double earthRadius = 6371.00877;
            const double gridSize = 5.0;

            double radians = Math.PI / 180.0;
            double latitude1 = 30.0 * radians;
            double latitude2 = 60.0 * radians;
            double originLatitude = 38.0 * radians;
            double originLongitude = 126.0 * radians;
            double radius = earthRadius / gridSize;

            double n = Math.Log(Math.Cos(latitude1) / Math.Cos(latitude2));

            n /= Math.Log(
                Math.Tan(Math.PI / 4 + latitude2 / 2) /
                Math.Tan(Math.PI / 4 + latitude1 / 2));

            double factor =
                Math.Pow(Math.Tan(Math.PI / 4 + latitude1 / 2), n) *
                Math.Cos(latitude1) / n;

            double originDistance =
                radius * factor /
                Math.Pow(Math.Tan(Math.PI / 4 + originLatitude / 2), n);

            double distance =
                radius * factor /
                Math.Pow(Math.Tan(Math.PI / 4 + latitude * radians / 2), n);

            double angle = longitude * radians - originLongitude;

            if (angle > Math.PI)
                angle -= Math.PI * 2;

            if (angle < -Math.PI)
                angle += Math.PI * 2;

            angle *= n;

            double x = Math.Floor(distance * Math.Sin(angle) + 43 + 0.5);

            double y = Math.Floor(
                originDistance - distance * Math.Cos(angle) + 136 + 0.5);

            if (!double.IsFinite(x) || !double.IsFinite(y) ||
                x < 1 || x > 149 || y < 1 || y > 253)
            {
                throw new ArgumentOutOfRangeException(nameof(latitude));
            }

            return ((int)x, (int)y);
        }
        #endregion
    }
}