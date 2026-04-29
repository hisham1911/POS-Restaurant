namespace KasserPro.API.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasserPro.Application.DTOs;
using KasserPro.Application.DTOs.Common;
using KasserPro.Application.Services.Interfaces;
using KasserPro.Domain.Enums;
using KasserPro.API.Middleware;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserManagementService _userManagementService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        IUserManagementService userManagementService,
        ILogger<UsersController> logger)
    {
        _userManagementService = userManagementService;
        _logger = logger;
    }

    [HttpGet]
    [HasPermission(Permission.UsersView)]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> GetAllUsers()
    {
        var result = await _userManagementService.GetAllUsersAsync();
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id}")]
    [HasPermission(Permission.UsersView)]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
    {
        var result = await _userManagementService.GetUserByIdAsync(id);
        return result.Success ? Ok(result) : NotFound(result);
    }

    [HttpPost]
    [HasPermission(Permission.UsersManage)]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserRequest request)
    {
        var result = await _userManagementService.CreateUserAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id}")]
    [HasPermission(Permission.UsersManage)]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
    {
        var result = await _userManagementService.UpdateUserAsync(id, request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id}")]
    [HasPermission(Permission.UsersManage)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
    {
        var result = await _userManagementService.DeleteUserAsync(id);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPatch("{id}/toggle-status")]
    [HasPermission(Permission.UsersManage)]
    public async Task<ActionResult<ApiResponse<bool>>> ToggleUserStatus(
        int id, 
        [FromBody] ToggleUserStatusRequest request)
    {
        var result = await _userManagementService.ToggleUserStatusAsync(id, request.IsActive);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
