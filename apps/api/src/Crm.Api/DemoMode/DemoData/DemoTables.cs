using System.Globalization;
using Crm.Domain.Entities;

namespace Crm.Api.DemoMode.DemoData;

/// <summary>
/// The hand-built demo tables next to the donors and the volunteers: ten everyday topics of a community
/// organisation. The rows refer to each other and to the rest of the demo data (donors, colleagues, volunteers),
/// so the knowledge base reads like the records of one organisation.
/// </summary>
/// <remarks>
/// Every value is text, as the API stores them. Dates are relative to the seeding day and written as
/// <c>yyyy-MM-dd</c>, so they never look stale and sort as text. People, organisations, e-mail addresses and phone
/// numbers are made up in the same way as in <see cref="DemoDataContent"/>.
/// </remarks>
internal static class DemoTables
{
    private const string DateFormat = "yyyy-MM-dd";

    // Free numbers of the fake phone range (see DemoDataContent.FakePhone): 33-44 and 86-95.
    private const int FirstBeneficiaryPhoneNumber = 33;
    private const int FirstPartnerPhoneNumber = 86;

    /// <summary>
    /// How the values of a column are written. The API keeps every value as text; the kind documents the format
    /// and lets the tests check the demo data.
    /// </summary>
    internal enum ValueKind
    {
        /// <summary>Free text.</summary>
        Text,

        /// <summary>A date written as <c>yyyy-MM-dd</c>.</summary>
        Date,

        /// <summary>Digits, optionally with a decimal comma.</summary>
        Number,

        /// <summary>A number from <see cref="DemoDataContent.FakePhone"/>.</summary>
        Phone,

        /// <summary>An address from <see cref="DemoDataContent.Email"/>.</summary>
        Email,

        /// <summary>"Так" or "Ні".</summary>
        YesNo,
    }

    /// <summary>
    /// Gets the tables, in the order they are created. The web lists them by name.
    /// </summary>
    public static IReadOnlyList<TableDefinition> All { get; } =
    [
        Beneficiaries(),
        AidDistribution(),
        Warehouse(),
        Trips(),
        GrantApplications(),
        Expenses(),
        Partners(),
        Events(),
        TeamTrainings(),
        HotlineRequests(),
    ];

    private static string Day(DateOnly today, int offsetDays) =>
        today.AddDays(offsetDays).ToString(DateFormat, CultureInfo.InvariantCulture);

    private static string Phone(int number) => DemoDataContent.FakePhone(number);

    private static TableDefinition Beneficiaries() => new(
        "Бенефіціари",
        "Домогосподарства, яким допомагає організація: склад родини, категорія, потреби та остання видача допомоги.",
        [
            new("Домогосподарство", IsRequired: true),
            new("Місто", IsRequired: true),
            new("Осіб у родині", Kind: ValueKind.Number),
            new("Категорія", IsRequired: true),
            new("Телефон", Kind: ValueKind.Phone),
            new("Потреби"),
            new("Остання допомога", Kind: ValueKind.Date),
            new("Статус", IsRequired: true),
        ],
        today =>
        [
            ["Родина Зразченків", "Львів", "5", "ВПО", Phone(FirstBeneficiaryPhoneNumber), "Продукти, дитячий одяг", Day(today, -3), "Отримує допомогу"],
            ["Ганна Вигадюк", "Львів", "1", "Людина похилого віку", Phone(FirstBeneficiaryPhoneNumber + 1), "Ліки, теплі речі", Day(today, -10), "Отримує допомогу"],
            ["Родина Уявненків", "Харків", "4", "Постраждалі від обстрілів", Phone(FirstBeneficiaryPhoneNumber + 2), "Плівка на вікна, ковдри", Day(today, -6), "Отримує допомогу"],
            ["Петро Макетюк", "Дніпро", "2", "Людина з інвалідністю", Phone(FirstBeneficiaryPhoneNumber + 3), "Засоби гігієни, транспорт до лікарні", Day(today, -14), "Отримує допомогу"],
            ["Родина Шаблієнків", "Київ", "6", "Багатодітна родина", Phone(FirstBeneficiaryPhoneNumber + 4), "Продукти, шкільне приладдя", Day(today, -2), "Отримує допомогу"],
            ["Віра Пробенко", "Тернопіль", "1", "Людина похилого віку", Phone(FirstBeneficiaryPhoneNumber + 5), "Продукти, допомога вдома", Day(today, -21), "На паузі"],
            ["Родина Ескізюків", "Одеса", "3", "ВПО", Phone(FirstBeneficiaryPhoneNumber + 6), "Житло, продукти", Day(today, -8), "Отримує допомогу"],
            ["Людмила Модельчук", "Вінниця", "2", "ВПО", Phone(FirstBeneficiaryPhoneNumber + 7), "Юридична консультація", Day(today, -35), "Завершено"],
            ["Родина Казкаренків", "Полтава", "5", "Багатодітна родина", Phone(FirstBeneficiaryPhoneNumber + 8), "Дитячі набори, підгузки", Day(today, -5), "Отримує допомогу"],
            ["Іван Взірчук", "Чернівці", "1", "Людина з інвалідністю", Phone(FirstBeneficiaryPhoneNumber + 9), "Слуховий апарат, супровід", Day(today, -28), "На паузі"],
            ["Галина Умовчук", "Луцьк", "3", "Постраждалі від обстрілів", Phone(FirstBeneficiaryPhoneNumber + 10), "Генератор, павербанк", Day(today, -12), "Отримує допомогу"],
            ["Родина Прикладовських", "Житомир", "4", "ВПО", Phone(FirstBeneficiaryPhoneNumber + 11), "Продукти, психологічна підтримка", Day(today, -1), "Отримує допомогу"],
        ]);

    private static TableDefinition AidDistribution() => new(
        "Видача допомоги",
        "Журнал видачі гуманітарних наборів: коли, де, кому, що саме й хто видав.",
        [
            new("Дата", IsRequired: true, Kind: ValueKind.Date),
            new("Пункт видачі", IsRequired: true),
            new("Отримувач", IsRequired: true),
            new("Набір", IsRequired: true),
            new("Кількість", Kind: ValueKind.Number),
            new("Хто видав"),
            new("Підпис отримано", Kind: ValueKind.YesNo),
            new("Примітка"),
        ],
        today =>
        [
            [Day(today, -1), "Мобільний пункт, Житомирщина", "Родина Прикладовських", "Продуктовий набір", "2", "Северин Ескізний", "Так", "Привезли разом із ліками"],
            [Day(today, -2), "Пункт на вокзалі, Київ", "Родина Шаблієнків", "Дитячий набір", "3", "Максим Примірний", "Так", null],
            [Day(today, -3), "Центральний пункт, Львів", "Родина Зразченків", "Продуктовий набір", "2", "Леся Уявна", "Так", null],
            [Day(today, -3), "Центральний пункт, Львів", "Родина Зразченків", "Гігієнічний набір", "1", "Леся Уявна", "Так", null],
            [Day(today, -5), "Волонтерський пункт, Полтава", "Родина Казкаренків", "Дитячий набір", "2", "Устим Тестенко", "Так", "Підгузків розміру 4 не було, видали розмір 5"],
            [Day(today, -6), "Мобільний пункт, Харківщина", "Родина Уявненків", "Ковдри й теплі речі", "4", "Денис Умовенко", "Так", null],
            [Day(today, -8), "Волонтерський пункт, Одеса", "Родина Ескізюків", "Продуктовий набір", "1", "Ірина Шаблонюк", "Ні", "Підпис принесуть під час наступної видачі"],
            [Day(today, -9), "Пункт на вокзалі, Київ", "Родина Шаблієнків", "Продуктовий набір", "2", "Максим Примірний", "Так", null],
            [Day(today, -10), "Центральний пункт, Львів", "Ганна Вигадюк", "Медичний набір", "1", "Леся Уявна", "Так", "Ліки за списком від сімейної лікарки"],
            [Day(today, -12), "Волонтерський пункт, Луцьк", "Галина Умовчук", "Павербанк", "1", "Лук'ян Шаблонний", "Так", "Генератор — у черзі"],
            [Day(today, -14), "Склад партнера, Дніпро", "Петро Макетюк", "Гігієнічний набір", "2", "Ярема Вигадько", "Так", "Доставили додому"],
            [Day(today, -21), "Волонтерський пункт, Тернопіль", "Віра Пробенко", "Продуктовий набір", "1", "Орися Казкова", "Так", null],
            [Day(today, -28), "Волонтерський пункт, Чернівці", "Іван Взірчук", "Медичний набір", "1", "Остап Тестовий", "Так", null],
            [Day(today, -35), "Волонтерський пункт, Вінниця", "Людмила Модельчук", "Продуктовий набір", "1", "Мирослава Прикладна", "Так", "Остання видача: родина переїхала"],
        ]);

    private static TableDefinition Warehouse() => new(
        "Склад",
        "Залишки на складі: кількість, місце зберігання, термін придатності та мінімальний запас.",
        [
            new("Найменування", IsRequired: true),
            new("Категорія", IsRequired: true),
            new("Кількість", IsRequired: true, Kind: ValueKind.Number),
            new("Одиниця", IsRequired: true),
            new("Місце зберігання"),
            new("Придатне до", Kind: ValueKind.Date),
            new("Мінімальний запас", Kind: ValueKind.Number),
            new("Джерело"),
        ],
        today =>
        [
            ["Борошно пшеничне", "Продукти", "320", "кг", "Склад № 1, Львів", Day(today, 150), "100", "Пожертви мешканців"],
            ["Крупа гречана", "Продукти", "180", "кг", "Склад № 1, Львів", Day(today, 240), "80", "Фонд «Умовна підтримка»"],
            ["Олія соняшникова", "Продукти", "95", "л", "Склад № 1, Львів", Day(today, 200), "40", "Пожертви мешканців"],
            ["Консерви м'ясні", "Продукти", "410", "шт.", "Склад № 2, Дніпро", Day(today, 540), "150", "Фонд «Умовна підтримка»"],
            ["Дитяче харчування", "Дитячі товари", "60", "банок", "Склад № 1, Львів", Day(today, 45), "80", "Корпоративна програма «Демо-Партнер»"],
            ["Підгузки, розмір 4", "Дитячі товари", "0", "уп.", "Склад № 1, Львів", null, "20", "Пожертви мешканців"],
            ["Підгузки, розмір 5", "Дитячі товари", "35", "уп.", "Склад № 1, Львів", null, "20", "Пожертви мешканців"],
            ["Мило господарське", "Гігієна", "600", "шт.", "Склад № 1, Львів", null, "200", "Пожертви мешканців"],
            ["Зубна паста", "Гігієна", "240", "шт.", "Склад № 1, Львів", Day(today, 400), "100", "Корпоративна програма «Демо-Партнер»"],
            ["Ковдри флісові", "Теплі речі", "85", "шт.", "Склад № 2, Дніпро", null, "50", "Фонд «Умовна підтримка»"],
            ["Павербанки 20 000 мА·год", "Енергонезалежність", "14", "шт.", "Склад № 1, Львів", null, "10", "Збір «Світло для громади»"],
            ["Аптечки першої допомоги", "Медицина", "48", "шт.", "Склад № 1, Львів", Day(today, 300), "20", "Корпоративна програма «Демо-Партнер»"],
            ["Генератор 3 кВт", "Енергонезалежність", "2", "шт.", "Склад № 1, Львів", null, "1", "Збір «Світло для громади»"],
        ]);

    private static TableDefinition Trips() => new(
        "Поїздки й доставка",
        "Рейси з доставки допомоги: маршрут, транспорт, водій, вантаж і статус.",
        [
            new("Дата", IsRequired: true, Kind: ValueKind.Date),
            new("Маршрут", IsRequired: true),
            new("Транспорт"),
            new("Водій"),
            new("Вантаж"),
            new("Вага, кг", Kind: ValueKind.Number),
            new("Статус", IsRequired: true),
            new("Примітки"),
        ],
        today =>
        [
            [Day(today, -30), "Львів → Харків", "Бус № 1", "Максим Примірний", "Продуктові набори, ковдри", "950", "Доставлено", null],
            [Day(today, -22), "Львів → Дніпро", "Вантажівка партнера", "Андрій Макетний", "Консерви, борошно, ковдри", "2800", "Доставлено", "Вантажівку надало ТОВ «Приклад-Логістик»"],
            [Day(today, -16), "Київ → Житомирщина", "Бус № 2", "Максим Примірний", "Дитячі й гігієнічні набори", "640", "Доставлено", null],
            [Day(today, -12), "Львів → Луцьк", "Бус № 1", "Андрій Макетний", "Продуктові набори", "400", "Перенесено", "Бус на ремонті, рейс перенесли"],
            [Day(today, -9), "Львів → Тернопіль", "Легкове авто", "Андрій Макетний", "Аптечки, ліки", "120", "Доставлено", null],
            [Day(today, -4), "Дніпро → Запорізька обл.", "Бус № 1", "Максим Примірний", "Павербанки, ковдри", "380", "Доставлено", "Вантаж розподілили між двома селами"],
            [Day(today, -1), "Львів → Житомир", "Бус № 2", "Северин Ескізний", "Продуктові набори, ліки", "720", "Доставлено", null],
            [Day(today, 0), "Львів → Харків", "Бус № 1", "Максим Примірний", "Продуктові набори", "1100", "У дорозі", "Виїхали о 6:00"],
            [Day(today, 2), "Київ → Чернігівщина", "Бус № 2", "Андрій Макетний", "Гігієнічні набори, підгузки", "560", "Заплановано", "Чекаємо на підгузки розміру 4"],
            [Day(today, 5), "Львів → Дніпро", "Вантажівка партнера", "Северин Ескізний", "Генератор, ковдри", "1500", "Заплановано", "Генератор передаємо на склад № 2"],
        ]);

    private static TableDefinition GrantApplications() => new(
        "Заявки на гранти",
        "Заявки до донорів: проєкт, сума запиту, статус розгляду та дедлайн звіту.",
        [
            new("Проєкт", IsRequired: true),
            new("Донор", IsRequired: true),
            new("Сума запиту, грн", Kind: ValueKind.Number),
            new("Подано", Kind: ValueKind.Date),
            new("Статус", IsRequired: true),
            new("Звіт до", Kind: ValueKind.Date),
            new("Відповідальна особа"),
            new("Примітки"),
        ],
        today =>
        [
            ["Мобільний пункт видачі допомоги", "Фонд «Умовна підтримка»", "480000", Day(today, -120), "Звіт здано", Day(today, -10), "Ірина Шаблонюк", "Звіт прийняли без зауважень"],
            ["Літній табір для дітей ВПО", "Фонд «Ескіз майбутнього»", "350000", Day(today, -160), "Звіт здано", Day(today, -60), "Соломія Ескізна", null],
            ["Психологічна підтримка для ВПО", "Міжнародна ініціатива «Приклад для громад»", "950000", Day(today, -75), "Схвалено", Day(today, 105), "Дарина Вигадана", "Групи підтримки вже працюють у Харкові"],
            ["Навчання волонтерів першої допомоги", "Корпоративна програма «Демо-Партнер»", "80000", Day(today, -55), "Схвалено", Day(today, 35), "Соломія Ескізна", "Частину внеску отримали товарами: аптечки"],
            ["Курси цифрової грамотності 55+", "Фундація «Тестовий розвиток»", "210000", Day(today, -30), "Схвалено", Day(today, 150), "Богдан Пробний", "Заняття — у міській бібліотеці «Шаблон»"],
            ["Енергонезалежний гуманітарний хаб", "Партнерство «Шаблон сталості»", "1200000", Day(today, -40), "На розгляді", null, "Андрій Макетний", "Донор попросив ще дві комерційні пропозиції на генератори"],
            ["Юридичні консультації онлайн", "Грантова програма «Макет змін»", "620000", Day(today, -20), "На розгляді", null, "Тарас Прикладенко", null],
            ["Продуктові набори для літніх людей", "Благодійний фонд «Демо-Добро»", "300000", Day(today, -14), "Подано", null, "Ірина Шаблонюк", null],
            ["Інклюзивний простір у бібліотеці", "Освітня ініціатива «Модельна школа»", "270000", Day(today, -5), "Подано", null, "Олена Зразкова", null],
            ["Комунікаційна кампанія «Поруч»", "Програма малих грантів «Зразок»", "95000", Day(today, -200), "Відхилено", null, "Богдан Пробний", "Подамо ще раз у наступному раунді"],
            ["Сад громади", "Фонд громад «Вигаданий край»", "120000", null, "Чернетка", null, "Тарас Прикладенко", "Зібрати підписи мешканців до подання"],
        ]);

    private static TableDefinition Expenses() => new(
        "Витрати за проєктами",
        "Облік витрат: на що й за яким проєктом витратили кошти, постачальник, документ і стан оплати.",
        [
            new("Дата", IsRequired: true, Kind: ValueKind.Date),
            new("Стаття витрат", IsRequired: true),
            new("Проєкт", IsRequired: true),
            new("Сума, грн", IsRequired: true, Kind: ValueKind.Number),
            new("Постачальник"),
            new("Документ"),
            new("Оплата", IsRequired: true),
        ],
        today =>
        [
            [Day(today, -1), "Пальне для рейсу Львів → Харків", "Збір «Світло для громади»", "6200", "ТОВ «Приклад-Логістик»", "Рахунок № ПЛ-0412", "Очікує оплати"],
            [Day(today, -3), "Оренда класу для груп підтримки", "Психологічна підтримка для ВПО", "12000", "Ліцей «Взірець»", "Договір № 7/2", "Оплачено"],
            [Day(today, -5), "Ноутбуки для курсів, 3 шт.", "Курси цифрової грамотності 55+", "54000", "ФОП Взірченко Л.", "Накладна № 145", "Оплачено"],
            [Day(today, -7), "Манекени для навчання серцево-легеневої реанімації", "Навчання волонтерів першої допомоги", "18500", "ТОВ «Демо-Мед»", "Рахунок № ДМ-88", "Частково оплачено"],
            [Day(today, -9), "Друк листівок про гарячу лінію", "Адміністративні витрати", "3200", "Друкарня «Шаблон-Прінт»", "Рахунок № 2311", "Оплачено"],
            [Day(today, -11), "Ремонт буса № 1", "Адміністративні витрати", "14700", "ФОП Макетович Д.", "Акт № 31", "Оплачено"],
            [Day(today, -12), "Хліб для продуктових наборів", "Пожертви мешканців", "4800", "Пекарня «Ескіз»", "Накладна № 52", "Оплачено"],
            [Day(today, -15), "Генератор 3 кВт", "Збір «Світло для громади»", "38900", "ФОП Умовицький Р.", "Рахунок № 19", "Оплачено"],
            [Day(today, -18), "Супервізія психологів", "Психологічна підтримка для ВПО", "9000", "ФОП Вигадчук І.", "Акт № 4", "Оплачено"],
            [Day(today, -25), "Ліки за рецептами для людей похилого віку", "Пожертви мешканців", "7600", "Мережа аптек «Модельна»", "Накладна № 004512", "Оплачено"],
            [Day(today, -60), "Оренда буса для мобільного пункту", "Мобільний пункт видачі допомоги", "36000", "ТОВ «Приклад-Логістик»", "Договір № 12", "Оплачено"],
            [Day(today, -95), "Намет і обладнання для мобільного пункту", "Мобільний пункт видачі допомоги", "47300", "ФОП Шаблевич К.", "Накладна № 77", "Оплачено"],
        ]);

    private static TableDefinition Partners() => new(
        "Партнери",
        "Організації та компанії, з якими співпрацюємо: контакти, напрям співпраці й меморандуми.",
        [
            new("Організація", IsRequired: true),
            new("Тип", IsRequired: true),
            new("Місто"),
            new("Контактна особа"),
            new("Електронна пошта", Kind: ValueKind.Email),
            new("Телефон", Kind: ValueKind.Phone),
            new("Напрям співпраці"),
            new("Меморандум"),
        ],
        _ =>
        [
            ["ГО «Зразкові сусіди»", "Громадська організація", "Львів", "Василь Зразкевич", DemoDataContent.Email("zrazkovi.susidy"), Phone(FirstPartnerPhoneNumber), "Спільні видачі допомоги, житло для переселенців", "Так"],
            ["ТОВ «Приклад-Логістик»", "Бізнес", "Київ", "Ольга Прикладюк", DemoDataContent.Email("pryklad.logistyk"), Phone(FirstPartnerPhoneNumber + 1), "Склад і перевезення вантажів, пальне", "Так"],
            ["Управління соцзахисту Вигаданської громади", "Місцева влада", "Полтава", "Юрій Вигадовський", DemoDataContent.Email("sotszakhyst.vyhadanska"), Phone(FirstPartnerPhoneNumber + 2), "Направляють людей, які потребують допомоги", "Так"],
            ["Ліцей «Взірець»", "Навчальний заклад", "Харків", "Наталія Уявнюк", DemoDataContent.Email("litsei.vzirets"), Phone(FirstPartnerPhoneNumber + 3), "Класи для груп підтримки й тренінгів", "У процесі"],
            ["Амбулаторія «Тестова»", "Медичний заклад", "Дніпро", "Ігор Макетенко", DemoDataContent.Email("ambulatoriia.testova"), Phone(FirstPartnerPhoneNumber + 4), "Медичні огляди для бенефіціарів", "Так"],
            ["Пекарня «Ескіз»", "Бізнес", "Івано-Франківськ", "Артем Ескізенко", DemoDataContent.Email("pekarnia.eskiz"), Phone(FirstPartnerPhoneNumber + 5), "Хліб для продуктових наборів", "Ні"],
            ["Мережа аптек «Модельна»", "Бізнес", "Вінниця", "Світлана Модельська", DemoDataContent.Email("apteky.modelna"), Phone(FirstPartnerPhoneNumber + 6), "Ліки зі знижкою для бенефіціарів", "Так"],
            ["Радіо «Макет FM»", "Медіа", "Полтава", "Тетяна Шаблинська", DemoDataContent.Email("maket.fm"), Phone(FirstPartnerPhoneNumber + 7), "Анонси заходів і зборів", "Ні"],
            ["Міська бібліотека «Шаблон»", "Заклад культури", "Чернівці", "Олег Казковський", DemoDataContent.Email("biblioteka.shablon"), Phone(FirstPartnerPhoneNumber + 8), "Інклюзивний простір, кіноклуб, курси для старших людей", "У процесі"],
            ["Благодійна їдальня «Казкова ложка»", "Громадська організація", "Луцьк", "Марина Тестюк", DemoDataContent.Email("idalnia.kazkova"), Phone(FirstPartnerPhoneNumber + 9), "Гарячі обіди для людей похилого віку", "Так"],
        ]);

    private static TableDefinition Events() => new(
        "Заходи",
        "Заходи організації — зустрічі громади, тренінги, ярмарки: дата, формат, учасники та стан підготовки.",
        [
            new("Захід", IsRequired: true),
            new("Дата", IsRequired: true, Kind: ValueKind.Date),
            new("Місто"),
            new("Формат", IsRequired: true),
            new("Учасників (план)", Kind: ValueKind.Number),
            new("Учасників (факт)", Kind: ValueKind.Number),
            new("Відповідальна особа"),
            new("Статус", IsRequired: true),
        ],
        today =>
        [
            ["Вечір настільних ігор для підлітків", Day(today, -40), "Полтава", "Офлайн", "20", "9", "Дарина Вигадана", "Проведено"],
            ["Зустріч громади: потреби на наступні місяці", Day(today, -25), "Львів", "Офлайн", "40", "52", "Олена Зразкова", "Проведено"],
            ["Тренінг з домедичної допомоги для мешканців", Day(today, -18), "Київ", "Офлайн", "20", "18", "Соломія Ескізна", "Проведено"],
            ["Пікнік волонтерів", Day(today, -12), "Ужгород", "Офлайн", "35", null, "Соломія Ескізна", "Скасовано"],
            ["Онлайн-воркшоп: як написати грантову заявку", Day(today, -9), null, "Онлайн", "60", "47", "Ірина Шаблонюк", "Проведено"],
            ["Арт-терапія для дітей", Day(today, -4), "Харків", "Офлайн", "15", "15", "Дарина Вигадана", "Проведено"],
            ["Майстерня з плетіння маскувальних сіток", Day(today, 3), "Тернопіль", "Офлайн", "25", null, "Соломія Ескізна", "Реєстрацію відкрито"],
            ["Кіноклуб «Поруч»", Day(today, 6), "Чернівці", "Офлайн", "30", null, "Остап Тестовий", "Реєстрацію відкрито"],
            ["Форум волонтерів регіону", Day(today, 14), "Дніпро", "Змішаний", "120", null, "Тарас Прикладенко", "Заплановано"],
            ["Благодійний ярмарок «Добро поруч»", Day(today, 21), "Львів", "Офлайн", "300", null, "Богдан Пробний", "Заплановано"],
        ]);

    private static TableDefinition TeamTrainings() => new(
        "Навчання команди",
        "Тренінги для команди й волонтерів: тема, хто проводить, тривалість, учасники та оцінка.",
        [
            new("Тренінг", IsRequired: true),
            new("Дата", IsRequired: true, Kind: ValueKind.Date),
            new("Хто проводить"),
            new("Тривалість, год", Kind: ValueKind.Number),
            new("Учасників", Kind: ValueKind.Number),
            new("Сертифікат", Kind: ValueKind.YesNo),
            new("Оцінка (з 5)", Kind: ValueKind.Number),
            new("Нотатки"),
        ],
        today =>
        [
            ["Домедична допомога: зупинка кровотечі", Day(today, -60), "Запрошена інструкторка, ГО «Зразкові сусіди»", "6", "18", "Так", "4,9", "Практика на манекенах інструкторки"],
            ["Психологічна перша допомога", Day(today, -45), "Дарина Вигадана", "4", "22", "Так", "4,8", null],
            ["Безпека волонтерів у прифронтових громадах", Day(today, -38), "Андрій Макетний", "3", "15", "Ні", "4,6", "Оновили правила виїздів"],
            ["Робота з персональними даними", Day(today, -30), "Назар Умовний", "2", "25", "Ні", "4,4", "Оновили форму згоди на обробку даних"],
            ["Фандрейзинг для малих ГО", Day(today, -21), "Ірина Шаблонюк", "3", "12", "Так", "4,7", null],
            ["Фінансова звітність за грантами", Day(today, -14), "Остап Тестовий", "2", "8", "Ні", "4,5", "Шаблон кошторису розіслали команді"],
            ["Комунікації в соцмережах", Day(today, -7), "Богдан Пробний", "2", "16", "Ні", "4,3", null],
            ["Цифрова безпека", Day(today, 4), "Запрошений тренер, ліцей «Взірець»", "3", null, "Так", null, "Реєстрація триває"],
            ["Протидія вигоранню", Day(today, 11), "Дарина Вигадана", "2", null, "Ні", null, null],
            ["Інклюзивна мова в роботі з людьми", Day(today, 18), "Соломія Ескізна", "2", null, "Ні", null, null],
        ]);

    private static TableDefinition HotlineRequests() => new(
        "Звернення на гарячу лінію",
        "Звернення людей на гарячу лінію: канал, тема, пріоритет, хто опрацьовує і чим завершилось.",
        [
            new("Дата", IsRequired: true, Kind: ValueKind.Date),
            new("Канал", IsRequired: true),
            new("Тема", IsRequired: true),
            new("Місто"),
            new("Пріоритет", IsRequired: true),
            new("Статус", IsRequired: true),
            new("Хто опрацьовує"),
            new("Результат"),
        ],
        today =>
        [
            [Day(today, 0), "Телефон", "Потрібен продуктовий набір", "Житомир", "Середній", "Нове", null, null],
            [Day(today, 0), "Форма на сайті", "Хочу стати волонтером", "Львів", "Низький", "Нове", "Соломія Ескізна", null],
            [Day(today, -1), "Месенджер", "Транспорт до лікарні на діаліз", "Дніпро", "Високий", "У роботі", "Андрій Макетний", "Шукаємо водія на четвер"],
            [Day(today, -2), "Телефон", "Психологічна підтримка після обстрілу", "Харків", "Високий", "У роботі", "Дарина Вигадана", "Перша консультація вже відбулася"],
            [Day(today, -3), "Електронна пошта", "Оформлення статусу ВПО", "Одеса", "Середній", "Вирішено", "Тарас Прикладенко", "Допомогли зібрати документи"],
            [Day(today, -4), "Особисто", "Пошук житла для родини з дітьми", "Львів", "Високий", "Перенаправлено", "Олена Зразкова", "Передали до ГО «Зразкові сусіди»"],
            [Day(today, -6), "Телефон", "Ліки для хронічних хвороб", "Вінниця", "Високий", "Вирішено", "Тарас Прикладенко", "Ліки видали через мережу аптек «Модельна»"],
            [Day(today, -8), "Месенджер", "Юридична консультація щодо пошкодженого житла", "Харків", "Середній", "Вирішено", "Тарас Прикладенко", "Консультував юрист партнерів"],
            [Day(today, -10), "Форма на сайті", "Пропозиція партнерства від пекарні", "Івано-Франківськ", "Низький", "Вирішено", "Ірина Шаблонюк", "Домовилися про пробний місяць"],
            [Day(today, -11), "Телефон", "Подяка за допомогу", "Тернопіль", "Низький", "Вирішено", null, "Подяку передали волонтерам"],
            [Day(today, -13), "Електронна пошта", "Запит журналістів про роботу пункту видачі", "Полтава", "Середній", "Вирішено", "Богдан Пробний", "Інтерв'ю на радіо «Макет FM»"],
            [Day(today, -15), "Особисто", "Потрібен генератор для родини з немовлям", "Луцьк", "Високий", "У роботі", "Андрій Макетний", "Видали павербанк, генератор — у черзі"],
            [Day(today, -18), "Телефон", "Допомога з оплатою комунальних послуг", "Полтава", "Середній", "Перенаправлено", "Тарас Прикладенко", "Направили до управління соцзахисту"],
            [Day(today, -20), "Месенджер", "Потрібен слуховий апарат", "Чернівці", "Середній", "У роботі", "Остап Тестовий", "Шукаємо донора"],
        ]);

    /// <summary>
    /// A column of a demo table.
    /// </summary>
    /// <param name="Name">The field name.</param>
    /// <param name="IsRequired">Whether every row must have a value.</param>
    /// <param name="Kind">How the values are written.</param>
    internal sealed record ColumnDefinition(string Name, bool IsRequired = false, ValueKind Kind = ValueKind.Text);

    /// <summary>
    /// A demo table: its name, description, columns and rows (values in column order).
    /// </summary>
    /// <param name="name">The table name.</param>
    /// <param name="description">The table description.</param>
    /// <param name="columns">The columns, in order.</param>
    /// <param name="rows">Builds the rows for the seeding day; every row has one value per column.</param>
    internal sealed class TableDefinition(
        string name,
        string description,
        IReadOnlyList<ColumnDefinition> columns,
        Func<DateOnly, string?[][]> rows)
    {
        /// <summary>
        /// Gets the table name.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Gets the table description.
        /// </summary>
        public string Description { get; } = description;

        /// <summary>
        /// Gets the columns, in order.
        /// </summary>
        public IReadOnlyList<ColumnDefinition> Columns { get; } = columns;

        /// <summary>
        /// Builds the field schema of the table.
        /// </summary>
        /// <returns>One field per column, in column order.</returns>
        public List<CustomField> CreateFields() =>
        [
            .. Columns.Select((column, index) => new CustomField
            {
                Name = column.Name,
                IsRequired = column.IsRequired,
                Order = (byte)index,
            }),
        ];

        /// <summary>
        /// Builds the rows of the table: values by field name, dates relative to <paramref name="now"/>.
        /// </summary>
        /// <param name="now">The seeding moment.</param>
        /// <returns>The rows, in the authored order.</returns>
        /// <exception cref="InvalidOperationException">Thrown when a row does not have one value per column.</exception>
        public List<Dictionary<string, string?>> CreateRows(DateTimeOffset now) =>
            [.. rows(DateOnly.FromDateTime(now.UtcDateTime)).Select(ToRow)];

        private Dictionary<string, string?> ToRow(string?[] values)
        {
            if (values.Length != Columns.Count)
            {
                throw new InvalidOperationException(
                    $"A row of the demo table '{Name}' has {values.Length} values for {Columns.Count} columns.");
            }

            var row = new Dictionary<string, string?>(StringComparer.Ordinal);

            for (int i = 0; i < values.Length; i++)
            {
                row[Columns[i].Name] = values[i];
            }

            return row;
        }
    }
}
