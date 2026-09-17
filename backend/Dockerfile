FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MaintainPro.Api/MaintainPro.Api.csproj", "MaintainPro.Api/"]
COPY ["MaintainPro.Application/MaintainPro.Application.csproj", "MaintainPro.Application/"]
COPY ["MaintainPro.Domain/MaintainPro.Domain.csproj", "MaintainPro.Domain/"]
COPY ["MaintainPro.Infrastructure/MaintainPro.Infrastructure.csproj", "MaintainPro.Infrastructure/"]

RUN dotnet restore "MaintainPro.Api/MaintainPro.Api.csproj"

COPY . .

WORKDIR "/src/MaintainPro.Api"
RUN dotnet publish "MaintainPro.Api.csproj" -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:10000
EXPOSE 10000

ENTRYPOINT ["dotnet", "MaintainPro.Api.dll"]
