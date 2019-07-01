FROM debian:stretch
ARG BUILD_OUTPUT_DIR
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1

RUN apt-get update && \
    apt-get upgrade -y && \
    apt-get install -y apt-transport-https gnupg wget

RUN wget -O- https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor | apt-key add - && \
    wget https://packages.microsoft.com/config/debian/9/prod.list -O /etc/apt/sources.list.d/microsoft-prod.list && \
    echo "deb http://ftp.us.debian.org/debian testing main contrib non-free" > /etc/apt/sources.list.d/debian-testing.list && \
    # IDK what happened to the echo utility but if I call echo directly it echos "-e" too
    /bin/echo -e "Package: *\nPin: release a=testing\nPin-Priority: -1" > /etc/apt/preferences.d/debian-testing && \
    apt-get update && \
    apt-get install -y xorg libnss3 libxss1 libasound2 unzip dotnet-sdk-2.1 && \
    apt-get -y -t testing install fonts-noto libfreetype6 fontconfig libcairo-gobject2 libcairo2 && \
    rm -r /var/lib/apt/lists/*

COPY entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/*

COPY ${BUILD_OUTPUT_DIR}/ScreenShooter.zip /opt/screenshooter/
RUN unzip /opt/screenshooter/ScreenShooter.zip && \
    rm /opt/screenshooter/ScreenShooter.zip

WORKDIR /var/screenshooter
ENTRYPOINT [ "entrypoint.sh" ]
CMD [ "/usr/bin/dotnet", "/opt/ScreenShooter/ScreenShooter.dll", "--config", "/etc/ScreenShooter/config.toml" ]
