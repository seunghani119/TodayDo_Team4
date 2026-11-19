using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using TodayDo.Server.Models;

namespace TodayDo.Server.Data
{
    public class AppDbContext : DbContext
    {
        #region 01 DB 테이블 연결
        public DbSet<Member> Members { get; set; }
        public DbSet<Todo> Todos { get; set; }
        public DbSet<Location> Locations { get; set; }
        #endregion

        #region 02 DB 연결 옵션 전달
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }
        #endregion
    }
}
