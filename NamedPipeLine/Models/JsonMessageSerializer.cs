using System;
using System.Text.Json;
using NamedPipeLine.Interfaces;

namespace NamedPipeLine.Models
{
    public class JsonMessageSerializer : IMessageSerializer
    {
        private readonly JsonSerializerOptions _options;
        
        public JsonMessageSerializer(JsonSerializerOptions? options = null)
        {
            _options = options ?? new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }
        
        public string Serialize<T>(T message)
        {
            return JsonSerializer.Serialize(message, _options);
        }

        public T Deserialize<T>(string message)
        {
            return JsonSerializer.Deserialize<T>(message, _options) ?? throw new InvalidOperationException();
        }
    }
}