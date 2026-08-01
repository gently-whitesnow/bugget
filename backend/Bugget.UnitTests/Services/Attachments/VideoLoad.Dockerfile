FROM mcr.microsoft.com/dotnet/sdk:9.0-noble
RUN apt-get update \
 && apt-get install -y --no-install-recommends ffmpeg \
 && rm -rf /var/lib/apt/lists/*
