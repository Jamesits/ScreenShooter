# ScreenShooter

[![Build Status](https://dev.azure.com/nekomimiswitch/General/_apis/build/status/ScreenShooter)](https://dev.azure.com/nekomimiswitch/General/_build/latest?definitionId=26)

An automatic screenshot service.

> Some links are alive, but they have already died;<br>
> Some links have died, but they are still alive.

## Features

Currently supported:

Screenshot Actuator:
 * Chromium (`HeadlessChromeActuator`)

Messenging Connector:
 * Telegram (`TelegramBotConnector`)

## Usage

Launch: `dotnet ScreenShooter.dll --config path/to/your/config.toml`

Config file example:

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
MaxUploadRetries=3
```

Note that you can write multiple `[[Connector.x]]` to get multiple different instances of them.

## Donation

If this project is helpful to you, please consider buying me a coffee.

[![Buy Me A Coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/Jamesits) or [PayPal](https://paypal.me/Jamesits)