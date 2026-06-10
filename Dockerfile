# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview-alpine AS build
WORKDIR /src
COPY . .
RUN dotnet restore atompds.slnx
RUN dotnet publish src/atompds/atompds.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-alpine AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 2583
ENV ASPNETCORE_URLS=http://+:2583
ENTRYPOINT ["dotnet", "atompds.dll"]
