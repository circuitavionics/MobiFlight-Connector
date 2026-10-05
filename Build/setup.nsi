# define name of installer
Unicode True
!define APPNAME "MobiFlight Connector"
OutFile "Circuit-Avionics-MobiFlight-Installer-20260821.exe"

!include LogicLib.nsh
!include MUI2.nsh
# for creating new pages
!include "nsDialogs.nsh"
 
# define installation directory
InstallDir "$LOCALAPPDATA\MobiFlight\${APPNAME}"
 
# For removing Start Menu shortcut in Windows 7
RequestExecutionLevel user

; The icon in the top right corner of the installer windows and the desktop icon.
!define MUI_ICON "..\mobiflight.ico"
; The icon for the installer in the directory, not in Add or Remove Programs.
!define MUI_UNICON "..\mobiflight.ico"
!define MUI_ABORTWARNING
; !insertmacro MUI_PAGE_DIRECTORY
!define MUI_WELCOMEFINISHPAGE_BITMAP "setup-welcome-cxl.bmp"
installbuttontext "Proceed"
MiscButtonText "< Back" "I understand" "Cancel" "Close"
!define MUI_WELCOMEPAGE_TITLE "Circuit Avionics MobiFlight Installer"
!define MUI_WELCOMEPAGE_TEXT "This setup will install MobiFlight Connector with the required settings for Circuit Avionics customers.$\r$\n\
$\r$\n\
If you ever need to reinstall MobiFlight, please download this installer from the Circuit Avionics website.$\r$\n\
This ensures the required settings are applied and prevents unwanted interaction with third-party hardware.$\r$\n"

!insertmacro MUI_PAGE_WELCOME
Page custom CXLUpdatesPage
!insertmacro MUI_PAGE_INSTFILES

!define MUI_FINISHPAGE_RUN "$INSTDIR\MFConnector.exe"
!define MUI_FINISHPAGE_SHOWREADME
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Create Desktop Shortcut"
!define MUI_FINISHPAGE_SHOWREADME_FUNCTION CreateDesktopOnFinish

!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Name "${APPNAME}"
Icon "..\mobiflight.ico"
UninstallIcon "..\mobiflight.ico"

Function CreateDesktopOnFinish
CreateShortcut "$DESKTOP\${APPNAME}.lnk" "$INSTDIR\MFConnector.exe"
FunctionEnd
 

Function .onInit
    System::Call 'kernel32::OpenMutex(i 0x100000, i 0, t "{57699317-1D72-4B54-82BC-CF6B38254550}")p.R0'
    IntPtrCmp $R0 0 notRunning
        System::Call 'kernel32::CloseHandle(p $R0)'
        MessageBox MB_OK|MB_ICONEXCLAMATION "${APPNAME} is running. Please close it first" /SD IDOK
        Abort
    notRunning:
FunctionEnd

Function CXLUpdatesPage

    !insertmacro MUI_HEADER_TEXT "Software and Firmware Updates" "Important information about future updates"

    nsDialogs::Create 1018
    Pop $0

    ${If} $0 == error
        Abort
    ${EndIf}

    ${NSD_CreateLabel} 0 0 100% 120u "MobiFlight may occasionally offer updates to either:$\r$\n$\r$\n\
- the MobiFlight software on your PC, or$\r$\n\
- the firmware installed on your Circuit Avionics device.$\r$\n$\r$\n\
These updates are optional, and your hardware will continue to operate normally without them.$\r$\n$\r$\n\
Software updates to MobiFlight can be installed normally.$\r$\n$\r$\n\
If you choose to install a firmware update, please connect the Circuit Avionics device directly to a USB port on the PC before proceeding. Do not perform firmware updates through a USB hub, adapter, or extension device."

    Pop $0

    nsDialogs::Show

FunctionEnd

# start default section
Section "install"
    SetRegView 64
    # set the installation directory as the destination for the following actions
    SetOutPath $INSTDIR

    # Add the MobiFlight installer
    File "..\Release\MobiFlight-Installer-CXL.exe"
    File "..\mobiflight.ico"
 
    # create the uninstaller
    WriteUninstaller "$INSTDIR\uninstall.exe"

    ExecWait '"$INSTDIR\MobiFlight-Installer-CXL.exe" /installOnly'
 
    # Register file associations for .mfproj and .mcc files
    Call RegisterFileAssociations
 
    # create a shortcut named "new shortcut" in the start menu programs directory
    # point the new shortcut at the program uninstaller
    CreateShortcut "$SMPROGRAMS\${APPNAME}.lnk" "$INSTDIR\MFConnector.exe"

    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "Publisher" "MobiFlight"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayName" "${APPNAME}"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "DisplayIcon" "$instdir\mobiflight.ico"
    WriteRegStr HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "UninstallString" "$\"$INSTDIR\uninstall.exe$\""
    WriteRegDWORD HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "NoModify" 1
	WriteRegDWORD HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "NoRepair" 1
    WriteRegDWORD HKCU "SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}" "EstimatedSize" 45000
SectionEnd

# Register file associations for .mfproj and .mcc files
Function RegisterFileAssociations
    SetRegView 64
    
    # Register .mfproj file type
    WriteRegStr HKCU "Software\Classes\.mfproj" "" "MobiFlight.Project"
    WriteRegStr HKCU "Software\Classes\.mfproj" "Content Type" "application/x-mfproj"
    
    # Register .mcc file type (legacy format)
    WriteRegStr HKCU "Software\Classes\.mcc" "" "MobiFlight.Project"
    WriteRegStr HKCU "Software\Classes\.mcc" "Content Type" "application/x-mcc"
    
    # Configure MobiFlight.Project class
    WriteRegStr HKCU "Software\Classes\MobiFlight.Project" "" "MobiFlight Project"
    WriteRegStr HKCU "Software\Classes\MobiFlight.Project\DefaultIcon" "" "$INSTDIR\mobiflight.ico"
    WriteRegStr HKCU "Software\Classes\MobiFlight.Project\shell\open\command" "" "$\"$INSTDIR\MFConnector.exe$\" /cfg $\"%1$\""
    
    # Notify Windows of file association changes
    System::Call 'Shell32::SHChangeNotify(i 0x8000000, i 0, i 0, i 0)'
FunctionEnd

Function un.onInit
    System::Call 'kernel32::OpenMutex(i 0x100000, i 0, t "{57699317-1D72-4B54-82BC-CF6B38254550}")p.R0'
    IntPtrCmp $R0 0 notRunning
        System::Call 'kernel32::CloseHandle(p $R0)'
        MessageBox MB_OK|MB_ICONEXCLAMATION "${APPNAME} is running. Please close it first" /SD IDOK
        Abort
    notRunning:
FunctionEnd
# uninstaller section start
Section "uninstall"
    SetRegView 64
    
    # Remove the link from the start menu
    Delete "$SMPROGRAMS\${APPNAME}.lnk"

    Delete "$DESKTOP\${APPNAME}.lnk"
 
    # Delete the uninstall
    Delete $INSTDIR\uninstall.exe
 
    # Delete all files from the installation folder
    RMDir /r $INSTDIR

    # Delete all setting files
    RMDir /r $LOCALAPPDATA\MobiFlight

    DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APPNAME}"
    
    # Remove file associations
    DeleteRegKey HKCU "Software\Classes\.mfproj"
    DeleteRegKey HKCU "Software\Classes\.mcc"
    DeleteRegKey HKCU "Software\Classes\MobiFlight.Project"
    
    # Notify Windows of file association changes
    System::Call 'Shell32::SHChangeNotify(i 0x8000000, i 0, i 0, i 0)'
# uninstaller section end
SectionEnd
