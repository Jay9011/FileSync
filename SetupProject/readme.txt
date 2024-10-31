1. S1FileSync와 S1FileSyncService 프로젝트를 게시(Release로)하여 각 폴더의 "bin\Release\publish\"에 소스가 존재해야 한다.

2. PowerShell로 현재 폴더의 "GenerateCommonFolder.ps1"를 실행시켜 "Combined"가 자동으로 생성되게 하고 "GeneratedFiles\CombinedFiles.wxs"를 생성되게 한다. (자동 생성 됨)

3. SetupProject를 Release로 빌드하면, 중복 ID가 존재한다고 에러를 띄우는데, 이 중복 ID를 "GeneratedFiles\CombinedFiles.wxs"의 "Component"와 "ComponentRef"에서 삭제하고 진행하면 된다.