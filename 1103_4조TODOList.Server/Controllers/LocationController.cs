using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TodayDo.Server.Data;
using TodayDo.Server.Models;
using TodayDo.Server.Services;

namespace TodayDo.Server.Controllers
{
    [ApiController]
    [Route("/api/[controller]")]
    public class LocationController : ControllerBase
    {
        #region 01 DB 연결과 컨트롤러 생성
        private readonly AppDbContext db;
        public LocationController(AppDbContext context)
        {
            db = context;
        }
        #endregion

        #region 조회 API
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Location>>> GetAll()
        {
            var locations = await db.Locations.AsNoTracking().OrderBy(l => l.LocationID).ToListAsync();
            return Ok(locations);
        }
        #endregion

        #region 등록 API
        [HttpPost("map")]
        public async Task<ActionResult<Location>> CreateMap(Location request, CancellationToken cancellationToken)
        {
            string name = (request.LocationName ?? "").Trim();
            string address = (request.Address ?? "").Trim();


            if (name.Length == 0 || name.Length > 200 || address.Length == 0 || address.Length > 500)
            {
                return BadRequest(new
                {
                    message = "장소 이름(1~200자)과 주소(1~500자)를 확인해 주세요."
                });
            }

            if (request.Latitude is not double latitude || request.Longitude is not double longitude)
            {
                return BadRequest(new
                {
                    message = "위도와 경도가 필요합니다."
                });
            }

            int nx;
            int ny;

            try
            {
                (nx, ny) = KmaGridConverter.ToGrid(latitude, longitude);
            }
            catch (ArgumentOutOfRangeException)
            {
                return BadRequest(new
                {
                    message = "기상청 단기예보 범위 안의 위치를 선택해 주세요."
                });
            }

            var existing = await db.Locations
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.Latitude == latitude &&
                         l.Longitude == longitude &&
                         l.LocationName == name,
                    cancellationToken);

            if (existing != null)
                return Ok(existing);

            var location = new Location
            {
                LocationName = name,
                Address = address,
                Latitude = latitude,
                Longitude = longitude,
                Nx = nx,
                Ny = ny
            };

            db.Locations.Add(location);
            await db.SaveChangesAsync(cancellationToken);

            return Ok(location);
        }
        #endregion

        #region 수정 API
        [HttpPut("{id}")]
        public async Task<ActionResult<Location>> Update(int id, Location _location)
        {
            var location = await db.Locations.FindAsync(id);
            if (location == null)
                return NotFound();
            location.LocationName = _location.LocationName;

            await db.SaveChangesAsync();
            return Ok(location);
        }
        #endregion

        #region 삭제 API
        [HttpDelete("{id}")]
        public async Task<ActionResult<Location>> Delete(int id)
        {
            var location = await db.Locations.FindAsync(id);
            if (location == null)
                return NotFound();

            if (await db.Todos.AnyAsync(t => t.LocationID == id))
                return Conflict(new { message = "일정에서 사용 중인 장소는 삭제할 수 없습니다." });

            db.Locations.Remove(location);
            await db.SaveChangesAsync();
            return NoContent();
        }
        #endregion
    }
}
