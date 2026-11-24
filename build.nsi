Unicode true
RequestExecutionLevel admin
Name "Biên Tập Bản Đồ Địa Chính"
OutFile "CaiDat_BienTapBDDC_2026.11.24.2.exe"
InstallDir "$PROGRAMFILES32\BienTapBDDC"
InstallDirRegKey HKLM "Software\BienTapBDDC" "ThuMuc"
Icon "app.ico"

VIProductVersion "2026.11.24.2.0"
VIAddVersionKey /LANG=1040 "ProductName" "Biên Tập Bản Đồ Địa Chính"
VIAddVersionKey /LANG=1040 "CompanyName" "Nguyễn Xuân Thủy"
VIAddVersionKey /LANG=1040 "FileDescription" "Công cụ biên tập bản đồ địa chính cho MicroStation V8i"
VIAddVersionKey /LANG=1040 "FileVersion" "2026.11.24.2"
VIAddVersionKey /LANG=1040 "LegalCopyright" "Nguyễn Xuân Thủy"

!include "MUI2.nsh"
!define MUI_ABORTWARNING
!define MUI_ICON "app.ico"
!define MUI_UNICON "app.ico"
!define MUI_WELCOMEPAGE_TITLE "Biên Tập Bản Đồ Địa Chính"
!define MUI_WELCOMEPAGE_TEXT "Cài đặt phiên bản 2026.11.24.2 cho MicroStation V8i.$\r$\n$\r$\nHãy lưu bản vẽ và đóng MicroStation trước khi cài."
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\BienTapBDDC.exe"
!define MUI_FINISHPAGE_RUN_PARAMETERS "/baocao"
!define MUI_FINISHPAGE_RUN_TEXT "Mở báo cáo cài đặt"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_LANGUAGE "Vietnamese"

VIAddVersionKey /LANG=1066 "ProductName" "Biên Tập Bản Đồ Địa Chính"

Function .onInit
  SetRegView 32
FunctionEnd

Section "BienTapBDDC" SecMain
  SetOutPath "$INSTDIR"
  File "BTBCaiDat.exe"
  File "BienTapBDDC.exe"
  File "GoCaiDat.exe"
  File "app.ico"
  File "HUONG-DAN.txt"
  File "thaydoi.txt"
  File /r "src"
  SetOutPath "$INSTDIR\BienTapBDDC"
  File /r "BienTapBDDC\*.*"

  SetOutPath "$INSTDIR"
  FileOpen $0 "$INSTDIR\BienTapBDDC.duongdan" w
  FileWrite $0 "$INSTDIR"
  FileClose $0

  ; BTBCaiDat biên dịch add-in với các DLL Bentley có trên máy người dùng.
  ExecWait '"$INSTDIR\BTBCaiDat.exe" /caidat "$INSTDIR" /ban 2026.11.24.2 /cu "$INSTDIR" /chitiet "$INSTDIR\chitiet.txt"' $1

  SetRegView 32
  WriteRegStr HKLM "Software\BienTapBDDC" "ThuMuc" "$INSTDIR"
  WriteRegStr HKLM "Software\BienTapBDDC" "PhienBan" "2026.11.24.2"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "DisplayName" "Biên Tập Bản Đồ Địa Chính"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "DisplayVersion" "2026.11.24.2"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "Publisher" "Nguyễn Xuân Thủy"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "UninstallString" '"$INSTDIR\GoCaiDat.exe"'
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "QuietUninstallString" '"$INSTDIR\GoCaiDat.exe" /S'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC" "NoRepair" 1

  CreateDirectory "$SMPROGRAMS\Biên Tập BĐĐC"
  CreateShortCut "$DESKTOP\Biên Tập BĐĐC.lnk" "$INSTDIR\BienTapBDDC.exe" "%1" 0
  CreateShortCut "$SMPROGRAMS\Biên Tập BĐĐC\Biên Tập BĐĐC.lnk" "$INSTDIR\BienTapBDDC.exe" "" 0
  CreateShortCut "$SMPROGRAMS\Biên Tập BĐĐC\Cập nhật phiên bản mới.lnk" "$INSTDIR\BienTapBDDC.exe" "/capnhat" 0
  CreateShortCut "$SMPROGRAMS\Biên Tập BĐĐC\Báo cáo cài đặt, cập nhật.lnk" "$INSTDIR\BienTapBDDC.exe" "/baocao" 0
  CreateShortCut "$SMPROGRAMS\Biên Tập BĐĐC\Hướng dẫn sử dụng.lnk" "$INSTDIR\HUONG-DAN.txt" "" 0
  CreateShortCut "$SMPROGRAMS\Biên Tập BĐĐC\Gỡ cài đặt Biên Tập BĐĐC.lnk" "$INSTDIR\GoCaiDat.exe" "" 0

  WriteRegStr HKCR "SystemFileAssociations\.dgn\shell\BienTapBDDC" "" "Mở bằng Biên Tập BĐĐC"
  WriteRegStr HKCR "SystemFileAssociations\.dgn\shell\BienTapBDDC" "Icon" "$INSTDIR\BienTapBDDC.exe,0"
  WriteRegStr HKCR "SystemFileAssociations\.dgn\shell\BienTapBDDC\command" "" '"$INSTDIR\BienTapBDDC.exe" "%1"'
SectionEnd

Section "Uninstall"
  SetRegView 32
  DeleteRegKey HKCR "SystemFileAssociations\.dgn\shell\BienTapBDDC"
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\BienTapBDDC"
  DeleteRegKey HKLM "Software\BienTapBDDC"
  Delete "$DESKTOP\Biên Tập BĐĐC.lnk"
  RMDir /r "$SMPROGRAMS\Biên Tập BĐĐC"
  RMDir /r "$INSTDIR"
SectionEnd
