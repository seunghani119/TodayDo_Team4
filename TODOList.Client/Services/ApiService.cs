using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TODOList.Client.Models;
using System.Text.Json.Nodes;
using System.Threading;

namespace TODOList.Client.Services
{
    public static class ApiService
    {
        #region 01 용도별 HTTP 클라이언트
        private static readonly HttpClient client = new HttpClient()
        {
            BaseAddress = new Uri("https://localhost:7274/"),
            Timeout = TimeSpan.FromSeconds(15)
        };

        // 날씨 요청은 기상청 응답을 기다릴 수 있도록 별도로 설정
        private static readonly HttpClient weatherClient = new HttpClient()
        {
            BaseAddress = client.BaseAddress,
            Timeout = TimeSpan.FromSeconds(60)
        };

        // AI 요청은 날씨 조회와 AI 답변 생성까지 기다립니다.
        private static readonly HttpClient adviceClient = new HttpClient()
        {
            BaseAddress = client.BaseAddress,
            Timeout = TimeSpan.FromSeconds(180)
        };
        #endregion

        #region 02 AI 조언 API
        // 회원 번호와 일정 번호로 AI 조언을 요청합니다.
        public static async Task<JsonObject> GetTodoAdviceAsync(int memberId, int todoId, CancellationToken cancellationToken = default)
        {
            using var response = await adviceClient.PostAsync(
                $"api/Weather/todos/{memberId}/{todoId}/advice",
                null,
                cancellationToken);

            return await ReadResponseAsync<JsonObject>(
                response, "AI 조언 응답을 읽지 못했습니다.", cancellationToken);
        }
        #endregion

        #region 03 회원가입과 로그인 API
        public static async Task<MemberInfo> RegisterAsync(string memberName, string password)
        {
            var request = new
            {
                memberName = memberName,
                memberPW = password
            };

            // 회원가입 API에 POST 요청을 보냅니다.
            using HttpResponseMessage response = await client.PostAsJsonAsync("api/Member", request);

            return await ReadResponseAsync<MemberInfo>(response, "서버에서 회원 정보를 받지 못했습니다.");
        }

        public static async Task<MemberInfo> LoginAsync(string memberName, string password)
        {
            var request = new
            {
                memberName = memberName,
                memberPW = password
            };

            // 로그인 API에 POST 요청을 보냅니다.
            using HttpResponseMessage response = await client.PostAsJsonAsync("api/Member/login", request);

            //이름과 비밀번호가 일치하지 않는 경우
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new InvalidOperationException("가입명 또는 비밀번호가 일치하지 않습니다.");
            }

            return await ReadResponseAsync<MemberInfo>(response, "서버에서 회원 정보를 받지 못했습니다.");
        }
        #endregion

        #region 04 장소 조회와 지도 장소 저장 API
        //지역 목록 조회
        public static async Task<List<LocationInfo>> GetLocationsAsync()
        {
            using var response = await client.GetAsync("api/Location");

            return await ReadResponseAsync<List<LocationInfo>>(response, "지역 목록을 읽지 못했습니다.");
        }

        //지도에서 고른 위치를 서버에 보내고, 저장된 장소 번호를 받아오는 역할
        public static async Task<LocationInfo> CreateMapLocationAsync(MapLocationInfo location)
        {
            var request = new
            {
                locationName = location.LocationName,
                address = location.Address,
                latitude = location.Latitude,
                longitude = location.Longitude
            };

            using var response = await client.PostAsJsonAsync("api/Location/map", request);

            return await ReadResponseAsync<LocationInfo>(response, "서버에서 장소 정보를 받지 못했습니다.");
        }
        #endregion

        #region 05 일정 조회 등록 수정 삭제 API
        // 로그인한 회원의 일정 목록 조회
        public static async Task<List<TodoInfo>> GetTodosAsync(int memberId)
        {
            using var response = await client.GetAsync($"api/Todo/{memberId}");

            return await ReadResponseAsync<List<TodoInfo>>(response, "일정 목록을 읽지 못했습니다.");
        }

        // 일정 등록
        public static async Task CreateTodoAsync(int memberId, TodoInfo todo)
        {
            using var response = await client.PostAsJsonAsync($"api/Todo/{memberId}", todo);

            await CheckResponseAsync(response);
        }

        // 일정 수정
        public static async Task UpdateTodoAsync(int memberId, int todoId, TodoInfo todo)
        {
            using var response = await client.PutAsJsonAsync($"api/Todo/{memberId}/{todoId}", todo);

            await CheckResponseAsync(response);
        }

        // 일정 삭제
        public static async Task DeleteTodoAsync(int memberId, int todoId)
        {
            using var response = await client.DeleteAsync($"api/Todo/{memberId}/{todoId}");

            await CheckResponseAsync(response);
        }
        #endregion

        #region 06 일정 날씨 조회 API
        // 선택한 일정의 실제 날씨 조회
        public static async Task<JsonObject> GetTodoWeatherAsync(int memberId, int todoId, CancellationToken cancellationToken = default)
        {
            using var response = await weatherClient.GetAsync($"api/Weather/todos/{memberId}/{todoId}", cancellationToken);

            return await ReadResponseAsync<JsonObject>(
                response, "서버에서 날씨 정보를 받지 못했습니다.", cancellationToken);
        }
        #endregion

        #region 07 공통 응답과 HTTP 오류 처리
        // 조회 API는 성공 확인과 JSON 읽기를 같은 순서로 처리합니다.
        private static async Task<T> ReadResponseAsync<T>(
            HttpResponseMessage response, string emptyMessage, CancellationToken cancellationToken = default)
            where T : class
        {
            await CheckResponseAsync(response, cancellationToken);

            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken)
                ?? throw new InvalidOperationException(emptyMessage);
        }

        // HTTP 요청의 성공 여부 확인
        private static async Task CheckResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (response.IsSuccessStatusCode)
                return;

            string? message = null;
            try
            {
                var error = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: cancellationToken);
                message = error?["message"]?.ToString()
                    ?? error?["detail"]?.ToString()
                    ?? error?["title"]?.ToString();
            }
            catch (System.Text.Json.JsonException)
            {
                // HTML·빈 응답 등의 오류 본문은 HTTP 상태 코드로 안내합니다.
            }

            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? $"서버 요청 실패: HTTP {(int)response.StatusCode} ({response.ReasonPhrase})"
                    : message);
        }
        #endregion

    }
}
