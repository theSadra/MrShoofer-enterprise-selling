# Use the SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Set the working directory
WORKDIR /AspnetCoreMvcStarter

# Copy the solution and project files
COPY "src/Asp/mrshoofer organizational.sln" ./
COPY "src/Asp/Application.csproj" ./

# Restore the project dependencies
RUN dotnet restore "mrshoofer organizational.sln"

# Copy the rest of the application code
COPY src/Asp/. ./

# Build the application
RUN dotnet publish -c Release -o out

# Use the runtime image to run the application
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app
COPY --from=build-env /AspnetCoreMvcStarter/out .

# Start the application
ENTRYPOINT ["dotnet", "Application.dll"]
