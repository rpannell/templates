# syntax=docker/dockerfile:1
# check=error=true
# Build, test and publish in the SDK image. A failing test (including the architecture tests) fails the image build.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached until a project file changes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props Company.Product.slnx ./
COPY src/Company.Product.Api/Company.Product.Api.csproj src/Company.Product.Api/
COPY tests/Company.Product.Api.Tests/Company.Product.Api.Tests.csproj tests/Company.Product.Api.Tests/
RUN dotnet restore Company.Product.slnx

COPY . .
RUN dotnet build Company.Product.slnx -c Release --no-restore
RUN dotnet test Company.Product.slnx -c Release --no-build
RUN dotnet publish src/Company.Product.Api/Company.Product.Api.csproj -c Release --no-build -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
# Numeric non-root user (the "app" user in .NET images) so Kubernetes runAsNonRoot can verify it.
USER 1654
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Company.Product.Api.dll"]
