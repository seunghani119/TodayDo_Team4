using TODOList.Client.Models;

namespace TODOList.Client
{
    public static class UserSession
    {
        #region 01 데이터 속성과 연결 정보
        public static MemberInfo? CurrentMember { get; set; }
        #endregion
    }
}
