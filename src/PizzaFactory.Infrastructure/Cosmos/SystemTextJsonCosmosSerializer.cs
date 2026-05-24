using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace PizzaFactory.Infrastructure.Cosmos;

/// <summary>
/// A System.Text.Json serializer for the Cosmos SDK (its default is Newtonsoft). Keeps our
/// documents on STJ with camelCase + string enums, so 'Id' -> "id" and 'PartitionKey' -> "partitionKey".
/// </summary>
public sealed class SystemTextJsonCosmosSerializer(JsonSerializerOptions options) : CosmosSerializer
{
    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.Length == 0)
            {
                return default!;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return JsonSerializer.Deserialize<T>(stream, options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, options);
        stream.Position = 0;
        return stream;
    }
}
