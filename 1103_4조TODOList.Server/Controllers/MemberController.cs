using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodayDo.Server.Data;
using TodayDo.Server.Models;

namespace TodayDo.Server.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class MemberController : ControllerBase
    {
        #region 01 DB 연결과 컨트롤러 생성
        private readonly AppDbContext db;
        public MemberController(AppDbContext context)
        {
            db = context;
        }
        #endregion

        #region 조회 API
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Member>>> GetAll()
        {
            var members = await db.Members
                .AsNoTracking()
                .OrderBy(m => m.MemberID)
                .ToListAsync();

            return Ok(members);
        }
        #endregion

        #region 등록 API
        [HttpPost]
        public async Task<ActionResult<Member>> Create(Member member)
        {
            member.MemberDate = DateTime.Now;
            db.Members.Add(member);
            await db.SaveChangesAsync();

            return Ok(member);
        }
        #endregion

        #region 로그인 API
        [HttpPost("login")]
        public async Task<ActionResult<Member>> Login(Member member)
        {
            var existingMember = await db.Members
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MemberName == member.MemberName && m.MemberPW == member.MemberPW);
            if (existingMember == null)
            {

                return Unauthorized(); // 로그인 실패 시 401 Unauthorized 반환
            }
            return Ok(existingMember); // 로그인 성공 시 회원 정보 반환
        }
        #endregion

        #region 수정 API
        [HttpPut("{id}")]
        public async Task<ActionResult<Member>> Update(int id, Member _member)
        {
            var member = await db.Members.FindAsync(id);
            if (member == null)
                return NotFound();

            //비밀번호랑 이름 수정끌끌
            member.MemberPW = _member.MemberPW;
            member.MemberName = _member.MemberName;

            await db.SaveChangesAsync();

            return Ok(member);
        }
        #endregion

        #region 삭제 API
        [HttpDelete("{id}")]
        public async Task<ActionResult> Delete(int id)
        {
            var member = await db.Members.FindAsync(id);
            if (member == null)
                return NotFound();

            db.Members.Remove(member);

            await db.SaveChangesAsync();

            return NoContent();
        }
        #endregion
    }
}
