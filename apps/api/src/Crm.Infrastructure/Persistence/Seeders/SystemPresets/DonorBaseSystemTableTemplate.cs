using Crm.Domain.Entities;
using Crm.Domain.ValueObjects;

namespace Crm.Infrastructure.Persistence.Seeders.SystemPresets;

/// <summary>
/// System preset definition for the "База донорів" table template.
/// </summary>
public class DonorBaseSystemTableTemplate : ISystemTableTemplate
{
    /// <inheritdoc />
    public TableTemplate GetTemplate()
    {
        return new TableTemplate
        {
            Name = "База донорів",
            Description = "Стандартний шаблон для ведення обліку грантодавців, донорських організацій та програм підтримки.",
            IsSystemPreset = true,
            Fields =
            [
                new TemplateFieldDefinition("Назва", isRequired: true, order: 0),
                new TemplateFieldDefinition("Напрямки та пріоритети", isRequired: false, order: 1),
                new TemplateFieldDefinition("Особливості подання", isRequired: false, order: 2),
                new TemplateFieldDefinition("Терміни", isRequired: false, order: 3),
                new TemplateFieldDefinition("Контакти", isRequired: false, order: 4),
            ],
        };
    }
}
