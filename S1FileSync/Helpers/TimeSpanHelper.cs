using System.Text.RegularExpressions;

namespace S1FileSync.Helpers;

public static class TimeSpanHelper
{
    /// <summary>
    /// TimeSpan 형식이 맞는지 확인
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public static bool IsValidTimeSpanFormat(string value)
    {
        var regex = new Regex(@"^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})$");
        return regex.IsMatch(value.Trim());
    }
    
    /// <summary>
    /// 문자열을 TimeSpan으로 변환하는 함수
    /// </summary>
    /// <param name="value">TimeSpan 형식으로 되어있는 문자열</param>
    /// <returns>변환된 TimeSpan</returns>
    /// <exception cref="FormatException">TimeSpan 형식이 아닌 경우 발생</exception>
    public static TimeSpan ParseTimeSpan(string? value)
    {
        if (value != null)
        {
            value = value.Trim();

            var regex = new Regex(@"^(?:(\d+)\.)?(\d{2}):(\d{2}):(\d{2})$");
            var match = regex.Match(value);

            if (!match.Success)
            {
                throw new FormatException("Invalid TimeSpan format. Expected format: [d.]hh:mm:ss");
            }

            int days = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
            int hours = int.Parse(match.Groups[2].Value);
            int minutes = int.Parse(match.Groups[3].Value);
            int seconds = int.Parse(match.Groups[4].Value);

            // 시간이 24시간을 넘어가면 일수로 변환
            if (hours >= 24)
            {
                days += hours / 24;
                hours %= 24;
            }

            return new TimeSpan(days, hours, minutes, seconds);
        }
        
        return TimeSpan.Zero;
    }
    
    /// <summary>
    /// TimeSpan을 문자열로 변환하는 함수
    /// </summary>
    /// <param name="timeSpan">TimeSpan</param>
    /// <returns>변환된 문자열</returns>
    public static string TimeSpanToString(TimeSpan timeSpan)
    {
        return $"{timeSpan.Days}.{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }
}