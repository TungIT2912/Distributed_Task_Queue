FROM mcr.microsoft.com/dotnet/runtime:10.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["WorkerNode/WorkerNode.csproj", "WorkerNode/"]
RUN dotnet restore "WorkerNode/WorkerNode.csproj"
COPY . .
WORKDIR "/src/WorkerNode"
RUN dotnet build "WorkerNode.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WorkerNode.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerNode.dll"]

