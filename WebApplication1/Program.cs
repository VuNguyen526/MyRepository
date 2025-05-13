using System.Net;
using System.Text.Json;
using static RequestLoggingMiddleware;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<TokenAuthenticationMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

var users = new List<User>
{
    new User { Name = "Alice", Age = 25 },
    new User { Name = "Bob", Age = 30 }
};

app.MapGet("/", () => Results.Ok("hello word!"));

// Test endpoint that requires authentication
app.MapGet("/protected", () => "Access granted to protected resource!");

app.MapGet("/test-error", () =>
{
    throw new Exception("Simulated error for testing!");
});

// GET: Retrieve all users
app.MapGet("/users", () => Results.Ok(users));

// GET: Retrieve a specific user by name
app.MapGet("/users/{name}", (string name) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (user is null)
            return Results.NotFound(new { Error = $"User '{name}' not found", StatusCode = 404 });

        return Results.Ok(user);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving user: {ex.Message}");
    }
});

app.MapPost("/users", (User user) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(user.Name))
            return Results.BadRequest("Name is required.");

        if (user.Age < 18 || user.Age > 100)
            return Results.BadRequest("Age must be between 18 and 100.");

        if (users.Any(u => u.Name.Equals(user.Name, StringComparison.OrdinalIgnoreCase)))
            return Results.BadRequest($"User '{user.Name}' already exists.");

        users.Add(user);
        return Results.Created($"/users/{user.Name}", user);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error adding user: {ex.Message}");
    }
});

// PUT: Update an existing user
app.MapPut("/users/{name}", (string name, User updatedUser) =>
{
    var index = users.FindIndex(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    if (index == -1)
        return Results.NotFound($"User '{name}' not found");

    // Validate Age before updating
    if (updatedUser.Age < 18 || updatedUser.Age > 100)
    {
        return Results.BadRequest("Age must be between 18 and 100.");
    }

    users[index] = updatedUser;
    return Results.Ok(updatedUser);
});


// DELETE: Remove a user by name
app.MapDelete("/users/{name}", (string name) =>
{
    var user = users.FirstOrDefault(u => u.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    if (user is null)
    {
        return Results.NotFound(new { Error = $"Cannot delete: User '{name}' not found", StatusCode = 404 });
    }

    users.Remove(user);
    return Results.Ok(new { Message = $"User '{name}' deleted successfully" });
});


// Start the app
app.Run();

public class User
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log Request Details
        _logger.LogInformation($"Incoming Request: {context.Request.Method} {context.Request.Path}");

        // Read Request Body
        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body);
        var requestBody = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        _logger.LogInformation($"Request Body: {requestBody}");

        // Capture Response
        var originalResponseStream = context.Response.Body;
        using var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await _next(context);

        // Read Response Body
        responseStream.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(responseStream).ReadToEndAsync();
        responseStream.Seek(0, SeekOrigin.Begin);

        _logger.LogInformation($"Response Status: {context.Response.StatusCode}");
        _logger.LogInformation($"Response Body: {responseBody}");

        await responseStream.CopyToAsync(originalResponseStream);
    }

    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context); // Process request
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception occurred.");
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var response = new
            {
                Error = "An unexpected error occurred.",
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Details = exception.Message // Avoid exposing full stack traces in production
            };

            var jsonResponse = JsonSerializer.Serialize(response);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            return context.Response.WriteAsync(jsonResponse);
        }
    }

    public class TokenAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TokenAuthenticationMiddleware> _logger;
        private const string SecretKey = "my_super_secret_key_12345"; // Should be stored securely

        public TokenAuthenticationMiddleware(RequestDelegate next, ILogger<TokenAuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (string.IsNullOrEmpty(token) || !ValidateToken(token))
            {
                _logger.LogWarning("Invalid or missing token.");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized: Invalid token.");
                return;
            }

            await _next(context);
        }

        private bool ValidateToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(SecretKey);

                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                }, out _);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public static class TokenHelper
    {
        private const string SecretKey = "my_super_secret_key_12345"; // Should be stored securely

        public static string GenerateToken(string username)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(SecretKey);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, username) }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

}
