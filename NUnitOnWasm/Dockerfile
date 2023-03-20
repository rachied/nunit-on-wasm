FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["NUnitOnWasm/NUnitOnWasm.csproj", "NUnitOnWasm/"]
RUN dotnet restore "NUnitOnWasm/NUnitOnWasm.csproj"
COPY . .
WORKDIR "/src/NUnitOnWasm"
RUN dotnet build "NUnitOnWasm.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "NUnitOnWasm.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "NUnitOnWasm.dll"]
