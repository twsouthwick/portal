FROM mcr.microsoft.com/devcontainers/base:ubuntu-24.04

ARG ASPIRE_VERSION=17.0.0

ENV DOTNET_ROOT=/usr/share/dotnet
ENV ASPIRE_ROOT=/opt/aspire
ENV ASPIRE_BIN=${ASPIRE_ROOT}/bin
ENV PATH="${ASPIRE_BIN}:${DOTNET_ROOT}:${PATH}"

COPY global.json /tmp/global.json

RUN apt-get update \
    && apt-get install -y --no-install-recommends ca-certificates curl jq libicu-dev libssl3 zlib1g gnupg lsb-release \
    && sdk_version="$(jq -er '.sdk.version | select(type == "string" and length > 0)' /tmp/global.json)" \
    && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --version "${sdk_version}" --install-dir "${DOTNET_ROOT}" \
    && ln -s "${DOTNET_ROOT}/dotnet" /usr/local/bin/dotnet \
    && dotnet --info \
    && rm -f /tmp/dotnet-install.sh /tmp/global.json \
    && rm -rf /var/lib/apt/lists/*

RUN curl -fsSL https://get.helm.sh/helm-v3.15.4-linux-amd64.tar.gz -o /tmp/helm.tar.gz \
    && tar -C /tmp -xzf /tmp/helm.tar.gz \
    && install /tmp/linux-amd64/helm /usr/local/bin/helm \
    && rm -rf /tmp/helm.tar.gz /tmp/linux-amd64 \
    && curl -fsSL "https://dl.k8s.io/release/$(curl -fsSL https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl" -o /usr/local/bin/kubectl \
    && chmod +x /usr/local/bin/kubectl \
    && curl -fsSL https://kind.sigs.k8s.io/dl/v0.24.0/kind-linux-amd64 -o /usr/local/bin/kind \
    && chmod +x /usr/local/bin/kind \
    && helm version --short \
    && kubectl version --client --output=yaml \
    && kind --version

RUN dotnet tool install --tool-path "${ASPIRE_BIN}" aspire.cli --version "${ASPIRE_VERSION}" \
    && aspire --version \
    && chown -R vscode:vscode "${ASPIRE_ROOT}"

RUN mkdir -p /home/vscode/.nuget/NuGet /home/vscode/.nuget/packages \
    && chown -R vscode:vscode /home/vscode/.nuget
