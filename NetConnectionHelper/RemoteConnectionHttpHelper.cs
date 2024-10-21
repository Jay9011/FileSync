using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using NetConnectionHelper.Interface;

namespace NetConnectionHelper
{
    public class RemoteConnectionHttpHelper : IRemoteConnectionHelper
    {
        private readonly HttpClient _httpClient;

        public RemoteConnectionHttpHelper()
        {
            _httpClient = new HttpClient();
        }
        
        /// <summary>
        /// 연결 테스트
        /// </summary>
        /// <param name="server">원격지 서버 주소</param>
        /// <param name="username">사용자 이름</param>
        /// <param name="password">비밀번호</param>
        /// <returns>연결 성공 여부</returns>
        public async Task<(bool, string)> ConnectionAsync(string server, string username, string password)
        {
            try
            {
                // 헤더 설정
                var byteArray = Encoding.ASCII.GetBytes($"{username}:{password}");
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
                
                // GET 요청
                var response = await _httpClient.GetAsync(server);
                
                // 응답 확인
                return (response.IsSuccessStatusCode, "connection successful");
            }
            catch (HttpRequestException e)
            {
                return (false, e.Message);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        public string GetRightPath(string server)
        {
            throw new NotImplementedException();
        }
    }

}
