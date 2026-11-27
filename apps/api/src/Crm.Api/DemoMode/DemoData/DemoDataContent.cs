using System.Globalization;
using System.Text.Json.Nodes;
using Crm.Domain.Entities;
using Crm.Domain.Entities.Base;
using Crm.Domain.Enums;
using Crm.Domain.Extensions;

namespace Crm.Api.DemoMode.DemoData;

/// <summary>
/// Holds the fictional Ukrainian content written by <see cref="DemoDataSeeder"/>.
/// </summary>
/// <remarks>
/// Everything here is made up on purpose: surnames are derived from words such as "зразок", "приклад" or "вигаданий",
/// e-mail addresses live under the reserved <c>demo.example.org</c> domain and phone numbers use the
/// non-existent <c>+380 00 000 00 xx</c> range. No real person or organisation is referenced.
/// </remarks>
internal static class DemoDataContent
{
    public const string NgoName = "ГО «Демо-спільнота»";

    public const string EmailDomain = "demo.example.org";

    /// <summary>
    /// The name of the system preset table template the donor table is instantiated from.
    /// </summary>
    public const string DonorTemplateName = "База донорів";

    public const string VolunteerTableName = "Волонтери";

    public const string VolunteerTableDescription =
        "Облік волонтерів організації: контакти, навички, зайнятість і статус участі.";

    /// <summary>
    /// The name of a user custom field of the shared schema (see <see cref="CreateUserCustomFields"/>).
    /// </summary>
    public const string CityFieldName = "Місто";

    /// <inheritdoc cref="CityFieldName"/>
    public const string FocusFieldName = "Напрям";

    /// <inheritdoc cref="CityFieldName"/>
    public const string AvailabilityFieldName = "Доступність";

    /// <inheritdoc cref="CityFieldName"/>
    public const string ShirtSizeFieldName = "Розмір футболки";

    /// <summary>
    /// How long before the seeding moment the demo organisation was "founded".
    /// </summary>
    public const int NgoAgeDays = 540;

    // The fields of the "База донорів" system template, by name.
    private const string DonorNameField = "Назва";
    private const string DonorPrioritiesField = "Напрямки та пріоритети";
    private const string DonorSubmissionField = "Особливості подання";
    private const string DonorTermsField = "Терміни";
    private const string DonorContactsField = "Контакти";

    // The fields of the hand-built volunteer table, by name.
    private const string VolunteerNameField = "ПІБ";
    private const string VolunteerPhoneField = "Телефон";
    private const string VolunteerEmailField = "Електронна пошта";
    private const string VolunteerCityField = "Місто";
    private const string VolunteerSkillsField = "Навички";
    private const string VolunteerJoinedField = "Дата приєднання";
    private const string VolunteerHoursField = "Годин на тиждень";
    private const string VolunteerStatusField = "Статус";
    private const string VolunteerTrainingField = "Пройшов навчання";
    private const string VolunteerLastContactField = "Останній контакт";
    private const string VolunteerProfileField = "Анкета";

    private const string Country = "Україна";

    private const string PreferredLanguage = "uk";

    private const string FallbackLastName = "Демо";

    private const int MaxFallbackFirstNameLength = 100;

    private const int EmergencyPhoneOffset = 50;

    private const int FirstDonorPhoneNumber = 71;

    private const int FirstVolunteerPhoneNumber = 21;

    private static readonly AccessRight[] CoordinatorRights =
    [
        AccessRight.ViewUser,
        AccessRight.CreateUser,
        AccessRight.UpdateUser,
        AccessRight.DisableUser,
        AccessRight.ViewEmergencyContact,
        AccessRight.ViewCustomFields,
        AccessRight.ViewLoginPageImages,
        AccessRight.ViewTableTemplate,
        AccessRight.CreateTableTemplate,
        AccessRight.UpdateTableTemplate,
        AccessRight.DeleteTableTemplate,
        AccessRight.ViewCustomTable,
        AccessRight.CreateCustomTable,
        AccessRight.UpdateCustomTable,
        AccessRight.DeleteCustomTable,
        AccessRight.ViewCustomRecord,
        AccessRight.CreateCustomRecord,
        AccessRight.UpdateCustomRecord,
        AccessRight.DeleteCustomRecord,
    ];

    private static readonly AccessRight[] VolunteerRights =
    [
        AccessRight.ViewUser,
        AccessRight.ViewTableTemplate,
        AccessRight.ViewCustomTable,
        AccessRight.ViewCustomRecord,
        AccessRight.CreateCustomRecord,
    ];

    private static readonly Dictionary<string, RoleBlueprint> RoleBlueprints = new(StringComparer.OrdinalIgnoreCase)
    {
        [DemoAccountOptions.CoordinatorRole] = new("Координаторка", "Координатори", CoordinatorRights),
        [DemoAccountOptions.VolunteerRole] = new("Волонтерка", "Волонтери", VolunteerRights),
    };

    // Profiles of the public demo accounts, matched to the configured accounts by position.
    private static readonly DemoPerson[] AccountPersons =
    [
        new("Олена", "Зразкова", "Іванівна", "olena.zrazkova", PronounCategory.Feminine, 1, 500, "Львів", "Керування організацією", "Будні, 9:00–18:00", "S", "Петро Зразковий (чоловік)"),
        new("Тарас", "Прикладенко", "Миколайович", "taras.prykladenko", PronounCategory.Masculine, 2, 410, "Київ", "Координація проєктів", "Будні, 10:00–19:00", "L", "Ганна Прикладенко (мати)"),
        new("Марта", "Взірцева", "Олегівна", "marta.vzirtseva", PronounCategory.Feminine, 3, 150, "Тернопіль", "Допомога на заходах", "Вихідні та вечори будніх днів", "M", "Олег Взірцевий (батько)"),
    ];

    private static readonly DemoPerson[] ColleaguePersons =
    [
        new("Андрій", "Макетний", "Степанович", "andrii.maketnyi", PronounCategory.Masculine, 11, 380, "Харків", "Логістика", "Будні після 18:00", "XL", "Оксана Макетна (дружина)"),
        new("Ірина", "Шаблонюк", "Василівна", "iryna.shabloniuk", PronounCategory.Feminine, 12, 340, "Одеса", "Фандрейзинг", "Будні, 9:00–17:00", "M", "Василь Шаблонюк (батько)"),
        new("Богдан", "Пробний", "Андрійович", "bohdan.probnyi", PronounCategory.Masculine, 13, 300, "Дніпро", "Комунікації та соцмережі", "Вівторок і четвер", "L", "Любов Пробна (мати)"),
        new("Соломія", "Ескізна", "Романівна", "solomiia.eskizna", PronounCategory.Feminine, 14, 260, "Івано-Франківськ", "Робота з волонтерами", "Будні, 12:00–20:00", "S", "Роман Ескізний (батько)"),
        new("Назар", "Умовний", "Ігорович", "nazar.umovnyi", PronounCategory.Masculine, 15, 220, "Вінниця", "Юридичний супровід", "Наразі недоступний", "M", "Ігор Умовний (батько)", IsActive: false),
        new("Дарина", "Вигадана", "Сергіївна", "daryna.vyhadana", PronounCategory.Neutral, 16, 180, "Полтава", "Психологічна підтримка", "Понеділок, середа, п'ятниця", "M", "Сергій Вигаданий (батько)"),
        new("Остап", "Тестовий", "Володимирович", "ostap.testovyi", PronounCategory.Masculine, 17, 120, "Чернівці", "Облік і звітність", "Будні, 8:00–16:00", "L", "Надія Тестова (сестра)"),
        new("Квітослава", "Модельна", "Юріївна", "kvitoslava.modelna", PronounCategory.Feminine, 18, 60, "Луцьк", "Освітні програми", "Наразі недоступна", "S", "Юрій Модельний (батько)", IsActive: false),
    ];

    private static readonly DemoDonor[] Donors =
    [
        new("Фонд «Умовна підтримка»", "Гуманітарна допомога, підтримка внутрішньо переміщених осіб", "Онлайн-форма, бюджет до 500 тис. грн, обов'язковий лист підтримки від громади", "Постійний прийом заявок; розгляд до 30 днів", "umovna-pidtrymka"),
        new("Програма малих грантів «Зразок»", "Розвиток громад, молодіжні ініціативи", "Коротка концепція на 2 сторінки, далі повна заявка для фіналістів", "Двічі на рік: до 1 березня та до 1 вересня", "zrazok-grants"),
        new("Благодійний фонд «Демо-Добро»", "Продуктові набори, допомога літнім людям", "Заявка довільної форми та кошторис; співфінансування не вимагається", "Щокварталу, до 15 числа першого місяця", "demo-dobro"),
        new("Міжнародна ініціатива «Приклад для громад»", "Інституційний розвиток громадських організацій", "Заявка англійською мовою, логічна матриця, аудит за попередній рік", "Один конкурс на рік; дедлайн 30 квітня", "pryklad-hromad"),
        new("Фундація «Тестовий розвиток»", "Цифровізація, навчання команд", "Подання через портал фундації, потрібен план моніторингу", "Розгляд заявок щомісяця", "testovyi-rozvytok"),
        new("Грантова програма «Макет змін»", "Адвокація, участь громадян у прийнятті рішень", "Партнерство щонайменше двох організацій, бюджет до 1 млн грн", "Дедлайн 15 червня; проєкти тривалістю до 12 місяців", "maket-zmin"),
        new("Фонд громад «Вигаданий край»", "Благоустрій, локальні культурні події", "Проста анкета, обов'язкова участь мешканців у реалізації", "Постійний прийом; рішення раз на два місяці", "vyhadanyi-krai"),
        new("Партнерство «Шаблон сталості»", "Екологічні ініціативи, енергоефективність", "Технічне обґрунтування та три комерційні пропозиції", "Двічі на рік: до 1 лютого та до 1 серпня", "shablon-stalosti"),
        new("Агенція розвитку «Пробний крок»", "Соціальне підприємництво, працевлаштування", "Бізнес-модель, фінансовий прогноз на 2 роки, пітч онлайн", "Дедлайн 20 жовтня; старт проєктів у січні", "probnyi-krok"),
        new("Фонд «Ескіз майбутнього»", "Освіта дітей і підлітків, STEM-гуртки", "Заявка з описом програми та резюме тренерів", "Один конкурс на рік; дедлайн 31 травня", "eskiz-maibutnoho"),
        new("Корпоративна програма «Демо-Партнер»", "Волонтерство працівників, матеріальна допомога", "Лист-запит і презентація організації; можливі внески товарами", "Розгляд запитів протягом 14 днів", "demo-partner"),
        new("Фонд підтримки молоді «Взірець»", "Молодіжні центри, неформальна освіта, обміни", "Заявку подає команда віком 16–30 років, потрібен ментор", "Щокварталу, до останнього дня кварталу", "vzirets-molod"),
        new("Освітня ініціатива «Модельна школа»", "Підвищення кваліфікації вчителів, інклюзія", "Спільна заявка з навчальним закладом, лист від адміністрації", "Дедлайн 10 вересня; реалізація протягом навчального року", "modelna-shkola"),
        new("Екологічний фонд «Умовний ліс»", "Озеленення, збереження малих річок", "Карта території, погодження з місцевою радою", "Весняний і осінній конкурси", "umovnyi-lis"),
        new("Фонд культурних проєктів «Вигадана сцена»", "Театр, музика, збереження локальної спадщини", "Творча концепція, кошторис, приклади попередніх робіт", "Дедлайн 1 грудня; проєкти до 9 місяців", "vyhadana-stsena"),
    ];

    private static readonly DemoVolunteer[] Volunteers =
    [
        new("Леся Уявна", "lesia.uiavna", "Львів", "Перша допомога, робота з дітьми", 420, 8, "Активний", true, 3, true),
        new("Максим Примірний", "maksym.prymirnyi", "Київ", "Водіння (категорія B), логістика", 365, 12, "Активний", true, 7, true),
        new("Орися Казкова", "orysia.kazkova", "Тернопіль", "Дизайн, ведення соцмереж", 300, 5, "Активний", true, 12, true),
        new("Денис Умовенко", "denys.umovenko", "Харків", "Ремонтні роботи, пакування допомоги", 270, 10, "Активний", true, 5, false),
        new("Зоряна Зразчук", "zoriana.zrazchuk", "Одеса", "Переклад (англійська, польська)", 240, 4, "На паузі", true, 45, true),
        new("Ярема Вигадько", "yarema.vyhadko", "Дніпро", "Фото- та відеозйомка", 200, 6, "Активний", false, 9, true),
        new("Мирослава Прикладна", "myroslava.prykladna", "Вінниця", "Бухгалтерія, звітність", 160, 3, "Активний", true, 14, false),
        new("Устим Тестенко", "ustym.testenko", "Полтава", "ІТ-підтримка, налагодження техніки", 120, 6, "Активний", true, 2, true),
        new("Христина Макетна", "khrystyna.maketna", "Чернівці", "Психологічна підтримка", 90, 4, "На паузі", true, 30, false),
        new("Лук'ян Шаблонний", "lukian.shablonnyi", "Луцьк", "Кулінарія, організація заходів", 45, 8, "Активний", false, 6, true),
        new("Ніна Пробна", "nina.probna", "Ужгород", "Робота з літніми людьми", 14, 5, "Новий", false, null, false),
        new("Северин Ескізний", "severyn.eskiznyi", "Житомир", "Водіння (категорія C), вантажні перевезення", 5, 10, "Новий", false, null, false),
    ];

    /// <summary>
    /// Gets the e-mail addresses and profile data of the fictional colleagues created next to the demo accounts.
    /// </summary>
    public static IReadOnlyList<DemoPerson> Colleagues => ColleaguePersons;

    /// <summary>
    /// Gets the number of rows written to the donor table.
    /// </summary>
    public static int DonorRecordCount => Donors.Length;

    /// <summary>
    /// Gets the number of rows written to the volunteer table.
    /// </summary>
    public static int VolunteerRecordCount => Volunteers.Length;

    /// <summary>
    /// Returns the permission set and name variants for a demo role. Unknown role names get the read-mostly set.
    /// </summary>
    public static RoleBlueprint GetRoleBlueprint(string roleName) =>
        RoleBlueprints.TryGetValue(roleName, out var blueprint)
            ? blueprint
            : new RoleBlueprint(null, null, VolunteerRights);

    /// <summary>
    /// Builds the shared schema of user custom fields: three fields every colleague sees and one (the shirt size)
    /// only for those with <c>ViewCustomFields</c>. Every field id is new, so it is created once per database.
    /// </summary>
    public static List<UserCustomField> CreateUserCustomFields() =>
    [
        new() { Name = CityFieldName, IsRequired = false, IsPublic = true, Order = 0 },
        new() { Name = FocusFieldName, IsRequired = false, IsPublic = true, Order = 1 },
        new() { Name = AvailabilityFieldName, IsRequired = false, IsPublic = true, Order = 2 },
        new() { Name = ShirtSizeFieldName, IsRequired = false, IsPublic = false, Order = 3 },
    ];

    /// <summary>
    /// Builds the profile of the demo account at the given position in the configured account list.
    /// </summary>
    public static User CreateAccountProfile(
        int accountIndex,
        string accountName,
        Guid ngoId,
        DateTimeOffset now,
        IReadOnlyList<UserCustomField> userFields)
    {
        if (accountIndex < AccountPersons.Length)
        {
            return CreateProfile(AccountPersons[accountIndex], ngoId, now, userFields);
        }

        string firstName = accountName.Trim();

        return new User
        {
            FirstName = firstName.Length > MaxFallbackFirstNameLength ? firstName[..MaxFallbackFirstNameLength] : firstName,
            LastName = FallbackLastName,
            Country = Country,
            PreferredLanguage = PreferredLanguage,
            IsActive = true,
            CreatedAt = now.UtcDateTime,
            NgoId = ngoId,
            CustomFields = userFields.CreateDefaultPayload(),
        };
    }

    /// <summary>
    /// Builds a filled user profile, including its values of the shared custom fields (keyed by field id, as the
    /// API stores them), for a fictional person.
    /// </summary>
    public static User CreateProfile(DemoPerson person, Guid ngoId, DateTimeOffset now, IReadOnlyList<UserCustomField> userFields) => new()
    {
        FirstName = person.FirstName,
        LastName = person.LastName,
        Patronymic = person.Patronymic,
        PhoneNumber = FakePhone(person.PhoneNumber),
        Country = Country,
        EmergencyContact = $"{person.EmergencyContactName}, {FakePhone(person.PhoneNumber + EmergencyPhoneOffset)}",
        PreferredLanguage = PreferredLanguage,
        Pronouns = person.PronounCategory switch
        {
            PronounCategory.Masculine => "він/його",
            PronounCategory.Feminine => "вона/її",
            PronounCategory.Neutral => "вони/їх",
            PronounCategory.None or _ => null,
        },
        PronounCategory = person.PronounCategory,
        IsActive = person.IsActive,
        CreatedAt = now.UtcDateTime.AddDays(-person.JoinedDaysAgo),
        NgoId = ngoId,
        CustomFields = ProfileFieldValues(userFields, person),
    };

    /// <summary>
    /// Turns the values of a table row given by field name into the stored form, keyed by field id (the id as the
    /// API writes it). Fields without a value are left out, so the payload validation fills them in.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a value names a field the schema does not have.</exception>
    public static JsonObject ToFieldValues(IEnumerable<FieldDefinitionBase> fields, IReadOnlyDictionary<string, string?> valuesByName)
    {
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(valuesByName);

        var idsByName = fields.ToDictionary(f => f.Name, f => f.Id.ToString(), StringComparer.Ordinal);
        var data = new JsonObject();

        foreach (var (name, value) in valuesByName)
        {
            if (!idsByName.TryGetValue(name, out string? id))
            {
                throw new InvalidOperationException($"The demo data fills in the field '{name}', which the schema does not have.");
            }

            data[id] = value;
        }

        return data;
    }

    /// <summary>
    /// Builds the e-mail address of a fictional person or organisation under the reserved demo domain.
    /// </summary>
    public static string Email(string localPart) => $"{localPart}@{EmailDomain}";

    /// <summary>
    /// Builds an E.164 phone number in the non-existent <c>+380 00 000 00 xx</c> range.
    /// </summary>
    public static string FakePhone(int number) => string.Create(CultureInfo.InvariantCulture, $"+3800000000{number:00}");

    /// <summary>
    /// Builds the rows of the donor table: values by the field names of the "База донорів" system template.
    /// </summary>
    public static List<Dictionary<string, string?>> CreateDonorRows() =>
    [
        .. Donors.Select((donor, index) => new Dictionary<string, string?>
        {
            [DonorNameField] = donor.Name,
            [DonorPrioritiesField] = donor.Priorities,
            [DonorSubmissionField] = donor.SubmissionFeatures,
            [DonorTermsField] = donor.Terms,
            [DonorContactsField] = $"{Email(donor.EmailLocalPart)}, {FakePhone(FirstDonorPhoneNumber + index)}",
        }),
    ];

    /// <summary>
    /// Builds the schema of the hand-built volunteer table. Every value is text, as the API stores them; the names
    /// and the order show what each column holds.
    /// </summary>
    public static List<CustomField> CreateVolunteerFields() =>
    [
        new() { Name = VolunteerNameField, IsRequired = true, Order = 0 },
        new() { Name = VolunteerPhoneField, IsRequired = true, Order = 1 },
        new() { Name = VolunteerEmailField, IsRequired = false, Order = 2 },
        new() { Name = VolunteerCityField, IsRequired = false, Order = 3 },
        new() { Name = VolunteerSkillsField, IsRequired = false, Order = 4 },
        new() { Name = VolunteerJoinedField, IsRequired = true, Order = 5 },
        new() { Name = VolunteerHoursField, IsRequired = false, Order = 6 },
        new() { Name = VolunteerStatusField, IsRequired = true, Order = 7 },
        new() { Name = VolunteerTrainingField, IsRequired = false, Order = 8 },
        new() { Name = VolunteerLastContactField, IsRequired = false, Order = 9 },
        new() { Name = VolunteerProfileField, IsRequired = false, Order = 10 },
    ];

    /// <summary>
    /// Builds the rows of the volunteer table: values by field name. Dates are relative to <paramref name="now"/>
    /// so the data never looks stale, and written as <c>yyyy-MM-dd</c> so they sort as text.
    /// </summary>
    public static List<Dictionary<string, string?>> CreateVolunteerRows(DateTimeOffset now)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var lastContactBase = new DateTimeOffset(today.Year, today.Month, today.Day, 10, 30, 0, TimeSpan.Zero);

        return
        [
            .. Volunteers.Select((volunteer, index) => new Dictionary<string, string?>
            {
                [VolunteerNameField] = volunteer.FullName,
                [VolunteerPhoneField] = FakePhone(FirstVolunteerPhoneNumber + index),
                [VolunteerEmailField] = Email(volunteer.EmailLocalPart),
                [VolunteerCityField] = volunteer.City,
                [VolunteerSkillsField] = volunteer.Skills,
                [VolunteerJoinedField] = today.AddDays(-volunteer.JoinedDaysAgo).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                [VolunteerHoursField] = volunteer.HoursPerWeek.ToString(CultureInfo.InvariantCulture),
                [VolunteerStatusField] = volunteer.Status,
                [VolunteerTrainingField] = volunteer.HasTraining ? "Так" : "Ні",
                [VolunteerLastContactField] = volunteer.LastContactDaysAgo is { } daysAgo
                    ? lastContactBase.AddDays(-daysAgo).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                    : null,
                [VolunteerProfileField] = volunteer.HasProfilePage
                    ? string.Create(CultureInfo.InvariantCulture, $"https://{EmailDomain}/volunteers/{index + 1:00}")
                    : null,
            }),
        ];
    }

    /// <summary>
    /// The values of a person for every field of the schema: the demo fields by name, the defaults of the API for
    /// any other field (an empty text if required, otherwise nothing).
    /// </summary>
    private static JsonObject ProfileFieldValues(IReadOnlyList<UserCustomField> userFields, DemoPerson person)
    {
        var values = userFields.CreateDefaultPayload();

        foreach (var field in userFields)
        {
            string? value = field.Name switch
            {
                CityFieldName => person.City,
                FocusFieldName => person.Focus,
                AvailabilityFieldName => person.Availability,
                ShirtSizeFieldName => person.ShirtSize,
                _ => null,
            };

            if (value is not null)
            {
                values[field.Id.ToString()] = value;
            }
        }

        return values;
    }

    /// <summary>
    /// A fictional member of the demo organisation.
    /// </summary>
    internal sealed record DemoPerson(
        string FirstName,
        string LastName,
        string Patronymic,
        string EmailLocalPart,
        PronounCategory PronounCategory,
        int PhoneNumber,
        int JoinedDaysAgo,
        string City,
        string Focus,
        string Availability,
        string ShirtSize,
        string EmergencyContactName,
        bool IsActive = true);

    /// <summary>
    /// The name variants and permissions of a role created for the demo.
    /// </summary>
    internal sealed record RoleBlueprint(string? FeminitiveName, string? PluralName, IReadOnlyList<AccessRight> Rights);

    private sealed record DemoDonor(
        string Name,
        string Priorities,
        string SubmissionFeatures,
        string Terms,
        string EmailLocalPart);

    private sealed record DemoVolunteer(
        string FullName,
        string EmailLocalPart,
        string City,
        string Skills,
        int JoinedDaysAgo,
        int HoursPerWeek,
        string Status,
        bool HasTraining,
        int? LastContactDaysAgo,
        bool HasProfilePage);
}
