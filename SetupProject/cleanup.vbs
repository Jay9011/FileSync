Option Explicit

Function Main
    Dim fso, folder
    Set fso = CreateObject("Scripting.FileSystemObject")
    
    ' 로그 파일이 있는 폴더 경로
    Dim logPath
    logPath = Session.Property("INSTALLFOLDER")
    
    ' 폴더가 존재하면 삭제
    If fso.FolderExists(logPath) Then
        Set folder = fso.GetFolder(logPath)
        folder.Delete True
    End If
    
    Set fso = Nothing
End Function