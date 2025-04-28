using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace FunilRollout.Services;

public class RedisConfigProvider
{
    private readonly IConfiguration _configuration;
    private readonly ConnectionMultiplexer _redis;
    
    public RedisConfigProvider(IConfiguration configuration)
    {
        _configuration = configuration;
        string redisConnectionString = _configuration.GetConnectionString("Redis") ?? "localhost:6379";
        _redis = ConnectionMultiplexer.Connect(redisConnectionString);
    }
    
    public IDatabase GetDatabase()
    {
        return _redis.GetDatabase();
    }
    
    /// <summary>
    /// Obtém um valor de configuração do Redis
    /// </summary>
    /// <param name="key">Chave da configuração</param>
    /// <param name="defaultValue">Valor padrão caso a chave não exista</param>
    /// <returns>Valor da configuração</returns>
    public string GetConfigValue(string key, string defaultValue)
    {
        IDatabase db = GetDatabase();
        string value = db.StringGet(key);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }
    
    /// <summary>
    /// Obtém um valor numérico de configuração do Redis
    /// </summary>
    /// <param name="key">Chave da configuração</param>
    /// <param name="defaultValue">Valor padrão caso a chave não exista</param>
    /// <returns>Valor da configuração</returns>
    public int GetConfigValueInt(string key, int defaultValue)
    {
        string value = GetConfigValue(key, defaultValue.ToString());
        return int.TryParse(value, out int result) ? result : defaultValue;
    }
    
    /// <summary>
    /// Configura um valor no Redis
    /// </summary>
    /// <param name="key">Chave da configuração</param>
    /// <param name="value">Valor a ser configurado</param>
    public void SetConfigValue(string key, string value)
    {
        IDatabase db = GetDatabase();
        db.StringSet(key, value);
    }
} 