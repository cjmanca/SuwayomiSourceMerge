FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish SuwayomiSourceMerge/SuwayomiSourceMerge.csproj -c Release -o /app/publish -p:EnableAutoFormat=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        bash \
        ca-certificates \
        fuse3 \
        gosu \
        inotify-tools \
        libcap2-bin \
        mergerfs \
        util-linux \
    && setcap cap_sys_admin+ep /usr/bin/fusermount3 \
    && setcap cap_sys_admin+ep "$(command -v mergerfs)" \
    && getcap /usr/bin/fusermount3 | grep -F "cap_sys_admin=ep" >/dev/null \
    && getcap "$(command -v mergerfs)" | grep -F "cap_sys_admin=ep" >/dev/null \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish/ /app/
COPY docker/entrypoint.sh /usr/local/bin/ssm-entrypoint.sh
RUN chmod +x /usr/local/bin/ssm-entrypoint.sh

VOLUME ["/ssm/config", "/ssm/sources", "/ssm/override", "/ssm/merged", "/ssm/state"]

ENTRYPOINT ["/usr/local/bin/ssm-entrypoint.sh"]
CMD ["dotnet", "/app/SuwayomiSourceMerge.dll"]
