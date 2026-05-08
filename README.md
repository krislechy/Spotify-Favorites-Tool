# Spotify Relay Overlay

Мини-приложение для Windows: прозрачный topmost-оверлей показывает текущий трек Spotify, статус лайка и позволяет лайкнуть/убрать лайк.

## Запуск

```powershell
dotnet run --project .\SpotifyRelayOverlay\SpotifyRelayOverlay.csproj
```

## Настройка Spotify

1. Открой [Spotify Developer Dashboard](https://developer.spotify.com/dashboard) и создай приложение.
2. В настройках Spotify app добавь Redirect URI:

```text
http://127.0.0.1:53154/callback/
```

3. Скопируй `Client ID`.
4. Запусти оверлей, нажми `⚙`, вставь `Client ID`, нажми `Сохранить`, затем `Войти в Spotify`.

Приложение использует OAuth Authorization Code with PKCE и scopes:

```text
user-read-currently-playing user-read-playback-state user-modify-playback-state user-library-read user-library-modify
```

Если приложение уже было подключено до добавления кнопок управления треками, нажми `Выйти`, затем `Войти в Spotify`, чтобы выдать новый scope `user-modify-playback-state`.

Если при переключении треков или паузе появляется `401`, это значит, что сохраненный токен Spotify больше не подходит для управления playback. Открой `⚙`, нажми `Выйти`, затем снова `Войти в Spotify`.

## Определение клавиш

В репозитории есть мини-консоль `KeyInspector`, которая показывает, какую клавишу нажала Windows. Она слушает low-level keyboard hook и raw input, поэтому полезна для медиа-кнопок, нестандартных кнопок и HID consumer controls.

```powershell
dotnet run --project .\KeyInspector\KeyInspector.csproj
```

Нажми нужную кнопку и смотри строки `HOOK`, `RAWK` и `RAWH`. Для медиа-кнопок чаще всего нужны значения вроде `VK_MEDIA_PLAY_PAUSE`, `VK_MEDIA_NEXT_TRACK`, `VK_VOLUME_UP` или HID usage `0x00CD Play/Pause`. Выход из консоли: `Ctrl+C`.

Если часть клавиш не отображается, попробуй запустить консоль от администратора. Если клавишу полностью перехватывает фирменная программа клавиатуры или она уходит как vendor-specific HID без события клавиатуры/consumer control, Windows может не отдавать ее обычному приложению.

## Управление

- Перетащи оверлей мышью за любую пустую область.
- `♡` / `♥` лайкает или убирает лайк с текущего трека.
- `⏮` / `▶` / `⏸` / `⏭` переключают треки и паузу.
- `Ctrl+Alt+L` лайкает/убирает лайк без клика по оверлею.
- `Ctrl+Alt+Left`, `Ctrl+Alt+Space`, `Ctrl+Alt+Right` управляют предыдущим треком, паузой и следующим треком.
- `Ctrl+Alt+H` скрывает/показывает оверлей.

Управление play/pause/skip через Spotify Web API обычно требует Spotify Premium и активное устройство Spotify.

## Режимы

В настройках есть галочка `Показывать постоянный оверлей`.

- В режиме оверлея показывается постоянное компактное окно. Для него доступен `Безопасный режим для игр`.
- В фоновом режиме постоянный оверлей скрыт, а лайк/анлайк вешается на указанный `VK`-код кнопки из `KeyInspector`.
- Галочка `Показывать всплывающие уведомления` отдельно управляет toast-окном при лайке/анлайке и переключении треков.

Если постоянный оверлей выключен, повторный запуск приложения не создает вторую копию, а открывает настройки уже запущенного экземпляра.

## Трей

Приложение ведет себя как обычное desktop-приложение: пока окно открыто, оно видно на панели задач. Кнопка закрытия не завершает процесс, а прячет окно в системный трей. Двойной клик по значку в трее возвращает окно, правый клик открывает меню `Открыть`, `Настройки`, `Выход`.

## Безопасный режим

В настройках есть переключатель `Безопасный режим для игр`. Он отключает глобальные хоткеи, `Topmost` и повторное принудительное поднятие окна через Win32. Это снижает количество сигналов, которые могут не понравиться античитам, но оверлей может скрываться за полноэкранной игрой.

## Важное ограничение

Оверлей принудительно держится `Topmost` и переустанавливает topmost-режим каждые 2 секунды. Это работает поверх обычных окон и обычно поверх borderless/windowed fullscreen.

Поверх эксклюзивного fullscreen, некоторых DirectX/Vulkan игр и игр с античитом обычное desktop-приложение может не отображаться. Для такого уровня нужны специальные overlay API, Game Bar widget или инжект в графический процесс, что небезопасно и часто нарушает правила игр.

## Ссылки на Spotify API

- [Authorization Code with PKCE](https://developer.spotify.com/documentation/web-api/tutorials/code-pkce-flow)
- [Get Currently Playing Track](https://developer.spotify.com/documentation/web-api/reference/get-the-users-currently-playing-track)
- [Pause Playback](https://developer.spotify.com/documentation/web-api/reference/pause-a-users-playback)
- [Start/Resume Playback](https://developer.spotify.com/documentation/web-api/reference/start-a-users-playback)
- [Skip To Previous](https://developer.spotify.com/documentation/web-api/reference/skip-users-playback-to-previous-track)
- [Skip To Next](https://developer.spotify.com/documentation/web-api/reference/skip-users-playback-to-next-track)
- [Check User's Saved Items](https://developer.spotify.com/documentation/web-api/reference/check-library-contains)
- [Save Items to Library](https://developer.spotify.com/documentation/web-api/reference/save-library-items)
- [Remove Items from Library](https://developer.spotify.com/documentation/web-api/reference/remove-library-items)
