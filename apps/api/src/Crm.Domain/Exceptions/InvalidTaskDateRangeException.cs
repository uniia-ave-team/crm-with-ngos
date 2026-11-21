using Crm.Domain.Consts;

namespace Crm.Domain.Exceptions;

/// <summary>
/// Exception thrown when a task's end date is earlier than its start date.
/// </summary>
public class InvalidTaskDateRangeException()
    : DomainValidationException(DomainErrorMessages.EndDateBeforeStartDate);
