FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["Coordinator/Coordinator.csproj", "Coordinator/"]
RUN dotnet restore "Coordinator/Coordinator.csproj"
COPY . .
WORKDIR "/src/Coordinator"
RUN dotnet build "Coordinator.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Coordinator.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Coordinator.dll"]

