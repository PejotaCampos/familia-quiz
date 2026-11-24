# Build from the repository root (the server needs the Shared project):
#   docker build -f src/FamiliaQuiz.Server/Dockerfile -t familiaquiz-server .
# On Render: Dockerfile path = src/FamiliaQuiz.Server/Dockerfile, Docker context = repo root.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY src/FamiliaQuiz.Shared/FamiliaQuiz.Shared.csproj src/FamiliaQuiz.Shared/
COPY src/FamiliaQuiz.Server/FamiliaQuiz.Server.csproj src/FamiliaQuiz.Server/
RUN dotnet restore src/FamiliaQuiz.Server/FamiliaQuiz.Server.csproj
COPY src/FamiliaQuiz.Shared/ src/FamiliaQuiz.Shared/
COPY src/FamiliaQuiz.Server/ src/FamiliaQuiz.Server/
RUN dotnet publish src/FamiliaQuiz.Server/FamiliaQuiz.Server.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
# Render injects $PORT (Program.cs honours it); 8080 is the fallback.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "FamiliaQuiz.Server.dll"]
