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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalizedEmail = registerUser.Email.Trim().ToLowerInvariant();
        var normalizedName = registerUser.Name.Trim();

        var emailExists = await db.Users
            .AsNoTracking()
            .AnyAsync(x => x.Email == normalizedEmail);

        if (emailExists)
        {
            return BadRequest(new
            {
                message = "Email already registered"
            });
        }

        var user = new User
        {
            Name = normalizedName,
            Email = normalizedEmail
        };

        user.PasswordHash = passwordHasher.HashPassword(
            user,
            registerUser.Password
        );

        try
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return BadRequest(new
            {
                message = "Email already registered"
            });
        }

        var token = tokenService.GenerateToken(user);

        return Ok(new
        {
            message = "Registration successful",
            token,
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
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalizedEmail = loginRequest.Email.Trim().ToLowerInvariant();

        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail);

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

