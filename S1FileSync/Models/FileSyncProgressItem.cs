using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace S1FileSync.Models;

public class FileSyncProgressItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    private const int FileConversionUnit = 1024;
    
    private string _fileName;
    public string FileName
    {
        get => _fileName;
        set => SetField(ref _fileName, value);
    }
    
    private long _fileSize;
    public long FileSize
    {
        get => _fileSize;
        set => SetField(ref _fileSize, value);
    }
    
    public string FileSizeFormatted => FormatFileSize(FileSize);

    private double _syncSpeed;
    public double SyncSpeed
    {
        get => _syncSpeed;
        set => SetField(ref _syncSpeed, value);
    }

    public string SyncSpeedFormatted => $"{FormatFileSize((long)SyncSpeed)}/s";
    
    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetField(ref _progress, value);
    }
    
    private bool _isCompleted;
    public bool IsCompleted
    {
        get => _isCompleted;
        set => SetField(ref _isCompleted, value);
    }
    
    private DateTime _lastSyncTime;
    public DateTime LastSyncTime
    {
        get => _lastSyncTime;
        set => SetField(ref _lastSyncTime, value);
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        
        if (propertyName == nameof(FileSize))
        {
            OnPropertyChanged(nameof(FileSizeFormatted));
        }
        else if (propertyName == nameof(SyncSpeed))
        {
            OnPropertyChanged(nameof(SyncSpeedFormatted));
        }
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        double len = bytes;
        int order = 0;

        while (len >= FileConversionUnit && order < sizes.Length - 1)
        {
            order++;
            len = len / FileConversionUnit;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }

    
}