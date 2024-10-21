using System.Runtime.InteropServices;
using System.Text;
using FileIOHelper;

namespace FileIOHelper.Helpers
{
    public class IniFileHelper : IIniFileHelper, IFileIOHelper
    {
        private string _filePath;
        
        public IniFileHelper(string filePath)
        {
            _filePath = filePath;
        }
        
        public void WriteValue(string section, string key, string value)
        {
            WritePrivateProfileString(section, key, value, _filePath);
        }
        
        public string ReadValue(string section, string key, string defaultValue = "")
        {
            StringBuilder sb = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, defaultValue, sb, 255, _filePath);
            return sb.ToString();
        }

        public string GetFilePath()
        {
            return _filePath;
        }

        public string ReadAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public void WriteAllText(string path, string contents)
        {
            File.WriteAllText(path, contents);
        }

        public bool FileExists(string path)
        {
            return File.Exists(path);
        }
        
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string fileName);
        
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool WritePrivateProfileString(string section, string key, string val, string fileName);
    }
}
