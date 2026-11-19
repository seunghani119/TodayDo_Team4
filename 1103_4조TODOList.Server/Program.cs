//Program.cs
using Microsoft.EntityFrameworkCore;
using TodayDo.Server.Data;
using TodayDo.Server.Services;

#region 01 서버 생성과 환경 설정
var builder = WebApplication.CreateBuilder(args);

// 서버 프로젝트의 .env 파일을 찾습니다.
string envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");

// .env가 있으면 기존 환경변수를 덮어쓰지 않고 읽습니다.
if (File.Exists(envPath))
{
    DotNetEnv.Env.NoClobber().Load(envPath);
}

// 로딩한 환경변수를 .NET 설정에 반영합니다.
builder.Configuration.AddEnvironmentVariables();

#endregion

#region 02 API 문서 AI HTTP 서비스 등록
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<AiAdviceService>();
builder.Services.AddHttpClient<KmaForecastService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
}).RemoveAllLoggers();

#endregion

#region 03 MySQL DB 연결 등록
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

#endregion

#region 04 요청 처리 경로와 서버 실행
var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
#endregion
