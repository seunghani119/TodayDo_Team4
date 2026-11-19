using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace TODOList.Client.Models
{
    public class TodoInfo : INotifyPropertyChanged
    {
        #region 01 서버와 주고받는 일정 데이터
        public int TodoID { get; set; }

        public int MemberID { get; set; }

        public string Title { get; set; } = "";

        public string? Content { get; set; }

        public bool IsCompleted { get; set; }

        public DateTime TodoDate { get; set; }

        public DateTime? DueDate { get; set; }

        public int? LocationID { get; set; }
        #endregion

        #region 02 화면 표시와 변경 알림
        [JsonIgnore]
        public string LocationName { get; set; } = "지역 미선택";

        private string weatherSummary = "날씨 확인 전";

        [JsonIgnore]
        public string WeatherSummary
        {
            get => weatherSummary;

            set
            {
                if (weatherSummary == value)
                    return;

                weatherSummary = value;

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WeatherSummary)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion
    }
}
