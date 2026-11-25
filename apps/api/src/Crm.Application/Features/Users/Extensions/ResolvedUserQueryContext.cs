using Crm.Application.Features.Users.Queries;
using Crm.Domain.Common;
using Crm.Domain.Enums;

namespace Crm.Application.Features.Users.Extensions;

/// <summary>
/// Holds the resolved sorting, JSONB search keys, schema ordering keys, and prechecked permissions
/// for executing <see cref="GetUsersQuery"/>.
/// </summary>
/// <param name="EffectiveOrderBy">The resolved built-in sort column, or <see langword="null"/> when sorting by a custom field or unauthorized field.</param>
/// <param name="CustomFieldSort">The resolved custom field key and data type when sorting by a visible custom field; otherwise, <see langword="null"/>.</param>
/// <param name="AllOrderedCustomFieldKeys">All custom field schema keys ordered by display order.</param>
/// <param name="PublicOrderedCustomFieldKeys">Public custom field schema keys ordered by display order.</param>
/// <param name="SearchablePublicCustomFieldKeys">Schema keys searchable across all users for the current search term.</param>
/// <param name="SearchablePrivateCustomFieldKeys">Private schema keys searchable only on the current user's own row.</param>
/// <param name="PrecheckedEmergencyContactAccess">The prechecked <see cref="AccessRight.ViewEmergencyContact"/> result if already evaluated.</param>
/// <param name="PrecheckedCustomFieldsAccess">The prechecked <see cref="AccessRight.ViewCustomFields"/> result if already evaluated.</param>
public sealed record ResolvedUserQueryContext(
    string? EffectiveOrderBy,
    UserCustomFieldSort? CustomFieldSort,
    string[] AllOrderedCustomFieldKeys,
    string[] PublicOrderedCustomFieldKeys,
    string[] SearchablePublicCustomFieldKeys,
    string[] SearchablePrivateCustomFieldKeys,
    bool? PrecheckedEmergencyContactAccess,
    bool? PrecheckedCustomFieldsAccess);
