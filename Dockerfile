FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY . .
RUN dotnet publish SuwayomiSourceMerge/SuwayomiSourceMerge.csproj -c Release -o /app/publish -p:EnableAutoFormat=false

FROM mcr.microsoft.com/dotnet/runtime:9.0 AS runtime
WORKDIR /app

ARG TARGETARCH
ARG MERGERFS_VERSION=2.41.1
ARG MERGERFS_SHA256_AMD64=5bdc1df054bb27713c9fe195b97cf80f4850efdd287eef45118cc89fcceb2d27
ARG MERGERFS_SHA256_ARM64=9c4293e5763413c4f99260a5b732e838f82b9af445108907d4595dc50477358f

RUN set -eux; \
    apt-get update; \
    apt-get install -y --no-install-recommends \
        bash \
        ca-certificates \
        curl \
        fuse3 \
        gosu \
        inotify-tools \
        libcap2-bin \
        util-linux; \
    case "${TARGETARCH}" in \
        amd64) mergerfs_arch="${TARGETARCH}"; mergerfs_sha256="${MERGERFS_SHA256_AMD64}" ;; \
        arm64) mergerfs_arch="${TARGETARCH}"; mergerfs_sha256="${MERGERFS_SHA256_ARM64}" ;; \
        *) echo "Unsupported TARGETARCH '${TARGETARCH}' for mergerfs package selection." >&2; exit 1 ;; \
    esac; \
    mergerfs_deb="mergerfs_${MERGERFS_VERSION}.debian-bookworm_${mergerfs_arch}.deb"; \
    curl -fsSL -o "/tmp/${mergerfs_deb}" "https://github.com/trapexit/mergerfs/releases/download/${MERGERFS_VERSION}/${mergerfs_deb}"; \
    echo "${mergerfs_sha256}  /tmp/${mergerfs_deb}" | sha256sum -c -; \
    apt-get install -y --no-install-recommends "/tmp/${mergerfs_deb}"; \
    rm -f "/tmp/${mergerfs_deb}"; \
    dpkg-query -W -f='${Version}' mergerfs | grep -F "${MERGERFS_VERSION}" >/dev/null; \
    setcap cap_sys_admin+ep /usr/bin/fusermount3; \
    setcap cap_sys_admin+ep "$(command -v mergerfs)"; \
    getcap /usr/bin/fusermount3 | grep -F "cap_sys_admin=ep" >/dev/null; \
    getcap "$(command -v mergerfs)" | grep -F "cap_sys_admin=ep" >/dev/null; \
    apt-get purge -y --auto-remove curl; \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish/ /app/
COPY docker/entrypoint.sh /usr/local/bin/ssm-entrypoint.sh
RUN chmod +x /usr/local/bin/ssm-entrypoint.sh

VOLUME ["/ssm/config", "/ssm/sources", "/ssm/override", "/ssm/merged", "/ssm/state"]

ENTRYPOINT ["/usr/local/bin/ssm-entrypoint.sh"]
CMD ["dotnet", "/app/SuwayomiSourceMerge.dll"]
