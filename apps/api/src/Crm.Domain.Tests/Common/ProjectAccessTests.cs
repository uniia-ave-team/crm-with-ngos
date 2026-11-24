using Crm.Domain.Common;
using Crm.Domain.Entities;
using Crm.Domain.Enums;
using Crm.Domain.Exceptions;
using Shouldly;

namespace Crm.Domain.Tests.Common;

/// <summary>
/// Unit tests for <see cref="ProjectAccess"/> verifying strictly membership-based permissions
/// across private, public-view, and public projects.
/// </summary>
public class ProjectAccessTests
{
    [Fact]
    public void PrivateProjectWithoutMembershipShouldDenyViewEditAndManageWithNotFound()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var access = new ProjectAccess(ProjectVisibility.Private, Claim: null, userId);

        access.CanView.ShouldBeFalse();
        access.CanEdit.ShouldBeFalse();
        access.CanManage.ShouldBeFalse();

        Should.Throw<EntityNotFoundException<Project>>(() => access.EnsureHasClaim(projectId, ProjectClaim.View));
        Should.Throw<EntityNotFoundException<Project>>(() => access.EnsureHasClaim(projectId, ProjectClaim.Edit));
        Should.Throw<EntityNotFoundException<Project>>(() => access.EnsureHasClaim(projectId, ProjectClaim.Manage));
    }

    [Fact]
    public void PublicProjectWithoutMembershipShouldAllowViewAndEditButDenyManage()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var access = new ProjectAccess(ProjectVisibility.Public, Claim: null, userId);

        access.CanView.ShouldBeTrue();
        access.CanEdit.ShouldBeTrue();
        access.CanManage.ShouldBeFalse();

        Should.NotThrow(() => access.EnsureHasClaim(projectId, ProjectClaim.View));
        Should.NotThrow(() => access.EnsureHasClaim(projectId, ProjectClaim.Edit));
        Should.Throw<UnauthorizedAccessException>(() => access.EnsureHasClaim(projectId, ProjectClaim.Manage));
    }

    [Fact]
    public void MemberClaimsShouldGrantHierarchicalPermissions()
    {
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var viewer = new ProjectAccess(ProjectVisibility.Private, ProjectClaim.View, userId);
        viewer.CanView.ShouldBeTrue();
        viewer.CanEdit.ShouldBeFalse();
        viewer.CanManage.ShouldBeFalse();
        Should.Throw<UnauthorizedAccessException>(() => viewer.EnsureHasClaim(projectId, ProjectClaim.Edit));

        var editor = new ProjectAccess(ProjectVisibility.Private, ProjectClaim.Edit, userId);
        editor.CanView.ShouldBeTrue();
        editor.CanEdit.ShouldBeTrue();
        editor.CanManage.ShouldBeFalse();
        Should.Throw<UnauthorizedAccessException>(() => editor.EnsureHasClaim(projectId, ProjectClaim.Manage));

        var manager = new ProjectAccess(ProjectVisibility.Private, ProjectClaim.Manage, userId);
        manager.CanView.ShouldBeTrue();
        manager.CanEdit.ShouldBeTrue();
        manager.CanManage.ShouldBeTrue();
        Should.NotThrow(() => manager.EnsureHasClaim(projectId, ProjectClaim.Manage));
    }
}
