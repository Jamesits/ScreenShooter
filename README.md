# ScreenShooter

[![Build Status](https://dev.azure.com/nekomimiswitch/General/_apis/build/status/ScreenShooter)](https://dev.azure.com/nekomimiswitch/General/_build/latest?definitionId=26)

An automatic screenshot mailer service.

> Some links have died, but they are still alive;<br>
> Some links are alive, but they have already died.

## Features

Screenshot Actuator:
 * Chrome (`HeadlessChromeActuator`)

Messenging Connector:
 * Telegram (`TelegramBotConnector`)

## Usage

`dotnet ScreenShooter.dll --config path/to/your/config.toml`

```toml
[Actuator]
[[Actuator.HeadlessChromeActuator]]
WindowWidth=1920
WindowHeight=1080
# Wait on initial page load
PageDownloadTimeout=30000
# Wait on every scroll event
PageScrollActionWaitDelay=2000
# Wait after scroll (for lazy loaded images to load)
ExtraDownloadWaitDelay=10000
# How many characters of the page title you want to prepend to the file name
MaxTitlePrependLength=32

[Connector]
[[Connector.TelegramBotConnector]]
ApiKey="your-api-key"
```