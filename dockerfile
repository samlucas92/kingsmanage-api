FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY KingsManage.slnx ./
COPY src/KingsManage/KingsManage.csproj src/KingsManage/
COPY src/KingsManage.Mongo/KingsManage.Mongo.csproj src/KingsManage.Mongo/
COPY src/KingsManage.Web/KingsManage.Web.csproj src/KingsManage.Web/

RUN dotnet restore src/KingsManage.Web/KingsManage.Web.csproj

COPY . .

RUN dotnet publish src/KingsManage.Web/KingsManage.Web.csproj \
	-c Release \
	-o /app/publish \
	--no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "KingsManage.Web.dll"]