namespace FileIOHelper;

public interface IFileIOHelper
{
    /// <summary>
    /// 파일 읽기
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>파일 내용</returns>
    string ReadAllText(string path);
    /// <summary>
    /// 파일 쓰기
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <param name="contents">파일 내용</param>
    void WriteAllText(string path, string contents);
    /// <summary>
    /// 파일 존재 여부 확인
    /// </summary>
    /// <param name="path">파일 경로</param>
    /// <returns>존재 여부</returns>
    bool FileExists(string path);
}