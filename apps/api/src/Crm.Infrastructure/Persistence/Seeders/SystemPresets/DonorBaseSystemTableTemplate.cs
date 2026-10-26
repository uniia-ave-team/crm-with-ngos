using Crm.Domain.Entities;
using Crm.Domain.Enums;

namespace Crm.Infrastructure.Persistence.Seeders.SystemPresets;

/// <summary>
/// System preset definition for the "База донорів" table template.
/// </summary>
public class DonorBaseSystemTableTemplate : ISystemTableTemplate
{
    public TableTemplate GetTemplate()
    {
        return new TableTemplate
        {
            Name = "База донорів",
            Description = "Стандартний шаблон для ведення обліку грантодавців, донорських організацій та програм підтримки.",
            IsSystemPreset = true,
            Fields =
            [
                new()
                {
                    Name = "Назва",
                    Key = "name",
                    DataType = FieldDataType.Text,
                    IsRequired = true,
                    Order = 0,
                },
                new()
                {
                    Name = "Напрямки та пріоритети",
                    Key = "priorities",
                    DataType = FieldDataType.Text,
                    IsRequired = false,
                    Order = 1,
                },
                new()
                {
                    Name = "Особливості подання",
                    Key = "submission_features",
                    DataType = FieldDataType.Text,
                    IsRequired = false,
                    Order = 2,
                },
                new()
                {
                    Name = "Терміни",
                    Key = "terms",
                    DataType = FieldDataType.Text,
                    IsRequired = false,
                    Order = 3,
                },
                new()
                {
                    Name = "Контакти",
                    Key = "contacts",
                    DataType = FieldDataType.Text,
                    IsRequired = false,
                    Order = 4,
                },
            ],
        };
    }
}
