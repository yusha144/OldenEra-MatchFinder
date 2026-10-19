# Разработка Match Finder

## 1. Структура

`source/` содержит плагин и встроенный WAV, `installer/` установку и удаление, `Docs/` пользовательские инструкции. В `development/tests/` находятся автономные проверки, в `development/tools/` генератор звука. `development/artifacts/` содержит результаты сборки и временные файлы, исключённые из Git.

## 2. Сборка и проверки

Нужны Windows, Git, Windows PowerShell 5.1, .NET SDK 8 или новее и совместимая копия игры с BepInEx 6.0.0-be.788. Библиотеки из `BepInEx/interop` создаются при первом запуске игры и используются при сборке. Они не входят в репозиторий.

Команды из корня проекта:

```powershell
dotnet build .\source\MatchFinder.csproj -c Release '-p:GameRoot=<папка игры>'
dotnet run --project .\development\tests\MatchFinder.Tests.csproj -c Release
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\development\tests\Installer.Tests.ps1
```

Плагин собирается для .NET 6, проверки для .NET 8. Проверки мода проверяют условия уведомления, повторы, отмену, срок действия, ошибки и формат WAV. Они не запускают игру и не воспроизводят звук. Проверки установщика используют искусственные папки и проверяют сохранение соседних модов, обновление, откат при ошибке записи, удаление и запуск BAT из исходников.

[Проверка звука в игре](testing.md) выполняется игроком. Автономные проверки не подтверждают слышимость и реальный сетевой поиск.

WAV уже находится в исходниках. Для его повторного создания из корня проекта:

```powershell
python .\development\tools\generate_sound.py
```

## 3. Установочный архив

Сначала зафиксируйте изменения. Сборщик использует выбранный коммит:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\development\Build-Release.ps1 -GameRoot '<папка игры>' -Revision HEAD
```

Сборщик проверяет файлы игры, выполняет проверки, собирает DLL и сверяет версии проекта и Plugin.cs. Зависимости закреплены в `development/release-settings.json`. Параметры `-LoaderArchive` и `-UnityLibrariesArchive` принимают локальные архивы с совпадающими SHA-256.

В `development/artifacts/releases/` создаются `OldenEra-MatchFinder-<версия>.zip`, `.zip.sha256` и `.release.json`. Параметр `-OutputDirectory` меняет папку результата. Существующие файлы не перезаписываются.

ZIP содержит `Install.bat`, `Uninstall.bat`, `_files`, README, LICENSE и Docs. `release-manifest.json` связывает поставляемые файлы с коммитом и SHA-256. `_files/manifest.json` описывает установку. Библиотеки загрузчика и Unity скачиваются установщиком с официальных серверов при необходимости.

Сборщик не устанавливает мод и не запускает игру. Он не создаёт теги и Releases. Поле `gameplayVerified = false` означает отсутствие игровой проверки в этой процедуре.

## 4. Выпуск

Используйте тег `v<версия>` на проверенном коммите и загрузите три выходных файла в Release. После загрузки скачайте ZIP и сравните SHA-256. Существующие теги и опубликованные файлы сохраняются. Видимость репозитория меняется только по отдельному указанию владельца.

[Правила изменений](../.github/CONTRIBUTING.md) · [Справочник](../Docs/general.md) · [Главная](../README.md)
