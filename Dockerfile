# 1. Derleme Aşaması (.NET 8 SDK)
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Proje referanslarını kopyala ve restore et
COPY ["WebAPI/WebAPI.csproj", "WebAPI/"]
COPY ["Business/Business.csproj", "Business/"]
COPY ["DataAccess/DataAccess.csproj", "DataAccess/"]
COPY ["Core/Core.csproj", "Core/"]
RUN dotnet restore "WebAPI/WebAPI.csproj"

# Kalan tüm dosyaları al ve derle
COPY . .
WORKDIR "/src/WebAPI"
RUN dotnet publish "WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

# 2. Çalışma Aşaması (ASP.NET Core Runtime)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Resim/Dosya yükleme klasörünü oluştur
RUN mkdir -p /app/Storage/MedicalUploads

# Render portu
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "WebAPI.dll"]