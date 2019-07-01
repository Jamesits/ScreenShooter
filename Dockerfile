FROM debian:9

RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y apt-transport-https gnupg wget

RUN wget -qO- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | apt-key add \
    && wget -q https://packages.microsoft.com/config/debian/9/prod.list -O /etc/apt/sources.list.d/microsoft-prod.list \
    apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y xorg libnss3 libxss1 libasound2 fonts-wqy-zenhei fonts-emojione dotnet-sdk-2.1 && \
    rm -r /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/*

COPY ${BUILD_OUTPUT_DIR} /opt/screenshooter/

WORKDIR /var/screenshooter
ENTRYPOINT [ "entrypoint.sh" ]
CMD [ "/usr/bin/dotnet", "/opt/ScreenShooter/ScreenShooter.dll", "--config", "/etc/ScreenShooter/config.toml" ]
