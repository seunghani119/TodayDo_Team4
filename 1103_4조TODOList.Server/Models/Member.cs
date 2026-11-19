using System.Text.Json.Serialization;

namespace TodayDo.Server.Models
{
    public class Member
    {
        #region 01 데이터 속성과 연결 정보
        public int MemberID { get; set; }
        public string MemberPW { get; set; } = "";
        public string MemberName { get; set; } = "";
        public DateTime MemberDate { get; set; }
        [JsonIgnore]
        public ICollection<Todo> Todos { get; set; } = new List<Todo>();
        #endregion
    }
}
