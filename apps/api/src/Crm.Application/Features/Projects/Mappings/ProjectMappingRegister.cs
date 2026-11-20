using Crm.Application.Dtos.Project;
using Crm.Application.Dtos.Project.Commands;
using Crm.Domain.Entities;
using Mapster;

namespace Crm.Application.Features.Projects.Mappings;

/// <summary>
/// Registers Mapster mapping configurations for projects, members, topics, and tags,
/// including SQL-translatable <c>ProjectToType</c> projections and command-to-entity mappings.
/// </summary>
public sealed class ProjectMappingRegister : IRegister
{
    /// <inheritdoc />
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Project, ProjectSummaryDto>()
            .MapWith(src => new ProjectSummaryDto(
                src.Id,
                src.Title,
                src.Description,
                src.Visibility,
                src.StartDate,
                src.EndDate,
                src.CreatedBy));

        config.NewConfig<ProjectMember, ProjectMemberDto>()
            .MapWith(src => new ProjectMemberDto(
                src.ProjectId,
                src.UserId,
                src.User.FirstName,
                src.User.LastName,
                src.User.Patronymic,
                src.User.Avatar,
                src.RoleTitle,
                src.Claim));

        config.NewConfig<Topic, TopicDto>()
            .MapWith(src => new TopicDto(
                src.Id,
                src.ProjectId,
                src.Title,
                src.Order));

        config.NewConfig<Tag, TagDto>()
            .MapWith(src => new TagDto(
                src.Id,
                src.ProjectId,
                src.Title));

        config.NewConfig<Project, ProjectDto>()
            .MapWith(src => new ProjectDto(
                src.Id,
                src.Title,
                src.Description,
                src.Visibility,
                src.StartDate,
                src.EndDate,
                src.CreatedBy,
                src.Members
                    .OrderBy(m => m.User.LastName)
                    .ThenBy(m => m.User.FirstName)
                    .Select(m => new ProjectMemberDto(
                        m.ProjectId,
                        m.UserId,
                        m.User.FirstName,
                        m.User.LastName,
                        m.User.Patronymic,
                        m.User.Avatar,
                        m.RoleTitle,
                        m.Claim))
                    .ToList(),
                src.Topics
                    .OrderBy(t => t.Order)
                    .ThenBy(t => t.Title)
                    .Select(t => new TopicDto(
                        t.Id,
                        t.ProjectId,
                        t.Title,
                        t.Order))
                    .ToList(),
                src.Tags
                    .OrderBy(t => t.Title)
                    .Select(t => new TagDto(
                        t.Id,
                        t.ProjectId,
                        t.Title))
                    .ToList()));

        config.NewConfig<CreateProjectCommand, Project>()
            .Ignore(dest => dest.Id)
            .Map(dest => dest.Title, src => src.Title.Trim())
            .Map(dest => dest.Description, src => src.Description == null ? null : src.Description.Trim());

        config.NewConfig<UpdateProjectCommand, Project>()
            .Ignore(dest => dest.Id)
            .Map(dest => dest.Title, src => src.Title.Trim())
            .Map(dest => dest.Description, src => src.Description == null ? null : src.Description.Trim());

        config.NewConfig<AssignProjectMemberCommand, ProjectMember>()
            .Map(dest => dest.RoleTitle, src => (src.RoleTitle ?? string.Empty).Trim());

        config.NewConfig<UpdateProjectMemberCommand, ProjectMember>()
            .Ignore(dest => dest.ProjectId)
            .Ignore(dest => dest.UserId)
            .Map(dest => dest.RoleTitle, src => (src.RoleTitle ?? string.Empty).Trim());

        config.NewConfig<CreateTopicCommand, Topic>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.NormalizedTitle)
            .Map(dest => dest.Title, src => src.Title.Trim());

        config.NewConfig<UpdateTopicCommand, Topic>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.ProjectId)
            .Ignore(dest => dest.NormalizedTitle)
            .Map(dest => dest.Title, src => src.Title.Trim());

        config.NewConfig<CreateTagCommand, Tag>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.NormalizedTitle)
            .Map(dest => dest.Title, src => src.Title.Trim());

        config.NewConfig<UpdateTagCommand, Tag>()
            .Ignore(dest => dest.Id)
            .Ignore(dest => dest.ProjectId)
            .Ignore(dest => dest.NormalizedTitle)
            .Map(dest => dest.Title, src => src.Title.Trim());
    }
}
