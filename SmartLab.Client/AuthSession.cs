using System.Net.Http;
using System.Net.Http.Headers;

namespace SmartLab.Client
{
    public static class AuthSession
    {
        public static string Token { get; private set; } =
            string.Empty;

        public static int UserId { get; private set; }

        public static string Username { get; private set; } =
            string.Empty;

        public static string Role { get; private set; } =
            string.Empty;

        public static bool IsAuthenticated =>
            !string.IsNullOrWhiteSpace(Token);

        public static void SetSession(
            string token,
            int userId,
            string username,
            string role)
        {
            Token = token ?? string.Empty;
            UserId = userId;
            Username = username ?? string.Empty;
            Role = role ?? string.Empty;
        }

        public static void Apply(
            HttpClient client)
        {
            client.DefaultRequestHeaders.Authorization =
                null;

            if (!string.IsNullOrWhiteSpace(Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        Token);
            }
        }

        public static void Clear()
        {
            Token = string.Empty;
            UserId = 0;
            Username = string.Empty;
            Role = string.Empty;
        }
    }
}
