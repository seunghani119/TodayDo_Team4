using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Text.Json.Serialization;

namespace TodayDo.Server.Models
{
    public class Todo
    {
        #region 01 데이터 속성과 연결 정보
        public int TodoID { get; set; }
        public int MemberID { get; set; }
        public string Title { get; set; } = "";
        public string? Content { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime TodoDate { get; set; }
        public DateTime? DueDate { get; set; }

        [JsonIgnore]
        [ValidateNever]
        public Member Member { get; set; } = null!;

        public int? LocationID { get; set; }
        [JsonIgnore]
        public Location? Location { get; set; }
        #endregion
    }
}
