# Use the official .NET SDK image for the build environment
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env

# Set the working directory
WORKDIR /AspnetCoreMvcStarter

# Copy the solution and project files into the container
COPY src/Asp/*.sln ./
COPY src/Asp/*.csproj ./

# Restore the project dependencies
RUN dotnet restore

# Copy the rest of the application code
COPY src/Asp/ ./

# Build the application
RUN dotnet publish -c Release -o out

# Use the official ASP.NET runtime image for the production environment
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Set the working directory
WORKDIR /app

# Copy the built application from the previous stage
COPY --from=build-env /AspnetCoreMvcStarter/out .

# Expose the port the app runs on
EXPOSE 80

# Set the entry point for the application
ENTRYPOINT ["dotnet", "YourProjectName.dll"]
