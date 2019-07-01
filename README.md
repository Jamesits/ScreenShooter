# ScreenShooter

An automatic web page screenshot service.

> Some links are alive, but they have already died;<br>
> Some links have died, but they are still alive.

## CI and Demo

[![Build Status](https://dev.azure.com/nekomimiswitch/General/_apis/build/status/ScreenShooter)](https://dev.azure.com/nekomimiswitch/General/_build/latest?definitionId=26)
[![](https://images.microbadger.com/badges/version/jamesits/screenshooter.svg)](https://microbadger.com/images/jamesits/screenshooter "Get your own version badge on microbadger.com")
[![](https://images.microbadger.com/badges/image/jamesits/screenshooter.svg)](https://microbadger.com/images/jamesits/screenshooter "Get your own image badge on microbadger.com")

Demo services are not guaranteed to work - they run the latest code from the master branch. Feel free to play with them, but don't take up too much resources.

* Telegram bot demo: [@ScreenShooterDemoBot](https://t.me/ScreenShooterDemoBot)

[The pre-built Docker image](https://hub.docker.com/r/jamesits/screenshooter) is very large as it contains noto fonts.

## Features

Currently supported:

Screenshot Actuator:
 * Chromium (`HeadlessChromeActuator`)

Messenging Connector:
 * Telegram (`TelegramBotConnector`)

## Usage

### Requirements

* Minimal free memory 512MiB (Chrome eats memory, you know)
* dotnet core 2.1
* Manually install [all dependencies of a chromium](https://github.com/Jamesits/ScreenShooter/wiki/Deploy-Example#chrome-runtime)

On machine with <4GiB memory, please set a smaller `ParallelJobs` and use a >2GiB swap.

### Deployment

A complete example of deployment can be found at [wiki/Deploy-Example](https://github.com/Jamesits/ScreenShooter/wiki/Deploy-Example)

Launch: `dotnet ScreenShooter.dll --config path/to/your/config.toml`

Config file example:

```toml
[GlobalConfig]
# max parallel jobs
ParallelJobs=1
# do not retain saved file on the server
RemoveLocalFile=true

[Actuator]
[[Actuator.HeadlessChromeActuator]]
WindowWidth=1920
WindowHeight=1080
# Wait on initial page load
PageDownloadTimeout=30000
# Wait after page load
DocumentLoadDelay=500
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
Administrators = [ userid1, userid2, ... ]
```

Note that you can write multiple `[[Connector.x]]` to get multiple different instances of them.

## Donation

If this project is helpful to you, please consider buying me a coffee.

[![Buy Me A Coffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/Jamesits) or [PayPal](https://paypal.me/Jamesits)