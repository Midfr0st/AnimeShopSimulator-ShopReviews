namespace WolfShopReviews;

internal enum ReviewFactor
{
    General,
    Cleanliness,
    Queue,
    Stock,
    Price,
    Service,
    AgeCheck,
    MangaRequest,
    CourierOrder,
    Crowding
}

internal sealed class ReviewObservation
{
    public ReviewFactor Factor { get; init; }
    public int Tone { get; init; }
    public float Importance { get; init; } = 1f;
}

internal sealed class ReviewDraft
{
    public int Seed { get; init; }
    public int Stars { get; init; }
    public int GenderIndex { get; init; } = -1;
    public string ShopName { get; init; } = string.Empty;
    public IReadOnlyList<ReviewObservation> Observations { get; init; } = Array.Empty<ReviewObservation>();
}

internal sealed class GeneratedReviewText
{
    public string ReviewerName { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public List<ReviewFactor> Factors { get; init; } = new();
}

internal static class ReviewTextGenerator
{
    private static readonly string[] MaleNames =
    {
        "Алексей М.", "Андрей К.", "Антон В.", "Артём С.", "Вадим П.", "Виктор Н.",
        "Денис А.", "Дмитрий Л.", "Егор Р.", "Иван Т.", "Илья Б.", "Кирилл Д.",
        "Максим Ф.", "Михаил Г.", "Никита Е.", "Олег З.", "Павел И.", "Роман Ш.",
        "Сергей Ю.", "Тимофей О.", "Фёдор Я.", "Юрий Х."
    };

    private static readonly string[] FemaleNames =
    {
        "Алёна М.", "Алина К.", "Анастасия В.", "Анна С.", "Валерия П.", "Виктория Н.",
        "Дарья А.", "Ева Л.", "Екатерина Р.", "Ирина Т.", "Ксения Б.", "Марина Д.",
        "Мария Ф.", "Надежда Г.", "Наталья Е.", "Ольга З.", "Полина И.", "Светлана Ш.",
        "София Ю.", "Татьяна О.", "Юлия Я.", "Яна Х."
    };

    private static readonly string[] NeutralNames =
    {
        "Саша М.", "Женя К.", "Валя С.", "Слава П.", "Лера Н.", "Ника А.",
        "Тоня Р.", "Даня Б.", "Рита Д.", "Крис Ф."
    };

    private static readonly Dictionary<(ReviewFactor Factor, int Tone), string[]> FactorSentences = new()
    {
        [(ReviewFactor.General, 2)] = new[]
        {
            "Покупка прошла без каких-либо проблем.",
            "Всё прошло легко и приятно.",
            "Получилось купить всё без лишних хлопот.",
            "Поход за покупками оказался на редкость удачным.",
            "В магазине всё устроено удобно для покупателей.",
            "Визит прошёл именно так, как и хотелось.",
            "От покупки остались только хорошие впечатления."
        },
        [(ReviewFactor.General, 1)] = new[]
        {
            "В целом всё прошло хорошо.",
            "От посещения осталось приятное впечатление.",
            "Обычный хороший поход за покупками.",
            "Зашёл за покупкой и остался доволен.",
            "Магазин оставил хорошее впечатление.",
            "Визит прошёл спокойно и без неприятных сюрпризов.",
            "Покупать здесь было вполне комфортно."
        },
        [(ReviewFactor.General, 0)] = new[]
        {
            "Впечатления остались смешанные.",
            "В целом всё обычно, без особых впечатлений.",
            "Есть как приятные моменты, так и недочёты."
        },
        [(ReviewFactor.General, -1)] = new[]
        {
            "Посещение оказалось менее приятным, чем ожидалось.",
            "В этот раз впечатление от магазина было скорее отрицательным.",
            "Есть несколько вещей, которые стоило бы исправить."
        },
        [(ReviewFactor.General, -2)] = new[]
        {
            "Посещение магазина сильно разочаровало.",
            "Покупка обернулась сплошными неудобствами.",
            "Такой поход в магазин повторять не хочется."
        },

        [(ReviewFactor.Cleanliness, 2)] = new[]
        {
            "В торговом зале очень чисто, находиться там приятно.",
            "Отдельно понравились порядок и чистота в магазине.",
            "Полки и торговый зал содержатся в отличном состоянии.",
            "Редко встретишь настолько ухоженный торговый зал.",
            "Ни мусора, ни беспорядка — за магазином отлично следят.",
            "Чистота здесь сразу бросается в глаза.",
            "В таком аккуратном магазине приятно задержаться подольше."
        },
        [(ReviewFactor.Cleanliness, 1)] = new[]
        {
            "В магазине чисто и аккуратно.",
            "Порядок в торговом зале порадовал.",
            "За чистотой здесь явно следят.",
            "Торговый зал выглядит ухоженным.",
            "Внутри было аккуратно и опрятно.",
            "С уборкой в магазине всё в порядке.",
            "Приятно, что вокруг чисто."
        },
        [(ReviewFactor.Cleanliness, -1)] = new[]
        {
            "В торговом зале было не очень чисто.",
            "Магазину не помешало бы уделять больше внимания уборке.",
            "Беспорядок немного испортил впечатление."
        },
        [(ReviewFactor.Cleanliness, -2)] = new[]
        {
            "Грязь в торговом зале испортила всё впечатление.",
            "В магазине было настолько грязно, что хотелось поскорее уйти.",
            "Состояние торгового зала оказалось совершенно неприемлемым."
        },

        [(ReviewFactor.Queue, 2)] = new[]
        {
            "На кассе обслужили очень быстро, ждать почти не пришлось.",
            "Очередь прошла незаметно — обслуживание действительно быстрое.",
            "На оплату ушло меньше минуты, что приятно удивило.",
            "Касса сработала быстро, задерживаться не пришлось.",
            "Рассчитали почти сразу — отличный темп работы.",
            "Даже остановиться в очереди толком не успел.",
            "Оплата заняла совсем немного времени."
        },
        [(ReviewFactor.Queue, 1)] = new[]
        {
            "Очередь двигалась достаточно быстро.",
            "На кассе долго ждать не пришлось.",
            "Обслуживание на кассе было довольно оперативным.",
            "У кассы всё прошло без заметной задержки.",
            "Ждать пришлось совсем недолго.",
            "С очередью справились нормально.",
            "Оплатить покупку удалось довольно быстро."
        },
        [(ReviewFactor.Queue, -1)] = new[]
        {
            "Очередь двигалась медленно, пришлось подождать.",
            "На кассе хотелось бы более быстрого обслуживания.",
            "Ожидание в очереди немного затянулось."
        },
        [(ReviewFactor.Queue, -2)] = new[]
        {
            "На кассе потерялось слишком много времени из-за длинной очереди.",
            "Очередь почти не двигалась, ожидание было утомительным.",
            "Обслуживание на кассе оказалось непозволительно медленным."
        },

        [(ReviewFactor.Stock, 2)] = new[]
        {
            "Все нужные товары оказались в наличии.",
            "Ассортимент порадовал: нашлось всё, что было нужно.",
            "Полки хорошо заполнены, выбор отличный.",
            "Ассортимент оказался даже лучше, чем ожидалось.",
            "Удалось найти сразу все товары из списка.",
            "Выбор большой, а нужные позиции были на месте.",
            "За наличием здесь следят очень хорошо."
        },
        [(ReviewFactor.Stock, 1)] = new[]
        {
            "С наличием товаров в целом всё хорошо.",
            "Нужный товар удалось найти без проблем.",
            "Выбор товаров оказался вполне достойным.",
            "За нужной покупкой долго ходить не пришлось.",
            "Ассортимент оставил хорошее впечатление.",
            "Большинство интересующих товаров было на полках.",
            "С запасами в магазине всё неплохо."
        },
        [(ReviewFactor.Stock, -1)] = new[]
        {
            "Не все нужные товары оказались в наличии.",
            "На полках не хватало части ассортимента.",
            "Хотелось бы, чтобы запасы пополняли быстрее."
        },
        [(ReviewFactor.Stock, -2)] = new[]
        {
            "Пришлось уйти без покупки: нужного товара на полке не оказалось.",
            "Того, ради чего был визит, в наличии не нашлось.",
            "Пустые полки полностью испортили поход в магазин."
        },

        [(ReviewFactor.Price, 2)] = new[]
        {
            "Цены приятно удивили и показались очень выгодными.",
            "За такие товары цены просто отличные.",
            "Соотношение цены и покупки оказалось прекрасным.",
            "Покупка вышла выгоднее, чем рассчитывал.",
            "Стоимость товаров приятно порадовала.",
            "За эту цену покупкой особенно доволен.",
            "Ценники здесь располагают к новым покупкам."
        },
        [(ReviewFactor.Price, 1)] = new[]
        {
            "Цены показались вполне разумными.",
            "Стоимость товаров здесь вполне устраивает.",
            "Цены в целом соответствуют ожиданиям.",
            "По стоимости всё оказалось без неприятных сюрпризов.",
            "Ценники выглядят адекватно.",
            "Покупка уложилась в ожидаемую сумму.",
            "Уровень цен вполне приемлемый."
        },
        [(ReviewFactor.Price, -1)] = new[]
        {
            "Некоторые цены показались завышенными.",
            "Хотелось бы видеть более доступные цены.",
            "Стоимость части товаров неприятно удивила."
        },
        [(ReviewFactor.Price, -2)] = new[]
        {
            "Покупку пришлось отменить из-за слишком высокой цены.",
            "Цены оказались настолько высокими, что покупать ничего не захотелось.",
            "Стоимость товаров совершенно не соответствует ожиданиям."
        },

        [(ReviewFactor.Service, 2)] = new[]
        {
            "Обслуживание было внимательным и безупречным.",
            "Сотрудники отлично справились со своей работой.",
            "Клиентам здесь действительно стараются помочь.",
            "Работа персонала заслуживает отдельной похвалы.",
            "Обслужили внимательно и профессионально.",
            "Сотрудники сделали визит особенно приятным.",
            "Такое отношение к покупателям хочется видеть чаще."
        },
        [(ReviewFactor.Service, 1)] = new[]
        {
            "Обслуживание было вежливым и аккуратным.",
            "Сотрудники оставили хорошее впечатление.",
            "С обслуживанием никаких проблем не возникло.",
            "Персонал работал спокойно и уверенно.",
            "Покупку оформили без лишней суеты.",
            "Сотрудники справились со своей задачей хорошо.",
            "К качеству обслуживания вопросов нет."
        },
        [(ReviewFactor.Service, -1)] = new[]
        {
            "Обслуживание могло бы быть внимательнее.",
            "Сотрудникам стоило бы уделять покупателям больше внимания.",
            "В работе персонала чувствовалась небольшая неорганизованность."
        },
        [(ReviewFactor.Service, -2)] = new[]
        {
            "Покупку так и не удалось нормально завершить.",
            "Сервис оказался настолько плохим, что пришлось уйти.",
            "Такое безразличие к покупателям трудно оправдать."
        },

        [(ReviewFactor.AgeCheck, 2)] = new[]
        {
            "Проверка возраста прошла быстро и корректно.",
            "Возраст проверили внимательно, но без лишней задержки.",
            "Приятно видеть ответственное отношение к возрастным ограничениям.",
            "Документы проверили грамотно и очень быстро.",
            "На кассе корректно соблюдают правила продажи 18+.",
            "Проверка прошла чётко и без неловких ситуаций."
        },
        [(ReviewFactor.AgeCheck, 1)] = new[]
        {
            "С проверкой возраста всё прошло нормально.",
            "Документы проверили спокойно и без лишних вопросов.",
            "Возрастное ограничение оформили корректно.",
            "Проверка документов не доставила неудобств.",
            "С товаром 18+ на кассе разобрались нормально.",
            "Все формальности прошли спокойно."
        },
        [(ReviewFactor.AgeCheck, -1)] = new[]
        {
            "Проверка возраста заняла больше времени, чем ожидалось.",
            "С документами на кассе возникла небольшая путаница.",
            "Процедуру проверки возраста стоило бы организовать лучше."
        },
        [(ReviewFactor.AgeCheck, -2)] = new[]
        {
            "С проверкой возраста вышла очень неприятная ситуация.",
            "На кассе неправильно разобрались с возрастным ограничением.",
            "Из-за ошибки при проверке документов покупка была испорчена."
        },

        [(ReviewFactor.MangaRequest, 2)] = new[]
        {
            "С подбором манги помогли быстро и точно.",
            "Просьбу о манге выполнили идеально.",
            "Нужную мангу нашли очень быстро — отличный сервис.",
            "Сотрудник сразу понял запрос и принёс нужный выпуск.",
            "Помощь с поиском манги приятно удивила.",
            "Редкую мангу удалось получить без долгих ожиданий."
        },
        [(ReviewFactor.MangaRequest, 1)] = new[]
        {
            "С заказом манги помогли без лишней суеты.",
            "Нужную мангу удалось получить вовремя.",
            "Просьбу о манге выполнили как надо.",
            "С поиском нужного выпуска помогли нормально.",
            "Заказанную мангу принесли без проблем.",
            "На просьбу о манге отреагировали вовремя."
        },
        [(ReviewFactor.MangaRequest, -1)] = new[]
        {
            "Ответа на просьбу о манге пришлось ждать слишком долго.",
            "С подбором нужной манги возникли сложности.",
            "Обслуживание заказа манги можно было бы ускорить."
        },
        [(ReviewFactor.MangaRequest, -2)] = new[]
        {
            "На просьбу о манге так и не отреагировали.",
            "Нужную мангу получить не удалось, хотя ожидание заняло много времени.",
            "Запрос на мангу полностью проигнорировали."
        },

        [(ReviewFactor.CourierOrder, 2)] = new[]
        {
            "Заказ для доставки подготовили очень быстро.",
            "Выдача заказа курьеру была организована отлично.",
            "Все товары для доставки собрали точно и без задержек.",
            "Курьерский заказ уже ждал и был собран правильно.",
            "Получение доставки прошло образцово.",
            "С выдачей заказа справились быстро и организованно."
        },
        [(ReviewFactor.CourierOrder, 1)] = new[]
        {
            "Заказ для доставки выдали без проблем.",
            "Работа с курьерским заказом прошла нормально.",
            "Товары для доставки подготовили вовремя.",
            "Заказ забрал без лишних вопросов.",
            "На выдаче доставки всё прошло штатно.",
            "Сборка курьерского заказа заняла разумное время."
        },
        [(ReviewFactor.CourierOrder, -1)] = new[]
        {
            "С выдачей заказа для доставки пришлось подождать.",
            "Курьерский заказ могли бы подготовить быстрее.",
            "При получении заказа возникла небольшая задержка."
        },
        [(ReviewFactor.CourierOrder, -2)] = new[]
        {
            "Заказ для доставки так и не смогли подготовить.",
            "Из-за долгого ожидания пришлось уехать без заказа.",
            "Работа с курьерским заказом оказалась полностью провалена."
        },

        [(ReviewFactor.Crowding, 1)] = new[]
        {
            "Даже при большом числе посетителей в магазине было удобно.",
            "Торговый зал организован так, что покупатели друг другу не мешают.",
            "В магазине достаточно свободного места.",
            "Между полками удобно ходить даже с другими посетителями.",
            "Планировка не создаёт толкучки.",
            "В торговом зале легко разойтись с другими покупателями."
        },
        [(ReviewFactor.Crowding, -1)] = new[]
        {
            "В торговом зале было тесновато.",
            "Из-за большого числа посетителей ходить между полками неудобно.",
            "Магазин оказался слишком переполненным."
        },
        [(ReviewFactor.Crowding, -2)] = new[]
        {
            "Внутри было настолько тесно, что пришлось уйти.",
            "Из-за переполненного торгового зала нормально выбрать товар невозможно.",
            "Толпа в магазине полностью испортила посещение."
        }
    };

    private static readonly string[][] Openings =
    {
        new[] { "Очень неудачный визит.", "Магазин сильно разочаровал.", "Осталось крайне неприятное впечатление.", "Пожалел, что зашёл именно сюда.", "Давно покупка не оставляла такого неприятного осадка.", "Поход в магазин оказался настоящим разочарованием." },
        new[] { "В этот раз посещение не порадовало.", "Впечатление скорее отрицательное.", "Магазину определённо есть над чем поработать.", "Ожидал от этого места большего.", "Визит вышел довольно неудачным.", "Покупка оставила больше вопросов, чем приятных впечатлений." },
        new[] { "Впечатления получились смешанными.", "Обычный магазин со своими плюсами и минусами.", "Посещение оставило неоднозначное впечатление.", "Ничего ужасного, но и похвалить особенно не за что.", "Визит получился самым обычным.", "Пока не решил, хочется ли вернуться снова." },
        new[] { "В целом магазин понравился.", "Хорошее место для покупок.", "Посещение оставило приятное впечатление.", "Заглянул сюда не зря.", "Вполне удачный поход за покупками.", "После визита осталось хорошее настроение.", "Магазин приятно удивил.", "Покупкой и самим посещением доволен." },
        new[] { "Отличный магазин.", "Очень приятное место для покупок.", "Посещение превзошло ожидания.", "Один из самых удачных походов за покупками.", "Редко ставлю высшую оценку, но здесь она заслужена.", "Магазин оставил исключительно приятное впечатление.", "Именно таким и должен быть хороший магазин.", "Всё прошло даже лучше, чем ожидалось." }
    };

    private static readonly string[][] ShopOpenings =
    {
        new[] { "Магазин «{shop}» сильно разочаровал.", "После посещения магазина «{shop}» осталось крайне неприятное впечатление.", "В «{shop}» больше возвращаться не хочется.", "Поход в «{shop}» оказался большой ошибкой." },
        new[] { "Магазину «{shop}» определённо есть над чем поработать.", "В этот раз посещение магазина «{shop}» не порадовало.", "От «{shop}» ожидал заметно большего.", "Визит в «{shop}» вышел довольно неудачным." },
        new[] { "Посещение магазина «{shop}» оставило неоднозначное впечатление.", "Впечатления от магазина «{shop}» получились смешанными.", "У «{shop}» есть и плюсы, и заметные недостатки.", "Пока у меня нет однозначного мнения о «{shop}»." },
        new[] { "В магазине «{shop}» было приятно делать покупки.", "После посещения магазина «{shop}» осталось приятное впечатление.", "«{shop}» оказался хорошим местом для покупок.", "Визит в «{shop}» прошёл удачно.", "Из «{shop}» ушёл в хорошем настроении.", "Магазин «{shop}» приятно удивил." },
        new[] { "«{shop}» — отличный магазин.", "Посещение «{shop}» превзошло ожидания.", "«{shop}» определённо заслуживает высшей оценки.", "От визита в «{shop}» остались только отличные впечатления.", "Теперь «{shop}» — одно из моих любимых мест.", "В «{shop}» хочется вернуться снова." }
    };

    private static readonly string[][] Closings =
    {
        new[] { "Возвращаться сюда не хочется.", "Не могу рекомендовать это место.", "Надеюсь, ситуацию исправят.", "Второго шанса магазину пока давать не хочется.", "После такого советовать магазин точно не буду.", "Очень надеюсь, что руководство обратит внимание на проблемы." },
        new[] { "Пока рекомендовать магазин сложно.", "Надеюсь, в следующий раз будет лучше.", "Без изменений возвращаться не хочется.", "Может быть, со временем здесь наведут порядок.", "Повторять такой опыт пока не планирую.", "Исправить впечатление ещё можно, но работы много." },
        new[] { "Возможно, зайду ещё раз и посмотрю, что изменится.", "Пока ставлю среднюю оценку.", "Неплохо, но улучшения точно не помешают.", "Дам магазину ещё один шанс позже.", "Есть потенциал, но сейчас всё довольно средне.", "Окончательное мнение составлю после следующего визита." },
        new[] { "Скорее всего, загляну сюда снова.", "Магазин можно рекомендовать.", "Хочется снова зайти сюда за покупками.", "При случае обязательно вернусь.", "Хороший вариант для следующего похода за покупками.", "Можно смело заглядывать сюда снова.", "Добавлю магазин в список удачных мест." },
        new[] { "Обязательно вернусь снова.", "С удовольствием порекомендую магазин другим.", "Теперь это одно из любимых мест для покупок.", "Такой магазин хочется советовать друзьям.", "Следующую покупку постараюсь сделать именно здесь.", "Пять звёзд без сомнений.", "Буду рад снова заглянуть сюда." }
    };

    public static GeneratedReviewText Generate(ReviewDraft draft)
    {
        var stars = Math.Clamp(draft.Stars, 1, 5);
        var random = new Random(draft.Seed);
        var selected = SelectObservations(draft.Observations, stars, random);
        var sentences = new List<string>(4);

        if (random.NextDouble() < 0.58 || selected.Count == 0)
            sentences.Add(PickOpening(stars, draft.ShopName, random));

        foreach (var observation in selected)
            sentences.Add(PickFactorSentence(observation, random));

        if (sentences.Count < 2 || random.NextDouble() < 0.42)
            sentences.Add(Pick(Closings[stars - 1], random));

        return new GeneratedReviewText
        {
            ReviewerName = PickReviewerName(draft.GenderIndex, random),
            Text = Normalize(string.Join(" ", sentences)),
            Factors = selected.Select(item => item.Factor).Distinct().ToList()
        };
    }

    public static IReadOnlyList<string> ValidateTemplates()
    {
        var errors = new List<string>();
        foreach (var entry in FactorSentences)
        {
            foreach (var sentence in entry.Value)
                ValidateSentence($"{entry.Key.Factor}/{entry.Key.Tone}", sentence, false, errors);
        }

        for (var index = 0; index < Openings.Length; index++)
            foreach (var sentence in Openings[index])
                ValidateSentence($"opening/{index + 1}", sentence, false, errors);

        for (var index = 0; index < ShopOpenings.Length; index++)
            foreach (var sentence in ShopOpenings[index])
                ValidateSentence($"shop-opening/{index + 1}", sentence, true, errors);

        for (var index = 0; index < Closings.Length; index++)
            foreach (var sentence in Closings[index])
                ValidateSentence($"closing/{index + 1}", sentence, false, errors);

        for (var seed = 0; seed < 2500; seed++)
        {
            var stars = seed % 5 + 1;
            var observations = Enum.GetValues<ReviewFactor>()
                .Select((factor, index) => new ReviewObservation
                {
                    Factor = factor,
                    Tone = Math.Clamp(stars - 3 + ((seed + index) % 3 - 1), -2, 2),
                    Importance = 0.5f + (index % 4) * 0.25f
                }).ToArray();
            var generated = Generate(new ReviewDraft
            {
                Seed = seed,
                Stars = stars,
                GenderIndex = seed % 3 - 1,
                ShopName = seed % 2 == 0 ? "Лисья полка" : string.Empty,
                Observations = observations
            });
            ValidateGenerated(seed, generated, errors);
        }

        return errors;
    }

    private static List<ReviewObservation> SelectObservations(
        IReadOnlyList<ReviewObservation> observations,
        int stars,
        Random random)
    {
        var desiredSign = stars >= 4 ? 1 : stars <= 2 ? -1 : 0;
        var candidates = observations
            .Where(item => item.Tone != 0)
            .OrderByDescending(item => Math.Abs(item.Tone) * item.Importance + random.NextDouble() * 0.35)
            .ToList();

        if (desiredSign != 0)
        {
            var matching = candidates.Where(item => Math.Sign(item.Tone) == desiredSign).ToList();
            if (matching.Count > 0)
                candidates = matching.Concat(candidates.Where(item => Math.Sign(item.Tone) != desiredSign)).ToList();
        }

        var wanted = candidates.Count == 0
            ? 0
            : random.NextDouble() < 0.48
                ? 1
                : random.NextDouble() < 0.82 ? 2 : 3;
        return candidates.Take(wanted).ToList();
    }

    private static string PickOpening(int stars, string shopName, Random random)
    {
        if (!string.IsNullOrWhiteSpace(shopName) && random.NextDouble() < 0.58)
            return Pick(ShopOpenings[stars - 1], random).Replace("{shop}", EscapeShopName(shopName));
        return Pick(Openings[stars - 1], random);
    }

    private static string PickFactorSentence(ReviewObservation observation, Random random)
    {
        var tone = Math.Clamp(observation.Tone, -2, 2);
        if (tone == 0 || !FactorSentences.TryGetValue((observation.Factor, tone), out var choices))
        {
            var fallbackTone = tone < 0 ? -1 : tone > 0 ? 1 : 0;
            if (!FactorSentences.TryGetValue((ReviewFactor.General, fallbackTone), out choices))
                choices = FactorSentences[(ReviewFactor.General, 0)];
        }
        return Pick(choices, random);
    }

    private static string PickReviewerName(int genderIndex, Random random)
    {
        // In the current game 0 and 1 are its two regular appearance groups. If that
        // changes in an update, a neutral pool avoids a visibly absurd hard failure.
        var pool = genderIndex switch
        {
            0 => MaleNames,
            1 => FemaleNames,
            _ => NeutralNames
        };
        return Pick(pool, random);
    }

    private static string EscapeShopName(string value)
    {
        var compact = string.Join(" ", value.Replace('\r', ' ').Replace('\n', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        compact = compact.Replace('«', '"').Replace('»', '"').Trim('"', ' ', '.', ',', '!', '?');
        return compact.Length <= 60 ? compact : compact[..60];
    }

    private static string Normalize(string value)
    {
        while (value.Contains("  ", StringComparison.Ordinal))
            value = value.Replace("  ", " ", StringComparison.Ordinal);
        return value.Trim();
    }

    private static string Pick(string[] values, Random random) => values[random.Next(values.Length)];

    private static void ValidateSentence(string source, string sentence, bool allowsShop, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(sentence))
            errors.Add($"{source}: пустая фраза");
        if (sentence != sentence.Trim())
            errors.Add($"{source}: пробел по краям: {sentence}");
        if (sentence.Contains("  ", StringComparison.Ordinal))
            errors.Add($"{source}: двойной пробел: {sentence}");
        if (!(sentence.EndsWith('.') || sentence.EndsWith('!') || sentence.EndsWith('?')))
            errors.Add($"{source}: нет завершающего знака: {sentence}");
        var stripped = sentence.Replace("{shop}", string.Empty, StringComparison.Ordinal);
        if (stripped.Contains('{') || stripped.Contains('}'))
            errors.Add($"{source}: неизвестный маркер: {sentence}");
        if (!allowsShop && sentence.Contains("{shop}", StringComparison.Ordinal))
            errors.Add($"{source}: неожиданный маркер магазина: {sentence}");
        if (allowsShop && !sentence.Contains("{shop}", StringComparison.Ordinal))
            errors.Add($"{source}: пропущен маркер магазина: {sentence}");
    }

    private static void ValidateGenerated(int seed, GeneratedReviewText review, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(review.ReviewerName))
            errors.Add($"sample {seed}: пустое имя");
        if (review.Text.Length < 25 || review.Text.Length > 420)
            errors.Add($"sample {seed}: подозрительная длина {review.Text.Length}");
        if (review.Text.Contains("{shop}", StringComparison.Ordinal) || review.Text.Contains("  ", StringComparison.Ordinal))
            errors.Add($"sample {seed}: артефакт: {review.Text}");
        if (!(review.Text.EndsWith('.') || review.Text.EndsWith('!') || review.Text.EndsWith('?')))
            errors.Add($"sample {seed}: нет завершающего знака");
    }
}
