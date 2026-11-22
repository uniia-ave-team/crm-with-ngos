using Crm.Application.Common.Consts;
using Crm.Application.Dtos.System;
using Crm.Application.Dtos.System.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Interfaces.Repositories;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Features.System.Queries;

/// <summary>
/// Handles the <see cref="GetSystemSetupStatusQuery"/> to determine the overall onboarding status,
/// application version, and active scheduled maintenance notice of the CRM.
/// </summary>
/// <param name="userRepository">The repository used to access user data.</param>
/// <param name="ngoRepository">The repository used to access NGO data.</param>
/// <param name="appVersionProvider">The provider returning the running application version.</param>
/// <param name="maintenanceNoticeCache">The short-lived cache providing the active maintenance notice.</param>
/// <param name="logger">The logger used to record the execution of the query.</param>
public sealed partial class GetSystemSetupStatusQueryHandler(
    IUserRepository userRepository,
    INgoRepository ngoRepository,
    IAppVersionProvider appVersionProvider,
    IMaintenanceNoticeCache maintenanceNoticeCache,
    ILogger<GetSystemSetupStatusQueryHandler> logger) : IRequestHandler<GetSystemSetupStatusQuery, SystemStatusDto>
{
    public async ValueTask<SystemStatusDto> Handle(GetSystemSetupStatusQuery request, CancellationToken cancellationToken)
    {
        LogCheckingSystemSetupStatus(logger);

        bool hasAdmin = await userRepository.AnyAsync(cancellationToken);
        bool hasNgo = await ngoRepository.AnyAsync(cancellationToken);
        var notice = await maintenanceNoticeCache.GetActiveNoticeAsync(cancellationToken);

        var status = new SystemStatusDto(
            HasAdmin: hasAdmin,
            HasNgo: hasNgo,
            IsSetupComplete: hasAdmin && hasNgo,
            Version: appVersionProvider.Version,
            Notice: notice);

        LogSystemSetupStatusResult(logger, status.HasAdmin, status.HasNgo, status.IsSetupComplete);

        return status;
    }

    [LoggerMessage(EventId = LogEventIds.CheckingSystemSetupStatus, Level = LogLevel.Information, Message = "Checking system setup status (Admin and NGO existence).")]
    private static partial void LogCheckingSystemSetupStatus(ILogger logger);

    [LoggerMessage(EventId = LogEventIds.SystemSetupStatusResult, Level = LogLevel.Information, Message = "System setup status check completed. HasAdmin: {HasAdmin}, HasNgo: {HasNgo}, IsSetupComplete: {IsSetupComplete}")]
    private static partial void LogSystemSetupStatusResult(ILogger logger, bool hasAdmin, bool hasNgo, bool isSetupComplete);
}
