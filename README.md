# Отзывы о магазине — мод для Anime Shop Simulator

![Anime Shop Simulator](https://img.shields.io/badge/Anime%20Shop%20Simulator-1.0.6-f6a800)
![Version](https://img.shields.io/badge/version-0.4.4-1685d1)
![WolfCore](https://img.shields.io/badge/requires-WolfCore-1685d1)
![MelonLoader](https://img.shields.io/badge/MelonLoader-0.7.3-7952b3)
![License](https://img.shields.io/badge/license-MIT-2ea44f)

**Отзывы о магазине** — мод для **Anime Shop Simulator** на базе **MelonLoader** и **WolfCore**. Он добавляет рейтинг и редкие отзывы покупателей, составленные по событиям их визита.

Лишь часть клиентов публикует отзыв. Удачный визит обычно проходит без комментария, а серьёзные проблемы повышают вероятность негативной оценки.

## Скачать

Готовая сборка находится в **[последнем выпуске](https://github.com/Midfr0st/AnimeShopSimulator-ShopReviews/releases/latest)**.

Для работы необходим [WolfCore](https://github.com/Midfr0st/AnimeShopSimulator-WolfCore).

## Возможности

- рейтинг магазина от 0 до 5 звёзд;
- имя покупателя, игровой день, оценка и естественно составленный текст;
- отдельная страница `Отзывы` в игровом терминале;
- необязательная панель рейтинга в обычном игровом интерфейсе;
- обычные или минималистичные уведомления о новых отзывах;
- редкие публикации с повышенным шансом для негативных визитов;
- сохранение отзывов отдельно от игрового сейва.

Оценка может учитывать покупку или отказ, очередь, чистоту, наличие товара, цену, обслуживание, возрастную проверку, запрос манги, доставку и тесноту магазина.

## Установка

1. Полностью закройте игру.
2. Установите [MelonLoader](https://github.com/LavaGang/MelonLoader).
3. Установите [WolfCore](https://github.com/Midfr0st/AnimeShopSimulator-WolfCore/releases/latest).
4. Скачайте `WolfShopReviews.dll` из [Releases](https://github.com/Midfr0st/AnimeShopSimulator-ShopReviews/releases/latest).
5. Поместите обе DLL в папку `Anime Shop Simulator\Mods`.
6. Запустите игру.

Подробности: [INSTALLATION.ru.md](docs/INSTALLATION.ru.md).

## Настройки

Откройте `Esc` → `Моды` → `Отзывы о магазине`. Там можно независимо настроить:

- отображение рейтинга на экране;
- уведомления о новых отзывах;
- минималистичный формат уведомлений;
- вероятности публикации для разных типов визита.

Уведомления появляются во время обычной игры и скрываются вместе с основным интерфейсом в меню и терминале.

## Данные пользователя

```text
Anime Shop Simulator\UserData\WolfShopReviews.json
Anime Shop Simulator\UserData\WolfShopReviews.settings.json
```

Игровое сохранение мод не изменяет.

## Совместимость

- Anime Shop Simulator `1.0.6`;
- MelonLoader `0.7.3`;
- WolfCore `0.2.5`;
- Windows x64, Unity IL2CPP.

## Если что-то не работает

См. [решение проблем](docs/TROUBLESHOOTING.ru.md). Для отчёта приложите `MelonLoader\Latest.log` и создайте обращение в [GitHub Issues](https://github.com/Midfr0st/AnimeShopSimulator-ShopReviews/issues).

## Связанные проекты

- [WolfCore](https://github.com/Midfr0st/AnimeShopSimulator-WolfCore) — обязательное ядро и меню настроек;
- [Фильтры полок](https://github.com/Midfr0st/AnimeShopSimulator-ShelfFilters);
- [Статистика товаров](https://github.com/Midfr0st/AnimeShopSimulator-ProductStatistics);
- [Расписание работников](https://github.com/Midfr0st/AnimeShopSimulator-EmployeeSchedules).

## Лицензия

Проект распространяется по условиям [MIT License](LICENSE). Это неофициальная пользовательская модификация. Мод предоставляется «как есть» и используется на свой риск.
