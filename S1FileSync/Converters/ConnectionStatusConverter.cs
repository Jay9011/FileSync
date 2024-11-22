using System.Globalization;
using System.ServiceProcess;
using System.Windows.Data;

namespace S1FileSync.Converters;

public class ConnectionStatusConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isServiceRunning = ((ServiceControllerStatus)values[0] == ServiceControllerStatus.Running);
        string serviceStatusMsg = (string)values[1];
        bool isRemoteConnected = (bool)values[2];
        string remoteStatus = (string)values[3];
        
        if (!isServiceRunning)
        {
            return serviceStatusMsg;
        }
        else if (!isRemoteConnected)
        {
            return remoteStatus;
        }
        
        return "Connected";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// 연결 상태를 확인하는 컨버터
/// </summary>
public class IsConnectionStatusConverter : IMultiValueConverter
{
    /// <summary>
    /// 연결 상태를 확인하는 컨버터
    /// </summary>
    /// <param name="values">
    /// <list type="number">
    ///     <item>서비스 실행 여부</item>
    ///     <item>원격지 연결 상태</item>
    /// </list>
    /// </param>
    /// <param name="targetType"></param>
    /// <param name="parameter"></param>
    /// <param name="culture"></param>
    /// <returns></returns>
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isServiceRunning = ((ServiceControllerStatus)values[0] == ServiceControllerStatus.Running);
        bool isRemoteConnected = (bool)values[1];

        if (!isServiceRunning || !isRemoteConnected)
        {
            return false;
        }

        return true;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
