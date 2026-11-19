using System.Text.Json.Serialization;
using TodayDo.Server.Models;

public class Location
{
    #region 01 데이터 속성과 연결 정보
    public int LocationID { get; set; }
    public string LocationName { get; set; } = "";
    public int Nx { get; set; }
    public int Ny { get; set; }
    public string? Address { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    [JsonIgnore]
    public ICollection<Todo> Todos { get; set; } = new List<Todo>();
    #endregion
}
