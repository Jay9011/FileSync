namespace FileIOHelper;

public interface IIniFileHelper
{
    /// <summary>
    /// ini파일에 값을 쓴다.
    /// </summary>
    /// <param name="section">설정 그룹(섹션)</param>
    /// <param name="key">설정 키</param>
    /// <param name="value">설정 값</param>
    void WriteValue(string section, string key, string value);
    /// <summary>
    /// ini파일에서 값을 읽어온다.
    /// </summary>
    /// <param name="section">설정 그룹(섹션)</param>
    /// <param name="key">설정 키</param>
    /// <returns>설정 값</returns>
    string ReadValue(string section, string key, string defaultValue = "");
    /// <summary>
    /// 파일 위치를 반환한다.
    /// </summary>
    /// <returns></returns>
    string GetFilePath();
}