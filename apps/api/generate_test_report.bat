@echo off
setlocal

if exist ".\TestResults" rd /s /q ".\TestResults"

echo Running tests...
dotnet test src\Crm.Tests\Crm.Tests.csproj ^
    --results-directory "./TestResults" ^
    --collect:"XPlat Code Coverage" ^
    -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.ExcludeByFile="**\*.generated.cs;**\*.g.cs"

echo Generating report...
reportgenerator ^
    -reports:"./TestResults/*/coverage.cobertura.xml" ^
    -targetdir:./TestResults/Report ^
    -reporttypes:Html ^
    -filefilters:"-*.generated.cs;-*.g.cs" ^
    -classfilters:"-*Microsoft.AspNetCore.OpenApi*" ^
    -sourcedirs:./src ^
    -verbosity:Error

echo.
echo ========================================================
echo Report generated successfully in: ./TestResults/Report/index.html
echo ========================================================
echo.

pause