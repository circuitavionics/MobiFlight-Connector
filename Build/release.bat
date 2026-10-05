@ECHO OFF
SET TARGET_DIR=%1
SET SOLUTION_DIR=%2
SET RELEASE_DIR=%SOLUTION_DIR%Release
SET BUILD_TOOLS_DIR=%SOLUTION_DIR%Build\
SET CMD_RAR=utils\7z\7z.exe
SET INSTALLER_NAME=MobiFlight-Installer
SET CXL_INSTALLER_NAME=MobiFlight-Installer-CXL

echo -----------------------------------------------------------
echo CLEAN install log file
echo -----------------------------------------------------------
del %TARGET_DIR%\install.log.txt /Q

echo -----------------------------------------------------------
echo CLEAN Release Directory
echo -----------------------------------------------------------
del %RELEASE_DIR%\MobiFlightConnector-release.* /Q
del %RELEASE_DIR%\%INSTALLER_NAME%.exe /Q
del %RELEASE_DIR%\%CXL_INSTALLER_NAME%.exe /Q
echo OK

echo -----------------------------------------------------------
echo REMOVE hubhop json presets from release
echo -----------------------------------------------------------
del %TARGET_DIR%\Presets\msfs2020_hubhop_presets.json /Q
del %TARGET_DIR%\Presets\xplane_hubhop_presets.json /Q
echo OK

echo -----------------------------------------------------------
echo Building MobiFlight-Connector ZIP package
echo -----------------------------------------------------------
%BUILD_TOOLS_DIR%%CMD_RAR% a %RELEASE_DIR%\MobiFlightConnector-release.zip %TARGET_DIR%*.* -r
REM Copy the compiled standard MobiFlight installer into the Release folder
REM and rename the packaged copy to MobiFlight-Installer-CXL.exe.
copy "%SOLUTION_DIR%src\%INSTALLER_NAME%\bin\Release\%INSTALLER_NAME%.exe" "%RELEASE_DIR%\%CXL_INSTALLER_NAME%.exe"
echo OK

echo -----------------------------------------------------------
echo CLEAN build folder
echo -----------------------------------------------------------

echo -----------------------------------------------------------
echo DONE
echo -----------------------------------------------------------