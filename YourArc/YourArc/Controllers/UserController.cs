using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YourArc.Data;
using YourArc.Database;
using YourArc.Dtos;
using YourArc.Services;

namespace YourArc.Controllers;

[ApiController]
[Route("api/user")]
public class UserController(
    AppDbContext db,
    PasswordHasher<User> passwordHasher,
    ITokenService tokenService) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> CreateUser([FromBody] RegisterRequest registerUser)
    {
        var existingUser = await db.Users.FirstOrDefaultAsync(x => x.Email == registerUser.Email);

        if (existingUser != null)
        {
            return BadRequest(new
            {
                message = "Email already registered"
            });
        }

        var user = new User
        {
            Name = registerUser.Name,
            Email = registerUser.Email
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            registerUser.Password
        );

        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new
        {
            message = "Registration successful",
            user = new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email
            }
        });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest loginRequest)
    {
        var user = await db.Users.FirstOrDefaultAsync(x => x.Email == loginRequest.Email);

        if (user == null)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password"
            });
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            loginRequest.Password
        );

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new
            {
                message = "Invalid email or password"
            });
        }

        var token = tokenService.GenerateToken(user);

        return Ok(new
        {
            message = "Login successful",
            token,
            user = new
            {
                id = user.Id,
                name = user.Name,
                email = user.Email
            }
        });
    }
}
