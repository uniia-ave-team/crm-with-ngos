using Crm.Domain.Enums;

namespace Crm.Api.DemoMode.DemoData;

/// <summary>
/// The demo projects of the planner: members, topics, tags, tasks in every status and a few comments. They follow
/// the rest of the demo data (the grants, the fair and the purchases in <see cref="DemoTables"/>), so the planner
/// and the knowledge base tell the same story.
/// </summary>
/// <remarks>
/// People are named by the e-mail local part of a fictional person of <see cref="DemoDataContent"/>: the profiles of
/// the demo accounts and the colleagues. Only active people are used. Days are counted from the seeding day.
/// </remarks>
internal static class DemoProjects
{
    /// <summary>
    /// How many days before its start a project is created.
    /// </summary>
    public const int ProjectLeadDays = 3;

    /// <summary>
    /// How many days before its start (or before the seeding day, for later tasks) a task is created.
    /// </summary>
    public const int TaskLeadDays = 2;

    // The hour of the day the demo dates point at (UTC).
    private const int WorkdayStartHour = 9;

    // The profiles of the three default demo accounts: the administrator, the coordinator and the volunteer.
    private const string Olena = "olena.zrazkova";
    private const string Taras = "taras.prykladenko";
    private const string Marta = "marta.vzirtseva";

    // Active fictional colleagues.
    private const string Andrii = "andrii.maketnyi";
    private const string Iryna = "iryna.shabloniuk";
    private const string Bohdan = "bohdan.probnyi";
    private const string Solomiia = "solomiia.eskizna";
    private const string Daryna = "daryna.vyhadana";
    private const string Ostap = "ostap.testovyi";

    /// <summary>
    /// Gets the projects, in the order they are created.
    /// </summary>
    public static IReadOnlyList<ProjectDefinition> All { get; } =
    [
        PsychologicalSupport(),
        DigitalLiteracy(),
        CharityFair(),
        EnergyHub(),
        MobileAidPoint(),
    ];

    /// <summary>
    /// Turns a day counted from the seeding day into a moment of that day.
    /// </summary>
    /// <param name="now">The seeding moment.</param>
    /// <param name="day">The day: negative in the past, positive in the future.</param>
    /// <returns>The start of the working day, in UTC.</returns>
    public static DateTime At(DateTimeOffset now, int day) =>
        DateTime.SpecifyKind(now.UtcDateTime.Date.AddDays(day).AddHours(WorkdayStartHour), DateTimeKind.Utc);

    /// <summary>
    /// Gets the day a task was created: before its start, and never after the seeding day.
    /// </summary>
    /// <param name="task">The task.</param>
    /// <returns>The day, counted from the seeding day.</returns>
    public static int CreatedDay(TaskDefinition task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return Math.Min(task.StartDay, 0) - TaskLeadDays;
    }

    private static ProjectDefinition PsychologicalSupport() => new(
        "Психологічна підтримка для ВПО",
        "Групи підтримки та індивідуальні консультації для внутрішньо переміщених людей у Харкові. Фінансує Міжнародна ініціатива «Приклад для громад».",
        ProjectVisibility.PublicViewPrivateEdit,
        StartDay: -60,
        EndDay: 105,
        [
            new(Daryna, ProjectClaim.Manage, "Керівниця проєкту"),
            new(Olena, ProjectClaim.Manage, "Директорка"),
            new(Taras, ProjectClaim.Edit, "Координатор"),
            new(Marta, ProjectClaim.Edit, "Волонтерка"),
            new(Ostap, ProjectClaim.View, "Бухгалтер"),
        ],
        ["Групи підтримки", "Консультації", "Звітність"],
        ["Харків", "Діти", "Донор"],
        [
            new(
                "Групи підтримки",
                "Набрати другу групу підтримки",
                "Потрібно 8–10 учасників. Записатися можна через гарячу лінію або в ліцеї «Взірець».",
                ProjectTaskStatus.InProgress,
                TaskPriority.High,
                StartDay: -10,
                EndDay: 5,
                Daryna,
                [Daryna, Marta],
                ["Харків"],
                [
                    new(Marta, -3, "Уже записалися шестеро, ще двоє чекають на дзвінок."),
                    new(Daryna, -2, "Дякую! Якщо до кінця тижня буде вісім, стартуємо."),
                ]),
            new("Групи підтримки", "Домовитися з ліцеєм «Взірець» про клас", null, ProjectTaskStatus.Done, TaskPriority.Medium, StartDay: -20, EndDay: -12, Taras, [Taras], ["Харків"]),
            new("Групи підтримки", "Закупити матеріали для арт-терапії", "Фарби, папір і глина для занять з дітьми.", ProjectTaskStatus.Completed, TaskPriority.Low, StartDay: -12, EndDay: -6, Daryna, [Marta], ["Діти"]),
            new("Консультації", "Скласти графік супервізій для психологів", null, ProjectTaskStatus.ToDo, TaskPriority.Medium, StartDay: 3, EndDay: 10, Daryna, [Daryna]),
            new(
                "Консультації",
                "Перші консультації для нових звернень з гарячої лінії",
                null,
                ProjectTaskStatus.InProgress,
                TaskPriority.High,
                StartDay: -2,
                EndDay: 3,
                Olena,
                [Daryna],
                Comments: [new(Olena, -1, "Двоє людей звернулися після обстрілу — візьміть їх, будь ласка, першими.")]),
            new("Звітність", "Підготувати анкету зворотного зв'язку", null, ProjectTaskStatus.InProgress, TaskPriority.Medium, StartDay: -5, EndDay: 7, Taras, [Marta, Taras]),
            new("Звітність", "Проміжний звіт для донора", "Звіт за перше півріччя: кількість учасників, відгуки, витрати.", ProjectTaskStatus.ToDo, TaskPriority.High, StartDay: 30, EndDay: 45, Olena, [Olena, Ostap], ["Донор"]),
            new(
                "Звітність",
                "Узгодити кошторис оренди класу",
                null,
                ProjectTaskStatus.Blocked,
                TaskPriority.Medium,
                StartDay: -4,
                EndDay: 2,
                Ostap,
                [Ostap],
                Comments: [new(Ostap, -1, "Чекаю на підписаний договір від ліцею, без нього не проведу оплату.")]),
        ]);

    private static ProjectDefinition DigitalLiteracy() => new(
        "Курси цифрової грамотності 55+",
        "Безкоштовні заняття для людей поважного віку: смартфон, месенджери, безпечні платежі. Заняття проходять у міській бібліотеці «Шаблон».",
        ProjectVisibility.Public,
        StartDay: -25,
        EndDay: 150,
        [
            new(Bohdan, ProjectClaim.Manage, "Керівник курсу"),
            new(Olena, ProjectClaim.Edit, "Директорка"),
            new(Marta, ProjectClaim.Edit, "Волонтерка-тренерка"),
            new(Ostap, ProjectClaim.Edit, "Бухгалтер"),
        ],
        ["Підготовка", "Заняття", "Закупівлі"],
        ["Бібліотека", "Реєстрація", "Техніка"],
        [
            new("Закупівлі", "Купити три ноутбуки", null, ProjectTaskStatus.Completed, TaskPriority.High, StartDay: -12, EndDay: -5, Ostap, [Ostap], ["Техніка"]),
            new("Підготовка", "Скласти програму на 8 занять", null, ProjectTaskStatus.Done, TaskPriority.Medium, StartDay: -20, EndDay: -10, Bohdan, [Bohdan, Marta]),
            new(
                "Підготовка",
                "Відкрити реєстрацію учасників",
                null,
                ProjectTaskStatus.InProgress,
                TaskPriority.High,
                StartDay: -3,
                EndDay: 4,
                Bohdan,
                [Bohdan],
                ["Реєстрація"],
                [new(Bohdan, -1, "Форма готова, сьогодні ввечері поширю посилання в соцмережах.")]),
            new(
                "Підготовка",
                "Домовитися з бібліотекою «Шаблон» про зал",
                null,
                ProjectTaskStatus.Blocked,
                TaskPriority.High,
                StartDay: -7,
                EndDay: 2,
                Olena,
                [Olena],
                ["Бібліотека"],
                [new(Olena, -2, "Бібліотека погоджується, але чекаємо на підписаний меморандум.")]),
            new("Заняття", "Заняття 1: смартфон і месенджери", null, ProjectTaskStatus.ToDo, TaskPriority.Medium, StartDay: 10, EndDay: 10, Bohdan, [Marta], ["Бібліотека"]),
            new("Заняття", "Заняття 2: онлайн-банкінг без шахраїв", null, ProjectTaskStatus.ToDo, TaskPriority.Medium, StartDay: 17, EndDay: 17, Bohdan, [Marta], ["Бібліотека"]),
            new("Закупівлі", "Роздрукувати пам'ятки для учасників", null, ProjectTaskStatus.ToDo, TaskPriority.Low, StartDay: 6, EndDay: 9, Bohdan, [Bohdan]),
        ]);

    private static ProjectDefinition CharityFair() => new(
        "Благодійний ярмарок «Добро поруч»",
        "Ярмарок у Львові: випічка від партнерів, лотерея й концерт. Зібрані кошти підуть на продуктові набори для людей похилого віку.",
        ProjectVisibility.Public,
        StartDay: -14,
        EndDay: 21,
        [
            new(Bohdan, ProjectClaim.Manage, "Організатор"),
            new(Iryna, ProjectClaim.Edit, "Фандрейзинг"),
            new(Solomiia, ProjectClaim.Edit, "Волонтери"),
            new(Taras, ProjectClaim.Edit, "Координатор"),
            new(Marta, ProjectClaim.Edit, "Волонтерка"),
        ],
        ["Організація", "Партнери", "Комунікації"],
        ["Волонтери", "Друк", "Терміново"],
        [
            new("Організація", "Погодити місце проведення з міською радою", null, ProjectTaskStatus.Done, TaskPriority.High, StartDay: -14, EndDay: -7, Taras, [Taras]),
            new(
                "Організація",
                "Зібрати 20 волонтерів на день ярмарку",
                null,
                ProjectTaskStatus.InProgress,
                TaskPriority.High,
                StartDay: -7,
                EndDay: 14,
                Solomiia,
                [Solomiia, Marta],
                ["Волонтери"],
                [
                    new(Solomiia, -2, "Маємо 12 підтверджень. Марто, допоможеш обдзвонити список з минулого року?"),
                    new(Marta, -1, "Так, візьму першу половину списку."),
                ]),
            new("Партнери", "Домовитися з пекарнею «Ескіз» про випічку", null, ProjectTaskStatus.InProgress, TaskPriority.Medium, StartDay: -5, EndDay: 7, Iryna, [Iryna]),
            new(
                "Партнери",
                "Скласти кошторис ярмарку",
                null,
                ProjectTaskStatus.Blocked,
                TaskPriority.Medium,
                StartDay: -3,
                EndDay: 3,
                Iryna,
                [Iryna, Taras],
                ["Терміново"],
                [new(Iryna, -1, "Чекаємо на ціну від друкарні, без неї кошторис не закрити.")]),
            new("Комунікації", "Анонс на радіо «Макет FM»", null, ProjectTaskStatus.ToDo, TaskPriority.Medium, StartDay: 5, EndDay: 10, Bohdan, [Bohdan]),
            new("Комунікації", "Надрукувати афіші й листівки", null, ProjectTaskStatus.ToDo, TaskPriority.Low, StartDay: 7, EndDay: 12, Bohdan, [Bohdan], ["Друк"]),
        ]);

    private static ProjectDefinition EnergyHub() => new(
        "Енергонезалежний гуманітарний хаб",
        "Підготовка заявки до партнерства «Шаблон сталості»: генератори й сонячні панелі для складу в Дніпрі. Проєкт бачать лише його учасники.",
        ProjectVisibility.Private,
        StartDay: -40,
        EndDay: null,
        [
            new(Andrii, ProjectClaim.Manage, "Керівник проєкту"),
            new(Olena, ProjectClaim.Manage, "Директорка"),
            new(Taras, ProjectClaim.Edit, "Координатор"),
        ],
        ["Заявка", "Обладнання"],
        ["Донор", "Генератори"],
        [
            new(
                "Заявка",
                "Зібрати дві комерційні пропозиції на генератори",
                "Донор попросив дві пропозиції від різних постачальників.",
                ProjectTaskStatus.InProgress,
                TaskPriority.Critical,
                StartDay: -6,
                EndDay: 4,
                Andrii,
                [Andrii],
                ["Донор", "Генератори"],
                [new(Andrii, -3, "Одна пропозиція вже є, другу постачальник обіцяє до кінця тижня.")]),
            new("Заявка", "Отримати лист підтримки від громади", null, ProjectTaskStatus.Completed, TaskPriority.Medium, StartDay: -35, EndDay: -28, Olena, [Olena]),
            new("Обладнання", "Розрахувати потужність для складу № 2", null, ProjectTaskStatus.Done, TaskPriority.Medium, StartDay: -30, EndDay: -20, Andrii, [Andrii]),
            new("Обладнання", "Скласти план монтажу сонячних панелей", null, ProjectTaskStatus.ToDo, TaskPriority.Low, StartDay: 14, EndDay: 40, Taras, [Taras]),
        ]);

    private static ProjectDefinition MobileAidPoint() => new(
        "Мобільний пункт видачі допомоги",
        "Мобільний пункт у громадах Харківщини: оренда буса, обладнання й щотижневі виїзди. Проєкт завершено, звіт донору здано.",
        ProjectVisibility.Public,
        StartDay: -150,
        EndDay: -10,
        [
            new(Iryna, ProjectClaim.Manage, "Керівниця проєкту"),
            new(Andrii, ProjectClaim.Edit, "Логістика"),
            new(Olena, ProjectClaim.Edit, "Директорка"),
        ],
        ["Логістика", "Звітність"],
        [],
        [
            new("Логістика", "Орендувати бус для мобільного пункту", null, ProjectTaskStatus.Completed, TaskPriority.High, StartDay: -140, EndDay: -120, Andrii, [Andrii]),
            new("Логістика", "Купити намет і обладнання", null, ProjectTaskStatus.Completed, TaskPriority.Medium, StartDay: -110, EndDay: -95, Andrii, [Andrii]),
            new("Звітність", "Фінальний звіт донору", null, ProjectTaskStatus.Completed, TaskPriority.High, StartDay: -30, EndDay: -10, Iryna, [Iryna, Olena]),
        ]);

    /// <summary>
    /// A demo project.
    /// </summary>
    /// <param name="Title">The project title.</param>
    /// <param name="Description">The project description.</param>
    /// <param name="Visibility">Who sees and edits the project.</param>
    /// <param name="StartDay">The start, counted from the seeding day.</param>
    /// <param name="EndDay">The end, counted from the seeding day, or <c>null</c> for an open-ended project.</param>
    /// <param name="Members">The members; at least one manages the project.</param>
    /// <param name="Topics">The topics (columns of the board), in order.</param>
    /// <param name="Tags">The tags of the project.</param>
    /// <param name="Tasks">The tasks.</param>
    internal sealed record ProjectDefinition(
        string Title,
        string Description,
        ProjectVisibility Visibility,
        int StartDay,
        int? EndDay,
        IReadOnlyList<MemberDefinition> Members,
        IReadOnlyList<string> Topics,
        IReadOnlyList<string> Tags,
        IReadOnlyList<TaskDefinition> Tasks);

    /// <summary>
    /// A member of a demo project.
    /// </summary>
    /// <param name="Person">The e-mail local part of the fictional person.</param>
    /// <param name="Claim">What the member may do in the project.</param>
    /// <param name="RoleTitle">The role in the project.</param>
    internal sealed record MemberDefinition(string Person, ProjectClaim Claim, string RoleTitle);

    /// <summary>
    /// A task of a demo project.
    /// </summary>
    /// <param name="Topic">The title of the task's topic.</param>
    /// <param name="Title">The task title.</param>
    /// <param name="Description">The task description, or <c>null</c>.</param>
    /// <param name="Status">The status.</param>
    /// <param name="Priority">The priority.</param>
    /// <param name="StartDay">The start, counted from the seeding day.</param>
    /// <param name="EndDay">The end (deadline, or the day it was completed), counted from the seeding day.</param>
    /// <param name="Author">The person who created the task.</param>
    /// <param name="Assignees">The people the task is assigned to; all of them are members.</param>
    /// <param name="Tags">The titles of the task's tags, or <c>null</c>.</param>
    /// <param name="Comments">The comments, oldest first, or <c>null</c>.</param>
    internal sealed record TaskDefinition(
        string Topic,
        string Title,
        string? Description,
        ProjectTaskStatus Status,
        TaskPriority Priority,
        int StartDay,
        int? EndDay,
        string Author,
        IReadOnlyList<string> Assignees,
        IReadOnlyList<string>? Tags = null,
        IReadOnlyList<CommentDefinition>? Comments = null);

    /// <summary>
    /// A comment on a demo task.
    /// </summary>
    /// <param name="Author">The person who wrote it; a member of the project.</param>
    /// <param name="Day">When it was written, counted from the seeding day (always in the past).</param>
    /// <param name="Content">The text.</param>
    internal sealed record CommentDefinition(string Author, int Day, string Content);
}
