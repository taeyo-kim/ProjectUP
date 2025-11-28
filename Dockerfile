# 디버그 컨테이너를 사용자 지정하는 방법과 Visual Studio 이 Dockerfile을 사용하여 더 빠른 디버깅을 위해 이미지를 빌드하는 방법을 알아보려면 https://aka.ms/customizecontainer를 참조하세요.

# 이 스테이지는 VS에서 빠른 모드로 실행할 때 사용됩니다(디버그 구성의 기본값).
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS base
WORKDIR /home/site/wwwroot
RUN apt-get update && \
    apt-get install -y wget unzip curl gnupg locales && \
    # 한글 로케일 설정
    echo "ko_KR.UTF-8 UTF-8" > /etc/locale.gen && \
    locale-gen ko_KR.UTF-8 && \
    # Chrome 설치
    curl -fsSL https://dl-ssl.google.com/linux/linux_signing_key.pub | gpg --dearmor -o /usr/share/keyrings/google-linux-signing-keyring.gpg && \
    echo "deb [arch=amd64 signed-by=/usr/share/keyrings/google-linux-signing-keyring.gpg] http://dl.google.com/linux/chrome/deb/ stable main" > /etc/apt/sources.list.d/google-chrome.list && \
    apt-get update && \
    apt-get install -y google-chrome-stable && \
    # 한글 폰트 설치 (Noto Sans KR)
    apt-get install -y fonts-noto-cjk fonts-noto-cjk-extra fonts-nanum && \
    # ChromeDriver 설치
    wget https://storage.googleapis.com/chrome-for-testing-public/138.0.7204.183/linux64/chromedriver-linux64.zip && \
    unzip chromedriver-linux64.zip && \
    mv chromedriver-linux64/chromedriver /usr/bin/chromedriver && \
    chmod +x /usr/bin/chromedriver && \
    rm chromedriver-linux64.zip && \
    # 폰트 캐시 갱신
    fc-cache -fv && \
    # 불필요한 패키지 정리
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*
EXPOSE 8080
ENV LANG=ko_KR.UTF-8
ENV LC_ALL=ko_KR.UTF-8
ENV LANGUAGE=ko_KR:ko
# 상기 ChromeDriver 설치가 매우 중요함. 특히, 138.0.7204.183 버전을 안 쓰면 버전이 안 맞는다는 에러가 남.
# 배포 시에는 docker push로 먼저 이미지를 배포한 뒤에 Az Funtion 배포를 수행하는 것이 좋음.


# 이 스테이지는 서비스 프로젝트를 빌드하는 데 사용됩니다.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["ProjectUP.csproj", "."]
RUN dotnet restore "./ProjectUP.csproj"
COPY . .
WORKDIR "/src/."
RUN dotnet build "./ProjectUP.csproj" -c $BUILD_CONFIGURATION -o /app/build


# 이 스테이지는 최종 스테이지에 복사할 서비스 프로젝트를 게시하는 데 사용됩니다.
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./ProjectUP.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false


# 이 스테이지는 프로덕션에서 사용되거나 VS에서 일반 모드로 실행할 때 사용됩니다(디버그 구성을 사용하지 않는 경우 기본값).
FROM base AS final
WORKDIR /home/site/wwwroot
COPY --from=publish /app/publish .
ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true 
