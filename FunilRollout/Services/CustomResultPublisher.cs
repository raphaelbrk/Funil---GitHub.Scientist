using System;
using System.Text.Json;
using System.Threading.Tasks;
using GitHub;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FunilRollout.Services;

/// <summary>
/// Publisher que armazena os resultados no Redis e também loga em um arquivo
/// </summary>
public class CustomResultPublisher : IResultPublisher
{
    private readonly RedisConfigProvider _redisConfig;
    private readonly ILogger<CustomResultPublisher> _logger;
    
    public CustomResultPublisher(RedisConfigProvider redisConfig, ILogger<CustomResultPublisher> logger)
    {
        _redisConfig = redisConfig;
        _logger = logger;
    }
    
    public async Task Publish<T, TClean>(Result<T, TClean> result)
    {
        try
        {
            // Registra no Redis
            StoreResultInRedis(result);
            
            // Loga resultado
            LogResult(result);
            
            // Também poderia salvar em um banco de dados, enviar para um sistema de monitoramento, etc.
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao publicar resultado do experimento {ExperimentName}", result.ExperimentName);
        }
    }
    
    private void StoreResultInRedis(object result)
    {
        try
        {
            var database = _redisConfig.GetDatabase();
            
            // Serializa o resultado
            string resultJson = JsonSerializer.Serialize(result, new JsonSerializerOptions { 
                WriteIndented = true 
            });
            
            // Define uma chave única para o resultado
            string key = $"experiment:result:{Guid.NewGuid()}";
            
            // Salva no Redis (com expiração de 7 dias)
            database.StringSet(key, resultJson, TimeSpan.FromDays(7));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao armazenar resultado no Redis");
        }
    }
    
    private void LogResult<T, TClean>(Result<T, TClean> result)
    {
        string status = result.Matched ? "SUCESSO" : "FALHA";
        
        _logger.LogInformation(
            "Experimento: {ExperimentName}, Status: {Status}, Controle: {Control}, CandidatoNome: {CandidateName}, CandidatoValor: {CandidateValue}",
            result.ExperimentName,
            status,
            result.Control.Value,
            result.Candidates[0].Name,
            result.Candidates[0].Value
        );
        
        if (!result.Matched)
        {
            _logger.LogWarning(
                "Diferença detectada em {ExperimentName} - Controle: {Control}, Candidato: {Candidate}",
                result.ExperimentName,
                result.Control.Value,
                result.Candidates[0].Value
            );
        }
    }
} 