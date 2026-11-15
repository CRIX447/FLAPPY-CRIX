; Flappy Crix installer extras.
;
; Before installing, remove the OLD Electron version of the app (2.x) if it is on this PC, so
; players end up with one Flappy Crix, not two. Players on 2.x are updated to this app by their
; old app's own updater, which runs this installer silently.
; Only the old app's program files are removed. Nothing of the player's is touched.

!define CRIX_OLD_GUID "da497a7e-606a-5711-a989-1f4407a8879c"

!macro CRIX_REMOVE_OLD DIR
  ${If} ${FileExists} "${DIR}\Uninstall Flappy Crix.exe"
    DetailPrint "Removing the old Flappy Crix app..."
    ; _?= runs the old uninstaller in place and waits for it to finish
    ExecWait '"${DIR}\Uninstall Flappy Crix.exe" /S /currentuser _?=${DIR}'
    Delete "${DIR}\Uninstall Flappy Crix.exe"
    RMDir "${DIR}"
  ${EndIf}
!macroend

!macro NSIS_HOOK_PREINSTALL
  ; where the old app said it was installed (for this user)
  ReadRegStr $R7 HKCU "Software\${CRIX_OLD_GUID}" "InstallLocation"
  ${If} $R7 != ""
    !insertmacro CRIX_REMOVE_OLD "$R7"
  ${EndIf}
  ReadRegStr $R7 HKCU "Software\{${CRIX_OLD_GUID}}" "InstallLocation"
  ${If} $R7 != ""
    !insertmacro CRIX_REMOVE_OLD "$R7"
  ${EndIf}
  ; and its usual place, in case the registry entry is missing
  !insertmacro CRIX_REMOVE_OLD "$LOCALAPPDATA\Programs\flappy-crix"
!macroend
