using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FoodieProject.Helpers
{
    /// <summary>
    /// Tạo và xác thực JWT token.
    /// Cấu hình đọc từ Web.config (appSettings).
    /// NuGet: System.IdentityModel.Tokens.Jwt (>= 6.x)
    /// </summary>
    public static class JwtHelper
    {
        private static readonly string SecretKey = ConfigurationManager.AppSettings["Jwt:SecretKey"];
        private static readonly string Issuer = ConfigurationManager.AppSettings["Jwt:Issuer"];
        private static readonly string Audience = ConfigurationManager.AppSettings["Jwt:Audience"];
        private static readonly int ExpirationHours = int.Parse(ConfigurationManager.AppSettings["Jwt:ExpirationHours"] ?? "24");

        public static string GenerateToken(int userId, string username)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, username),
                new Claim("userId", userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat,
                    new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            };

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(ExpirationHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public static ClaimsPrincipal ValidateToken(string token)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(SecretKey);

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                SecurityToken validatedToken;
                return tokenHandler.ValidateToken(token, validationParameters, out validatedToken);
            }
            catch
            {
                return null;
            }
        }

        public static int GetUserIdFromPrincipal(ClaimsPrincipal principal)
        {
            var claim = principal.FindFirst("userId")
                     ?? principal.FindFirst(ClaimTypes.NameIdentifier)
                     ?? principal.FindFirst(JwtRegisteredClaimNames.Sub);

            if (claim != null && int.TryParse(claim.Value, out int userId))
                return userId;

            throw new UnauthorizedAccessException("Không tìm thấy UserId trong token.");
        }
    }
}
