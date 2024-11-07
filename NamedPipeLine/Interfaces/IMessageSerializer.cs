namespace NamedPipeLine.Interfaces
{
    public interface IMessageSerializer
    {
        /// <summary>
        /// 메시지를 형식에 맞춰 직렬화
        /// </summary>
        /// <param name="message">메시지</param>
        /// <typeparam name="T">메시지 형식</typeparam>
        /// <returns></returns>
        string Serialize<T>(T message);
        /// <summary>
        /// 직렬화된 메시지를 형식에 맞춰 역직렬화
        /// </summary>
        /// <param name="message">직렬화 된 메시지</param>
        /// <typeparam name="T">메시지 형식</typeparam>
        /// <returns></returns>
        T Deserialize<T>(string message);
    }
}