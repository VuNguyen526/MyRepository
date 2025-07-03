using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebApplication3.Model;
using WebApplication3.Services;

namespace WebApplication3.Controllers.cs
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly TokenService _tokenService;

        private readonly string secretKey = "SuperSecretKeyForJwtTokenAuthorization123456789";

        private static readonly Dictionary<string, string> Users = new()
        {
            { "user1", "password1" },
            { "admin", "password2" }
        };

        public UserController(TokenService tokenService)
        {
            _tokenService = tokenService;
        }
    
        [HttpPost("login")]
        public IActionResult Login2([FromBody] UserRegistrationDto request)
        {
            if (Users.TryGetValue(request.Username, out var password) && password == request.Password)
            {
                var token = _tokenService.GenerateToken(request.Username, request.Role);
                return Ok(new { Token = token });
            }
            return Unauthorized();
        }


        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public IActionResult GetAdminData()
        {
            return Ok("This is Admin data");
        }

        [HttpGet("user")]
        [Authorize(Roles = "User")]
        public IActionResult GetUserData()
        {
            return Ok("This is User data");
        }

        [HttpGet]
        [Authorize]
        public IActionResult GetSecureData()
        {
            return Ok(new { Message = "This is a secure endpoint." });
        }
    }


}
