FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
ARG TARGETARCH
WORKDIR /app
EXPOSE 8080
COPY . ./ 

RUN dotnet publish ./Executable/Executable.csproj \ 
-a $TARGETARCH \ 
-c Release \ 
-o /app/publish \ 
/p:Version=1.5.18 \ 
--self-contained \ 
--use-current-runtime \ 
/p:StripSymbols=true \ 
/p:PublishSingleFile=true 

FROM mcr.microsoft.com/dotnet/runtime-deps:9.0-alpine 

RUN apk add --no-cache tzdata
ENV TZ=Etc/UTC
ENV DOTNET_USE_POLLING_FILE_WATCHER=true
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["./testing"]