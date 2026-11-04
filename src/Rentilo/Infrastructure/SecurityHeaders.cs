namespace Rentilo.Infrastructure;

public static class SecurityHeaders
{
    /// <summary>
    /// Adds a strict Content-Security-Policy and related headers. The app serves all scripts and styles
    /// itself and uses no inline script, so 'self' is enough.
    /// </summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["Content-Security-Policy"] =
                "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; " +
                "font-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'";
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            await next();
        });
}
