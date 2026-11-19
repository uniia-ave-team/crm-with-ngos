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
        config.NewConfig<Project, ProjectSummaryDto>();

        config.NewConfig<ProjectMember, ProjectMemberDto>()
            .MapToConstructor(true)
            .Map(dest => dest.FirstName, src => src.User.FirstName)
            .Map(dest => dest.LastName, src => src.User.LastName)
            .Map(dest => dest.Patronymic, src => src.User.Patronymic)
            .Map(dest => dest.AvatarUrl, src => src.User.Avatar);

        config.NewConfig<Topic, TopicDto>();
        config.NewConfig<Tag, TagDto>();

        config.NewConfig<Project, ProjectDto>()
            .MapToConstructor(true)
            .Map(dest => dest.Members, src => src.Members.OrderBy(m => m.User.LastName).ThenBy(m => m.User.FirstName))
            .Map(dest => dest.Topics, src => src.Topics.OrderBy(t => t.Order).ThenBy(t => t.Title))
            .Map(dest => dest.Tags, src => src.Tags.OrderBy(t => t.Title));

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
