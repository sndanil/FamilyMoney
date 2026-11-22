# Самообновление Windows и Android: проект

Статус: **проект, не реализовано**. Документ описывает механизм целиком:
контракт релиза, общий код проверки, установку на Windows и Android, откат,
безопасность и порядок внедрения. Связанные файлы: `.github/workflows/release.yml`,
`src/FamilyMoney.Android/FamilyMoney.Android.csproj`, `src/Desktop.Publish.props`,
`src/FamilyMoney/AppInit.cs`, `README.md`.

## 1. Цель и границы

### 1.1 Цель

Приложение само узнаёт, что на GitHub появилась новая собранная сборка, и
предлагает обновиться:

- **Windows** — скачивает `FamilyMoney.Desktop-win-x64.zip`, проверяет,
  распаковывает и заменяет каталог приложения, перезапускается. Без установщика,
  без прав администратора, данные в `%LOCALAPPDATA%\FamilyMoney` не трогаются.
- **Android** — скачивает `FamilyMoney.Android.apk`, проверяет подпись и
  `versionCode`, отдаёт файл системному установщику (`PackageInstaller`).
  Данные приложения сохраняются, потому что пакет ставится поверх с тем же
  `applicationId` и той же подписью.

**Признак наличия обновления** — опубликованный GitHub Release, в котором есть
`update.json` с записью для текущей платформы и версия больше установленной.
Сами по себе артефакты GitHub Actions (build artifacts) обновлением не считаются:
они приватны для репозитория, истекают и не имеют стабильного URL. Обновлением
становится только то, что job `release` положил в Release.

### 1.2 Границы первой версии

| Входит | Не входит |
|---|---|
| GitHub Releases как единственный источник | Свой update-сервер, CDN, S3 |
| Windows x64, классический `FamilyMoney.Desktop` | Linux, macOS, `FamilyMoney.Navigation.Desktop` |
| Android APK вне Google Play | Google Play / Play Integrity / in-app updates |
| Проверка версии, скачивание, проверка хеша и подписи | Дельта-патчи, инкрементальные обновления |
| Установка с подтверждением пользователя | Тихая установка без ведома пользователя |
| Откат на Windows при неудачном старте | Автоматический откат Android (система не умеет downgrade) |
| Канал `stable` | Каналы `beta`/`nightly` (задел оставлен) |

### 1.3 Допущения

- Репозиторий публичный: `https://github.com/sndanil/FamilyMoney`. Для
  приватного репозитория понадобился бы токен внутри приложения — это отдельное
  решение, в первой версии его нет.
- Windows-сборка распространяется zip-архивом и запускается из папки, куда у
  пользователя есть запись. Если папка защищена (`Program Files`, сетевой диск,
  только чтение), self-update невозможен — остаётся ручное обновление.
- Android-сборка подписана тем же ключом, что и установленная. Смена ключа
  ломает обновление: Android не поставит APK с другой подписью, потребуется
  переустановка.
- Часы на устройстве более-менее верны; версия сравнивается по номеру, а не по
  времени публикации.

## 2. Что есть сейчас

| Место | Сейчас | Следствие для обновлений |
|---|---|---|
| `.github/workflows/release.yml` | На push в `main` создаётся релиз `v0.$(date +%y%m%d)`, повторный запуск в тот же день перезаписывает файлы через `gh release upload --clobber` | Один тег в сутки; перезапись меняет содержимое под тем же номером, клиент не отличит новую сборку от старой |
| `src/FamilyMoney.Android/FamilyMoney.Android.csproj` | `ApplicationVersion` = `yyMMdd`, `ApplicationDisplayVersion` = `yy.MM.dd` | `versionCode` растёт раз в сутки; локальная сборка и CI в один день могут совпасть |
| Android-манифест | Нет `REQUEST_INSTALL_PACKAGES` | Приложение не может запустить установку APK |
| `src/Desktop.Publish.props` | Release: `PublishSingleFile=true`, `SelfContained=false`, ReadyToRun | В поставке один exe плюс нативные библиотеки; заменять нужно весь каталог, а не только exe |
| Windows-релиз | Только `FamilyMoney.Desktop-win-x64.zip` | Нет установщика, нет updater-процесса, нет проверки целостности |
| Android-релиз | `FamilyMoney.Android.apk`; при отсутствии ключа публикуется `-unsigned.apk` | Неподписанный APK бесполезен для обновления; релиз должен падать, а не предупреждать |
| `README.md` | «Автообновления у APK нет» | После внедрения раздел нужно обновить |

Дополнительно: в релизе уже есть `FamilyMoney.Desktop-linux-x64.tar.gz`, но
Linux в первую версию не входит. Общий код проверки стоит писать так, чтобы
Linux можно было добавить позже без переделки.

## 3. Краткое решение

| Вопрос | Решение |
|---|---|
| Источник обновлений | GitHub Releases API, `releases/latest`; без авторизации, с ETag-кешем |
| Что считается обновлением | Опубликованный (не draft, не prerelease) релиз с `update.json` и версией больше текущей |
| Версия релиза | `v0.<YYMMDD>.<run>`, где `run` — `GITHUB_RUN_NUMBER`; монотонно растёт |
| `versionCode` Android | Секунды с 2020-01-01 UTC; растёт и локально, и в CI |
| Манифест | `update.json` — маленький JSON с версией, именами ассетов, размерами и SHA-256 |
| Проверка на клиенте | При старте (не чаще раза в 12 ч) и вручную; фоновая проверка не блокирует UI |
| Windows | Отдельный `FamilyMoney.Updater.exe`: ждёт выхода приложения, меняет каталоги местами, перезапускает, при неудаче откатывает |
| Android | `DownloadManager` → проверка SHA-256/подписи/`versionCode` → `PackageInstaller` → системное подтверждение |
| Подпись | Android: обязательная подпись APK + сверка с установленной. Windows: SHA-256; Authenticode и подпись манифеста — опционально |
| Данные | Обновление не касается каталога данных; перед установкой — обычный бэкап базы |

## 4. Контракт релиза

### 4.1 Версия и тег

Версия генерируется в CI один раз, в job `prepare`, и передаётся во все
сборочные job:

```bash
BUILD_DATE="$(date -u +%y%m%d)"          # 261122
RUN_NUMBER="${GITHUB_RUN_NUMBER}"        # 42
VERSION="0.${BUILD_DATE}.${RUN_NUMBER}"  # 0.261122.42
TAG="v${VERSION}"                        # v0.261122.42
DISPLAY_VERSION="0.$(date -u +%y.%m.%d)" # 0.26.11.22
VERSION_CODE="$(( $(date -u +%s) - 1577836800 ))"  # секунды с 2020-01-01 UTC
```

В `$GITHUB_OUTPUT` job `prepare` пишет `version`, `tag`, `display_version`,
`version_code` — остальные job читают их через `needs.prepare.outputs.*`.

- `run` — `GITHUB_RUN_NUMBER`, а не дата. Поэтому два релиза в один день
  различаются, а `--clobber` больше не нужен для «новой» сборки.
- Дата в теге — дата запуска сборки (UTC), а не дата коммита. Релиз делается
  только из текущего HEAD `main`; перезапуск старого коммита — не плановый
  релиз, для проверки используйте draft. Иначе старый код мог бы получить
  более новый номер версии.
- Тег и `version` в манифесте — одно и то же без префикса `v`.
- `versionCode` — целое положительное, максимум 2 100 000 000 (ограничение
  Google Play; Android сам принимает `int`). В 2026 году это ~217 млн, запас до
  ~2088 года. Для двух сборок в одну секунду (бывает только локально)
  `ApplicationVersion` переопределяется вручную.
- `versionName` Android принимает вид `0.26.11.22` (четыре компонента, каждый
  не больше 65534): `0.261122.42` в `AssemblyVersion` не помещается. Полная
  версия `0.261122.42` живёт в `BuildInfo` и манифесте, а в интерфейсе
  показывается как «22.11.2026, сборка 42».
- Локальная сборка без CI получает `BuildVersion = 0.0.0-dev`,
  `BuildChannel = dev`. Автопроверка и установка для неё отключены: разработчик
  не должен случайно заменить свою сборку релизом. Ручная проверка показывает
  статус «локальная сборка».

#### 4.1.1 Что меняется в csproj и BuildInfo

В `FamilyMoney.Android.csproj` меняется только формула по умолчанию; значение
по-прежнему можно переопределить через `-p:ApplicationVersion=...`:

```xml
<!-- Секунды с 2020-01-01 UTC: versionCode растёт и у локальных сборок, и у CI.
     Старый yyMMdd (261122) меньше нового значения (~217 млн), поэтому первый
     релиз с новой схемой ставится поверх установленного. -->
<ApplicationVersion Condition="'$(ApplicationVersion)' == ''">$([MSBuild]::Subtract($([System.DateTimeOffset]::UtcNow.ToUnixTimeSeconds()), 1577836800))</ApplicationVersion>
<!-- versionName: 0.yy.MM.dd, в AssemblyVersion каждый компонент <= 65534. -->
<ApplicationDisplayVersion Condition="'$(ApplicationDisplayVersion)' == ''">0.$([System.DateTime]::UtcNow.ToString("yy.MM.dd"))</ApplicationDisplayVersion>
```

`BuildVersion`/`BuildChannel` попадают в сборку через `AssemblyMetadata` в
проекте `FamilyMoney.Updates`:

```xml
<PropertyGroup>
  <BuildVersion Condition="'$(BuildVersion)' == ''">0.0.0-dev</BuildVersion>
  <BuildChannel Condition="'$(BuildChannel)' == ''">dev</BuildChannel>
</PropertyGroup>

<ItemGroup>
  <AssemblyMetadata Include="FamilyMoney.BuildVersion" Value="$(BuildVersion)" />
  <AssemblyMetadata Include="FamilyMoney.BuildChannel" Value="$(BuildChannel)" />
  <AssemblyMetadata Include="FamilyMoney.BuildCommit" Value="$(SourceRevisionId)" />
</ItemGroup>
```

CI передаёт `-p:BuildVersion=0.261122.42 -p:BuildChannel=stable`; локальная
сборка остаётся `0.0.0-dev`. Если на Android Release включён агрессивный
trimming и атрибуты не доживают, `BuildInfo` лучше генерировать в
`BuildInfo.g.cs` MSBuild-таргетом (`WriteLinesToFile`) и читать константы —
это надёжнее рефлексии. DTO манифеста и релиза сериализуются через
source-generated `JsonSerializerContext`, чтобы trimming/AOT не вырезал
свойства.

### 4.2 Артефакты релиза

| Файл | Назначение | Обязателен |
|---|---|---|
| `FamilyMoney.Desktop-win-x64.zip` | Windows-приложение + `FamilyMoney.Updater.exe` | да |
| `FamilyMoney.Android.apk` | Подписанный APK | да |
| `FamilyMoney.Desktop-linux-x64.tar.gz` | Linux (пока вне механизма) | нет |
| `update.json` | Манифест обновления | да |

Имена фиксированы: клиент ищет их по имени, а не по «первому попавшемуся»
ассету. Неподписанный APK (`FamilyMoney.Android-unsigned.apk`) в релиз не
попадает: release job проверяет наличие именно `FamilyMoney.Android.apk` и
падает, если его нет.

### 4.3 `update.json`

Манифест генерируется в release job из уже собранных файлов, поэтому хеши
всегда соответствуют опубликованным ассетам.

```json
{
  "schema": 1,
  "channel": "stable",
  "version": "0.261122.42",
  "tag": "v0.261122.42",
  "platforms": {
    "windows-x64": {
      "asset": "FamilyMoney.Desktop-win-x64.zip",
      "size": 51234567,
      "sha256": "3f2c1d0e9a4b7c8d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5",
      "entryPoint": "FamilyMoney.Desktop.exe"
    },
    "android": {
      "asset": "FamilyMoney.Android.apk",
      "size": 61234567,
      "sha256": "9a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9",
      "versionCode": 217522175,
      "minSdk": 23
    },
    "linux-x64": {
      "asset": "FamilyMoney.Desktop-linux-x64.tar.gz",
      "size": 156789012,
      "sha256": "0f1e2d3c4b5a69788796a5b4c3d2e1f00f1e2d3c4b5a69788796a5b4c3d2e1f0"
    }
  }
}
```

Правила:

- `schema` — целое, текущая версия `1`. Неизвестная схема → обновление не
  предлагается, показывается «обновите приложение вручную».
- `channel` — `stable`. Клиент с другим каналом игнорирует релиз.
- `version`/`tag` — должны совпадать друг с другом (с точностью до `v`) и
  парситься как `0.<YYMMDD>.<run>`.
- `platforms` — ключ выбирается по платформе: `windows-x64`, `android`.
  Отсутствие ключа означает «для этой платформы обновления нет» (например,
  релиз только с Android-фиксом).
- `asset` — точное имя файла в том же релизе. Клиент сверяет его с
  `assets[].name` из GitHub API, иначе манифест считается недействительным.
- `size` — размер в байтах; проверяется до и после скачивания.
- `sha256` — lowercase hex, 64 символа. Сравнение — после полной загрузки.
- `versionCode` — фактический `versionCode` APK. Это независимая проверка:
  если манифест и APK разошлись, установка отменяется.
- `minSdk` — минимальный API; на старом устройстве обновление не предлагается.
- `entryPoint` — только Windows; updater проверяет, что файл есть в архиве.

Манифест **не подписан** в первой версии. Для Android это компенсируется
проверкой подписи APK: даже подменённый манифест не позволит поставить APK с
чужим ключом. Для Windows первая версия полагается на TLS + SHA-256; усиление
(подпись манифеста или Authenticode) описано в разделе 8.

### 4.4 Изменения в `release.yml`

1. **Job `prepare`** считает `version`, `tag`, `display_version`,
   `version_code` и отдаёт их через `outputs`.
2. **Сборочные job** получают `needs: prepare` и передают свойства:
   - Windows/Linux: `-p:BuildVersion=${{ needs.prepare.outputs.version }}
     -p:BuildChannel=stable`;
   - Android дополнительно:
     `-p:ApplicationVersion=${{ needs.prepare.outputs.version_code }}
     -p:ApplicationDisplayVersion=${{ needs.prepare.outputs.display_version }}`.
3. **Android**: если `ANDROID_KEYSTORE_BASE64` пуст на push в `main` — job
   падает, а не публикует неподписанный APK. На pull request неподписанная
   сборка допустима: она нужна только для проверки компиляции.
4. **Windows**: в `build-windows` добавляется публикация updater-проекта и
   копирование его файлов в `publish` до создания zip. В zip обязаны быть
   `FamilyMoney.Desktop.exe` и `FamilyMoney.Updater.exe`.
5. **Проверка APK**: `aapt2 dump badging` (или `apkanalyzer`) сверяет
   `package`, `versionCode`, `versionName` с ожидаемыми; `apksigner verify
   --print-certs` подтверждает подпись. Ошибка → job падает.
6. **Job `release`** скачивает артефакты, проверяет наличие файлов, считает
   `sha256`/`size`, генерирует `update.json`, валидирует его (`jq -e`) и только
   потом делает `gh release create`.
7. Повторный запуск того же workflow run (re-run) видит существующий тег:
   ассеты перезаписываются, релиз не создаётся заново. Это аварийный путь;
   обычная новая сборка всегда получает новый `run` и новый тег.
8. Для проверки механизма до публикации — `workflow_dispatch` с флагом
   `draft=true`: релиз создаётся черновиком. `releases/latest` черновики не
   видит, поэтому тестировщики скачивают файлы вручную, а пользователи ничего
   не получают.
9. **Серийность релизов.** В workflow добавляется
   `concurrency: { group: release-${{ github.ref }}, cancel-in-progress: false }`.
   Без этого два параллельных push в `main` могут собрать APK с `versionCode`,
   который меньше уже опубликованного: Android откажется ставить «новую»
   версию. Дополнительно release job сравнивает свой `versionCode` с
   `versionCode` из `update.json` предыдущего релиза и падает, если он не
   больше. Это дешёвая страховка от гонок и от ручных перезапусков.

Фрагмент для release job:

```bash
version="${{ needs.prepare.outputs.version }}"
tag="${{ needs.prepare.outputs.tag }}"
version_code="${{ needs.prepare.outputs.version_code }}"

test -f dist/FamilyMoney.Desktop-win-x64.zip
test -f dist/FamilyMoney.Android.apk
unzip -l dist/FamilyMoney.Desktop-win-x64.zip | grep -q 'FamilyMoney.Desktop.exe'
unzip -l dist/FamilyMoney.Desktop-win-x64.zip | grep -q 'FamilyMoney.Updater.exe'

win_sha=$(sha256sum dist/FamilyMoney.Desktop-win-x64.zip | cut -d' ' -f1)
apk_sha=$(sha256sum dist/FamilyMoney.Android.apk | cut -d' ' -f1)
win_size=$(stat -c%s dist/FamilyMoney.Desktop-win-x64.zip)
apk_size=$(stat -c%s dist/FamilyMoney.Android.apk)

python3 - "$version" "$tag" "$version_code" "$win_sha" "$win_size" "$apk_sha" "$apk_size" <<'PY'
import json, sys
version, tag, code, wsha, wsize, asha, asize = sys.argv[1:]
data = {
  "schema": 1,
  "channel": "stable",
  "version": version,
  "tag": tag,
  "platforms": {
    "windows-x64": {
      "asset": "FamilyMoney.Desktop-win-x64.zip",
      "size": int(wsize),
      "sha256": wsha,
      "entryPoint": "FamilyMoney.Desktop.exe",
    },
    "android": {
      "asset": "FamilyMoney.Android.apk",
      "size": int(asize),
      "sha256": asha,
      "versionCode": int(code),
      "minSdk": 23,
    },
  },
}
json.dump(data, open("dist/update.json", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
PY

jq -e --arg version "$version" '.schema == 1 and .version == $version' dist/update.json
gh release create "$tag" dist/* --title "$tag" --generate-notes
```

Замечания:

- `python3`/`jq` есть на `ubuntu-latest`; отдельный скрипт
  `packaging/update/manifest.py` лучше вынести в репозиторий, чтобы его можно
  было тестировать и переиспользовать локально.
- `update.json` не кладётся в zip и не нужен приложению внутри поставки: он
  читается из релиза.
- `gh release create` с `dist/*` захватит и `update.json`, и все платформенные
  ассеты. Лишние файлы (например, `test-results`) в `dist` попадать не должны:
  `download-artifact` должен качать только нужные артефакты.

## 5. Общая архитектура

### 5.1 Компоненты

```
┌────────────────────────────────────────────────────────────────────┐
│ FamilyMoney.Updates (net10.0, без UI)                              │
│  BuildInfo            текущая версия, канал, commit                 │
│  ReleaseVersion       разбор и сравнение 0.<YYMMDD>.<run>          │
│  UpdateManifest       DTO манифеста                                │
│  GitHubReleaseClient  releases/latest, ETag, лимиты, манифест       │
│  UpdateService        проверка, выбор ассета, состояние             │
│  UpdateStateStore     etag.json, lastCheck, пропущенная версия      │
│  IUpdateInstaller     платформенная установка                       │
└───────────────┬───────────────────────────────┬────────────────────┘
                │                               │
┌───────────────▼──────────────┐  ┌─────────────▼────────────────────┐
│ FamilyMoney.Desktop          │  │ FamilyMoney.Android              │
│  WindowsUpdateInstaller      │  │  AndroidUpdateInstaller          │
│  UpdateWindow / баннер       │  │  DownloadManager + PackageInstaller│
│  SettingsView                │  │  SettingsPage / диалог            │
└───────────────┬──────────────┘  └───────────────────────────────────┘
                │
┌───────────────▼──────────────┐
│ FamilyMoney.Updater (Win exe)│
│  ожидание PID, замена каталога│
│  перезапуск, ack, откат       │
└──────────────────────────────┘
```

`FamilyMoney.Updates` не ссылается на Avalonia и на Android API. Это позволяет
покрыть его юнит-тестами и позже добавить Linux. Платформенные проекты
реализуют только `IUpdateInstaller` и показывают UI.

**Направление зависимостей** (важно, иначе получится цикл):

```
FamilyMoney.Updates  →  только BCL + Microsoft.Extensions.Logging.Abstractions
FamilyMoney          →  FamilyMoney.Updates
FamilyMoney.Desktop / FamilyMoney.Android → FamilyMoney + FamilyMoney.Updates
```

Поэтому `UpdateConfiguration` — тип из `FamilyMoney.Updates`, а
`RootConfiguration` (проект `FamilyMoney`) просто содержит его свойство.
`UpdateService` не знает про `IGlobalConfiguration`: настройки он получает через
`IUpdatePreferences` (интерфейс в `FamilyMoney.Updates`, реализация — в
`FamilyMoney`), а путь к `state.json` — через конструктор `UpdateStateStore`.
Обратной зависимости `FamilyMoney.Updates → FamilyMoney` нет.

### 5.2 Регистрация в DI

В `AppInit.ConfigureServices` (общий код):

```csharp
services.AddSingleton(new UpdateStateStore(
    Path.Combine(GlobalConfiguration.HomeFolder, "updates")));
services.AddSingleton<IUpdatePreferences, GlobalConfigurationUpdatePreferences>();
services.AddSingleton<GitHubReleaseClient>();
services.AddSingleton<IUpdateService, UpdateService>();
```

Платформенный `ConfigureServices` (вызывается после общего) добавляет:

```csharp
// Desktop, Windows
services.AddSingleton<IUpdateInstaller, WindowsUpdateInstaller>();
// Desktop, Linux (проект тот же, но updater только для Windows)
services.AddSingleton<IUpdateInstaller, NullUpdateInstaller>();
// Android
services.AddSingleton<IUpdateInstaller, AndroidUpdateInstaller>();
// Design mode / Navigation.Desktop
services.AddSingleton<IUpdateInstaller, NullUpdateInstaller>();
```

`UpdateService` получает `IUpdateInstaller` лениво, поэтому порядок регистрации
не важен.

### 5.3 Проверка обновлений

**Эндпоинт:**

```
GET https://api.github.com/repos/sndanil/FamilyMoney/releases/latest
Accept: application/vnd.github+json
X-GitHub-Api-Version: 2022-11-28
User-Agent: FamilyMoney/0.261122.42 (Windows 10.0.19045; x64)
If-None-Match: "<etag из прошлой проверки>"
```

- `releases/latest` не отдаёт черновики и prerelease — это ровно канал
  `stable`.
- Ответ `200` — новый релиз; `304` — ничего не изменилось. `304` не расходует
  лимит GitHub, поэтому ETag обязателен.
- Ответ `404` — релизов ещё нет; это не ошибка сети.
- `403`/`429` — лимит. Читаем `X-RateLimit-Reset`, показываем «повторите после
  HH:mm», автоматически не долбим.
- Таймауты: соединение 10 с, весь запрос 30 с, скачивание — 60 с без прогресса.
- Лимит неавторизованных запросов — 60/час на IP. При интервале 12 ч и
  ETag этого достаточно; при ручных нажатиях — не чаще одного запроса в 60 с.

**Платформенный ключ:** Windows — `windows-{ProcessArchitecture}` (для текущего
релиза `windows-x64`), Android — `android`. Ключ вычисляется по процессу, а не
по ОС: x64-приложение под ARM64-эмуляцией остаётся `windows-x64`.

**Алгоритм:**

```
1. Если BuildInfo.Channel == "dev" и проверка автоматическая → выходим.
2. Если с последней проверки прошло < 12 ч и проверка не ручная → выходим.
3. GET releases/latest с ETag.
4. На 304 берём закешированный release JSON.
5. Проверяем: tag/version, channel, наличие update.json в assets.
6. Скачиваем update.json (лимит 64 КБ) с таймаутом 15 с.
7. Валидируем схему, версию, платформу, имя ассета, размер, sha256.
8. Сравниваем version с BuildInfo.Version:
   - больше → UpdateAvailable;
   - равно/меньше → UpToDate;
   - версия пропущена пользователем → Skipped (но ручная проверка показывает).
9. Для Android дополнительно: `versionCode` из манифеста > текущего
   (`IUpdateInstaller.CurrentVersionCode`).
10. Сохраняем lastCheck, etag, кеш релиза.
```

**Состояния:**

| Статус | Что видит пользователь | Что делает приложение |
|---|---|---|
| `UpdateAvailable` | «Доступна версия …» + заметки | предлагает скачать |
| `UpToDate` | «У вас последняя версия» | — |
| `Skipped` | «Версия … пропущена» | не предлагает автоматически |
| `Disabled` | «Проверка обновлений выключена» | — |
| `DevelopmentBuild` | «Локальная сборка» | установка недоступна |
| `NoReleases` | «Опубликованных версий пока нет» | — |
| `NetworkError` | «Не удалось связаться с GitHub» | повтор по кнопке |
| `RateLimited` | «GitHub временно ограничил запросы» | ждёт `reset` |
| `InvalidData` | «Релиз повреждён или не содержит файл обновления» | ссылка на релиз |
| `Unsupported` | «Для этой платформы обновления нет» | — |

**Когда проверять:**

- При старте: через 5 с после появления главного окна, если автоматическая
  проверка включена и прошло больше 12 ч. Не блокирует запуск.
- Вручную: кнопка «Проверить обновления» в настройках.
- Периодически: в первой версии не нужно; при желании — таймер раз в 6 ч, пока
  приложение открыто. Фоновой службы на Windows нет, на Android WorkManager не
  используется: проверка при запуске достаточна.

### 5.4 Сравнение версий

`ReleaseVersion` — это `0.<YYMMDD>.<run>`:

```csharp
public sealed record ReleaseVersion(int Date, int Build) : IComparable<ReleaseVersion>
{
    // Date: 261122, Build: 42
    public static bool TryParse(string? value, out ReleaseVersion version);
    public int CompareTo(ReleaseVersion? other) =>
        Date != other!.Date ? Date.CompareTo(other.Date) : Build.CompareTo(other.Build);
}
```

- Допускается `v` в начале, пробелы по краям, legacy-формат без третьего
  компонента (`v0.261122` → `Build = 0`).
- `YYMMDD` проверяется как реальная дата (год 2000 + YY), а не просто шесть цифр.
- `0.0.0-dev` не парсится как релиз: это признак dev-сборки.
- Сравнение — сначала дата, потом номер сборки. Время публикации и
  `published_at` для решения не используются (часы могут врать).
- Если тег и `version` в манифесте не совпадают — `InvalidData`.

### 5.5 Настройки и состояние

Пользовательские настройки — в `RootConfiguration` (локальный `appsettings.json`,
не синхронизируется). Сам тип `UpdateConfiguration` лежит в
`FamilyMoney.Updates`, чтобы общий код не зависел от `FamilyMoney`:

```csharp
// FamilyMoney.Updates
public sealed class UpdateConfiguration
{
    public bool CheckOnStartup { get; set; } = true;
    public bool WifiOnly { get; set; } = true;        // Android
    public string? SkippedVersion { get; set; }       // "0.261122.42"
    public DateTimeOffset? LastPromptedAt { get; set; }
    public string? LastPromptedVersion { get; set; }
}

public interface IUpdatePreferences
{
    UpdateConfiguration Get();
}
```

```csharp
// FamilyMoney.Configuration
public sealed class RootConfiguration
{
    // ...
    public FamilyMoney.Updates.UpdateConfiguration Updates { get; set; } = new();
}
```

Техническое состояние — отдельный файл, чтобы не засорять настройки:

- Windows: `%LOCALAPPDATA%\FamilyMoney\updates\state.json`,
  `...\updates\etag.txt`, `...\updates\logs\`.
- Android: `FilesDir/updates/state.json`, `FilesDir/updates/`.

```json
{
  "etag": "\"abc123\"",
  "lastCheckedAt": "2026-11-22T14:00:00Z",
  "lastNotifiedVersion": "0.261122.42",
  "cachedRelease": { "tag_name": "v0.261122.42", "body": "...", "...": "..." }
}
```

Кеш релиза нужен для ответа `304`: без него нечего показать. Если кеш
повреждён — просто делаем полный запрос.

### 5.6 Интерфейс

**Windows:**

- В `MainWindow` — тонкий баннер над рабочей областью: «Доступна версия
  0.261122.42 · Обновить · Позже». Баннер не модальный, не мешает работе.
  «Позже» скрывает его до следующего запуска; «Пропустить версию» — в окне
  обновления.
- `UpdateWindow` — модальное окно (`ShowDialog` к `MainWindow`) с:
  - текущей и новой версией, датой, размером;
  - заметками релиза (plain text, первые ~4000 символов) и ссылкой «Открыть на
    GitHub»;
  - кнопками «Обновить» (слева) и «Позже» (справа) — по правилу репозитория
    главная кнопка слева;
  - прогрессом скачивания, отменой, статусом проверки/установки.
- В `SettingsView` — карточка «Обновления»: текущая версия, переключатель
  «Проверять при запуске», кнопка «Проверить сейчас», статус последней проверки,
  «Пропущенная версия» и кнопка снять пропуск.

**Android:**

- В `SettingsPage` — карточка «Обновления»: версия, переключатель автопроверки,
  «Только по Wi-Fi», кнопка «Проверить обновления», статус.
- При доступном обновлении — диалог/страница с заметками, размером и кнопками
  «Скачать и установить» / «Позже» / «Пропустить».
- После скачивания — объяснение, зачем нужно разрешение «Установка неизвестных
  приложений», и кнопка «Разрешить». Если разрешение уже есть — сразу
  системный установщик.
- Прогресс — в приложении и в шторке (`DownloadManager`).
- После успешной установки — «Что нового» один раз на версию.

Тексты — русские, как весь интерфейс. Ошибки формулируются действием:
не «Exception 0x...», а «Не удалось скачать файл. Проверьте подключение и
повторите».

## 6. Windows: самообновление

### 6.1 Раскладка и требования

```
<папка установки>\                 ← заменяется целиком
  FamilyMoney.Desktop.exe
  FamilyMoney.Updater.exe
  appsettings.json                 ← defaults, не пользовательские настройки
  ... нативные библиотеки ...

%LOCALAPPDATA%\FamilyMoney\        ← никогда не заменяется
  appsettings.json                 ← пользовательские настройки
  database.db
  Backups\
  logs\
  updates\                         ← загрузки, состояние, логи updater
```

- Каталог данных и каталог программы — разные, поэтому замена каталога
  приложения безопасна. В каталоге приложения не должно быть пользовательских
  файлов: всё своё пользователь хранит в `%LOCALAPPDATA%\FamilyMoney`.
- Если пользователь правил `appsettings.json` рядом с exe, при обновлении он
  будет заменён на новый default. Это документируется; настоящие настройки —
  в `%LOCALAPPDATA%`.
- Установка рекомендуется в `%LOCALAPPDATA%\Programs\FamilyMoney` (без прав
  администратора). Из `Program Files` self-update не работает: нет записи в
  каталог и его родителя.
- Проверка возможности обновления:
  - каталог установки и его родитель доступны для записи (пробный файл);
  - диск локальный (не `DriveType.Network`), не CD-ROM;
  - каталог не находится во временной папке (`Path.GetTempPath()`): запуск
    exe прямо из zip во временном каталоге иначе «обновил» бы временную копию;
  - `FamilyMoney.Updater.exe` существует;
  - путь не является корнем диска и не содержит reparse point в последнем
    компоненте.
  Если что-то не так — `CanSelfUpdate = false`, UI предлагает скачать zip
  вручную и открыть папку.

### 6.2 Скачивание и проверка

1. Свободное место: `size * 2 + 100 МБ` (zip + распакованная копия).
2. Файл: `%LOCALAPPDATA%\FamilyMoney\updates\<version>\package.zip`.
3. Докачка: если есть `package.zip.part`, отправляем `Range: bytes=N-`.
   Если сервер ответил `200` вместо `206` (подписанный URL GitHub мог
   измениться), начинаем заново. При `403`/`410` — заново.
4. По завершении: размер == `size` из манифеста, SHA-256 == `sha256`.
   Хеш считается потоком, без чтения всего файла в память.
5. Распаковка в `staging` **рядом с каталогом установки**, на том же томе:
   `<install>.update-<version>`. Это принципиально: `Directory.Move` не работает
   между томами, а `%LOCALAPPDATA%` и `D:\Apps` могут быть на разных дисках.
6. Безопасная распаковка:
   - каждая запись проверяется через `Path.GetFullPath` и префикс staging;
   - запрещены `..`, абсолютные пути, `:` в имени (ADS), символические ссылки;
   - лимит на число записей (10 000) и суммарный распакованный размер
     (например, 1 ГБ), защита от zip-bomb;
   - обязательный `entryPoint` из манифеста должен присутствовать.
7. Диагностическая проверка версии распакованного exe (не обязательная):
   `FileVersionInfo.ProductVersion` может содержать версию, если в сборке
   задан `InformationalVersion`. Для single-file exe это не гарантировано,
   поэтому основной контроль — SHA-256 всего архива; отсутствие версии в
   Win32-ресурсе не должно отменять обновление. Если проверка есть и версия
   явно другая — пакет не ставить, показать ошибку и записать лог.

### 6.3 `FamilyMoney.Updater.exe`

Отдельный проект `src/FamilyMoney.Updater`:

- `net10.0`, `OutputType=Exe`, `RuntimeIdentifier=win-x64`,
  `PublishSingleFile=true`, `SelfContained=false` (приложение и так требует
  .NET 10 Desktop Runtime).
- Не ссылается на Avalonia и на `FamilyMoney.Updates`: план — собственный
  маленький DTO, чтобы updater не тянул лишние зависимости и работал, даже если
  приложение повреждено.
- Публикуется в `build-windows` и копируется в каталог desktop-публикации до
  создания zip.
- В UI не показывается; на фатальную ошибку может показать `MessageBoxW`
  через P/Invoke (без WinForms).

**План обновления** пишется в JSON и передаётся одним аргументом
`--plan <path>`; так не нужно экранировать пробелы и кириллицу в путях:

```json
{
  "pid": 12345,
  "installDir": "D:\\Apps\\FamilyMoney",
  "stagingDir": "D:\\Apps\\FamilyMoney.update-0.261122.42",
  "backupDir": "D:\\Apps\\FamilyMoney.old-20261122T140500",
  "entryPoint": "FamilyMoney.Desktop.exe",
  "arguments": ["--database", "family.db"],
  "ackPath": "C:\\Users\\user\\AppData\\Local\\FamilyMoney\\updates\\ack.json",
  "logPath": "C:\\Users\\user\\AppData\\Local\\FamilyMoney\\updates\\logs\\updater.log",
  "version": "0.261122.42"
}
```

Приложение:

1. Скачивает и проверяет пакет.
2. Копирует `FamilyMoney.Updater.exe` (и его зависимости, если не single-file) в
   `%TEMP%\FamilyMoneyUpdater\<guid>\` — иначе updater окажется внутри каталога,
   который сам же будет переименован.
3. Запускает копию updater с `--plan`, через `ProcessStartInfo.ArgumentList`,
   `UseShellExecute=false`, `CreateNoWindow=true`.
4. Завершает приложение штатно (`desktop.Shutdown()`), чтобы `desktop.Exit`
   сделал бэкап (`AppInit.BackupDatabases`) и закрыл LiteDB. Если бэкап не
   удался — пишет в лог, но обновление не блокирует: данные не изменяются.
5. Если запуск updater не удался — приложение остаётся открытым и показывает
   ошибку.

Updater:

```
1. Проверить план: installDir не корень, stagingDir — sibling installDir,
   все пути полные, entryPoint без separators.
2. Взять named mutex "FamilyMoney.Updater" (два updater одновременно нельзя).
3. Ждать выхода PID до 120 с. PID уже мог выйти — это нормально. PID
   сверяется с именем процесса и временем старта: после переиспользования PID
   updater не должен ждать чужой процесс.
4. Убедиться, что staging существует, installDir существует.
5. backupDir берётся из плана; если занят — добавить суффикс.
6. Directory.Move(installDir, backupDir).
7. Directory.Move(stagingDir, installDir).
   Если 6/7 упали — вернуть backupDir на место, записать ошибку, запустить
   старую версию.
8. Запустить <installDir>\<entryPoint> с сохранёнными arguments,
   WorkingDirectory = installDir.
9. Ждать ack-файл до 60 с. Приложение пишет его после открытия главного окна.
10. ack есть → success: удалить backupDir (если не вышло — оставить, повторить
    при следующем запуске), записать update-result.json.
11. ack нет → kill процесса, Directory.Move(installDir, failedDir),
    Directory.Move(backupDir, installDir), запустить старую версию,
    записать failure.
```

Детали, которые важно не потерять:

- **Same volume**: staging создаётся рядом с install, а не в `%TEMP%` или
  `%LOCALAPPDATA%`. Иначе `Directory.Move` упадёт.
- **Swap journal**: перед шагом 6 updater пишет
  `%LOCALAPPDATA%\FamilyMoney\updates\swap-journal.json` с путями install,
  staging, backup и версией; после успеха удаляет. Если updater упал между
  переименованиями, при следующем запуске приложение видит journal и
  восстанавливает backup (или подсказывает точную команду).
- **Retry**: `Directory.Move` и удаление могут кратковременно падать из-за
  антивируса/Explorer. 50 попыток с паузой 100 мс; после — откат.
- **Path safety**: запрещено `installDir == Path.GetPathRoot(installDir)`,
  иначе updater попытается переименовать весь диск.
- **Reparse points**: если каталог установки — junction/symlink, self-update
  отключается (или путь разрешается и проверяется отдельно).
- **Long paths**: `app.manifest` не объявляет `longPathAware`; для первой
  версии считаем путь установки коротким. Если добавлять — проверить
  `\\?\`-префиксы и manifest.
- **ACL**: после перемещения новый каталог получает наследуемые права
  родителя; специальных действий не нужно.
- **Ярлыки**: путь `installDir\FamilyMoney.Desktop.exe` не меняется, поэтому
  ярлык и `.desktop`-файл (Linux) остаются валидными. Ярлык на старый exe
  внутри backup не создаётся.
- **Аргументы командной строки**: сохраняются и передаются новой версии. Если
  приложение запущено с `--database`, это важно.
- **Уборка**: после успешного старта удаляются старые `*.old-*`, `*.failed-*`
  и `updates\<version>\package.zip`; достаточно оставить одну предыдущую копию
  для ручного отката. Ошибка удаления не фатальна — повторить при следующем
  запуске.
- **Single instance mutex**: приложение отпускает его при выходе; updater не
  пытается захватить mutex, а ждёт PID. Гонка «пользователь запустил второй
  экземпляр во время замены» маловероятна; новая версия стартует после swap.

**Ack-файл**:

```json
{ "version": "0.261122.42", "startedAt": "2026-11-22T14:10:03Z" }
```

Пишется в `%LOCALAPPDATA%\FamilyMoney\updates\ack.json` после того, как главное
окно показано и прошло ~2 с (чтобы поймать падение при инициализации БД).
Updater удаляет старый ack перед запуском.

**Результат**:

```json
{
  "status": "success" | "rolled_back" | "failed",
  "fromVersion": "0.261122.41",
  "toVersion": "0.261122.42",
  "at": "2026-11-22T14:10:05Z",
  "message": "…",
  "backupDir": "…"
}
```

Приложение при старте читает результат и показывает:
- `success` — тихо/«Обновление установлено»;
- `rolled_back` — «Не удалось запустить новую версию, восстановлена предыдущая»
  + путь к логу;
- `failed` — «Обновление не установлено» + действие.

### 6.4 Откат и крайние случаи

| Ситуация | Поведение |
|---|---|
| Нет прав на каталог/родителя | `CanSelfUpdate=false`; кнопка «Скачать zip» и «Открыть папку» |
| Сетевой диск, CD-ROM | self-update отключён |
| Запуск из zip/временной папки | self-update отключён; «Распакуйте архив в постоянную папку» |
| Каталог — корень диска | отключён (защита от переименования диска) |
| Антивирус держит файл | retry, затем откат и сообщение |
| Приложение не вышло за 120 с | updater не трогает файлы, пишет ошибку; старый экземпляр продолжает работать |
| Новая версия не пишет ack за 60 с | kill, откат, запуск старой |
| Откат тоже упал | backup остаётся на диске; сообщение с путями и логом |
| Updater убит между переименованиями | каталог установки может отсутствовать; рядом лежит `*.old-*`; восстановить вручную и посмотреть лог |
| Пользователь закрыл окно во время скачивания | загрузка отменяется; `.part` остаётся для докачки |
| Нет места | ошибка до скачивания, `.part` не создаётся |
| Zip повреждён / хеш не сошёлся | удалить файл, предложить повторить |
| В zip нет entryPoint | считать пакет недействительным |
| Пользователь удалил каталог установки | updater пишет ошибку; приложение не восстановить |
| Обновление во время открытого диалога транзакции | UI блокирует кнопку «Обновить», пока есть модальные окна, либо просит закрыть их |

### 6.5 Ручной режим

Если self-update невозможен, окно обновления предлагает:

1. Скачать `FamilyMoney.Desktop-win-x64.zip` в `Downloads` (с прогрессом и
   проверкой хеша).
2. Открыть папку с файлом.
3. Инструкцию: закрыть приложение, распаковать архив поверх/в новую папку,
   запустить exe. Данные не пострадают.

Это же — путь для первого перехода: у установленных старых версий нет
updater-кода, поэтому первый релиз с механизмом ставится вручную.

## 7. Android: самообновление

### 7.1 Разрешения и манифест

В `src/FamilyMoney.Android/Properties/AndroidManifest.xml` добавить:

```xml
<uses-permission android:name="android.permission.REQUEST_INSTALL_PACKAGES" />
```

Это обычное разрешение, но на Android 8+ пользователь отдельно разрешает
установку из конкретного источника. Приложение не получает его автоматически.

Дополнительно для receiver результата установки:

```xml
<receiver
    android:name=".Services.InstallResultReceiver"
    android:exported="false" />
```

`INTERNET` уже есть. Внешнее хранилище не нужно: APK кладётся в
`GetExternalFilesDir(...)` или `FilesDir`.

### 7.2 Скачивание

Используем системный `DownloadManager`:

- `Request.SetTitle("Обновление FamilyMoney")`,
  `SetDescription("Версия 0.261122.42")`,
  `SetMimeType("application/vnd.android.package-archive")`,
  `SetDestinationInExternalFilesDir(context, Environment.DirectoryDownloads,
  "updates/FamilyMoney-0.261122.42.apk")`,
  `SetAllowedOverMetered(!wifiOnly)`, `SetAllowedOverRoaming(false)`,
  `SetNotificationVisibility(VisibleNotify)`.
- `DownloadManager` переживает сворачивание приложения, показывает прогресс в
  шторке, умеет докачивать после обрыва. На Android 13+ без разрешения
  `POST_NOTIFICATIONS` уведомление может быть скрыто, но загрузка и прогресс в
  самом приложении продолжают работать.
- ID загрузки и версия сохраняются в `state.json`. При следующем запуске
  приложение проверяет статус по ID: `SUCCESSFUL` → проверка и установка;
  `FAILED`/`PAUSED` → предложить повторить; `RUNNING` → показать прогресс.
- Пока приложение открыто, прогресс для UI читается через
  `DownloadManager.Query(id)` раз в 500 мс.
- Завершение ловим динамическим receiver `DownloadManager.ActionDownloadComplete`
  (на Android 13+ регистрировать с `ReceiverFlags.NotExported`), а статус по ID
  проверяем и при следующем запуске — broadcast может не дойти, если процесс
  был убит.
- Если `DownloadManager` недоступен/отключён — fallback на `HttpClient` с
  докачкой и записью в `FilesDir/updates`.
- Wi-Fi only: если активная сеть metered, показываем вопрос «Скачать через
  мобильную сеть?»; по умолчанию ждём Wi-Fi.
- Отмена — `DownloadManager.Remove(id)` и удаление частичного файла; после
  отмены состояние сбрасывается, повторная попытка начинает заново.
- Свободное место: `2 * size + 50 МБ`; проверка через `StatFs`.

После скачивания файл лежит по известному пути (мы сами задали destination),
поэтому `FileProvider` не нужен: `PackageInstaller` читает его потоком.

### 7.3 Проверка APK

До установки проверяем **все** пункты; любой провал — установка отменяется,
файл удаляется, показывается причина.

1. **Размер** файла == `size` из манифеста.
2. **SHA-256** файла == `sha256` из манифеста.
3. **Пакет**: `PackageManager.GetPackageArchiveInfo(path, flags)` →
   `PackageName == "com.familymoney.app"`.
4. **versionCode**: `LongVersionCode` из APK == `versionCode` манифеста и
   `>` текущего `LongVersionCode`. На API 23–27 берём устаревшее
   `PackageInfo.VersionCode` (int), на 28+ — `LongVersionCode`. Это защита от
   downgrade и от расхождения манифеста с файлом.
5. **minSdk**: `ApplicationInfo.MinSdkVersion <= Build.VERSION.SdkInt`.
   Свойство доступно с API 24; на API 23 проверку пропускаем — установщик сам
   откажет, если APK несовместим.
6. **Подпись**: сертификат подписи APK совпадает с сертификатом установленного
   приложения.
   - API 28+: `PackageInfo.SigningInfo.GetApkContentsSigners()`.
   - API 23–27: `PackageInfo.Signatures` (устаревшее, но работает).
   - Сравниваем SHA-256 каждого сертификата; наборы должны совпадать.
   - Лишний подписант в новой APK — отказ: обновление не должно расширять круг
     доверенных ключей незаметно.
7. **ABI** (опционально): в APK есть `lib/arm64-v8a` или `lib/x86_64`;
   проверяем, что хотя бы одна ABI пересекается с `Build.SUPPORTED_ABIS`.
   Это даёт понятную ошибку вместо отказа установщика на 32-битном устройстве.

Проверка подписи — главный барьер: даже если GitHub-релиз подменён, APK с чужим
ключом не установится. Android сам проверит подпись при установке, но наша
проверка даёт понятное сообщение и не запускает системный диалог зря.

### 7.4 Разрешение на установку

- API 26+: `PackageManager.CanRequestPackageInstalls()`.
  Если `false` — объясняем и открываем
  `Settings.ActionManageUnknownAppSources` с
  `Uri.Parse("package:" + PackageName)`.
  На некоторых OEM — fallback на `Settings.ActionSecuritySettings`.
- API 23–25: отдельного per-app разрешения нет, открываем общий экран
  «Неизвестные источники» (`Settings.ActionSecuritySettings`).
- Возврат в приложение — `OnResume`: если разрешение появилось и есть
  отложенная установка, продолжаем её. Если пользователь отказался — статус
  «Установка запрещена», файл можно оставить для повторной попытки.
- `ActivityNotFoundException` при открытии настроек — показываем текстовую
  инструкцию, не падаем.
- `InstallAsync` в этом случае возвращает `PermissionRequired`; UI сохраняет
  «отложенную установку» (путь к APK, версия) и на `OnResume` повторяет
  проверку разрешения и `PackageInstaller`, не заставляя пользователя заново
  скачивать файл.

### 7.5 Установка через `PackageInstaller`

```csharp
var installer = PackageManager.PackageInstaller;
var parameters = new PackageInstaller.SessionParams(
    PackageInstallMode.FullInstall);
parameters.SetAppPackageName(PackageName);
parameters.SetSize(apkSize);

var sessionId = installer.CreateSession(parameters);
using var session = installer.OpenSession(sessionId);
using (var input = File.OpenRead(apkPath))
using (var output = session.OpenWrite("base.apk", 0, apkSize))
{
    input.CopyTo(output);
    session.Fsync(output);
}

var intent = new Intent(context, typeof(InstallResultReceiver));
var flags = PendingIntentFlags.UpdateCurrent;
if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
{
    flags |= PendingIntentFlags.Mutable;
}
var pending = PendingIntent.GetBroadcast(context, sessionId, intent, flags);
session.Commit(pending.IntentSender);
```

Почему `PackageInstaller`, а не `ACTION_VIEW`:

- не нужен `FileProvider` и выдача URI-разрешений;
- не нужны разрешения на внешнее хранилище;
- системный установщик сам показывает подтверждение и прогресс;
- результат приходит в явный receiver, его можно сохранить и показать при
  следующем запуске.

`InstallResultReceiver`:

- `android:exported="false"`, явный `PendingIntent` — системный установщик
  доставит его, даже если процесс приложения был убит (explicit PendingIntent
  даёт системе право доставки). Если конкретная прошивка ведёт себя иначе,
  допустим `exported="true"` с проверкой action: receiver только сохраняет
  статус и не устанавливает APK;
- читает `PackageInstaller.ExtraStatus`:
  - `STATUS_SUCCESS` — сохранить «установлено» в `SharedPreferences`, не
    показывать UI (процесс всё равно будет убит);
  - `STATUS_PENDING_USER_ACTION` — достать `Intent` из
    `Intent.ExtraIntent` и `StartActivity` с `ActivityFlags.NewTask`
    (системный диалог);
  - `STATUS_FAILURE_ABORTED` — пользователь отменил;
  - `STATUS_FAILURE_BLOCKED` — запрещено политикой/Play Protect;
  - `STATUS_FAILURE_INCOMPATIBLE` — несовместимый APK;
  - `STATUS_FAILURE_INVALID` — повреждён;
  - `STATUS_FAILURE_STORAGE` — нет места.
- При ошибке — сохранить код и текст, на следующем запуске показать.

После успешной установки Android убивает процесс. Данные (LiteDB) остаются в
каталоге приложения; обновление пакета их не трогает. При следующем запуске
`versionCode` уже новый, приложение показывает «Что нового» и очищает
`install-result`.

Перед установкой полезно вызвать `IDatabaseBackup.BackupAll()` и записать в лог
результат: это не обязательно для целостности (файлы БД не заменяются), но
даёт запасную копию на случай сбоя питания.

### 7.6 Крайние случаи Android

| Ситуация | Поведение |
|---|---|
| Разрешение на установку не выдано | объяснение + настройки; после возврата продолжить |
| Пользователь отменил системный диалог | статус «Установка отменена», файл можно удалить/оставить |
| Нет места | ошибка до скачивания; удалить частичный файл |
| Скачивание прервалось | `DownloadManager` докачает; при повторном запуске — проверка статуса |
| Хеш не сошёлся | удалить файл, «Файл повреждён, повторите» |
| Подпись не совпала | «Обновление подписано другим ключом»; не устанавливать |
| `versionCode` не больше текущего | «Версия на GitHub не новее установленной» |
| `minSdk` выше устройства | «Обновление не поддерживает эту версию Android» |
| APK для другой ABI | «Обновление не поддерживает процессор устройства» |
| Play Protect блокирует | объяснить, что APK вне Google Play; кнопка «Открыть на GitHub» |
| Приложение убито во время установки | результат в receiver/SharedPreferences; проверить при следующем запуске |
| Пользователь удалил приложение | данные удалены; обновление тут ни при чём |
| Смена ключа подписи | Android откажет; нужна переустановка с потерей данных |

## 8. Безопасность

### 8.1 Модель угроз

| Угроза | Контроль |
|---|---|
| MITM/подмена файла при скачивании | HTTPS, проверка `size` и SHA-256 из манифеста |
| Подмена релиза/манифеста на GitHub | Android: сверка подписи APK с установленной; Windows: TLS + хеш, опционально Authenticode/подпись манифеста |
| Zip-slip / path traversal | безопасная распаковка с проверкой каждого пути |
| Zip-bomb | лимиты на число записей и распакованный размер |
| Downgrade | сравнение версий; Android дополнительно `versionCode` |
| APK с чужим `applicationId`/ключом | проверка package name и cert SHA-256 |
| Кража/подмена локального каталога | вне модели: тот же пользователь уже имеет доступ к данным |
| Компрометация CI-секретов | ротация ключа; подпись APK — единственный доверенный якорь |
| Слепая установка | всегда явное подтверждение пользователя и системный диалог |

### 8.2 Что усиливает защиту

- **Android**: подпись APK обязательна; CI падает без ключа. Сверка с
  установленным сертификатом исключает установку APK, подписанного другим
  ключом, даже если манифест и хеш подменены.
- **Windows**: Authenticode-подпись exe (если появится сертификат) снимает
  часть предупреждений SmartScreen и позволяет проверять издателя. Проверка:
  `WinVerifyTrust` или `X509Certificate.CreateFromSignedFile` + цепочка.
  Сертификат не обязателен для работы механизма, но желателен.
- **Подпись манифеста** (опционально, этап 4): отдельный offline-ключ
  Ed25519/minisign, публичная часть в приложении, `update.json.sig` в релизе.
  Защищает от подмены релиза на GitHub, если у атакующего нет ключа. Добавляет
  операционную сложность; для семейного приложения может быть избыточно.
- **Пиннинг репозитория**: owner/repo зашиты в код, не настраиваются
  пользователем. Нельзя перенаправить обновление на чужой репозиторий.
- **Никаких токенов GitHub в приложении**: только публичный API.

### 8.3 Ограничения

- Хеш в манифесте защищает от повреждения/подмены по дороге, но не от
  компрометации самого релиза. Для Windows это осознанный компромисс первой
  версии.
- Подпись манифеста не защищает от компрометации CI-секрета подписи.
- Пользователь с правами на каталог приложения может подменить файлы; это вне
  модели угроз.

## 9. Надёжность, логирование, диагностика

- Все сетевые операции — с `CancellationToken`, таймаутами и ограничением
  размера ответа. Ошибки не всплывают в UI необработанными исключениями.
- Один `SemaphoreSlim` на проверку/скачивание/установку: параллельные нажатия
  не создают две загрузки.
- Логи (NLog, уже настроен):
  - `update.check.start/finish` — статус, длительность, ETag/304;
  - `update.download.start/finish` — версия, байты, хеш (первые 12 символов);
  - `update.verify` — размер/хеш/подпись/versionCode;
  - `update.install.start/result` — платформа, путь, код результата;
  - `update.updater` — путь к логу updater, код выхода.
- Windows updater пишет свой лог в
  `%LOCALAPPDATA%\FamilyMoney\updates\logs\updater-YYYYMMDD.log`, потому что
  каталог приложения в момент замены может быть недоступен.
- Android: `Log` + NLog; путь `FilesDir/logs`.
- В настройках — «Открыть папку с логами» (Windows) / «Поделиться логом»
  (Android, через `FileProvider` — отдельная задача, можно отложить).

## 10. Тестирование

### 10.1 Юнит-тесты (`tests/FamilyMoney.Tests`)

- `ReleaseVersion.TryParse`: валидные/невалидные даты, `v`-префикс, legacy,
  `0.0.0-dev`, переполнение, сравнение.
- `UpdateManifest` parsing: неизвестная схема, отсутствующая платформа,
  неверный `sha256`, несовпадение `version`/`tag`, огромный JSON.
- `GitHubReleaseClient` на фейковом `HttpMessageHandler`: 200, 304, 403 с
  `reset`, 404, 500, таймаут, битый JSON, отсутствие `update.json`.
- Выбор ассета: правильное имя, несколько ассетов, отсутствие, `digest`.
- Политика проверки: 12 ч, ручная проверка, пропущенная версия, dev-сборка.
- Безопасная распаковка: `../`, абсолютный путь, `C:\`, symlink, лимиты.
- `UpdateStateStore`: повреждённый JSON, атомарная запись (temp + rename).

### 10.2 Интеграционные

- Фейковый GitHub-сервер (локальный `HttpListener` или `HttpMessageHandler`):
  полный цикл «проверка → скачивание → хеш».
- Windows updater: `--self-test` на temp-каталогах — успешный swap, откат при
  отсутствии ack, откат при занятом файле, путь с пробелами/кириллицей,
  staging на другом томе (должен отказать до swap).
- Android: проверка APK-манифеста и подписи — instrumented-тест на эмуляторе
  или хотя бы unit-тест вокруг парсинга `PackageInfo` (сложно; допускается
  ручная матрица).

### 10.3 Ручная матрица

**Windows:**

- Windows 10 22H2 x64, Windows 11 24H2 x64.
- Установка в `C:\Apps\FamilyMoney`, `%LOCALAPPDATA%\Programs\FamilyMoney`,
  путь с пробелами и кириллицей, путь на `D:`.
- `Program Files` (без прав) → ручной режим.
- Обновление при открытом главном окне, при открытом диалоге транзакции, при
  идущей синхронизации.
- Обрыв сети на 50 %, нехватка места, повреждённый zip, неверный хеш.
- Блокировка файла антивирусом, медленный диск.
- Ручное убийство updater, повторный запуск, откат.
- Обновление с версии без updater (ручная установка первого релиза).

**Android:**

- Android 8.0 (API 26), 10 (29), 12 (31), 13 (33), 14 (34), 16 (36).
- arm64-устройство и x86_64-эмулятор; 32-битное устройство (должно получить
  понятный отказ).
- Разрешение «неизвестные источники» выключено/включено, возврат из настроек.
- Wi-Fi и мобильная сеть, «только по Wi-Fi», обрыв загрузки, сворачивание
  приложения, перезапуск во время загрузки.
- APK с чужим ключом (собрать локально другим keystore) → отказ.
- APK с меньшим `versionCode` → отказ.
- Битая подпись/повреждённый APK → отказ.
- Установка поверх текущей версии: данные, настройки, база на месте.
- Play Protect предупреждает — объяснение в UI.
- После установки — «Что нового», корректный `versionName`/`versionCode`.

### 10.4 CI-проверки

- `update.json` валиден (`jq`), хеши совпадают (`sha256sum -c`).
- В Windows-zip есть `FamilyMoney.Desktop.exe` и `FamilyMoney.Updater.exe`.
- APK подписан, `apksigner verify` успешен, `aapt2 dump badging` совпадает с
  ожидаемыми `versionCode`/`versionName`.
- `FamilyMoney.Updater.exe --self-test` на `windows-latest`.
- Тесты `FamilyMoney.Updates` — в job `test` (ubuntu), без Windows-зависимостей.

## 11. План внедрения

### Этап 0. Контракт релиза (без изменений в приложении)

- Job `prepare`, новые теги, `versionCode`.
- Генерация `update.json`, проверки ассетов, обязательная подпись APK.
- `workflow_dispatch` с черновиком для теста.
- Обновить README: как ставится первый релиз с updater.

**Приёмка:** push в `main` даёт релиз с `update.json`; хеши и размеры совпадают;
APK подписан; старый APK не публикуется; повторный запуск не ломает релиз.

### Этап 1. Общая проверка и UI без установки

- `FamilyMoney.Updates`, `BuildInfo`, `GitHubReleaseClient`,
  `UpdateStateStore`, `UpdateService`.
- Карточка «Обновления» в настройках Windows и Android, баннер/диалог.
- Кнопка «Открыть на GitHub», «Пропустить версию».
- Автопроверка при старте, ETag, лимиты.

**Приёмка:** приложение показывает доступную версию и заметки; при отсутствии
сети/лимите — понятный статус; dev-сборка не предлагает установку; тесты
проходят.

### Этап 2. Windows self-update

- `FamilyMoney.Updater`, `WindowsUpdateInstaller`, скачивание/распаковка,
  swap, ack, откат, ручной режим.
- Включение updater в zip, self-test в CI.

**Приёмка:** установленная версия N обновляется до N+1 без участия
пользователя, кроме подтверждения; данные целы; при искусственном падении
новой версии происходит откат; путь с пробелами/кириллицей работает;
`Program Files` уходит в ручной режим.

### Этап 3. Android self-update

- `REQUEST_INSTALL_PACKAGES`, `DownloadManager`, проверка APK,
  `PackageInstaller`, receiver, экран разрешения.
- «Что нового» после установки, очистка старых файлов.

**Приёмка:** установленная версия N обновляется до N+1 через системный
установщик; данные целы; чужой ключ/старый `versionCode`/битый файл
отклоняются; после возврата из настроек установка продолжается.

### Этап 4. Усиление (по желанию)

- Authenticode для Windows.
- Подпись `update.json` (Ed25519/minisign).
- Канал `beta` через prerelease и `releases` с фильтром.
- Linux-обновление (тот же `FamilyMoney.Updates`, другой installer).
- Периодическая проверка раз в 6 ч и «Что нового» на Windows.

## 12. Открытые вопросы

1. **Сертификат Authenticode для Windows** — есть ли возможность получить?
   Без него SmartScreen может предупреждать при первом запуске новой версии.
2. **Куда ставить Windows-приложение** — оставить portable-zip или
   рекомендовать `%LOCALAPPDATA%\Programs\FamilyMoney`? От этого зависит,
   насколько часто будет работать self-update.
3. **Нужен ли канал beta** — если да, релизы-пререлизы и отдельная настройка.
4. **Linux** — включать в тот же манифест и делать установку через
   `install.sh`-раскладку или оставить ручное обновление?
5. **Приватный репозиторий** — если когда-нибудь станет приватным, понадобится
   токен; хранить его в приложении нежелательно. Лучше оставить репозиторий
   публичным.
6. **Интервал автопроверки** — 12 ч выглядит разумным; можно 24 ч, чтобы реже
   беспокоить.

## Приложение A. Контракты C#

```csharp
namespace FamilyMoney.Updates;

public sealed class BuildInfo
{
    public required string Version { get; init; }        // "0.261122.42" или "0.0.0-dev"
    public required string Channel { get; init; }        // "stable" / "dev"
    public string? Commit { get; init; }
    public bool IsDevelopment => Version == "0.0.0-dev" || Channel == "dev";

    public static BuildInfo Current { get; } // из AssemblyMetadata
}

public sealed record ReleaseVersion(int Date, int Build) : IComparable<ReleaseVersion>
{
    public static bool TryParse(string? value, out ReleaseVersion version);
    public int CompareTo(ReleaseVersion? other);
    public override string ToString() => $"0.{Date:D6}.{Build}";
}

public sealed record UpdateAsset(
    string Asset,
    long Size,
    string Sha256,
    int? VersionCode,
    int? MinSdk,
    string? EntryPoint);

public sealed record UpdateManifest(
    int Schema,
    string Channel,
    string Version,
    string Tag,
    IReadOnlyDictionary<string, UpdateAsset> Platforms);

public sealed record UpdateInfo(
    string Version,
    string Tag,
    DateTimeOffset PublishedAt,
    string Notes,
    string ReleaseUrl,
    UpdateAsset Asset);

public enum UpdateCheckStatus
{
    UpdateAvailable,
    UpToDate,
    Skipped,
    Disabled,
    DevelopmentBuild,
    NoReleases,
    NetworkError,
    RateLimited,
    InvalidData,
    Unsupported,
}

public sealed record UpdateCheckResult(
    UpdateCheckStatus Status,
    UpdateInfo? Update,
    string? Message);

public sealed record UpdateProgress(long BytesReceived, long? TotalBytes)
{
    public double? Fraction => TotalBytes is > 0
        ? (double)BytesReceived / TotalBytes.Value
        : null;
}

public enum InstallStatus
{
    Success,
    RestartPending,       // Windows: updater запущен, приложение сейчас выйдет
    PermissionRequired,   // Android: нужно разрешение на установку
    UserActionPending,    // Android: системный диалог уже показан
    Failed,
}

public sealed record InstallResult(InstallStatus Status, string? Message);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(bool userInitiated, CancellationToken ct = default);
    Task<string> DownloadAsync(
        UpdateInfo update,
        IProgress<UpdateProgress> progress,
        CancellationToken ct = default);
    Task<InstallResult> InstallAsync(
        UpdateInfo update,
        string downloadedFile,
        CancellationToken ct = default);
}

public interface IUpdateInstaller
{
    bool CanSelfUpdate { get; }

    /// <summary>Android: текущий versionCode; на Windows null.</summary>
    long? CurrentVersionCode { get; }

    Task<InstallResult> InstallAsync(
        UpdateInfo update,
        string downloadedFile,
        IProgress<UpdateProgress> progress,
        CancellationToken ct = default);
}
```

## Приложение B. Пример `update.json`

Заметки релиза в манифест не дублируются: клиент берёт `body` из ответа
GitHub API. Это позволяет менять текст релиза без пересборки манифеста.

```json
{
  "schema": 1,
  "channel": "stable",
  "version": "0.261123.43",
  "tag": "v0.261123.43",
  "platforms": {
    "windows-x64": {
      "asset": "FamilyMoney.Desktop-win-x64.zip",
      "size": 51234567,
      "sha256": "3f2c1d0e9a4b7c8d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5",
      "entryPoint": "FamilyMoney.Desktop.exe"
    },
    "android": {
      "asset": "FamilyMoney.Android.apk",
      "size": 61234567,
      "sha256": "9a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f9",
      "versionCode": 217608575,
      "minSdk": 23
    }
  }
}
```

## Приложение C. Чек-лист приёмки

- [ ] Push в `main` создаёт релиз `v0.<YYMMDD>.<run>`.
- [ ] В релизе есть `update.json`, zip и подписанный APK; хеши совпадают.
- [ ] `update.json` валиден и содержит `versionCode`, равный APK.
- [ ] Windows-zip содержит `FamilyMoney.Updater.exe`.
- [ ] Старое приложение (без updater) ставится вручную и затем видит обновление.
- [ ] Windows: скачивание, проверка, замена, перезапуск, данные целы.
- [ ] Windows: искусственное падение новой версии → откат и старая версия.
- [ ] Windows: путь с пробелами/кириллицей; `Program Files` → ручной режим.
- [ ] Android: скачивание, проверка подписи, системный установщик, данные целы.
- [ ] Android: отказ при чужом ключе, старом `versionCode`, битом файле.
- [ ] Android: разрешение «неизвестные источники» и возврат из настроек.
- [ ] ETag/304, лимит GitHub, офлайн — понятные статусы.
- [ ] Пропущенная версия не предлагается автоматически.
- [ ] Dev-сборка не устанавливает релиз.
- [ ] Логи содержат версию, статус, хеш и результат установки.
- [ ] README обновлён: первый релиз вручную, дальше — автообновление.
