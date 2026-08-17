using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace SmartLab.Server
{
    public class AuthTokenService
    {
        private readonly IConfiguration _configuration;

        public AuthTokenService(
            IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string CreateToken(User user)
        {
            string key =
                _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException(
                    "JWT signing key is missing.");

            string issuer =
                _configuration["Jwt:Issuer"]
                ?? "SmartLab";

            string audience =
                _configuration["Jwt:Audience"]
                ?? "SmartLab.Client";

            List<Claim> claims =
                new List<Claim>
                {
                    new Claim(
                        ClaimTypes.NameIdentifier,
                        user.UserId.ToString()),

                    new Claim(
                        ClaimTypes.Name,
                        user.Username),

                    new Claim(
                        ClaimTypes.Role,
                        user.Role)
                };

            SymmetricSecurityKey securityKey =
                new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key));

            SigningCredentials credentials =
                new SigningCredentials(
                    securityKey,
                    SecurityAlgorithms.HmacSha256);

            JwtSecurityToken token =
                new JwtSecurityToken(
                    issuer: issuer,
                    audience: audience,
                    claims: claims,
                    notBefore: DateTime.UtcNow,
                    expires: DateTime.UtcNow.AddHours(8),
                    signingCredentials: credentials);

            return new JwtSecurityTokenHandler()
                .WriteToken(token);
        }
    }
}
