# Stage 1: Build React frontend
FROM node:22-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: Build ASP.NET backend and copy frontend dist into wwwroot
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app
COPY backend/Lighthouse.API/ ./Lighthouse.API/
COPY --from=frontend-build /app/frontend/dist ./Lighthouse.API/wwwroot/
WORKDIR /app/Lighthouse.API
RUN dotnet publish -c Release -o /publish

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=backend-build /publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
CMD ["dotnet", "Lighthouse.API.dll"]
