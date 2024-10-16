# Use the .NET 8 SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /AspnetCoreMvcStarter

# Copy the solution file and project files into the container
COPY src/Asp/*.sln ./
COPY src/Asp/ ./

# Specify the solution file for restoring dependencies
RUN dotnet restore AspnetCoreMvcStarter.sln

# Build the project
RUN dotnet publish -c Release -o out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /AspnetCoreMvcStarter

# Copy the build output from the build environment
COPY --from=build-env /AspnetCoreMvcStarter/out .

# Expose the necessary port (adjust to your app's port if needed)
EXPOSE 5050

# Set the entry point for the container
ENTRYPOINT ["dotnet", "AspnetCoreMvcStarter.dll"]

# Optional: Display a message when the container is built
RUN echo "-----------------------------------------------"
RUN echo " Application is running at http://localhost:5050"
RUN echo "-----------------------------------------------"
