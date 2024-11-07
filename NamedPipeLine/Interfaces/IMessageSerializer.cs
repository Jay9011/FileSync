namespace NamedPipeLine.Interfaces
{
    public interface IMessageSerializer
    {
        string Serialize<T>(T message);
        T Deserialize<T>(string message);
    }
}