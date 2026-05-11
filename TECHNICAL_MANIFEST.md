# Space Managers Prototype: Technical Manifest

Этот документ нужен для быстрого входа в проект другому ИИ-агенту или разработчику. Он описывает текущее устройство игры, важные команды, ключевые файлы, принятые договоренности и места, где легко сломать поведение.

## Коротко о проекте

`Space Managers Prototype` - Windows-first 2D real-time space prototype на Godot 4.6.2 Mono/.NET и C#.

Проект разделен на две основные части:

- `src/SpaceManagers.Core` - чистая C#-симуляция без Godot: физика корабля, ввод, снаряды, снапшоты мира.
- `game` - Godot-проект: сцена, рендер, ассеты, визуальные эффекты, фон, планеты, корабли, HUD.

Главная сцена Godot:

- `game/project.godot`
- `game/scenes/Main.tscn`
- основной скрипт сцены: `game/scripts/GameRoot.cs`

## Текущий Handoff Для Новой Ветки

Текущий фокус проекта: прототип постепенно двигается от одной ручной системы `Sol` к секторной галактике с offline-generated star systems. Боевая основа уже есть: корабль игрока, режимы Navigation/Combat, afterburner, HP-модель, снаряды, debug-spawn врагов, простой агрессивный AI, астероиды с солнечной гравитацией и первые оптимизации под несколько динамических объектов на сцене.

Последнее подтвержденное пользователем техническое направление: `Sol` остается эталоном качества, а generated systems должны наследовать тот же визуальный подход, а не быть набором случайных ассетов. Генерация систем происходит вне runtime игры: инструменты создают catalog-backed JSON и runtime assets до запуска, а игра загружает только активную систему.

Последние важные решения:

- сектор `Orion` сейчас стартовый; текущий generated-набор временно переведен в visual-test режим: ручной `Sol` плюс одна generated system `Planet Showcase` (`orion_0001`) со всеми 12 high-res типами планет;
- `Sol` и его дефолтное солнце не трогать без явного запроса;
- новые звезды используют `StabilizedSunView`/`sun_stabilized.gdshader` как эталонный renderer, с параметрами цвета, масштаба, corona intensity и animation speed;
- generated backgrounds больше не должны использовать растянутые fullscreen imagegen sheets; все runtime backgrounds идут через `SpaceBackdropView` и Sol-style tiled world-space texture; для high-res background tiles renderer добавляет seed-based variation масштаба/фазы/слабых слоев, чтобы геометрия тайла меньше повторялась между системами;
- generated planet maps не должны использовать raw imagegen cuts напрямую; runtime JSON должен ссылаться на обработанные `game/assets/generated/planet_surfaces/*.png`;
- миникарта и видимый космос должны получать один общий `systemTimeSeconds` из `GameRoot`, иначе орбитальные позиции расходятся после переключения системы;
- burn damage/hitbox солнца масштабируется от размера текущей звезды; это покрыто core tests;
- shield bubble/impact VFX должны оставаться внутри эллипса щита на всех размерах кораблей; особенно проверять Klissan Scout и Klissan Battleship/Cruiser.

Перед началом работы в новом чате/ветке:

1. Прочитать этот файл целиком.
2. Запустить `dotnet build` для `game/SpaceManagersPrototype.csproj`.
3. Если меняется core-логика, запустить core tests.
4. Если меняется визуал Godot, сделать headless smoke и, по возможности, проверить в реальной игре через `tools/run_game.ps1`.

## Быстрые команды

Запуск игры:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
& ".\tools\run_game.ps1"
```

Тесты core-симуляции:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
& ".\tools\test_core.ps1"
```

Сборка Godot C# проекта:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
$env:DOTNET_ROOT = "C:\CodexTools\dotnet-8.0.420"
$env:PATH = "$env:DOTNET_ROOT;$env:PATH"
& "C:\CodexTools\dotnet-8.0.420\dotnet.exe" build ".\game\SpaceManagersPrototype.csproj" --configuration Debug
```

Headless-проверка Godot:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
$dotnetDir = "C:\CodexTools\dotnet-8.0.420"
$env:DOTNET_ROOT = $dotnetDir
$env:PATH = "$dotnetDir;$env:PATH"
$godot = ".\.tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe"
& $godot --headless --path ".\game" --import
& $godot --headless --path ".\game" --quit-after 3
```

Stress-проверка с врагами:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
$dotnetDir = "C:\CodexTools\dotnet-8.0.420"
$env:DOTNET_ROOT = $dotnetDir
$env:PATH = "$dotnetDir;$env:PATH"
$godot = ".\.tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe"
& $godot --path ".\game" -- --stress-enemies=4 --stress-seconds=10 --stress-autopilot
```

Запуск конкретной star system:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
& ".\tools\run_game.ps1" -- --system=orion_0002
```

Запуск с конкретным кораблем для визуальной проверки щитов/масштаба:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
& ".\tools\run_game.ps1" -- --ship 2KlissanBattleship
```

Пересборка текущего generated-сектора Orion:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
python ".\tools\process_planet_surface_maps.py" --update-catalog
python ".\tools\create_background_tiles.py" --update-catalog
python ".\tools\generate_star_systems.py" --seed 3311337 --clean --write-image-prompts
```

Visual frame capture для проверки generated-системы:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
$godot = ".\.tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe"
& $godot --path ".\game" -- --system=orion_0002 --stress-near "Kora Vyrum II" --capture-frame-dir ".\diagnostics\visual_check" --capture-frame-prefix kora_check --capture-frame-quit
```

Экспорт Windows exe:

```powershell
cd "C:\Users\Максим\Desktop\Игры\Space Managers"
& ".\tools\export_windows.ps1"
```

Результат экспорта:

```text
build/windows/SpaceManagersPrototype.exe
```

Последний подготовленный friend-friendly archive лежал здесь:

```text
build/package/SpaceManagersPrototype_Windows_2026-05-08.zip
```

Внутри package-папки есть `START_GAME.bat`, `README_START_HERE.txt` и копия технического манифеста. Если манифест или код менялись после сборки архива, архив нужно пересобрать, иначе внутри будет старая копия.

Важно: для Godot/MSBuild предпочтительно использовать SDK из `C:\CodexTools\dotnet-8.0.420`, потому что рабочая папка содержит кириллицу, а ASCII-путь к SDK стабильнее.

## Структура директорий

```text
.tools/                         Portable Godot/.NET/downloads for local workflow
build/windows/                  Exported Windows build
game/                           Godot project
game/assets/                    Runtime assets
game/assets/asteroids/          Asteroid sprites
game/assets/effects/            Fire/glow/smoke textures for asteroid VFX
game/assets/generated/          Offline-generated galaxy runtime data/assets
game/assets/generated/systems/  Runtime JSON for generated star systems
game/assets/generated/star_sources_4k/ Direct high-res generated star sources before runtime processing
game/assets/generated/planet_sources_4k/ Direct high-res generated planet sources before runtime processing
game/assets/generated/background_sources_4k/ Direct high-res generated background sources before runtime processing
game/assets/generated/stars/    Runtime generated star disk assets from direct high-res sources or legacy fallback sheets
game/assets/generated/planets/  Raw generated planet image cuts; not preferred for runtime JSON
game/assets/generated/planet_surfaces/ Processed seamless runtime planet surface maps; direct high-res targets 4096x2048
game/assets/generated/background_tiles/ Sol-style 4096px tileable generated space backgrounds
game/assets/generated/backgrounds/ Legacy imagegen background cuts; do not use as fullscreen runtime backgrounds
game/assets/landing/            AOP-derived station tiles/props for Landing mode
game/assets/backgrounds/        Space background textures
game/assets/planets/            Planet textures, spin maps, shaders
game/assets/ships/              Ship sprites and ships_manifest.json
game/scenes/Main.tscn           Main Godot scene
game/scenes/Landing.tscn        Isometric station/landing prototype scene
game/scripts/                   Godot C# scripts
src/SpaceManagers.Core/          Engine-agnostic C# simulation
tests/SpaceManagers.Core.Tests/  Lightweight executable tests
tools/                          Run/export/asset generation scripts
tmp/                            Temporary previews/generated scratch output
Корабли/                        Source/reference ship material
```

## Runtime Architecture

`GameRoot.cs` - главный координатор игры.

Он делает следующее:

- создает и хранит `LocalSimulation`;
- читает Godot input и превращает его в `InputCommand`;
- двигает `Camera2D`;
- обновляет `ShipView`, `ProjectileLayer`, `AsteroidLayer`, `AsteroidFireLayer`, `AsteroidDebrisLayer`, `SpaceBackground`, `HudOverlay`, `ReticleView`;
- регистрирует input actions, включая `ship_afterburner` на Left Shift;
- переключает обычный/Klissan каталог кораблей по тильде и корабли внутри активного каталога по `Tab`;
- переключает generated star systems внутри активной игры (`F11` возвращает `Sol`, `F12` циклит generated systems);
- открывает звездную карту через `M` или HUD-иконку; карта временно ставит симуляцию на паузу, показывает текущие runtime systems по секторам и по `OK` только настраивает варп-двигатель на выбранную систему без фактического прыжка;
- загружает JSON generated-системы только при выборе/старте этой системы, а не весь сектор сразу;
- передает один общий `systemTimeSeconds` в `SpaceBackground` и `HudOverlay`, чтобы видимые планеты и миникарта не расходились после system switch;
- обрабатывает debug-spawn врагов, godmode, stress CLI, `--system=...`, `--stress-system-switches`, frame capture и LOD видимости врагов.

`LocalSimulation.cs` - core-симуляция.

Она не зависит от Godot и отвечает за:

- ускорение/торможение/инерцию корабля;
- поворот и strafe;
- ограничение скорости;
- afterburner;
- navigation/combat ship mode;
- стрельбу и lifetime снарядов;
- астероиды: spawn с края карты, солнечную гравитацию, HP/структуру, сгорание у солнца, столкновения;
- hitbox корабля;
- щит/броню/структуру;
- применение урона от hostile projectiles;
- простую агрессивную enemy AI-логику;
- снапшот мира через `WorldSnapshot`.

`SpaceBackground.cs` - большой визуальный слой мира.

Он отвечает за:

- tiled space background;
- parallax stars;
- Sol-style generated background tiles из `game/assets/generated/background_tiles`;
- орбиты планет;
- солнце и generated stars через стабилизированный star-layer (`StabilizedSunView`) и fallback на старую отрисовку;
- позиции планет;
- Earth через `AnimatedEarthView`;
- остальные планеты через `AnimatedPlanetView`;
- procedural rings для Saturn/Uranus;
- viewport culling для планет, звезд, орбит и солнца.

`StarSystemLoader.cs` - загрузчик runtime JSON для generated systems.

Он конвертирует JSON из `game/assets/generated/systems/*.json` в `StarSystemDefinition`, включая star/background/planet visual profile. Если поле отсутствует или файл не найден, игра должна безопасно остаться в `Sol` или пропустить систему, а не ломать старт.

`StarMapOverlay.cs`, `StarMapToggleButton.cs`, `StarMapSystemEntry.cs` - runtime-звездная карта.

Карта строится из текущего `Sol` plus compact generated galaxy index, группирует systems по `SectorId/SectorName`, рисует секторные геометрические фигуры и звезды цветом star archetype/map color. Layout адаптивный: при малом числе секторов разрешены более выразительные формы, при высокой плотности используются более емкие многоугольники, уменьшается размер звезд/шрифта и отключаются второстепенные system labels. Current/tuned/selected/hovered systems остаются приоритетными и всегда доступны через правую панель. `OK` вызывает только tune warp target; реальный warp не выполняется.

`MusicController.cs` - фоновая музыка.

Он грузит первый доступный mp3 из `res://assets/music` в editor/dev режиме или из внешней папки `music` рядом с exe в exported build. На выходе обязательно останавливает `AudioStreamPlayer` и сбрасывает `Stream`, иначе Godot headless показывает leaked `AudioStreamMP3`.

## Core Simulation

Ключевые файлы:

- `src/SpaceManagers.Core/InputCommand.cs`
- `src/SpaceManagers.Core/LocalSimulation.cs`
- `src/SpaceManagers.Core/SimulationConfig.cs`
- `src/SpaceManagers.Core/ShipState.cs`
- `src/SpaceManagers.Core/ShipHitbox.cs`
- `src/SpaceManagers.Core/CombatStats.cs`
- `src/SpaceManagers.Core/ProjectileState.cs`
- `src/SpaceManagers.Core/AsteroidState.cs`
- `src/SpaceManagers.Core/AsteroidEventState.cs`
- `src/SpaceManagers.Core/AsteroidPhysics.cs`
- `src/SpaceManagers.Core/WorldSnapshot.cs`
- `src/SpaceManagers.Core/WorldBounds.cs`

Текущие важные параметры в `SimulationConfig.cs`:

```csharp
ForwardAcceleration = 920f
ReverseAcceleration = 290f
StrafeAcceleration = 560f
TurnSpeed = 3.05f
LinearDamping = 0.72f
MaxSpeed = 500f
AfterburnerMaxSpeed = 1000f
AfterburnerHighSpeedAcceleration = 760f
ProjectileSpeed = 1450f
ProjectileDamage = 100f
MaxShield = 1000f
MaxArmor = 1000f
MaxStructure = 1000f
ShieldRegenerationPerSecond = 40f
ShieldZeroRegenerationLockout = 12f
ShipModeSwitchCooldown = 3f
ShipCollisionDamageSpeedThreshold = 140f
ShipCollisionDamageReferenceSpeed = 900f
ShipCollisionDamageScale = 0.42f
ShipCollisionDamageCooldown = 0.28f
ShipCollisionRestitution = 0.10f
ShipCollisionImpulseScale = 0.46f
ShipCollisionSeparationPercent = 0.56f
ShipCollisionMaxCorrection = 32f
ShipAiAvoidanceMargin = 260f
ShipAiAsteroidAvoidanceMargin = 360f
SunBurnDamageRadius = 660f
SunBurnDamageMinPerSecond = 10f
SunBurnDamageMaxPerSecond = 100f
AsteroidMaxActiveCount = 12
AsteroidInitialActiveCount = 8
AsteroidMinReferenceDiameter = 50f
AsteroidMaxReferenceDiameter = 150f
AsteroidMinSpeed = 100f
AsteroidMaxSpeed = 1500f
AsteroidMaxStructure = 2500f
AsteroidSunGravity = 1000f
AsteroidHeatRadius = 5200f
```

Afterburner:

- активируется при зажатом Left Shift;
- не работает при reverse;
- работает только в `ShipMode.Navigation`;
- после `MaxSpeed = 500f` разгон слабее, но может дойти до `1000f`;
- отпускание Shift не сбрасывает скорость мгновенно, инерция продолжает работать.

Ship mode:

- корабль стартует в `Navigation`;
- `R` мгновенно переключает `Navigation <-> Combat`;
- после переключения `ModeSwitchCooldown = 3 sec`, повторный toggle блокируется;
- в `Navigation` нельзя стрелять, но можно использовать afterburner;
- в `Combat` можно стрелять, но нельзя использовать afterburner;
- обычное движение `W/S/A/D/Q/E` работает в обоих режимах;
- `ReticleView` меняет внешний вид прицела в зависимости от режима;
- HUD показывает `MODE NAV/COMBAT`, `SWAP RDY` или оставшееся время cooldown.

Combat model:

- у корабля есть `Shield`, `Armor`, `Structure`;
- урон проходит в порядке `shield -> armor -> structure`;
- солнце наносит ship burn-damage от `10/sec` на границе опасной зоны до `100/sec` в центре;
- burn-damage считается в core через `AsteroidPhysics.ShipSunBurnDamagePerSecond` и учитывает радиус hitbox корабля;
- shield регенится всегда со скоростью `40/sec`, кроме случая, когда shield упал до нуля;
- если shield стал `0`, реген блокируется на `12 sec`;
- armor и structure не регенятся в текущей итерации;
- если structure `<= 0`, корабль считается уничтоженным;
- projectile damage сейчас `100`;
- hostile projectile исчезает после попадания в hitbox.
- ship-vs-ship collision включен между всеми кораблями: столкновение разделяет корабли, добавляет инерционный отскок, а при достаточной относительной скорости наносит урон обоим;
- урон от столкновения идет через обычный цикл `shield -> armor -> structure`;
- формула collision damage использует текущую `Structure` второго корабля и квадрат превышения относительной скорости над `ShipCollisionDamageSpeedThreshold`;
- мягкие касания ниже порога дают separation/impulse без урона и без impact VFX;
- для пары кораблей есть damage cooldown `ShipCollisionDamageCooldown`, чтобы один застрявший overlap не наносил урон каждый tick;
- separation после первого пользовательского теста переведен с bounding-radius correction на OBB/SAT manifold по реальному overlap hitbox, чтобы корабли не дергались резким лишним отталкиванием;
- визуальные impact-события различают источник через `ProjectileImpactKind`: `Projectile`, `AsteroidCollision`, `SunBurn`, `ShipCollision`;
- shield impact VFX в `ProjectileImpactLayer.DrawShieldImpact()` обязан садиться на геометрию эллипса щита: вспышка/частицы используют normal/tangent от `ShieldContactFor`, а не круглую локальную вспышку поверх корабля;
- полноэкранные bubble/ripple текстуры щита не должны вращаться во времени; вращение визуально читалось как "вспышка крутится" на крупных кораблях;
- синее поле щита должно доходить почти до голубого контура без темного зазора: текущая настройка в `ProjectileImpactLayer.DrawShieldImpact()` рисует bubble/ripple под толщину rim, а не глубоко внутри эллипса.

Hitbox:

- `ShipHitbox` хранит oriented rectangle: `LocalCenter` и `Size`;
- hitbox вращается вместе с кораблем;
- для выбранного корабля hitbox считается по реальной alpha-маске PNG-спрайта через `ShipCatalog.AlphaBoundsForTexture`;
- `content_bounds` из `ships_manifest.json` используется как fallback, если alpha-bounds не удалось получить;
- alpha threshold сейчас `0.28`, чтобы не включать почти прозрачные края/свечение;
- collision с projectile проверяется через segment-vs-oriented-box, чтобы быстрые снаряды не пролетали сквозь корабль между кадрами;
- collision между кораблями использует broad-phase radius и затем oriented-rectangle SAT overlap/manifold по `ShipHitbox`;
- границы мира пока используют conservative bounding radius от hitbox;
- формула shield half-extents должна быть синхронна между `src/SpaceManagers.Core/LocalSimulation.cs::ShieldHalfExtents()` и `game/scripts/ProjectileImpactLayer.cs::ShieldShapeFrom()`. Не менять одно место без второго.

Enemy/NPC combat:

- случайный ambient-мусор NPC с карты убран;
- debug-враг создается по `F5` рядом с игроком;
- текущая модель врага: `PEOPLE P`;
- каждое нажатие `F5` добавляет еще одного врага, обычный debug-limit сейчас `120`;
- враг агрессивный: преследует игрока, переключает режимы, использует afterburner в Navigation и стреляет в Combat;
- на врага действуют те же ограничения Navigation/Combat, что и на игрока;
- AI старается не лететь тупо в границу карты;
- AI имеет collision avoidance/self-preservation: старается не таранить игрока, другие корабли и астероиды, снижает напор/afterburner и добавляет steering separation, если рядом опасный объект;
- таран для AI не является нормальным паттерном атаки, а остается крайней ситуацией при плохой геометрии/инерции;
- при смерти враг удаляется, создается визуальный взрыв, награды/дропа пока нет;
- `F3` показывает hitbox у всех кораблей, астероидов и опасный радиус солнца;
- у видимых ближайших врагов рисуются HP-полоски shield/armor/structure через `EnemyStatusLayer`.

Asteroids:

- астероиды живут в `SpaceManagers.Core`, не в Godot-only визуале;
- стартовый мир сидируется несколькими астероидами через `LocalSimulation.SeedAsteroids`;
- новые астероиды спавнятся с краев мира и летят внутрь с разной скоростью/тангенциальной составляющей;
- солнце влияет на траекторию через `AsteroidPhysics.SolarGravity`;
- размер задается reference diameter `50..150`, где `1000 reference = 1320 world` для солнца;
- HP астероида - только `Structure`, максимум `2500`, растет от размера;
- урон кораблю при столкновении равен текущей структуре астероида;
- столкновение астероида с кораблем создает `ProjectileImpactKind.AsteroidCollision`, чтобы щит/броня/структура получали отдельный тяжелый каменно-пылевой visual impact;
- попадания снарядов повреждают астероиды;
- при столкновении астероидов друг с другом оба уничтожаются с каменным взрывом;
- над солнцем астероид нагревается и сгорает через `AsteroidEventType.SunBurn`;
- при вылете за границы мира астероид удаляется только после дополнительного margin, чтобы не исчезать прямо на видимой границе;
- `WorldSnapshot` теперь содержит `Asteroids` и краткоживущие `AsteroidEvents`;
- core tests проверяют spawn cap, выстрелы, урон кораблю, сгорание, столкновения и debug burst.

## Controls

Текущие базовые controls:

- `W` - forward thrust
- `S` - slow reverse thrust
- `A/D` - turn
- `Q/E` - strafe
- mouse - aim
- `R` - toggle Navigation/Combat mode
- `M` - open/close star map
- left mouse button - fire in Combat mode
- Left Shift - afterburner in Navigation mode
- `~`/backtick - toggle ordinary ship catalog / Klissan ship catalog
- `F3` - toggle debug hitboxes for ships, asteroids, and the sun danger radius
- `F5` - spawn one aggressive debug enemy near player
- `F6` - toggle player-only godmode
- `F7` - spawn one max-size debug asteroid near player, flying toward player at min speed
- `F8` - burst nearest asteroid near player; if none is close, spawn a max-size test asteroid and burst it immediately
- `Tab` - switch player ship sprite inside the active catalog

Input actions создаются в `GameRoot.ConfigureInputMap()`.

## HUD And Reticle

Ключевые файлы:

- `game/scripts/HudOverlay.cs`
- `game/scripts/ReticleView.cs`
- `game/scripts/StarMapOverlay.cs`
- `game/scripts/StarMapToggleButton.cs`
- `game/scripts/EnemyStatusLayer.cs`
- `game/scripts/AsteroidLayer.cs`
- `game/scripts/AsteroidFireLayer.cs`
- `game/scripts/AsteroidDebrisLayer.cs`

Текущее состояние:

- стандартный Windows cursor скрывается через `Input.MouseMode = Input.MouseModeEnum.Hidden`;
- `GameRoot._Process()` повторно проверяет mouse mode, чтобы курсор не возвращался поверх кастомного прицела;
- `ReticleView` рисует разные прицелы для Navigation и Combat;
- HUD снижен по масштабу относительно ранней версии и собран в нижнюю панель;
- слева показываются `SHD/ARM/STR`, полоски HP, скорость и координаты;
- в центре показываются mode и cooldown переключения;
- справа показываются ship id/name, energy и gun status;
- `F6` godmode отображается отдельным `GODMODE` label.
- миникарта в правом верхнем углу рисует астероиды и их предсказанные дуги полета;
- астероиды на миникарте отображаются не планетными шариками, а угловатыми каменными полигонами с фасетками/трещинами;
- маркер игрока на миникарте отдельный: компактный янтарно-золотой корабельный chevron, плюс короткий velocity vector по текущей скорости в той же теплой палитре.
- под миникартой показывается имя текущей системы в формате `SYSTEM Sector / System`;
- под именем системы показывается текущая настройка варп-двигателя: `WARP --` или `WARP -> Target`;
- рядом с миникартой находится кликабельная HUD-иконка звездной карты; она дублирует `M`, открывает `StarMapOverlay` и при открытой карте показывает обычный Windows cursor;
- иконки звезды/планет на миникарте имеют отдельный screen-space scale, чтобы огромная звезда не забивала карту;
- планеты на миникарте должны использовать тот же `systemTimeSeconds`, что и `SpaceBackground`, а не независимый `snapshot.Tick`; иначе после переключения систем orbital position может визуально разъехаться с реальным положением планеты.

Осторожно: HUD сейчас сделан через прямую `Control._Draw()`-отрисовку и `Label`-позиционирование. Если править интерфейс, проверять на широком 1920x1080 окне и следить, чтобы текст не наезжал на полоски.

## Star Map

Ключевые файлы:

- `game/scripts/StarMapOverlay.cs`
- `game/scripts/StarMapToggleButton.cs`
- `game/scripts/StarMapSystemEntry.cs`
- `game/scripts/GameRoot.cs`

Текущее поведение:

- `M` и HUD-иконка открывают/закрывают карту;
- открытая карта ставит gameplay input/симуляцию на паузу, показывает обычный cursor и оставляет фон игры под затемнением;
- список систем строится из текущего `Sol` и `game/assets/generated/galaxy.json`; полноценный system JSON подгружается только для деталей конкретной generated-системы, без создания runtime nodes/textures для всех систем;
- системы группируются по сектору, каждая система получает кружок цвета своей звезды, имя, current marker и tuned target marker;
- левая кнопка мыши по system glyph/label выбирает систему; между current system и выбранной системой рисуется curved dashed warp route;
- удержание правой кнопки мыши над system glyph/label открывает planet popup со списком планет, цветными маркерами, названиями и типами;
- правая панель выбранной системы показывает только навигационные данные: имя системы, сектор, число планет, target/tuned status; физические характеристики звезды на Star Map не выводятся;
- сектор может быть разной геометрической формы, но layout выбирает более вместительные формы для плотных секторов, чтобы красота не ломала читаемость;
- при большом числе секторов карта адаптирует grid, star radius, sector font, system font и плотность подписей; routine labels могут скрываться, но current/tuned/selected/hovered labels остаются видимыми и выбранная система всегда раскрывается в правой панели;
- layout карты кэшируется и пересчитывается только при изменении списка systems, выбора или viewport size; pulse/redraw не должен каждый кадр пересоздавать sector polygons/layout arrays;
- `OK` на выбранной системе настраивает warp target, обновляет HUD `WARP -> Target` и закрывает карту; фактический warp между системами пока не выполняется.

Диагностический режим без изменения generated JSON:

```powershell
& ".\tools\run_game.ps1" -- --open-star-map --star-map-fixture-sectors=24 --star-map-fixture-systems-per-sector=7
```

Он нужен только для проверки масштабирования карты при большом числе секторов/систем.

Дополнительные visual-smoke flags:

```powershell
& ".\tools\run_game.ps1" -- --open-star-map --star-map-select=sol
& ".\tools\run_game.ps1" -- --open-star-map --star-map-inspect=orion_0001
```

Они заранее выбирают system или открывают planet popup только для capture/review.

## Planets And Solar System

Главный файл данных планет:

- `game/scripts/SolarSystem.cs`

Принятая шкала:

```csharp
ReferenceStarSize = 1000f
SunVisualWorldSize = 1320f
ReferenceSize(referenceSize) = SunVisualWorldSize * referenceSize / ReferenceStarSize
```

Это означает:

- видимое солнце не меняется и физически остается `1320 world units`;
- в геймдизайнерской шкале это считается `1000`;
- планеты задаются reference-size числами и затем пересчитываются относительно текущего визуального солнца.

Текущие reference-size значения:

```text
Mercury  150
Venus    280
Earth    300
Mars     200
Jupiter  650
Saturn   600
Uranus   500
Neptune  450
Sun     1000 reference / 1320 actual world size
```

Важно: Jupiter и Saturn уже были уменьшены пользователем относительно первоначального плана.

Орбитальная скорость:

- базовая точка: `EarthOrbitPeriodSeconds = 100f`;
- орбитальный период считается через `OrbitPeriod(realEarthYears) = EarthOrbitPeriodSeconds * sqrt(realEarthYears)`;
- внутренние планеты двигаются быстрее, внешние медленнее, но все ускорено для игры.

## Generated Star Systems And Offline Galaxy Pipeline

Текущий принцип: игра не генерирует галактику на лету при обычном старте. Генерация и подготовка ассетов происходят offline через `tools`, а runtime читает готовые JSON и грузит только активную систему.

Ключевые файлы:

- `tools/star_system_catalog.json` - catalog weights/ranges/texture sets/archetypes.
- `tools/generate_star_systems.py` - offline generator сектора/систем/планет; также имеет test modes `--coverage-highres-assets` и `--planet-showcase-highres`.
- `tools/import_imagegen_assets.py` - slicing imagegen sheets в raw generated assets.
- `tools/process_highres_imagegen_assets.py` - direct high-res imagegen source processing/validation/registration.
- `tools/process_planet_surface_maps.py` - обработка raw planet cuts в runtime-ready seamless maps.
- `tools/create_background_tiles.py` - создание Sol-style 4096px tileable background variants.
- `tools/star_system_pipeline.md` - короткая инструкция по asset/generator pipeline.
- `game/assets/generated/galaxy.json` - runtime sector index.
- `game/assets/generated/systems/orion_*.json` - runtime generated systems.
- `game/assets/generated/star_sources_4k/`, `planet_sources_4k/`, `background_sources_4k/` - новые direct high-res imagegen sources.
- `tools/generated/highres_asset_report.json` - validation report по direct high-res ассетам.
- `game/scripts/StarSystemLoader.cs` - JSON loader в `StarSystemDefinition`.

Текущий сектор:

```text
Orion
- Sol          preset/manual, source of visual quality baseline
- Planet Showcase generated, yellow main sequence, 12 planets, Black Silent Reach background
```

Последняя пересборка generated-сектора: 2026-05-11, `python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts --planet-showcase-highres`; пересозданы `orion_0001.json`, `galaxy.json` и `tools/generated/imagegen_prompts.jsonl`. Это временная visual-test конфигурация для оценки всех типов планет в одной системе.

High-res imagegen статус на 2026-05-11:

- полный target batch подготовлен в `tools/generated/highres_generation_batch.jsonl`: 14 star sources, 14 background sources, 32 planet sources;
- в runtime принята full direct high-res партия: 10 star archetypes, 12 background archetypes, 12 planet archetypes;
- текущие принятые sources лежат в `game/assets/generated/star_sources_4k/`, `planet_sources_4k/`, `background_sources_4k/`; runtime outputs лежат в `stars/`, `star_frames/`, `star_frames_experimental/`, `planet_surfaces/`, `background_tiles/`;
- validation report: `tools/generated/highres_asset_report.json`, сейчас `assetCount=34`, `failedCount=0`;
- stable и experimental generated star frame directories должны быть полными: 10 variants * 96 кадров в `star_frames/` и 10 variants * 96 кадров в `star_frames_experimental/`;
- старые 1024-ish sheet assets остаются fallback/историей, но новые generated systems не должны ссылаться на `game/assets/generated/planets/*.png` или stretched `game/assets/generated/backgrounds/*.png`.

Ограничения и договоренности:

- обычная процедурная генерация сектора содержит 2-5 систем; test modes могут намеренно обходить этот лимит: `--coverage-highres-assets` создает минимальный набор для покрытия всех high-res фонов/звезд/планет, а `--planet-showcase-highres` создает одну систему со всеми 12 типами планет;
- `Sol` всегда hand-authored preset, а не результат генератора;
- `--count` у генератора управляет только количеством generated-систем сверх `Sol`;
- размеры generated планет ограничены относительно размера звезды (`maxPlanetDiameterToStarSizeRatio` в catalog);
- star burn damage/hitbox масштабируется от `StarDefinition.WorldSize`, а не от фиксированного солнца;
- generated systems не должны использовать старые Sol planet assets как основные surface maps, кроме если это явно catalog fallback.
- новые generated assets нельзя утверждать только по превью: после обработки проверять размеры, sharpness score, seam delta и реальный Godot capture, чтобы не получить растянутый "мыльный" runtime вид.

Runtime loading:

- `GameRoot` хранит compact index generated systems, но полноценный JSON системы грузит только при выборе системы;
- `F12` переключает generated systems, `F11` возвращает `Sol`;
- `--system=orion_0001` запускает текущую тестовую generated-систему `Planet Showcase`;
- `--open-star-map` открывает карту сразу после старта; `--star-map-fixture-sectors` и `--star-map-fixture-systems-per-sector` включают synthetic layout fixture только для visual smoke масштабирования;
- при смене системы вызывается `ApplyStarSystemPhysics()`, `ResetSimulationForActiveSystem()`, `ClearTransientVisualState()`, затем `SpaceBackground.SetSystem()` и `HudOverlay.SetSystem()`;
- существование других систем не должно создавать nodes/textures/materials в сцене, пока актор не находится в этой системе.

Backgrounds:

- `Sol` использует `game/assets/backgrounds/space_nebula_tile.png`;
- generated backgrounds используют отдельный renderer-layer `SpaceBackdropView`, устроенный как Sol-style фон: texture tile layer + procedural starfield layer;
- `SpaceBackdropView` применяет тот же runtime-контракт, что Sol background: `TextureAlpha=1.0`, `TextureParallax=0.08`, `StarParallax=0.32`; `tools/generate_star_systems.py` фиксирует эти значения для generated JSON;
- `SpaceBackdropView` ломает заметную зеркальность и повторяемость в renderer-layer, а не пересинтезирует PNG: основной full-color/full-alpha tile pass + два слабых дополнительных tile pass с seed-based phase, layer offsets, per-system X/Y scale, layer alpha и небольшим parallax-offset для high-res `background_tiles`;
- brightness/цвет не запекать и не глушить в runtime без явного запроса: фоновые ассеты уже являются художественной основой, texture layer рисуется с `Colors.White` modulate и не использует `TextureTint` как color grade;
- generated-only nebula overlay вынесен в отдельный `GeneratedNebulaOverlayLayer`, но в baseline выключен (`UseGeneratedNebulaOverlay=false`), чтобы фон не уходил от эталона Sol;
- для новых imagegen-фонов `tools/process_highres_imagegen_assets.py` обрабатывает `game/assets/generated/background_sources_4k/*.png` в 4096px Sol-style tile variants в `game/assets/generated/background_tiles`: imagegen source дает nebula/dust цвет, broad shapes и restrained baked star/glint contribution, а не прямой fullscreen wallpaper;
- background tiles не должны читаться как зеркальный/kaleidoscope 2x2-паттерн; сначала править `SpaceBackdropView` layer contract, потому что asset-level synthesis может изменить approved art direction;
- исторически активные Sol/Kora/Nara tiles были восстановлены из backup перед asset-level synthesis; кислотный v6 сохранен отдельно в `tools/generated/backups/background_tiles_20260510_acid_asymmetric_v6_before_restore`; текущие high-res фоны подключены через catalog-backed `background_tiles/*_01_tile.png`;
- baked-звезды в texture tile можно сохранять как художественный слой, но не как зеркальный fullscreen starfield; `ProceduralStarfieldLayer` внутри `SpaceBackdropView` добавляет runtime starfield/parallax поверх этого слоя;
- `tools/create_background_tiles.py` остается legacy/fallback методом, который берет Sol tile как quality baseline и создает 4096px variants в `game/assets/generated/background_tiles`;
- runtime JSON должен ссылаться на `res://assets/generated/background_tiles/*.png`;
- новые accepted high-res background tiles должны иметь import-настройки как Sol tile: `compress/high_quality=true`, `mipmaps/generate=true`;
- `game/assets/generated/backgrounds/*.png` - legacy imagegen cuts; не использовать их как fullscreen stretched runtime backgrounds.

Planet surfaces:

- raw imagegen cuts в `game/assets/generated/planets/*.png` остаются исходниками;
- runtime JSON должен ссылаться на `game/assets/generated/planet_surfaces/*.png`;
- для новых imagegen-планет `tools/process_highres_imagegen_assets.py` делает 4096x2048 maps, blends horizontal seam, sharpens и записывает sharpness/seam validation в report;
- `tools/process_planet_surface_maps.py` остается legacy/fallback путем для старых sheet-sliced 1024x512 sources и делает 2048x1024 maps;
- `animated_planet.gdshader` использует `repeat_enable, filter_linear` для `surface_map`; mipmap-фильтрация на generated surface maps убрана, потому что размывала крупные планеты;
- если на планете появляется вертикальный черный шов, сначала проверять processed surface pipeline, а не переписывать shader.
- текущая visual-test система `Planet Showcase` содержит 12 планет в порядке catalog archetypes: `scorched_rock`, `barren_rock`, `desert`, `volcanic`, `ocean`, `earthlike`, `ice`, `toxic`, `warm_gas_giant`, `cold_gas_giant`, `ringed_giant`, `shattered_world`; все surface maps идут из `res://assets/generated/planet_surfaces/*_01.png`.

Stars:

- generated stars используют `StabilizedSunView`, но получают `DiskTint`, `CoronaColor`, `CoronaIntensity`, `AnimationSpeed`, `WorldSize` из JSON;
- imagegen star PNG может быть catalog/source material, но текущий качественный runtime path должен держаться на stabilized solar renderer;
- визуальный baseline: "примерно как наше Sol-солнце", а не плоская PNG-звезда;
- принятое решение для generated-star quality на 2026-05-11: direct high-res star source не является единственной анимацией, но experimental frame recipe теперь является основной сборкой для generated stars. `tools/process_highres_imagegen_assets.py` должен писать 96-frame sequence в `game/assets/generated/star_frames_experimental/<variant>/sun_00.png..sun_95.png`, а high-res source дает цвет, характер, крупную детализацию и catalog still texture;
- runtime JSON generated-систем по-прежнему указывает на `game/assets/generated/star_frames/<variant>/sun_00.png..sun_95.png` через `frameDirectory`, `framePrefix`, `frameCount=96`; `SpaceBackground` поверх этого автоматически предпочитает matching `star_frames_experimental/<variant>` как primary path, если там есть `sun_00.png`;
- быстрый откат primary generated-star visuals: запустить игру с `--star-frames=stable`, `--stable-star-frames` или `--experimental-star-frames=false`; stable кадры в `star_frames/<variant>` и Sol assets при этом не меняются;
- `SpaceBackground` загружает frame sequence текущей звезды и передает ее в `StabilizedSunView`; для повторного переключения систем используется cache последних star-frame sets, чтобы не грузить 96 текстур заново;
- import-настройки accepted generated star frames должны оставаться high-quality/mipmapped, как у Sol sun frames: `compress/high_quality=true`, `mipmaps/generate=true`;
- не возвращаться к варианту, где star source PNG просто remap/warp-анимируется: это дает мыло, статичную картинку с деформацией и заметный reset цикла;
- не делать corona/prominence как отдельные равномерные внешние пятна. Огненные языки должны быть привязаны к rim/короне, а не выглядеть как венок светящихся шаров вокруг диска.

Миникарта и системное время:

- `GameRoot` считает `systemTimeSeconds` из simulation tick/interpolation и передает его одновременно в `SpaceBackground` и `HudOverlay`;
- `SpaceBackground.SetVisualTime()` переводит фон/планеты/звезды на внешний visual time;
- `HudOverlay.DrawMinimap()` использует тот же time для `SolarSystem.PositionAt`;
- это исправляет разъезд положения планеты на экране и на миникарте после загрузки/переключения systems.

Диагностика, которая уже применялась:

- `diagnostics/visual_check_20260510/frame_capture/*` - кадры generated backgrounds/stars;
- `diagnostics/visual_check_20260510/minimap_sync/*` - проверка синхронизации миникарты и видимой планеты;
- `diagnostics/visual_check_20260510/planet_surface_fix/*` - сравнение raw/processed planet surface seam.
- `diagnostics/star_map_mvp_v5/*` - runtime smoke реальной карты с текущими системами;
- `diagnostics/star_map_dense_fixture_v2/*` - dense fixture 24 sectors x 7 systems для проверки читаемости и отключения второстепенных labels;
- `diagnostics/star_map_sparse_fixture_v1/*` - sparse fixture для проверки вариативных форм секторов.
- `diagnostics/star_map_v2_visual_final/*` - Star Map visual pass после улучшения фона, glyphs, typographic panel и star details;
- `diagnostics/star_map_v2_route_curve/*` - проверка curved dashed route после выбора target system;
- `diagnostics/star_map_v2_popup_final/*` - проверка right-hold planet popup со списком планет.

## Star And Sun Rendering

Текущие файлы решения:

- `game/scripts/SpaceBackground.cs`
- `game/scripts/StabilizedSunView.cs`
- `game/shaders/sun_stabilized.gdshader`
- исходные кадры солнца: `game/assets/backgrounds/sun/sun_00.png` и соседние кадры серии.

Утвержденное пользователем состояние: солнце выглядит хорошо, а мерцание ушло. Не менять визуал солнца без прямого запроса.

Как работает солнце сейчас:

- `SpaceBackground.LoadSunFrames()` по-прежнему грузит исходную последовательность кадров;
- `LoadStabilizedSun()` создает `StabilizedSunView`;
- `StabilizedSunView` разделяет рендер на disk-layer и corona-layer;
- disk-layer использует `sun_stabilized.gdshader`;
- shader получает четыре соседних кадра и `frame_blend`;
- внутренняя плазма остается обычной анимированной интерполяцией кадров;
- только внешний rim/контур получает локальное temporal smoothing;
- старая `DrawSun()`-отрисовка оставлена как fallback, если shader/layer недоступен.

Primary / Stable Generated-Star Modes:

- `primary` mode - текущая основная runtime-сборка для generated stars. JSON системы указывает на stable `star.frameDirectory`, но `SpaceBackground.PreferredGeneratedStarFrameDirectoryOrDefault()` автоматически подменяет путь на `game/assets/generated/star_frames_experimental/<variant>/sun_00.png..sun_95.png`, если matching experimental frames существуют;
- `stable` mode - явный fallback для проверки и отката. Кадры лежат в `game/assets/generated/star_frames/<variant>/sun_00.png..sun_95.png`, рендер идет через тот же `StabilizedSunView` + `sun_stabilized.gdshader`;
- старые флаги `--experimental-star-frames` и `--star-frames=experimental` оставлены совместимыми, но больше не нужны для обычного запуска;
- откат на stable выполняется runtime-флагом `--star-frames=stable`, `--stable-star-frames` или `--experimental-star-frames=false`. Никакой правки JSON, catalog или assets для отката не требуется;
- primary generated-star path не должен затрагивать Sol: ручное солнце продолжает использовать `game/assets/backgrounds/sun/sun_00.png..` и утвержденный Sol visual baseline;
- команды из папки проекта:

```powershell
$godot = (Resolve-Path .\.tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe).Path
& $godot --path .\game -- --system orion_0001
& $godot --path .\game -- --system orion_0001 --star-frames=stable
```

Почему это важно:

- пользователь видел мерцание не всего солнца, а части яркого оранжевого края;
- красивый огненный перелив внутри диска был правильный и его нельзя было гасить;
- попытки чинить через грубое замедление, уменьшение кадров, сильную статичную подложку или замену ассетов ломали ощущение живого солнца;
- правильный фикс оказался кодовым: стабилизировать только проблемный rim, не убивая плазму.

Правило для новых звезд/систем:

- использовать тот же принцип: source frames остаются живыми, rim стабилизируется локально;
- generated stars должны идти через `StabilizedSunView`, а не как простая статичная PNG-текстура;
- параметры generated star (`WorldSize`, `DiskTint`, `CoronaColor`, `CoronaIntensity`, `AnimationSpeed`) приходят из `StarDefinition`;
- для imagegen-generated stars основной runtime path такой: high-res source -> `process_highres_imagegen_assets.py --write-experimental-star-frames` -> 96 кадров `star_frames_experimental/<variant>/sun_00..sun_95` + stable fallback `star_frames/<variant>/sun_00..sun_95` -> `frameDirectory` в generated JSON -> runtime primary resolver -> `StabilizedSunView` + `sun_stabilized.gdshader`;
- для будущих A/B-проверок можно писать только experimental recipe через `process_highres_imagegen_assets.py --experimental-star-frames-only`; как только matching frames существуют, generated-система использует их по умолчанию, а откат остается через `--star-frames=stable`;
- 96-frame loop должен быть математически замкнутым: временные волны в генераторе использовать с целыми периодами относительно полного цикла, чтобы `sun_95 -> sun_00` не читался как reset;
- seam quality gate для generated stars: pixel-diff `95->00` должен быть того же порядка, что обычный соседний шаг `00->01`/`94->95`; если `95->00` в несколько раз выше, такой asset batch не принимать;
- visual quality gate: проверять не только PNG в image viewer, а runtime capture в Godot рядом со звездой (`--stress-near star`), потому что shader, tint, scale, HUD/camera и mipmaps меняют ощущение;
- accepted baseline после последней правки: `blue_white_star_01` и `orange_dwarf_01` имеют стабильную Sol-like анимацию без видимого reset loop; ориентировочные diff-значения после обработки: blue `00->01 ~= 0.015`, `95->00 ~= 0.015`; orange `00->01 ~= 0.014`, `95->00 ~= 0.014`;
- experimental baseline после A/B-правки 2026-05-10 сильнее сохраняет исходную PNG-фактуру generated stars; ориентировочные all-pixel diff на 256px preview: orange `00->01 ~= 0.00235`, `95->00 ~= 0.00235`; blue `00->01 ~= 0.00312`, `95->00 ~= 0.00316`;
- не пересоздавать assets звезды как первый способ лечения flicker;
- не добавлять полноэкранный blur или тяжелый постпроцесс ради звезды;
- не смешивать `ReferenceStarSize` и реальный `SunVisualWorldSize`;
- если новая звезда мерцает, сначала проверить контур/rim и temporal blending shader;
- если generated star выглядит статичной, мыльной или с явным циклом перезапуска, сначала проверить `process_highres_imagegen_assets.py` и loop/seam metrics, а не править игровой shader.

Полезные диагностики, которые уже применялись:

- визуальная проверка в реальной игре;
- stress-запуск с 4 врагами и автопилотом;
- generated-star runtime smoke: `--system orion_0001 --stress-near star` и `--system orion_0002 --stress-near star`;
- pixel-diff диагностика frame sequence: сравнивать `00->01`, `94->95`, `95->00`, `00->24`, `00->48` на маске alpha/bright pixels;
- `diagnostics/visual_check_20260510/star_runtime_orange_v3/*` - runtime capture accepted orange generated star после Sol-carrier решения;
- `diagnostics/sun_flicker/*` - артефакты анализа исходного мерцания;
- `diagnostics/sun_codefix_test/*` - сравнение вариантов code-fix;
- сравнение ощущения: "мерцает край, но не внутренняя плазма".

## Planet Rendering

Earth:

- `game/scripts/AnimatedEarthView.cs`
- `game/assets/planets/animated_earth.gdshader`
- текстуры:
  - `earth_surface_map.png`
  - `earth_clouds.png`

Очень важная договоренность: Землю не трогать без явного запроса пользователя. Пользователь уже доволен текущей Землей.

Non-Earth planets:

- `game/scripts/AnimatedPlanetView.cs`
- `game/assets/planets/animated_planet.gdshader`
- настройки подключаются в `SpaceBackground.PlanetRenderSettingsById`
- используются spin-map текстуры:
  - `mercury_spin_map.png`
  - `venus_spin_map.png`
  - `mars_spin_map.png`
  - `jupiter_spin_map.png`
  - `saturn_spin_map.png`
  - `uranus_spin_map.png`
  - `neptune_spin_map.png`

Рендер non-Earth планет:

- shader проецирует spin-map на сферу;
- вращение задается через longitude/time;
- есть light direction, rim, atmosphere/glow;
- Saturn и Uranus имеют кольца через `PlanetRingLayer`;
- кольца должны масштабироваться от размера тела планеты.

Старые процедурные `*_surface_map.png` для non-Earth могут оставаться в assets, но текущая визуальная система опирается на `*_spin_map.png`.

Generated planets:

- generated systems используют тот же `AnimatedPlanetView`, но settings приходят из `PlanetVisualProfile` в runtime JSON;
- `PlanetVisualProfile` задает atmosphere color/strength, rotation speed, flow strength, contrast, saturation, glow strength и rings;
- generated surface maps должны быть processed runtime maps: `res://assets/generated/planet_surfaces/*.png`;
- raw generated cuts `res://assets/generated/planets/*.png` не должны попадать в новые runtime JSON, потому что они давали размытие и черный vertical seam на сфере;
- для новых imagegen-ассетов сначала запускать `tools/import_imagegen_assets.py`, затем `tools/process_planet_surface_maps.py --update-catalog`, затем `tools/generate_star_systems.py`.

## Rings

Saturn/Uranus rings сейчас рисуются процедурно внутри `AnimatedPlanetView.PlanetRingLayer`.

Особенности:

- кольца имеют back/front layer, чтобы часть кольца была за планетой, часть перед ней;
- масштаб кольца зависит от `BodyDiameter`;
- есть несколько bands и темный gap;
- redraw кольца кешируется: `QueueRedraw()` вызывается только при изменении состояния.

Пользователь уже ругался на "убогие" кольца, затем визуал был улучшен. При новых правках колец обязательно проверять в игре крупным планом.

## Ships

Основные файлы:

- `game/scripts/ShipCatalog.cs`
- `game/scripts/ShipView.cs`
- `game/scripts/EnginePort.cs`
- `game/assets/ships/ships_manifest.json`

`ships_manifest.json` содержит:

- id/race/hull;
- `path` до sprite;
- `scale`;
- `content_bounds`;
- `exhaust_ports`.

Обычные корабли сейчас в основном используют `scale = 0.42`. Это прежний визуальный масштаб, перенесенный из `GameRoot` в manifest. При выборе корабля `GameRoot` берет `content_bounds`, умножает на `scale`, обновляет визуальный scale в `ShipView` и передает такой же world-space hitbox в `LocalSimulation`.

Актуальное уточнение: hitbox теперь в первую очередь берется из alpha-bounds PNG, а не из `content_bounds`. `scale` все равно применяется к hitbox и к visual scale одинаково.

Klissan ships:

- актуальный источник клиссанских кораблей - единый лист `C:\Users\Максим\Desktop\KLISSAN.png`; старые отдельные PNG из `C:\Users\Максим\Desktop\Клиссаны` считались неудачными по четкости;
- игровые `2Klissan*.png` теперь вырезаны из единого листа в 768x768 transparent PNG в `game/assets/ships`;
- старые версии перед заменой сохранены вне Godot asset scan: `.tools/backups/_backup_klissan_before_sheet_*`;
- добавлены `2KlissanFighter`, `2KlissanCruiser`, `2KlissanBattleship`, `2KlissanScout`, `2KlissanFrigate`;
- тильда переключает активный каталог `ordinary <-> Klissan`, а `Tab` листает корабли только внутри активного каталога;
- scale рассчитан относительно текущего управляемого `2PeopleR`: Frigate `1.0x`, Fighter `0.8x`, Scout `0.6x`, Cruiser `1.4x`, Battleship `2.0x`;
- для high-res Klissan textures включены `compress/high_quality=true` и `mipmaps/generate=true` в `.png.import`;
- радиусы сопел для high-res кораблей могут быть больше 18px: clamp в `ShipCatalog`/`ShipView` поднят до `64f`, существующие обычные корабли с маленькими радиусами от этого не меняются;
- точки сопел `2KlissanFighter`, `2KlissanCruiser`, `2KlissanBattleship` и `2KlissanFrigate` вручную привязаны к видимым blue/cyan engine nozzles на 768px PNG; `2KlissanScout` оставлен без смены портов, потому что его thrust уже выглядел корректно;
- `ShipView` масштабирует длину/смещения thrust по размеру текстуры (`texture/256`) и не применяет bottom-only фильтр к manifest-портам, поэтому high-res Klissan ships могут иметь разнесенные боковые сопла без потери портов;
- race `Klissan` в `ShipCatalog.RaceFromPath` имеет бирюзово-сине-зеленоватый thrust: outer `(0.02, 0.92, 0.76)`, core `(0.56, 1.0, 0.86)`;
- щиты и projectile/asteroid/sun impact VFX автоматически используют геометрию Klissan и всех остальных кораблей через `ShipHitbox`, `ProjectileImpactLayer.SetShieldTargets` и `ProjectileImpactState.TargetSize`;
- контактная вспышка щита выравнивается по касательной к shield ellipse, поэтому крупные вытянутые корабли вроде Klissan battleship/линкора не должны получать отдельную синюю "плюху" вне контура щита;
- после правки shield VFX от 2026-05-10 минимальный размер shield bubble больше не раздувает мелкие корпуса до 104px+ диаметра; `2KlissanScout` должен иметь компактный пузырь вокруг модели, а `2KlissanBattleship`/`2KlissanCruiser` не должны выпускать вспышку за бирюзовый контур;
- после follow-up правки от 2026-05-10 синее поле shield bubble/ripple подтянуто к rim: оно должно визуально доходить до голубого контура, но не рисоваться как отдельный слой за ним;
- для visual smoke shield-проверок добавлен startup debug arg `--ship <id>`, например `--ship 2KlissanBattleship` или `--ship 2KlissanScout`;
- последние visual captures shield-проверки лежат в `diagnostics/shield-vfx-rim-final-battleship/` и `diagnostics/shield-vfx-rim-final-scout/`;
- взрыв и распад на осколки используют тот же `ExplosionLayer.SpawnShip`, потому что выбранный Klissan sprite передается туда как текущая текстура корабля;
- проверочный лист с точками выхлопа: `game/assets/reviews/klissan-ships-import-review-v1.png`;
- актуальный проверочный лист вырезки и точек выхлопа: `game/assets/reviews/klissan-sheet-v2-import-review.png`;
- актуальный проверочный лист масштаба относительно `2PeopleR`: `game/assets/reviews/klissan-sheet-v2-scale-review.png`.

Важно: JSON поле сопел называется именно `exhaust_ports`. В `ShipCatalog.cs` это связано через:

```csharp
[JsonPropertyName("exhaust_ports")]
public ExhaustPortEntry[] ExhaustPorts { get; set; }
```

Если меняются координаты сопел в manifest, нужно перезапустить игру. Для exe нужен новый export.

Thrust follow-up от 2026-05-11: `2PelengD` теперь использует две близкие manifest-точки на видимых нижних соплах вместо одного смещенного порта; у `2PeopleR` центральный порт перенесен назад к корме, чтобы runtime glow не давал желтый круг внутри корпуса. Для всей race `Peleng` добавлен `ShipCatalog.ThrustSizeMultiplier(path) == 1.18f`; `GameRoot` передает его в `ShipView.EngineEffectScale`, а `ShipView` масштабирует им длину/ширину обычного thrust и afterburner без изменения PNG.

Thrust realign follow-up от 2026-05-11: `2PeopleR` оставлен без центрального thrust; `2GaalD`, `2GaalL`, `2GaalP`, `2FeiD`, `2FeiL`, `2FeiP` перепривязаны к видимым кормовым/синим точкам без крыльевых портов там, где пользователь попросил их убрать; свежий пользовательский `2GaalR.png` переимпортирован, `content_bounds` обновлен до новой кормы, а exhaust ports перенесены на актуальные видимые сопла. Для race `Klissan` после visual review убран отдельный `ThrustBubbleMultiplier == 0.28` / `ThrustParticleDensity == 1.45`, потому что он превращал line-based plume в прямоугольные "рельсы"; клиссаны теперь используют ту же структуру thrust, что обычные/федеральные корабли, но с клиссанскими `ThrustOuterColor` / `ThrustCoreColor`.

Thrust geometry follow-up от 2026-05-11: центральный “треугольник” у `2PeopleR` оказался не manifest-портом, а общим `cluster wake` между двумя разнесенными соплами. `ShipView.DrawMainThrust` больше не рисует общий polygon-wake между портами; для multi-port кораблей без центрального порта plume дополнительно сужается по каждому соплу. `DrawHeatCone` и `DrawShockDiamonds` переведены с polygon/triangle shapes на line-based plume/streak rendering, чтобы у Klissan и других кораблей не появлялись видимые геометрические треугольники.

`ShipView.cs` отвечает за:

- sprite корабля;
- ship contour/aura layer: временно отключен и удален из runtime после visual review; `ShipView` больше не создает silhouette outline/aura вокруг кораблей, чтобы пользователь мог спокойно доработать PNG вручную перед следующим реэкспортом;
- manual PNG re-ingest от 2026-05-11: пользователь вручную зачистил обычные `game/assets/ships/*.png`; `ships_manifest.json` обновлен под текущие alpha-bounds и сдвинутые после обрезки `exhaust_ports`; клиссанские PNG отдельно очищены от темного low-alpha green/cyan fringe после вырезки, без изменения их scale/ручных thrust layouts; backup исходного manifest и Klissan PNG лежит в `.tools/backups/ships_reingest_20260511_132701`;
- procedural ship idle animation для всех видимых кораблей: локальный pseudo-3D bank/hover спрайта, без заметного squash/stretch, чтобы крупные корпуса вроде `2KlissanBattleship` не "скукоживались"; эффект работает всегда, но у врагов следует существующему `ShipEffectQuality` LOD;
- asset-aware ship rig: `ShipCatalog.RigProfileForPath` генерирует зоны левого/правого крыла, корпуса, носа, ядра, wing roots/tips и engine ports из alpha-bounds/exhaust-портов; в v3 эти данные используются как якоря для core/engine glow и будущих ручных масок, а не для агрессивного деформирования корпуса;
- ship idle v3/v4 follow-up: после visual review shader-затемнение окончательно убрано из runtime; `ShipBankShaderCode`, отключенный `ShaderMaterial` и per-frame `UpdateShipBankMaterial()` no-op удалены. Текущая "живость" корабля идет через bank/hover transform и thrust/core glow; старые texture-based impulse shimmer / energy lanes / sparkle-circle элементы из EffectBlocks отключены, потому что визуально давали лишнюю световую пульсацию.
- ориентацию и визуальный поворот;
- debug hitbox;
- обычные thruster effects;
- afterburner effects: больше размер, яркость и количество частиц.

Недавняя оптимизация в `ShipView` убрала часть per-frame allocations в отрисовке thrust. Рефакторинг от 2026-05-11 также удалил старый polygon `cluster wake`, `DrawFivePointPolygon` / `DrawFourPointPolygon` и временные polygon buffers после перехода accepted thrust на line-based plume/streak rendering; это снижает лишнюю работу и убирает невостребованный код, который уже не должен возвращаться.

Klissan textured thrust follow-up от 2026-05-11: крупные клиссанские корабли больше не полагаются только на procedural circle/bubble plume, потому что радиус сопел на `2KlissanBattleship`/`2KlissanCruiser` масштабировал круги до заметных "пузырей". Для race `Klissan` `ShipCatalog.ThrustPlumeTexturePath(path)` теперь возвращает `res://assets/effects/klissan_thrust_plume.png`; `GameRoot` передает его в `ShipView.EnginePlumeTexture`, а `ShipView.DrawTexturedPlasmaPlume` рисует текстурную premium-mask форму + мелкие частицы, окрашивая ее существующими `ThrustOuterColor` / `ThrustCoreColor`. Runtime PNG `game/assets/effects/klissan_thrust_plume.png` получен из `premium assets/EffectBlocks v4/.../textures/muzzleflash/muzzleflash_1.png`: исходная muzzleflash-шапка срезана, оставлен вертикальный рваный поток без круглой головы. Raw `premium assets` остается внешним исходным материалом; в игру коммитится только derived runtime texture и `.png.import`.

## Background

Основной фон:

- `game/assets/backgrounds/space_nebula_tile.png`
- грузится в `SpaceBackground.cs`
- рисуется tile/parallax способом.

Текущие параметры:

```csharp
BackgroundTextureAlpha = 0.56f
BackgroundTextureParallax = 0.08f
StarParallax = 0.32f
```

История проблемы:

- слишком быстрое движение фона вызывало ощущение смазывания/укачивания;
- фон был замедлен, но не должен быть полностью статичным;
- важно не возвращать слишком агрессивный parallax.

## Performance Notes

Пользователь заметил FPS drop примерно до 29 FPS на мощном ПК, особенно когда 3-4 врага преследуют игрока на afterburner и активно стреляют. Это активная зона внимания.

Уже сделано:

- viewport culling для планет;
- viewport culling для Earth/non-Earth animated views;
- viewport culling для звезд;
- skip offscreen sun;
- skip offscreen orbits;
- уменьшены segments для orbit arcs;
- кольца кешируют redraw;
- убраны лишние `QueueRedraw()` для `ShipView` и `ReticleView`;
- уменьшены allocations в `ShipView` thrust rendering;
- `ShipView` thrust soft-cone рисуется безопасной треугольной envelope-геометрией, чтобы Godot не ловил `Invalid polygon data, triangulation failed` на редких вырожденных 5-point полигонах;
- `System.Runtime.GCSettings.LatencyMode = SustainedLowLatency` в `GameRoot._Ready()`;
- `GameRoot.InterpolateSnapshot()` переиспользует snapshot buffers для ships/projectiles/asteroids вместо создания новых массивов каждый render frame;
- enemy visual LOD: ближайшие враги full quality, часть balanced/minimal, далекие/offscreen hidden;
- offscreen enemy views скрываются и не process-ятся;
- enemy status bars рисуются только для ограниченного набора видимых ближайших врагов;
- projectile layer делает viewport culling;
- `ProjectileImpactLayer` переиспользует буферы точек для ellipse fill/arc, без `new Vector2[]` во время отрисовки импактов;
- `ProjectileImpactLayer` привязывает shield-hit particles/ripple к ellipse normal/tangent текущего target ship, чтобы эффект попадания следовал контуру щита на всех размерах кораблей;
- projectile hit checks используют broad-phase перед дорогой oriented-hitbox проверкой;
- enemy projectiles после промаха по игроку больше не сканируют всех enemy ships, потому что friendly fire для них не включен;
- ограничен projectile storm: `MaxProjectiles = 640`;
- астероиды ограничены `AsteroidMaxActiveCount = 12`;
- астероидные столкновения используют дешевый swept circle/circle для малого `n`;
- визуал астероидов рисуется одним `AsteroidLayer`, additive fireball/горение - одним `AsteroidFireLayer`, эффекты обломков и дыма - одним `AsteroidDebrisLayer`, без создания новых нод каждый кадр;
- AI command refresh у врагов снижен на дистанции;
- ship-vs-ship collision использует cheap broad-phase перед oriented-rectangle overlap;
- AI collision avoidance имеет ранние rough-distance отсечки перед расчетом world centers;
- добавлены stress CLI аргументы: `--stress-enemies=N`, `--stress-seconds=S`, `--stress-autopilot`, `--stress-near=planetId`;
- crash при массовом спавне врагов был отловлен через stress-нагрузки и закрыт ограничениями/LOD.

Последняя проверка после добавления ship-vs-ship collision и AI avoidance:

```text
dotnet build game/SpaceManagersPrototype.sln: success
core tests: 38 passed

Core perf smoke, Release:
offscreen pursuit 80 enemies: 0.1610 ms/tick, 7543 B/tick
close dogfight 32 enemies: 0.0198 ms/tick, 3387 B/tick
projectile storm 48 enemies / ~404 active projectiles: 0.5310 ms/tick, 17769 B/tick

Godot stress, Debug editor runtime:
combat 12 enemies / 30 sec / autopilot: avg_fps 130.0, min_fps 53.0, max_frame_ms 15.5, stderr errors 0
combat 24 enemies / 30 sec / autopilot: avg_fps 101.1, min_fps 16.0, max_frame_ms 18.2, stderr errors 0
```

Важно: collision/avoidance добавляет CPU-работу в больших скоплениях кораблей. Текущие цифры остаются в хорошем диапазоне, но если число активных кораблей будет расти выше debug-сценариев, следующий шаг - spatial grid/bucket для ship pairs и AI-neighbor queries.

Проверка после смягчения ship collision separation:

```text
dotnet build game/SpaceManagersPrototype.sln: success
core tests: 38 passed
Core perf smoke, Release:
offscreen pursuit 80 enemies: 0.1614 ms/tick, 7560 B/tick
close dogfight 32 enemies: 0.0184 ms/tick, 3385 B/tick
projectile storm 48 enemies / 400 active projectiles: 0.5370 ms/tick, 17620 B/tick
Godot stress 12 enemies / 20 sec / autopilot: avg_fps 114.6, min_fps 59.0, max_frame_ms 16.4, stderr errors 0
```

Последняя performance-проверка после pass по аллокациям, projectile hit scan и Godot stderr:

```text
dotnet build game/SpaceManagersPrototype.sln: success
core tests: 35 passed

Core perf smoke, Release:
offscreen pursuit 80 enemies: 0.0832 ms/tick, 7971 B/tick
close dogfight 32 enemies: 0.0336 ms/tick, 3385 B/tick
projectile storm 48 enemies / 400 active projectiles: 0.3584 ms/tick, 17647 B/tick

Godot stress, Debug editor runtime:
combat 4 enemies / 20 sec / autopilot: avg_fps 135.6, min_fps 119.0, max_frame_ms 11.7, stderr errors 0
combat 12 enemies / 30 sec / autopilot: avg_fps 113.8, min_fps 87.0, max_frame_ms 16.2, stderr errors 0
combat 24 enemies / 30 sec / autopilot: avg_fps 85.8, min_fps 63.0, max_frame_ms 23.9, stderr errors 0
vfx 8 enemies / 20 sec: avg_fps 132.3, min_fps 109.0, max_frame_ms 17.2, stderr errors 0
combat 24 enemies / 60 sec / autopilot accurate memory: avg_fps 92.5, min_fps 62.0, max_frame_ms 27.2, first 2.0 MB, peak 307.9 MB, last 234.8 MB, tail_delta -67.8 MB, stderr errors 0
```

Важно: первый memory sample в accurate run стартует почти сразу после wrapper launch, поэтому `first 2.0 MB` не является рабочей steady-state памятью игры. Для утечки важнее `peak/last/tail_delta`: к концу 60-секундного heavy run память падает, признаков нарастающей утечки нет.

Последняя проверка после generated systems/background/planet surface pass:

```text
dotnet build game/SpaceManagersPrototype.csproj --no-restore --nologo: success
core tests: 39 passed

Godot generated system smoke:
orion_0002 / 1 enemy / 2 sec: avg_fps 145.0, min_fps 145.0, max_frame_ms 6.9
system switch smoke / 3 switches / 3 sec: avg_fps ~100-108, min_fps 98-99

Visual captures:
Kora Vyrum background/star: diagnostics/visual_check_20260510/frame_capture/
Minimap sync: diagnostics/visual_check_20260510/minimap_sync/
Planet surface seam fix: diagnostics/visual_check_20260510/planet_surface_fix/
```

Последняя проверка после direct high-res imagegen starter pass:

```text
python -m py_compile tools/process_highres_imagegen_assets.py tools/prepare_highres_generation_batch.py tools/generate_star_systems.py: success
python tools/process_highres_imagegen_assets.py --report tools/generated/highres_asset_report.json: stars 2, planets 7, backgrounds 2, failed 0
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug: 0 warnings, 0 errors

Godot visual captures:
Kora Vyrum / warm_gas_giant high-res: game/diagnostics/highres_orion_0002/kora_highres_01.png
Nara Lenos / ringed_giant high-res: game/diagnostics/highres_orion_0001_final/nara_highres_01.png
```

Последняя проверка после выноса generated backgrounds в `SpaceBackdropView`:

```text
python -m py_compile tools/generate_star_systems.py tools/process_highres_imagegen_assets.py tools/prepare_highres_generation_batch.py: success
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts: success, 2 generated systems
orion_0001/orion_0002 backgrounds: high-res 4096 tile paths, TextureAlpha 0.56, TextureParallax 0.08, StarParallax 0.32 (historical; current baseline is TextureAlpha 1.0)
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug: 0 warnings, 0 errors
Godot --headless --import: success
Kora Vyrum SpaceBackdropView capture: game/diagnostics/space_backdrop_view_orion_0002/kora_backdrop_view_00.png and _01.png
Nara Lenos SpaceBackdropView capture: game/diagnostics/space_backdrop_view_orion_0001/nara_backdrop_view_00.png and _01.png
Godot headless smoke, 1 enemy / 2 sec: Nara avg_fps 122.0, Kora avg_fps 115.6
```

Последняя проверка после background de-mirror pass для Sol и активных generated systems:

```text
python -m py_compile tools/demirror_background_tiles.py tools/process_highres_imagegen_assets.py tools/generate_space_textures.py: success
python tools/demirror_background_tiles.py --target-set active --strength 0.50 --backup-id background_tiles_20260510_pre_demirror_v4: success
backup: tools/generated/backups/background_tiles_20260510_pre_demirror_v4
report: tools/generated/background_demirror_report.json
mirror max: Sol 1.000 -> 0.717, Kora/cold_blue_void 0.402 -> 0.239, Nara/smoky_amber_cloud 0.074 -> 0.165
seam max stayed low: <= 0.00184
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug: 0 warnings, 0 errors
Godot --headless --import: success
Godot headless smoke, 1 enemy / 2 sec: Sol avg_fps 129.0, Kora avg_fps 118.7
captures: game/diagnostics/background_demirror_v4_sol, background_demirror_v4_orion_0001, background_demirror_v4_orion_0002
```

Последняя проверка после сдвига tile origin от центра и runtime brightening:

```text
python tools/demirror_background_tiles.py --target-set active --strength 0.64 --backup-id background_tiles_20260510_pre_origin_shift_demirror_v5: success
backup: tools/generated/backups/background_tiles_20260510_pre_origin_shift_demirror_v5
mirror max: Sol 1.000 -> 0.584, Kora/cold_blue_void 0.402 -> 0.182, Nara/smoky_amber_cloud 0.074 -> 0.204
SpaceBackdropView: stable TexturePhaseOffset keeps tile seam/origin away from system center
runtime brightness: TextureAlpha 0.56, TextureTintPassthrough 0.16, TextureBrightnessBoost 1.04 (historical; current baseline uses source colors directly)
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts: success
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug: 0 warnings, 0 errors
Godot headless smoke, 1 enemy / 2 sec: Sol avg_fps 135.0, Kora avg_fps 121.3
captures: game/diagnostics/background_runtime_alpha_sol, background_runtime_alpha_orion_0001, background_runtime_alpha_orion_0002
```

Последняя проверка после asymmetric synthesis pass, который сохраняет baked-star стиль без зеркального 2x2-рисунка:

```text
python -m py_compile tools/demirror_background_tiles.py tools/process_highres_imagegen_assets.py tools/generate_space_textures.py: success
python tools/demirror_background_tiles.py --target-set active --synthesize-asymmetric --strength 0.92 --brightness 1.0 --vibrance 1.0 --contrast 1.0 --clarity 0 --backup-id background_tiles_20260510_pre_asymmetric_synthesis_v6: success
backup: tools/generated/backups/background_tiles_20260510_pre_asymmetric_synthesis_v6
report: tools/generated/background_demirror_report.json
mirror max: Sol 0.584 -> 0.202, Nara/smoky_amber_cloud 0.204 -> 0.131, Kora/cold_blue_void 0.182 -> 0.311; visual capture no longer reads as mirrored, metric alone is not sufficient
seams stayed acceptable: <= 0.00777
Godot --headless --import: success
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug: 0 warnings, 0 errors
Godot headless smoke, 1 enemy / 2 sec: Sol avg_fps 114.8, Kora avg_fps 114.8
captures: game/diagnostics/background_asymmetric_v6_sol, background_asymmetric_v6_orion_0001, background_asymmetric_v6_orion_0002
```

Последняя проверка после возврата approved background tiles и переноса de-mirror в `SpaceBackdropView`:

```text
restored active Sol/Kora/Nara background tiles from tools/generated/backups/background_tiles_20260510_pre_preserve_art_asym_v7
acid v6 backup kept at tools/generated/backups/background_tiles_20260510_acid_asymmetric_v6_before_restore
SpaceBackdropView: primary texture pass + weak secondary/tertiary phase-offset passes, PNG composition unchanged
python -m py_compile tools/demirror_background_tiles.py tools/process_highres_imagegen_assets.py tools/generate_space_textures.py with PYTHONPYCACHEPREFIX: success
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot --headless --import --quit: success
Godot headless smoke, 1 enemy / 2 sec: Sol avg_fps 113.9, Kora avg_fps 129.0
```

Последняя проверка после возврата исходной цветовой гаммы background assets:

```text
SpaceBackdropView texture color: Colors.White, no TextureTint color grade, no brightness boost, primary alpha 1.0
Sol/generated textureAlpha updated to 1.0; generate_star_systems.py writes textureAlpha 1.0
python -m py_compile tools/generate_star_systems.py tools/process_highres_imagegen_assets.py tools/generate_space_textures.py with PYTHONPYCACHEPREFIX: success
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot --headless --import --quit: success
Godot headless smoke, 1 enemy / 2 sec: Sol avg_fps 145.0, Kora avg_fps 145.0
```

Последняя проверка после full high-res import, background runtime randomization и planet showcase generation:

```text
python tools/process_highres_imagegen_assets.py safe-import equivalent: assetCount=34, failedCount=0
star frames completeness: stable 10*96, experimental 10*96
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts --planet-showcase-highres: success, 1 generated system
orion_0001 Planet Showcase: 12 planets, all surfaceMap paths under res://assets/generated/planet_surfaces/
SpaceBackdropView high-res background tiles: seed-based texture variation via StarfieldSeed/NebulaSeed, per-system X/Y scale, layer alpha and parallax offsets; Sol/legacy paths keep baseline behavior
python -m py_compile tools/generate_star_systems.py tools/process_highres_imagegen_assets.py: success
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot headless smoke --system=orion_0001 --star-frames=experimental: success, Startup star system: Planet Showcase
```

Последняя проверка после player-only ship outline от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, Startup star system: Planet Showcase; итоговые PNG переложены в diagnostics/player-outline-check/
```

Последняя проверка после procedural ship idle animation от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, PNG в diagnostics/ship-idle-animation-check/
Godot stress --system=orion_0001 --star-frames=experimental --stress-enemies=4 --stress-seconds=6 --stress-autopilot: avg_fps 113.8, min_fps 44.0, max_frame_ms 13.5
```

Последняя проверка после ship idle bank v1.1 от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ship-idle-bank-v11/
Godot stress --system=orion_0001 --star-frames=experimental --stress-enemies=4 --stress-seconds=6 --stress-autopilot: avg_fps 113.5, min_fps 47.0, max_frame_ms 14.2
```

Последняя проверка после asset-aware ship rig v2 от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, PNG в diagnostics/ship-rig-v2/player_rig_v2b_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ship-rig-v2/klissan_battleship_rig_v2b_*.png
Godot stress --system=orion_0001 --star-frames=experimental --stress-enemies=4 --stress-seconds=6 --stress-autopilot: avg_fps 106.1, min_fps 42.0, max_frame_ms 13.9
```

Последняя проверка после ship idle shader-bank v3 от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, PNG в diagnostics/ship-shader-bank-v3/player_shader_bank_v3c_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ship-shader-bank-v3/klissan_shader_bank_v3c_*.png
Godot stress --system=orion_0001 --star-frames=experimental --stress-enemies=4 --stress-seconds=6 --stress-autopilot: avg_fps 115.8, min_fps 47.0, max_frame_ms 13.4
```

Последняя проверка после player aura + no-darkening shader follow-up от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, PNG в diagnostics/ship-player-aura-v3/player_aura_v3b_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ship-player-aura-v3/klissan_aura_v3b_*.png
```

Последняя проверка после silhouette palette aura v4 от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, PNG в diagnostics/ship-silhouette-aura-v4/player_silhouette_aura_v4b_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ship-silhouette-aura-v4/klissan_silhouette_aura_v4b_*.png
```

Последняя проверка после electronic silhouette aura v5 от 2026-05-11:

```text
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental: success, PNG в diagnostics/ship-electronic-aura-v5/player_electronic_aura_v5_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ship-electronic-aura-v5/klissan_electronic_aura_v5_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --stress-enemies=4 --stress-autopilot: success, PNG в diagnostics/ship-electronic-aura-v5/multi_electronic_aura_v5_*.png
```

Последняя проверка после rollback ship contour/aura от 2026-05-11:

```text
ShipPlayerAura / ShowPlayerOutline runtime references removed from game/scripts/ShipView.cs and game/scripts/GameRoot.cs
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
```

Последняя проверка после manual ship PNG re-ingest от 2026-05-11:

```text
ships_manifest.json refreshed from current game/assets/ships PNG alpha bounds; ordinary ship exhaust_ports shifted to current rear/nozzle pixels; Klissan low-alpha fringe cleanup applied
backup: .tools/backups/ships_reingest_20260511_132701
review: diagnostics/ships_after_manual_png_reingest_final_ports.png
report: diagnostics/ships_after_manual_png_reingest_report.json
dotnet build game/SpaceManagersPrototype.csproj --configuration Debug via C:\CodexTools\dotnet-8.0.420\dotnet.exe: 0 warnings, 0 errors
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2MalocT: success, PNG в diagnostics/ships_after_manual_png_runtime/maloc_t_reingest_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --ship 2KlissanBattleship: success, PNG в diagnostics/ships_after_manual_png_runtime/klissan_battleship_reingest_*.png
Godot runtime capture --system=orion_0001 --star-frames=experimental --stress-enemies=4 --stress-autopilot: success, PNG в diagnostics/ships_after_manual_png_runtime/multi_reingest_*.png
```

Последняя проверка после refactor + primary experimental generated stars от 2026-05-11:

```text
ShipView refactor: dormant ShipBankShaderCode/ShaderMaterial path removed; old polygon cluster wake helpers removed after line-based thrust became accepted.
SpaceBackground: generated stars now prefer matching star_frames_experimental by default; stable fallback is explicit via --star-frames=stable / --stable-star-frames / --experimental-star-frames=false.
dotnet build game/SpaceManagersPrototype.csproj: 0 warnings, 0 errors
dotnet run --project tests/SpaceManagers.Core.Tests/SpaceManagers.Core.Tests.csproj: 39 tests passed
dotnet run --project tests/SpaceManagers.PerfSmoke/SpaceManagers.PerfSmoke.csproj -c Release: success
Godot headless smoke --system=orion_0001: success, Startup star system: Planet Showcase; primary frames loaded from res://assets/generated/star_frames_experimental/yellow_main_sequence_01; avg_fps 122.8, min_fps 68.0, max_frame_ms 6.9
Godot headless smoke --system=orion_0001 --star-frames=stable: success; stable fallback path used; avg_fps 136.0, min_fps 136.0, max_frame_ms 6.9
```

Последняя проверка после Star Map MVP от 2026-05-11:

```text
StarMapOverlay/StarMapToggleButton/StarMapSystemEntry added; GameRoot binds M and HUD icon; OK tunes warp target only.
Star map adaptive layout: sector grid, variable sector geometry, dense compact labels, priority current/tuned/selected/hover labels.
dotnet build game/SpaceManagersPrototype.sln: 0 warnings, 0 errors
dotnet run --project tests/SpaceManagers.Core.Tests/SpaceManagers.Core.Tests.csproj --no-restore: 39 tests passed
dotnet run --project tests/SpaceManagers.PerfSmoke/SpaceManagers.PerfSmoke.csproj --no-restore: success
Godot runtime capture --system=orion_0001 --star-frames=experimental --open-star-map: success, PNG в diagnostics/star_map_mvp_v5/
Godot runtime capture --star-map-fixture-sectors=24 --star-map-fixture-systems-per-sector=7: success, PNG в diagnostics/star_map_dense_fixture_v2/
Godot runtime capture --star-map-fixture-sectors=6 --star-map-fixture-systems-per-sector=2: success, PNG в diagnostics/star_map_sparse_fixture_v1/
```

Последняя проверка после Star Map interaction/visual pass от 2026-05-11:

```text
StarMapOverlay input moved to explicit _Input/SetInputAsHandled; map clicks no longer depend on _GuiInput routing.
Left click selects a system and draws curved dashed warp route; OK tunes warp target.
Right-hold on a star opens planet popup with planet names/types; side panel no longer exposes star physical characteristics.
Map background/star glyphs/text shadows refreshed for readability.
dotnet build game/SpaceManagersPrototype.sln: 0 warnings, 0 errors
dotnet run --project tests/SpaceManagers.Core.Tests/SpaceManagers.Core.Tests.csproj --no-restore: 39 tests passed
dotnet run --project tests/SpaceManagers.PerfSmoke/SpaceManagers.PerfSmoke.csproj --no-restore: success
Godot runtime capture --open-star-map: success, PNG в diagnostics/star_map_v2_visual_final/
Godot runtime capture --open-star-map --star-map-select=sol: success, PNG в diagnostics/star_map_v2_route_curve/
Godot runtime capture --open-star-map --star-map-inspect=orion_0001: success, PNG в diagnostics/star_map_v2_popup_final/
```

Последняя проверка после Lazyweb-informed Star Map visual polish от 2026-05-11:

```text
Lazyweb native MCP namespace still did not appear in the active tool list after restart; verified Lazyweb through the local MCP bridge and used visual references for the map pass.
StarMapOverlay OK now tunes the selected warp target and closes the map.
Star Map background now uses the accepted high-res generated tile cold_blue_void_01_tile.png as a subdued navigation backdrop, with reduced procedural noise.
System glyphs were redrawn as softer luminous navigation stars; sector outlines/fills were toned down; text positions are pixel-snapped and system font never drops below 10px.
Right panel now removes star archetype/size/corona/motion lines and keeps only system/sector/planet/target status.
dotnet build game/SpaceManagersPrototype.sln: 0 warnings, 0 errors
dotnet run --project tests/SpaceManagers.Core.Tests/SpaceManagers.Core.Tests.csproj --no-restore: 39 tests passed
dotnet run --project tests/SpaceManagers.PerfSmoke/SpaceManagers.PerfSmoke.csproj --no-restore: success
Godot runtime capture --open-star-map: success, PNG в diagnostics/star_map_v3_visual/
Godot runtime capture --open-star-map --star-map-select=sol: success, PNG в diagnostics/star_map_v3_route/
Godot runtime capture --open-star-map --star-map-inspect=orion_0001: success, PNG в diagnostics/star_map_v3_popup/
Godot runtime capture --star-map-fixture-sectors=24 --star-map-fixture-systems-per-sector=7: success, PNG в diagnostics/star_map_v3_dense/
```

Последняя проверка после Star Map font/background correction от 2026-05-11:

```text
Project stretch mode changed from canvas_items to disabled: the game now renders UI at native window resolution instead of scaling the 1280x720 canvas to 1920x1080, which was making Star Map text look dirty/blurry on the user's screen.
StarMapOverlay text shadow pass removed for Star Map labels/panels/popups; text is now single-pass over darker local panels.
Star Map high-res background now uses aspect-preserving cover/crop via DrawTextureRectRegion instead of stretching the tile into the map rect.
Procedural diagonal nebula bands removed from the map layer; only a light glass tint/grid and a small number of navigation specks remain over the high-res background.
Planet popup is clamped to the map rect so it no longer covers the right-side warp target panel.
dotnet build game/SpaceManagersPrototype.sln: 0 warnings, 0 errors
dotnet run --project tests/SpaceManagers.Core.Tests/SpaceManagers.Core.Tests.csproj --no-restore: 39 tests passed
dotnet run --project tests/SpaceManagers.PerfSmoke/SpaceManagers.PerfSmoke.csproj --no-restore: success
Godot runtime capture --resolution 1920x1080 --open-star-map --star-map-inspect=orion_0001: success, PNG в diagnostics/star_map_v4_1080p/
Godot runtime capture --resolution 1920x1080 --open-star-map --star-map-select=sol: success, PNG в diagnostics/star_map_v4_route_1080p/
Godot runtime capture --resolution 1920x1080 --star-map-fixture-sectors=24 --star-map-fixture-systems-per-sector=7: success, PNG в diagnostics/star_map_v4_dense_1080p/
```

Подозрительные зоны для дальнейшей проверки:

- `space_nebula_tile.png` имеет большой размер и может есть VRAM;
- generated background tiles теперь тоже 4096px; держать загруженной только активную систему;
- generated high-res planet runtime surfaces теперь могут быть 4096x2048; не грузить поверхности систем, где игрока сейчас нет;
- солнце использует много кадров, но текущий стабилизированный shader не должен создавать новые texture/material allocations каждый frame;
- HUD/Reticle/Projectile/Ship layers redraw каждый frame, часть этого ожидаема;
- любые новые shader/material/texture allocations в `_Process` или `_Draw` запрещены;
- нельзя каждый frame пересоздавать массивы, materials, textures, polygons, labels, nodes.

Правило для новых perf-правок: сначала culling/reuse/cache, потом снижение качества. Игра должна визуально оставаться сочной.

## Landing / Station Prototype

Добавлен первый вертикальный срез второго слоя геймплея: отдельный изометрический режим посадки на станцию.

Текущий статус: режим временно изолирован от космоса. В `GameRoot.cs`, `Main.tscn` и `project.godot` нет runtime-ссылок на `Landing`, `assets/landing` или `debug_enter_landing`; F11-переход из космоса отключен до будущей доработки режима. Сцену можно запускать только напрямую через Godot `--scene "res://scenes/Landing.tscn"` для разработки.

Ключевые файлы:

- `game/scenes/Landing.tscn` - отдельная сцена посадки.
- `game/scripts/LandingRoot.cs` - рендер станции, псевдо-hex сетка, click-to-move, A* pathfinding, плавное движение персонажа, возврат в космос.
- `game/assets/landing/` - отобранные и подготовленные AOP PNG-ассеты: металлические floor tiles, wall/scenery props, machine/barrel/console.
- `game/assets/reviews/landing-vertical-slice-v1/landing_scene.png` - текущий визуальный smoke-capture.

Управление:

- ЛКМ в `Landing` - бежать в выбранную псевдо-hex клетку.
- `F11` или `Escape` в `Landing` - debug-возврат в космос.
- В сцене также есть интерактивная return-cell/console; клик по ней строит маршрут и возвращает игрока в космос после подхода.

Техническое устройство:

- Координаты клетки хранятся как axial-like `Vector2I`.
- Визуализация изометрическая: `world = ((q - r) * 40, (q + r) * 18)`.
- Топология движения hex-like: 6 соседей, A* с `HexDistance`.
- Визуальный floor теперь рисуется как явные шестиугольные iso-панели; AOP floor diamonds не используются как ходовая сетка, чтобы сцена не читалась как квадратная/ромбовая.
- Движение не телепортирует персонажа: путь клеточный, но позиция интерполируется плавно.
- Декор сейчас рисуется из AOP PNG; часть старых AOP ассетов имела blue-mask, такие PNG нужно предварительно конвертировать в alpha.

Важно по AOP:

- `AOP/data/sprites/critters/Humanoid.spr` найден, но это FOnline sprite-контейнер, не готовый PNG.
- `AOP/data/art/critters/*.FRM/*.FR0..FR5` тоже доступны, но полноценный импорт humanoid-анимаций требует отдельного FRM/SPR decoder pipeline и palette handling.
- Текущий персонаж в `LandingRoot.cs` временный procedural humanoid, чтобы сначала проверить управление/сетку/режим. Следующий ассетный шаг - безопасно декодировать или конвертировать AOP humanoid frames и заменить placeholder.

Проверка landing-сцены:

```powershell
$dotnetDir = "C:\CodexTools\dotnet-8.0.420"
$env:DOTNET_ROOT = $dotnetDir
$env:PATH = "$dotnetDir;$env:PATH"
$godot = ".\.tools\godot\Godot_v4.6.2-stable_mono_win64\Godot_v4.6.2-stable_mono_win64_console.exe"
& $godot --headless --path ".\game" --scene "res://scenes/Landing.tscn" --quit-after 30
```

Визуальный capture landing-сцены требует обычный display driver, потому что headless/dummy viewport не возвращает image texture:

```powershell
$capture = (Resolve-Path ".\game\assets\reviews").Path + "\landing-vertical-slice-v1"
& $godot --path ".\game" --scene "res://scenes/Landing.tscn" -- "--landing-capture-dir=$capture"
```

Проверка, что космос не связан с Landing:

```powershell
rg -n "Landing|landing|debug_enter_landing|F11" ".\game\scripts\GameRoot.cs" ".\game\scenes\Main.tscn" ".\game\project.godot"
```

Ожидаемый результат: совпадений нет.

## Build And Export Notes

`tools/export_windows.ps1` делает:

- скачивает/готовит Godot Mono;
- скачивает export templates;
- готовит .NET SDK;
- запускает core tests;
- билдит Godot project Release;
- делает Godot import;
- экспортирует Windows Desktop build.

Чтобы изменения в manifest/assets/code попали в exe, нужен повторный:

```powershell
& ".\tools\export_windows.ps1"
```

## Asset Generation Tools

Скрипты в `tools`:

- `build_approved_ship_assets.py` - сборка утвержденного каталога кораблей;
- `prepare_ship_assets.py` - подготовка ship assets;
- `generate_space_textures.py` - генерация space texture;
- `generate_planet_textures.py` - генерация/подготовка planet textures;
- `import_imagegen_assets.py` - legacy импорт/нарезка imagegen sheets в raw generated stars/planets/backgrounds;
- `process_highres_imagegen_assets.py` - обработка direct high-res imagegen sources из `star_sources_4k`, `planet_sources_4k`, `background_sources_4k`, validation report и регистрация в catalog;
- `process_planet_surface_maps.py` - legacy обработка raw generated planet cuts в 2048x1024 seamless runtime surface maps;
- `create_background_tiles.py` - legacy создание Sol-style 4096px background tile variants для generated systems;
- `generate_star_systems.py` - offline generator galaxy sector/system JSON из `star_system_catalog.json`; test modes: `--planet-showcase-highres`, `--coverage-highres-assets`;
- `generate_asteroid_assets.py` - генерация asteroid sprites в `game/assets/asteroids`;
- `generate_effect_assets.py` - генерация flame-lobe, heat-corona, impact-flash, dust-ring, smoke и shard PNG для asteroid VFX в `game/assets/effects`;
- `run_game.ps1` - запуск Godot;
- `test_core.ps1` - запуск core tests;
- `export_windows.ps1` - экспорт exe.

Текущий star-system pipeline:

```powershell
python tools/process_highres_imagegen_assets.py --update-catalog --replace-lowres --replace-backgrounds
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts
```

Текущие test-mode команды для визуальной проверки high-res ассетов:

```powershell
# Одна система со всеми 12 high-res типами планет.
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts --planet-showcase-highres

# Минимальный набор систем, покрывающий все high-res звезды, фоны и планеты.
python tools/generate_star_systems.py --seed 3311337 --clean --write-image-prompts --coverage-highres-assets
```

Важно: полный `process_highres_imagegen_assets.py --write-experimental-star-frames` тяжелый, потому что пишет 96 кадров на каждый star variant, а с experimental - еще 96. Не запускать несколько processor-процессов параллельно. Если запуск был прерван, проверить `Get-Process python`, остановить зависшие `process_highres_imagegen_assets.py`, затем проверить полноту `star_frames/*/sun_00..sun_95.png` и `star_frames_experimental/*/sun_00..sun_95.png` перед catalog/generator update.

Новый imagegen-подход: не генерировать multi-asset sheets для планет/звезд/фонов. Генерировать отдельные high-res sources в:

- `game/assets/generated/star_sources_4k/*.png`;
- `game/assets/generated/planet_sources_4k/*.png`;
- `game/assets/generated/background_sources_4k/*.png`.

Затем запускать `process_highres_imagegen_assets.py`, который делает runtime outputs, проверяет размеры/резкость/seam risk и пишет `tools/generated/highres_asset_report.json`. Старый sheet pipeline оставлен только как fallback/история.

Перед изменением generated assets проверь, не перезатрешь ли ручные правки пользователя.

## User Preferences And Current Agreements

Важные договоренности с пользователем:

- отвечать и объяснять лучше на русском;
- Землю не трогать, она уже нравится;
- солнце визуально не менять без явного запроса;
- текущее решение мерцания солнца утверждено: стабилизировать rim/контур shader-ом, не гасить внутреннюю плазму;
- при создании новых звезд использовать текущий `StabilizedSunView`/`sun_stabilized.gdshader` как эталонный подход;
- generated systems должны ориентироваться на `Sol` как эталон качества;
- generated backgrounds должны использовать `SpaceBackdropView` и Sol-style tiled background method, а не stretched fullscreen imagegen sheets; для high-res tiles допустима только runtime-вариативность раскладки/слоев через system seeds, не пересинтез PNG без явного запроса;
- generated background runtime-параметры держать как у Sol: `TextureAlpha=1.0`, `TextureParallax=0.08`, `StarParallax=0.32`; новые ассеты подключать только в этот renderer-layer;
- generated planet/background/star imagegen assets должны идти через direct high-res pipeline и validation report; не принимать ассеты, которые после обработки станут мыльными или растянутыми;
- generated planet textures должны проходить через `process_highres_imagegen_assets.py` для новых high-res sources или через legacy `process_planet_surface_maps.py` только для старых sheet-sliced assets, чтобы не возвращались размытие и черный seam;
- generated systems создаются offline; runtime не должен при старте игры грузить всю галактику;
- стартовый сектор называется `Orion`; текущий generated-набор временно содержит `Sol` плюс одну тестовую систему `Planet Showcase` для оценки всех 12 типов планет;
- шкала планет идет от текущего видимого солнца: `1000 reference = 1320 world`;
- кольца Saturn/Uranus должны соответствовать масштабу планеты;
- планеты должны выглядеть качественно и быть анимированы, не процедурно "тяп-ляп";
- фон должен двигаться медленно, но не быть статичным;
- важна стабильная производительность, цель - вернуться к стабильным 60+ FPS;
- красоту не приносить в жертву первой же оптимизацией: сначала искать горячую точку, потом culling/cache/reuse/LOD;
- боевую систему развивать постепенно: оружие, AI, урон, взрывы, смерть, награды;
- после каждой кодовой/ассетной правки обновлять `TECHNICAL_MANIFEST.md`;
- после каждой правки в финальном ответе давать команду запуска игры.

## Safe Change Checklist

Перед финальным ответом после правок желательно:

1. Запустить core tests, если менялась `src/SpaceManagers.Core`.
2. Запустить `dotnet build` для `game/SpaceManagersPrototype.csproj`.
3. Запустить Godot headless import/smoke, если менялись Godot scripts/assets.
4. Если менялась визуальная часть, проверить в реальной игре через `tools/run_game.ps1`.
5. Обновить `TECHNICAL_MANIFEST.md` после правок.
6. В финальном ответе приложить команду запуска игры.
7. Если пользователь хочет exe, выполнить `tools/export_windows.ps1`.

## Common Pitfalls

- Простая строка пути к `.ps1` в PowerShell не запускает скрипт. Нужно:

```powershell
& ".\tools\run_game.ps1"
```

- После изменения `ships_manifest.json` нужно перезапустить игру.
- После изменения ассетов/кода для exe нужно заново экспортировать.
- Не менять `ReferenceStarSize` и `SunVisualWorldSize` местами.
- Не использовать прямой world-size для планет, если речь идет о дизайнерском размере планеты.
- Не трогать Earth pipeline без явного согласия пользователя.
- Не чинить мерцание солнца заменой ассетов или статичной "заморозкой" всей анимации: проблема была в rim, не во внутренней плазме.
- Не возвращать generated backgrounds к `game/assets/generated/backgrounds/*.png` как fullscreen/stretch texture.
- Не чинить generated background quality через новый inline `DrawNebulae()`/fullscreen overlay; править source tile или `SpaceBackdropView` layer contract.
- Не делать tileability космического фона через чистый `FLIP_LEFT_RIGHT`/`FLIP_TOP_BOTTOM` 2x2 без renderer-layer компенсации: такой тайл выглядит как зеркало.
- Не лечить зеркальность агрессивным удалением baked-звезд или RGB phase synthesis: это меняет approved art direction; сначала править `SpaceBackdropView`.
- Не добавлять runtime color grade/alpha dimming к background texture layer без явного запроса: accepted ассеты должны показываться в своей исходной цветовой гамме.
- Не писать в generated system JSON raw planet maps из `game/assets/generated/planets/*.png`; использовать `game/assets/generated/planet_surfaces/*.png`.
- Не считать позиции планет на миникарте от отдельного времени; HUD и `SpaceBackground` должны жить на одном `systemTimeSeconds`.
- Не загружать все generated systems/textures на старте; полноценная система грузится только когда игрок в ней.
- Не добавлять тяжелые per-frame allocations в `_Draw`/`_Process`.
- Не откатывать чужие изменения в git/worktree без прямого запроса.
