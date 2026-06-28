FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /source

# Copy project files first so dependency restore is cached independently of source changes
COPY Obj2Tiles.sln .
COPY Obj2Tiles/Obj2Tiles.csproj Obj2Tiles/
COPY Obj2Tiles.Library/Obj2Tiles.Library.csproj Obj2Tiles.Library/
COPY MeshDecimatorCore/MeshDecimatorCore.csproj MeshDecimatorCore/
COPY Obj2Gltf/Obj2Gltf.csproj Obj2Gltf/

RUN DOTNET_RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet restore Obj2Tiles/Obj2Tiles.csproj -r $DOTNET_RID

# Copy source and publish as a self-contained single-file binary
COPY Obj2Tiles/ Obj2Tiles/
COPY Obj2Tiles.Library/ Obj2Tiles.Library/
COPY MeshDecimatorCore/ MeshDecimatorCore/
COPY Obj2Gltf/ Obj2Gltf/

RUN DOTNET_RID=$([ "$TARGETARCH" = "arm64" ] && echo "linux-arm64" || echo "linux-x64") && \
    dotnet publish Obj2Tiles/Obj2Tiles.csproj \
    -c Release \
    -r $DOTNET_RID \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishTrimmed=true \
    -p:TrimMode=partial \
    -p:TrimmerRemoveSymbols=true \
    -p:DebuggerSupport=false \
    --no-restore \
    -o /app


# Minimal runtime
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /data
COPY --from=build /app/ /app/

ENTRYPOINT ["/app/Obj2Tiles"]
