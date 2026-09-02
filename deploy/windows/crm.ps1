# Керування інсталяцією CRM на комп'ютері з Windows і Docker Desktop.
# Запускається через install.cmd, update.cmd або backup.cmd з цього каталогу.
#
#   crm.ps1 install   перше встановлення або запуск наявної інсталяції
#   crm.ps1 update    оновлення до найновішої версії з резервною копією
#   crm.ps1 backup    резервна копія бази у каталог deploy\backups

param(
    [ValidateSet('install', 'update', 'backup')]
    [string]$Action = 'install'
)

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$DeployDir = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$EnvFile = Join-Path $DeployDir '.env'
$ComposeFiles = @('-f', 'docker-compose.yml', '-f', 'docker-compose.local.yml')
Set-Location $DeployDir

function Write-Step([string]$Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Stop-WithError([string]$Message) {
    Write-Host ''
    Write-Host "Помилка: $Message" -ForegroundColor Red
    Write-Host ''
    Read-Host 'Натисніть Enter, щоб закрити вікно' | Out-Null
    exit 1
}

function Invoke-Compose {
    & docker compose @ComposeFiles @args
    if ($LASTEXITCODE -ne 0) {
        Stop-WithError "Команда 'docker compose $($args -join ' ')' завершилася з помилкою. Текст помилки — вище."
    }
}

function New-Secret {
    $bytes = New-Object byte[] 32
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    return (($bytes | ForEach-Object { $_.ToString('x2') }) -join '')
}

function Set-EnvValue([string]$Content, [string]$Key, [string]$Value) {
    return ($Content -replace "(?m)^$Key=.*$", "$Key=$Value")
}

function Get-EnvValue([string]$Key, [string]$Default) {
    $line = Get-Content $EnvFile | Where-Object { $_ -match "^$Key=" } | Select-Object -First 1
    if ($line) { return $line.Substring($Key.Length + 1).Trim() }
    return $Default
}

# --- Перевірки ---------------------------------------------------------------

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Stop-WithError "Docker Desktop не встановлено. Завантажте його з https://www.docker.com/products/docker-desktop/ і повторіть."
}

cmd /c 'docker info >nul 2>&1'
if ($LASTEXITCODE -ne 0) {
    Stop-WithError "Docker Desktop не запущений. Відкрийте Docker Desktop, дочекайтеся напису 'Engine running' у нижньому лівому куті та повторіть."
}

# --- Дії ---------------------------------------------------------------------

switch ($Action) {

    'install' {
        if (-not (Test-Path $EnvFile)) {
            Write-Step 'Створення конфігурації'
            $content = Get-Content (Join-Path $DeployDir '.env.example') -Raw
            $content = Set-EnvValue $content 'CRM_MODE' 'local'
            $content = Set-EnvValue $content 'DOMAIN' 'localhost'
            $content = Set-EnvValue $content 'ACME_EMAIL' 'local@localhost'
            $content = Set-EnvValue $content 'POSTGRES_PASSWORD' (New-Secret)
            $content = Set-EnvValue $content 'AUTH_SIGNING_KEY' (New-Secret)
            [System.IO.File]::WriteAllText($EnvFile, $content, (New-Object System.Text.UTF8Encoding $false))
        }
        else {
            Write-Step 'Конфігурація вже існує. Запуск наявної інсталяції'
        }

        Write-Step 'Завантаження образів (при першому запуску триває кілька хвилин)'
        Invoke-Compose pull

        Write-Step 'Запуск сервісів'
        Invoke-Compose up -d --wait

        $certFile = Join-Path $DeployDir 'crm-root-ca.crt'
        Invoke-Compose cp 'caddy:/data/caddy/pki/authorities/local/root.crt' $certFile

        Write-Step 'Додавання сертифіката до довірених. Підтвердьте у вікні Windows, що з''явиться'
        & certutil -addstore -user -f Root $certFile | Out-Null

        Write-Host ''
        Write-Host '  CRM запущена. Адреса: https://localhost' -ForegroundColor Green
        Write-Host ''
        Write-Host "  Файл $EnvFile містить секрети. Збережіть його копію в менеджері паролів:"
        Write-Host '  без неї резервну копію бази неможливо відновити.'
        Write-Host ''
        Write-Host '  CRM працює, доки увімкнений цей комп''ютер і запущений Docker Desktop.'
        Write-Host '  Після перезавантаження достатньо знову запустити install.cmd.'
        Write-Host ''

        Start-Process 'https://localhost'
    }

    'update' {
        if (-not (Test-Path $EnvFile)) {
            Stop-WithError 'Інсталяцію не знайдено. Спершу виконайте install.cmd'
        }

        $dbUser = Get-EnvValue 'POSTGRES_USER' 'crm'
        $dbName = Get-EnvValue 'POSTGRES_DB' 'crm'

        Write-Step 'Резервна копія перед оновленням'
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        Invoke-Compose exec -T db sh -c "mkdir -p /backups/pre-update && pg_dump -U $dbUser -d $dbName --clean --if-exists | gzip -1 > /backups/pre-update/crm-$stamp.sql.gz"

        Write-Step 'Завантаження нової версії'
        Invoke-Compose pull

        Write-Step 'Перезапуск; оновлення бази виконується автоматично'
        Invoke-Compose up -d --wait

        Write-Host ''
        Write-Host '  Оновлення завершено. Адреса: https://localhost' -ForegroundColor Green
        Write-Host ''
    }

    'backup' {
        if (-not (Test-Path $EnvFile)) {
            Stop-WithError 'Інсталяцію не знайдено. Спершу виконайте install.cmd'
        }

        $dbUser = Get-EnvValue 'POSTGRES_USER' 'crm'
        $dbName = Get-EnvValue 'POSTGRES_DB' 'crm'
        $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
        $file = "crm-$stamp.sql.gz"
        $outDir = Join-Path $DeployDir 'backups'
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null

        Write-Step 'Створення копії'
        Invoke-Compose exec -T db sh -c "mkdir -p /backups/manual && pg_dump -U $dbUser -d $dbName --clean --if-exists | gzip -1 > /backups/manual/$file"
        Invoke-Compose cp "db:/backups/manual/$file" (Join-Path $outDir $file)

        Write-Host ''
        Write-Host "  Копію збережено: $outDir\$file" -ForegroundColor Green
        Write-Host ''
        Write-Host '  Скопіюйте файл на зовнішній носій або в хмарне сховище разом із'
        Write-Host '  копією файлу .env.'
        Write-Host ''

        Start-Process explorer.exe $outDir
    }
}

Read-Host 'Натисніть Enter, щоб закрити вікно' | Out-Null
