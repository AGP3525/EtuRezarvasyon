FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["EtuRezervasyon/EtuRezervasyon.csproj", "EtuRezervasyon/"]
RUN dotnet restore "EtuRezervasyon/EtuRezervasyon.csproj"
COPY . .
WORKDIR "/src/EtuRezervasyon"
RUN dotnet build "EtuRezervasyon.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "EtuRezervasyon.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EtuRezervasyon.dll"]
