using Crm.Application.Dtos.Users;
using Crm.Application.Features.Users.Queries;
using Crm.Application.Interfaces;
using Crm.Domain.Common;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;
using Crm.Domain.Interfaces.Repositories;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Provides query resolution extensions for <see cref="GetUsersQuery"/> to keep <see cref="GetUsersQueryHandler"/> thin.
/// </summary>
public static class UserQueryExtensions
{
    /// <summary>
    /// Loads the global user custom field schema once, resolves built-in vs custom-field sorting,
    /// partitions searchable keys by visibility, and prechecks only the permissions needed before querying users.
    /// </summary>
    /// <param name="query">The incoming paged users query.</param>
    /// <param name="permissionService">The permission service for checking sensitive field rights.</param>
    /// <param name="userCustomFieldRepository">The repository used to load global custom field definitions.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ResolvedUserQueryContext"/> ready for repository execution and response enrichment.</returns>
    public static async Task<ResolvedUserQueryContext> ResolveQueryContextAsync(
        this GetUsersQuery query,
        IPermissionService permissionService,
        IUserCustomFieldRepository userCustomFieldRepository,
        CancellationToken cancellationToken = default)
    {
        bool isOrderingByEmergencyContact = string.Equals(
            query.OrderBy?.Trim(),
            nameof(UserDto.EmergencyContact),
            StringComparison.OrdinalIgnoreCase);

        bool? precheckedEmergencyContactAccess = isOrderingByEmergencyContact
            ? await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewEmergencyContact,
                cancellationToken)
            : null;

        string? effectiveOrderBy = isOrderingByEmergencyContact && precheckedEmergencyContactAccess == false
            ? null
            : query.OrderBy;

        bool isBuiltInSort = UserCustomFieldExtensions.IsBuiltInSortField(effectiveOrderBy);
        bool isOrderingByCustomField = !isBuiltInSort
            && UserCustomFieldExtensions.ExtractSortKey(effectiveOrderBy) is not null;

        var schemaFields = await userCustomFieldRepository.GetAllOrderedAsync(cancellationToken);
        var (allKeys, publicKeys, privateKeys) = schemaFields.PartitionSchemaKeys();

        bool hasSearchTerm = !string.IsNullOrWhiteSpace(query.SearchTerm);
        bool needsCustomFieldsAccessEarly = privateKeys.Length > 0 && (isOrderingByCustomField || hasSearchTerm);

        bool? precheckedCustomFieldsAccess = needsCustomFieldsAccessEarly
            ? await permissionService.HasAccessAsync(
                query.CurrentUserRoleIds,
                AccessRight.ViewCustomFields,
                cancellationToken)
            : null;

        UserCustomFieldSort? customFieldSort = null;
        if (isOrderingByCustomField)
        {
            if (schemaFields.Count > 0)
            {
                var visibleFields = precheckedCustomFieldsAccess == true
                    ? schemaFields
                    : schemaFields.Where(f => f.IsPublic);

                customFieldSort = visibleFields.ResolveOrdering(effectiveOrderBy);
            }

            effectiveOrderBy = null;
        }
        else if (!isBuiltInSort)
        {
            effectiveOrderBy = null;
        }

        string[] searchablePublicKeys = [];
        string[] searchablePrivateKeys = [];
        if (hasSearchTerm && schemaFields.Count > 0)
        {
            if (precheckedCustomFieldsAccess == true)
            {
                searchablePublicKeys = allKeys;
            }
            else
            {
                searchablePublicKeys = publicKeys;
                searchablePrivateKeys = privateKeys;
            }
        }

        return new ResolvedUserQueryContext(
            effectiveOrderBy,
            customFieldSort,
            allKeys,
            publicKeys,
            searchablePublicKeys,
            searchablePrivateKeys,
            precheckedEmergencyContactAccess,
            precheckedCustomFieldsAccess);
    }

    /// <summary>
    /// Resolves any sensitive-field permissions not already prechecked in <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The previously resolved query context.</param>
    /// <param name="roleIds">The requesting user's role identifiers.</param>
    /// <param name="permissionService">The permission service used to evaluate missing checks.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A tuple containing emergency contact and custom fields visibility access flags.</returns>
    public static async Task<(bool HasEmergencyContactAccess, bool HasCustomFieldsAccess)> ResolveRemainingPermissionsAsync(
        this ResolvedUserQueryContext context,
        List<Guid> roleIds,
        IPermissionService permissionService,
        CancellationToken cancellationToken = default)
    {
        bool hasEmergencyContactAccess = context.PrecheckedEmergencyContactAccess
            ?? await permissionService.HasAccessAsync(roleIds, AccessRight.ViewEmergencyContact, cancellationToken);

        bool hasCustomFieldsAccess = context.PrecheckedCustomFieldsAccess
            ?? (context.AllOrderedCustomFieldKeys.Length > context.PublicOrderedCustomFieldKeys.Length
                && await permissionService.HasAccessAsync(roleIds, AccessRight.ViewCustomFields, cancellationToken));

        return (hasEmergencyContactAccess, hasCustomFieldsAccess);
    }
}
