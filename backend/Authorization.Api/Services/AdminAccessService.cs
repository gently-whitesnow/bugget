using System;
using System.Threading.Tasks;
using Authorization.Api.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Authorization.Api.Services;

public sealed class AdminAccessService(
    IUsersService usersService,
    IHostEnvironment hostEnvironment,
    ILogger<AdminAccessService> logger)
{
    public async Task<bool> HasAccessAsync(long userId)
    {
        if (hostEnvironment.IsDevelopment())
        {
            return true;
        }

        try
        {
            return await usersService.IsAdminAsync(userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve admin access for user {UserId}", userId);
            return false;
        }
    }
}
