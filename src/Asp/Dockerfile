# Use the .NET 8 SDK image for building the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /AspnetCoreMvcStarter

# Copy the .sln file and restore dependencies
COPY *.sln ./
COPY . ./
RUN dotnet restore

# Build the project
RUN dotnet publish -c Release -o out

# Build the runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /AspnetCoreMvcStarter

# Copy the build output from the build environment
COPY --from=build-env /AspnetCoreMvcStarter/out .

# Expose port 5050 (or whatever port your app listens on)
EXPOSE 5050

# Set the entry point for the container
ENTRYPOINT ["dotnet", "AspnetCoreMvcStarter.dll"]

# Optional: Display a message when the container is built
RUN echo "-----------------------------------------------"
RUN echo " Application is running at http://localhost:5050"
RUN echo "-----------------------------------------------"
