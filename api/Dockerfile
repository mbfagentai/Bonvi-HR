# ============================================================
#  Bonvi HR API — production image
#  Ko'p bosqichli build: yakuniy rasmda SDK qolmaydi (~110 MB)
# ============================================================

FROM mcr.microsoft.com/dotnet/sdk:10.0.400-noble AS build
WORKDIR /src

# Avval faqat loyiha fayllari — NuGet qatlami keshlanadi va
# kod o'zgarganda qayta yuklanmaydi
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Bonvi.Domain/*.csproj          src/Bonvi.Domain/
COPY src/Bonvi.Application/*.csproj     src/Bonvi.Application/
COPY src/Bonvi.Infrastructure/*.csproj  src/Bonvi.Infrastructure/
COPY src/Bonvi.Api/*.csproj             src/Bonvi.Api/
RUN dotnet restore src/Bonvi.Api/Bonvi.Api.csproj

# Endi qolgan kod
COPY src/ src/
RUN dotnet publish src/Bonvi.Api/Bonvi.Api.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false

# ------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
WORKDIR /app

# Root'dan ishlamaydi — xavfsizlik talabi
RUN groupadd --system --gid 2000 bonvi \
 && useradd  --system --uid 2000 --gid bonvi bonvi \
 && mkdir -p /data/uploads \
 && chown -R bonvi:bonvi /data

COPY --from=build --chown=bonvi:bonvi /app/publish .

USER bonvi

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1

EXPOSE 8080

# Holat tekshiruvi HTTP orqali: GET /health
# (orkestrator — compose yoki k8s — shu manzilni so'raydi;
#  konteyner ichida curl yo'q, ataylab: kichikroq hujum yuzasi)

ENTRYPOINT ["dotnet", "Bonvi.Api.dll"]
