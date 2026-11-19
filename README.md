# TodayDo 🌤️

> 일정과 날씨를 한 번에. 활동 장소의 기상청 예보를 일정에 연결하고, AI가 준비물과 대체 활동을 조언해 주는 일정 관리 프로그램

**2026년도 하반기 SW중심대학 오픈소스 AI·SW 활용 경진대회** · 프로토타입 분야 · 비트고급 4조

---

## 주요 기능

| 기능 | 설명 |
|---|---|
| 📝 개인 일정 관리 | 회원별 일정 등록·수정·삭제·완료 처리 |
| 🔍 일정 검색·조회 | 날짜별 검색, 지난 일정과 상세 내용 확인 |
| 🗺️ 지도 기반 장소 선택 | 장소·주소 검색 또는 지도 클릭으로 활동 위치 설정 |
| 🌦️ 일정별 날씨 | 활동 시간·지역의 기온, 강수확률, 강수량 조회 |
| 🔔 자동 갱신·변경 알림 | 예보를 주기적으로 갱신하고 바뀐 내용을 앱 안에서 알림 |
| 🤖 AI 맞춤 조언 | 일정과 날씨를 분석해 활동 적합성·준비물·대체 활동 제안 |

## 기술 스택

| 구분 | 사용 기술 |
|---|---|
| 클라이언트 | C# · WPF (.NET 9) · WebView2 |
| 서버 | ASP.NET Core Web API (.NET 9) · Entity Framework Core 9 · Swagger |
| 데이터베이스 | MySQL (Pomelo.EntityFrameworkCore.MySql) |
| 지도 | Kakao Maps JavaScript API |
| 날씨 | 기상청 단기예보 조회서비스 (공공데이터포털) |
| AI | Claude API (`claude-sonnet-4-5`) |

## 구조

```
WPF 클라이언트 ──HTTP──▶ ASP.NET Core 서버 ──▶ MySQL
   (일정·지도 화면)          │
                             ├──▶ 기상청 단기예보 API
                             └──▶ Claude API (AI 조언)
```

```
📁 1103_4조TODOList
├── 1103_4조TODOList.Server/   # 서버 (API, DB, 날씨·AI 연동)
│   ├── Controllers/            # Location · Member · Todo · Weather
│   ├── Services/               # 기상청 예보, AI 조언, 날씨 표시
│   ├── Service(좌표변환)/       # 위경도 → 기상청 격자 좌표 변환
│   ├── Models/  Data/  Migrations/
│   └── postman/                # API 테스트 컬렉션
├── TODOList.Client/            # WPF 클라이언트 (로그인·일정·지도 화면)
├── RegressionChecks/           # 회귀 테스트
└── docs/                       # 발표 정리 노트, 작업 계획
```

## API

| 메서드 | 경로 | 설명 |
|---|---|---|
| POST | `/api/Member` | 회원가입 |
| POST | `/api/Member/login` | 로그인 |
| GET · PUT · DELETE | `/api/Member`, `/api/Member/{id}` | 회원 조회·수정·삭제 |
| GET · POST | `/api/Todo/{memberId}` | 일정 목록 조회 · 등록 |
| PUT · DELETE | `/api/Todo/{memberId}/{todoId}` | 일정 수정 · 삭제 |
| GET · POST | `/api/Location`, `/api/Location/map` | 장소 조회 · 지도에서 선택한 장소 저장 |
| PUT · DELETE | `/api/Location/{id}` | 장소 수정 · 삭제 |
| GET | `/api/Weather/todos/{memberId}/{todoId}` | 일정의 날씨 조회 |
| POST | `/api/Weather/todos/{memberId}/{todoId}/advice` | AI 조언 받기 |

서버 실행 후 Swagger(`/swagger`)에서 직접 호출해 볼 수 있습니다.

## 실행 방법

**필요한 것:** Windows · Visual Studio 2022 · .NET 9 SDK · MySQL · 기상청 API 키 · Claude API 키

**1. 받기**

```bash
git clone https://github.com/seunghani119/TodayDo_Team4.git
```

**2. 키 설정** — `1103_4조TODOList.Server/.env.example`을 복사해 `.env`로 만들고 값을 넣습니다.

```
AiApi__ApiKey="Claude API 키"
WeatherApi__ServiceKey="공공데이터포털 기상청 서비스 키"
```

**3. DB 설정** — 같은 폴더에 `appsettings.json`을 만듭니다. (비밀번호가 들어가서 저장소에는 올리지 않습니다)

```json
{
  "AiApi": { "Model": "claude-sonnet-4-5" },
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;port=3306;database=todaydo;user=아이디;password=비밀번호;"
  },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*"
}
```

**4. DB 만들기**

```bash
cd 1103_4조TODOList.Server
dotnet ef database update
```

**5. 실행** — Visual Studio에서 `1103_4조TODOList.sln`을 열고 서버 → 클라이언트 순서로 실행합니다.

## 팀원

| 이름 | 학과 | 역할 |
|---|---|---|
| 이승한 (팀장) | AI·빅데이터학과 | 기획 · 서버 API · DB 설계 · AI 조언 연동 · 발표 |
| 오동근 | 컴퓨터정보보안전공 | WPF 클라이언트 화면 (로그인·일정 목록·상세) |
| 나성원 | AI·빅데이터학과 | 지도 장소 선택 · 기상청 예보 연동 · 테스트 |

## 문서

- [발표 정리 노트](docs/TodayDo_발표정리노트.md)
- [발표 판넬 다음 작업 계획](docs/발표판넬_다음작업계획.md)
