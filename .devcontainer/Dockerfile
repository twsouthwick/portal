FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

ARG ASPIRE_VERSION=13.5.3

ENV DOTNET_ROOT=/usr/share/dotnet
ENV ASPIRE_ROOT=/opt/aspire
ENV ASPIRE_BIN=${ASPIRE_ROOT}/bin
ENV PATH="${ASPIRE_BIN}:${DOTNET_ROOT}:${PATH}"

COPY global.json /tmp/global.json

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl jq libicu-dev libssl3 zlib1g \
    && sdk_version="$(jq -er '.sdk.version | select(type == "string" and length > 0)' /tmp/global.json)" \
    && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version "${sdk_version}" --install-dir "${DOTNET_ROOT}" \
    && ln -s "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet \
    && dotnet --info \
    && rm -f /tmp/dotnet-install.sh /tmp/global.json \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://aspire.dev/install.sh -o /tmp/install-aspire.sh \
    && bash /tmp/install-aspire.sh --version "${ASPIRE_VERSION}" --install-path "${ASPIRE_BIN}" --skip-path \
    && rm -f /tmp/install-aspire.sh \
    && aspire --version \
    && chown -R vscode:vscode "${ASPIRE_ROOT}"

RUN mkdir -p /home/vscode/.nuget/NuGet /home/vscode/.nuget/packages \
    && chown -R vscode:vscode /home/vscode/.nuget
