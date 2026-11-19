using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodayDo.Server.Data;
using TodayDo.Server.Models;

namespace TodayDo.Server.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class TodoController : ControllerBase
    {
        #region 01 DB 연결과 컨트롤러 생성
        private readonly AppDbContext db;
        public TodoController(AppDbContext context)
        {
            db = context;
        }
        #endregion

        #region 02 일정 입력 검증
        private async Task<string?> ValidateTodoAsync(Todo todo)
        {
            if (string.IsNullOrWhiteSpace(todo.Title))
                return "제목을 입력해 주세요.";

            if (todo.TodoDate == default)
                return "활동 시작 날짜와 시간을 입력해 주세요.";

            if (todo.DueDate is DateTime end && end < todo.TodoDate)
                return "종료 시간은 시작 시간보다 빠를 수 없습니다.";

            if (todo.LocationID is int locationId &&
                !await db.Locations.AnyAsync(l => l.LocationID == locationId))
            {
                return "존재하지 않는 장소입니다.";
            }

            return null;
        }
        #endregion

        #region 조회 API
        [HttpGet("{memberId}")]
        public async Task<ActionResult<IEnumerable<Todo>>> GetAll(int memberId)
        {
            var todos = await db.Todos
                .AsNoTracking()
                .Where(t => t.MemberID == memberId)
                .OrderBy(t => t.TodoDate)
                .ToListAsync();

            return Ok(todos);
        }
        #endregion

        #region 등록 API
        [HttpPost("{memberId}")]
        public async Task<ActionResult<Todo>> Create(int memberId, Todo todo)
        {
            var member = await db.Members.FindAsync(memberId);

            if (member == null)
                return NotFound();

            string? error = await ValidateTodoAsync(todo);

            if (error != null)
                return BadRequest(new { message = error });

            todo.MemberID = memberId;
            todo.Title = todo.Title.Trim();

            db.Todos.Add(todo);

            await db.SaveChangesAsync();

            return Ok(todo);
        }
        #endregion

        #region 수정 API
        [HttpPut("{memberId}/{todoId}")]
        public async Task<ActionResult<Todo>> Update(int memberId, int todoId, Todo _todo)
        {
            var todo = await db.Todos.FindAsync(todoId);

            if (todo == null)
                return NotFound();

            if (todo.MemberID != memberId)

                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "해당 회원의 일정이 아닙니다." });

            string? error = await ValidateTodoAsync(_todo);

            if (error != null)
                return BadRequest(new { message = error });

            todo.Title = _todo.Title.Trim();
            todo.Content = _todo.Content;
            todo.TodoDate = _todo.TodoDate;
            todo.DueDate = _todo.DueDate;
            todo.IsCompleted = _todo.IsCompleted;
            todo.LocationID = _todo.LocationID;

            await db.SaveChangesAsync();

            return Ok(todo);
        }
        #endregion

        #region 삭제 API
        [HttpDelete("{memberId}/{todoId}")]
        public async Task<ActionResult> Delete(int memberId, int todoId)
        {
            var todo = await db.Todos.FindAsync(todoId);
            if (todo == null)
                return NotFound();

            if (todo.MemberID != memberId)
                return StatusCode(StatusCodes.Status403Forbidden,
                    new { message = "해당 회원의 일정이 아닙니다." });

            db.Todos.Remove(todo);

            await db.SaveChangesAsync();

            return NoContent();
        }
        #endregion
    }
}
