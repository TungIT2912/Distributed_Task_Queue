using StackExchange.Redis;

namespace Coordinator
{
    public class RedisFactory
    {
        public static ConnectionMultiplexer Connect(string? connStr = null)
        {
            return ConnectionMultiplexer.Connect(connStr ?? "localhost:6379");
        }
    }
}
