using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FoodieProject.Helpers
{
    /// <summary>
    /// MessageHandler xác thực JWT cho Web API pipeline.
    /// Các endpoint login/register được bỏ qua.
    /// Các endpoint GET cũng cho phép anonymous (trả về userId=0).
    /// </summary>
    public class JwtAuthHandler : DelegatingHandler
    {
        private static readonly string[] AnonymousPaths = new[]
        {
            "/api/auth/login",
            "/api/auth/register"
        };

        // Các endpoint cho phép anonymous access (GET)
        private static readonly string[] OptionalAuthPaths = new[]
        {
            "/api/posts/feed",
            "/api/posts/trending",
            "/api/posts/search",
            "/api/restaurants"
        };

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            string path = request.RequestUri.AbsolutePath.ToLower();

            // Bỏ qua xác thực cho login/register
            if (AnonymousPaths.Any(p => path.EndsWith(p)))
                return base.SendAsync(request, cancellationToken);

            // Không xác thực các request không phải /api/
            if (!path.StartsWith("/api/"))
                return base.SendAsync(request, cancellationToken);

            // Lấy token từ header
            var authHeader = request.Headers.Authorization;
            bool hasToken = authHeader != null
                         && authHeader.Scheme == "Bearer"
                         && !string.IsNullOrEmpty(authHeader.Parameter);

            if (hasToken)
            {
                // Xác thực token
                var principal = JwtHelper.ValidateToken(authHeader.Parameter);
                if (principal != null)
                {
                    Thread.CurrentPrincipal = principal;
                    if (System.Web.HttpContext.Current != null)
                        System.Web.HttpContext.Current.User = principal;

                    return base.SendAsync(request, cancellationToken);
                }

                // Token không hợp lệ
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"error\":\"Token không hợp lệ hoặc đã hết hạn.\"}")
                });
            }

            // Không có token
            // Cho phép anonymous cho GET requests hoặc các path optional
            if (request.Method == HttpMethod.Get ||
                OptionalAuthPaths.Any(p => path.StartsWith(p)))
            {
                return base.SendAsync(request, cancellationToken);
            }

            // GET /api/posts/{id} và GET /api/posts/{id}/comments
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/posts/"))
            {
                return base.SendAsync(request, cancellationToken);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"Token không được cung cấp.\"}")
            });
        }
    }
}
