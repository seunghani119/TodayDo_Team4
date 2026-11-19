using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TODOList.Client.Models
{
    public class MemberInfo
    {
        #region 01 데이터 속성과 연결 정보
        public int MemberID { get; set; }
        public string MemberName { get; set; } = "";
        public DateTime MemberDate { get; set; }
        #endregion
    }
}
