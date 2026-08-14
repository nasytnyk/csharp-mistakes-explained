// Exhibit #0069: the fix

using System.Net;

// After login, the server issues a session cookie - with its security flags set.
var sessionCookie = new Cookie("session", "a1b2c3d4e5f6")
{
    Path = "/",
    HttpOnly = true, // invisible to document.cookie - an XSS bug cannot read it
    Secure = true,   // only ever sent over HTTPS
};

Console.WriteLine($"Set-Cookie: {SetCookieHeader(sessionCookie)}");
Console.WriteLine($"HttpOnly={sessionCookie.HttpOnly}, Secure={sessionCookie.Secure}");

// Self-audit: a session cookie must be HttpOnly (hidden from JavaScript) and Secure (HTTPS-only).
if (!sessionCookie.HttpOnly || !sessionCookie.Secure)
{
    throw new InvalidOperationException("the session cookie is not HttpOnly and Secure");
}

Console.WriteLine("Session cookie is HttpOnly and Secure. As it should be.");

// Renders the Set-Cookie header the server would emit for this cookie.
static string SetCookieHeader(Cookie c)
{
    string header = $"{c.Name}={c.Value}; Path={c.Path}";
    if (c.Secure) header += "; Secure";
    if (c.HttpOnly) header += "; HttpOnly";
    return header;
}
