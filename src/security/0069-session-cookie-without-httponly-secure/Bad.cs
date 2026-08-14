// Exhibit #0069: a session cookie with default flags

using System.Net;

// After login, the server issues a session cookie.
var sessionCookie = new Cookie("session", "a1b2c3d4e5f6") // 💥 defaults: HttpOnly = false, Secure = false
{
    Path = "/",
};

Console.WriteLine($"Set-Cookie: {SetCookieHeader(sessionCookie)}");
Console.WriteLine($"HttpOnly={sessionCookie.HttpOnly}, Secure={sessionCookie.Secure}");

// Self-audit: a session cookie must be HttpOnly (hidden from JavaScript) and Secure (HTTPS-only).
if (!sessionCookie.HttpOnly || !sessionCookie.Secure)
{
    throw new InvalidOperationException(
        "the session cookie is not HttpOnly and Secure - without HttpOnly any XSS reads it via document.cookie, " +
        "and without Secure it is sent over plain HTTP; both default to false and must be set explicitly");
}

Console.WriteLine("Session cookie is HttpOnly and Secure.");

// Renders the Set-Cookie header the server would emit for this cookie.
static string SetCookieHeader(Cookie c)
{
    string header = $"{c.Name}={c.Value}; Path={c.Path}";
    if (c.Secure) header += "; Secure";
    if (c.HttpOnly) header += "; HttpOnly";
    return header;
}
