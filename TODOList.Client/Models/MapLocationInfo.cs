using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TODOList.Client.Models
{
    public class MapLocationInfo
    {
        #region 01 데이터 속성과 연결 정보
        public string LocationName { get; set; } = "";
        public string Address { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        #endregion
    }
}
