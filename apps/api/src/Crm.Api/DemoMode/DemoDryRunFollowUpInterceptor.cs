using Crm.Application.Dtos.CustomRecords.Commands;
using Crm.Application.Dtos.CustomTables.Commands;
using Crm.Application.Dtos.CustomTemplates.Commands;
using Crm.Application.Dtos.LoginPageImage.Commands;
using Crm.Application.Dtos.Role.Commands;
using Crm.Application.Dtos.User.Commands;
using Crm.Domain.Exceptions;
using Mediator;

namespace Crm.Api.DemoMode;

/// <summary>
/// Tracks entity identifiers returned by dry-run creation commands and gracefully recovers
/// <see cref="NotFoundException"/> on subsequent dry-run mutations targeting those IDs
/// (as well as multi-step role claim operations), while preserving genuine <c>404 Not Found</c>
/// responses for unrelated unknown identifiers.
/// </summary>
/// <param name="idRegistry">The bounded registry tracking recently dry-run-created entity IDs.</param>
public sealed class DemoDryRunFollowUpInterceptor(DemoDryRunIdRegistry idRegistry) : IDemoCommandInterceptor
{
    private const string RoleKind = "Role";
    private const string CustomTableKind = "CustomTable";
    private const string CustomRecordKind = "CustomRecord";
    private const string TableTemplateKind = "TableTemplate";
    private const string LoginPageImageKind = "LoginPageImage";

    /// <inheritdoc />
    public void OnDryRunSucceeded(object message, object? response)
    {
        if (response is not Guid id || id == Guid.Empty)
        {
            return;
        }

        switch (message)
        {
            case CreateRoleCommand:
                idRegistry.Register(RoleKind, id);
                break;
            case CreateCustomTableCommand or CreateCustomTableFromTemplateCommand:
                idRegistry.Register(CustomTableKind, id);
                break;
            case CreateCustomRecordCommand:
                idRegistry.Register(CustomRecordKind, id);
                break;
            case CreateTableTemplateCommand:
                idRegistry.Register(TableTemplateKind, id);
                break;
            case CreateLoginPageImageCommand or UploadLoginPageImageCommand:
                idRegistry.Register(LoginPageImageKind, id);
                break;
            default:
                break;
        }
    }

    /// <inheritdoc />
    public ValueTask<(bool Handled, TResponse Response)> TryRecoverNotFoundAsync<TResponse>(
        object message,
        DemoExecutionMode mode,
        NotFoundException exception,
        CancellationToken cancellationToken)
    {
        if (mode != DemoExecutionMode.DryRun || typeof(TResponse) != typeof(Unit))
        {
            return ValueTask.FromResult((false, default(TResponse)!));
        }

        bool shouldRecover = message switch
        {
            AddClaimToRoleCommand or RemoveClaimFromRoleCommand => true,
            UpdateRoleCommand cmd => idRegistry.Contains(RoleKind, cmd.Id),
            DeleteRoleCommand cmd => idRegistry.Contains(RoleKind, cmd.Id),
            AssignUserRoleCommand cmd => idRegistry.Contains(RoleKind, cmd.RoleId),
            RemoveUserRoleCommand cmd => idRegistry.Contains(RoleKind, cmd.RoleId),
            UpdateCustomTableCommand cmd => idRegistry.Contains(CustomTableKind, cmd.Id),
            DeleteCustomTableCommand cmd => idRegistry.Contains(CustomTableKind, cmd.Id),
            UpdateCustomRecordCommand cmd => idRegistry.Contains(CustomRecordKind, cmd.Id),
            DeleteCustomRecordCommand cmd => idRegistry.Contains(CustomRecordKind, cmd.Id),
            UpdateTableTemplateCommand cmd => idRegistry.Contains(TableTemplateKind, cmd.Id),
            DeleteTableTemplateCommand cmd => idRegistry.Contains(TableTemplateKind, cmd.Id),
            DeleteLoginPageImageCommand cmd => idRegistry.Contains(LoginPageImageKind, cmd.Id),
            _ => false,
        };

        return shouldRecover
            ? ValueTask.FromResult((true, (TResponse)(object)Unit.Value))
            : ValueTask.FromResult((false, default(TResponse)!));
    }
}
