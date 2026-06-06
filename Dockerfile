# Multi-stage build for the Micro-Burst Exercise Tracker (Blazor Web App + Minimal API).
# Build with the .NET SDK, then ship only the framework-dependent runtime image.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first (cached unless project files change). Copy the solution + csproj layout.
COPY MicroExercise.slnx ./
COPY src/MicroExercise.Core/MicroExercise.Core.csproj          src/MicroExercise.Core/
COPY src/MicroExercise.Infrastructure/MicroExercise.Infrastructure.csproj src/MicroExercise.Infrastructure/
COPY src/MicroExercise.Web/MicroExercise.Web.csproj            src/MicroExercise.Web/
RUN dotnet restore src/MicroExercise.Web/MicroExercise.Web.csproj

# Copy the rest of the sources and publish a Release build.
COPY . .
RUN dotnet publish src/MicroExercise.Web/MicroExercise.Web.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Npgsql probes for the Kerberos/GSSAPI library during connection setup; without it the slim
# runtime image logs a noisy "libgssapi_krb5.so.2: cannot open shared object file" error.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

# TLS terminates at the reverse proxy; the app serves plain HTTP inside the Compose network.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "MicroExercise.Web.dll"]
