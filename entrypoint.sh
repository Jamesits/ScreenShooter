#!/bin/bash
set -Eeuo pipefail

! cp /etc/ScreenShooter/NLog.config .

exec "$@"
