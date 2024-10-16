# Base stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000  # Expose port 5000

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Application.csproj", "."]  # Update project name here
RUN dotnet restore "./Application.csproj"  # Update project name here
COPY . . 
WORKDIR "/src/."
RUN dotnet build "./Application.csproj" -c $BUILD_CONFIGURATION -o /app/build  # Update project name here

# Publish stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Application.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false  # Update project name here

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish . 
ENTRYPOINT ["dotnet", "Application.dll"]  # Update DLL name if necessary
