FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json ./
COPY kanban-cli/ ./kanban-cli/
RUN dotnet restore kanban-cli/Kanban.sln
RUN dotnet publish kanban-cli/src/Kanban.Api/Kanban.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5050
EXPOSE 5050
ENTRYPOINT ["dotnet", "Kanban.Api.dll"]