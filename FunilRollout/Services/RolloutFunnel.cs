using System;
using System.Threading.Tasks;
using GitHub;
using StackExchange.Redis;

namespace FunilRollout.Services;

/// <summary>
/// Implementação de funil de rollout para comparação entre API antiga e nova
/// </summary>
public class RolloutFunnel
{
    private readonly RedisConfigProvider _configProvider;
    private readonly Random _random = new();
    
    // Chaves do Redis para configuração
    private const string ROLLOUT_ENABLED_KEY = "rollout:enabled";
    private const string ROLLOUT_PERCENTAGE_KEY = "rollout:percentage";
    private const string ROLLOUT_PUBLISH_RESULTS_KEY = "rollout:publish_results";
    
    public RolloutFunnel(RedisConfigProvider configProvider)
    {
        _configProvider = configProvider;
        
        // Configura o publisher padrão
        Scientist.ResultPublisher = new ConsoleResultPublisher();
    }
    
    /// <summary>
    /// Define um publisher personalizado para os resultados dos experimentos
    /// </summary>
    /// <param name="publisher">Publisher a ser utilizado</param>
    public void SetPublisher(IResultPublisher publisher)
    {
        Scientist.ResultPublisher = new FireAndForgetResultPublisher(publisher);
    }
    
    /// <summary>
    /// Executa um experimento comparando os métodos antigo e novo, 
    /// respeitando a configuração de porcentagem de rollout
    /// </summary>
    /// <typeparam name="T">Tipo de retorno dos métodos</typeparam>
    /// <param name="experimentName">Nome do experimento</param>
    /// <param name="controlFunc">Método de controle (implementação antiga)</param>
    /// <param name="candidateFunc">Método candidato (nova implementação)</param>
    /// <param name="additionalContext">Contexto adicional para logging</param>
    /// <returns>Resultado do método de controle</returns>
    public T Execute<T>(
        string experimentName,
        Func<T> controlFunc, 
        Func<T> candidateFunc,
        object? additionalContext = null)
    {
        bool isEnabled = IsRolloutEnabled();
        int percentage = GetRolloutPercentage();
        bool shouldPublish = ShouldPublishResults();
        
        return Scientist.Science<T>(experimentName, experiment =>
        {
            // Define quando o experimento deve ser executado
            experiment.RunIf(() => isEnabled && ShouldRunExperiment(percentage));
            
            // Configura o publicador
            if (!shouldPublish)
            {
                Scientist.ResultPublisher = new NullResultPublisher();
            }
            
            // Adiciona contexto
            experiment.AddContext("rollout_percentage", percentage);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            if (additionalContext != null)
            {
                experiment.AddContext("additional_data", additionalContext);
            }
            
            // Define o método de controle e o candidato
            experiment.Use(controlFunc);
            experiment.Try(candidateFunc);
        });
    }
    
    /// <summary>
    /// Versão assíncrona para execução do experimento
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        string experimentName,
        Func<Task<T>> controlFunc, 
        Func<Task<T>> candidateFunc,
        object? additionalContext = null)
    {
        bool isEnabled = IsRolloutEnabled();
        int percentage = GetRolloutPercentage();
        bool shouldPublish = ShouldPublishResults();
        
        return await Scientist.ScienceAsync<T>(experimentName, experiment =>
        {
            // Define quando o experimento deve ser executado
            experiment.RunIf(() => isEnabled && ShouldRunExperiment(percentage));
            
            // Configura o publicador
            if (!shouldPublish)
            {
                Scientist.ResultPublisher = new NullResultPublisher();
            }
            
            // Adiciona contexto
            experiment.AddContext("rollout_percentage", percentage);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            if (additionalContext != null)
            {
                experiment.AddContext("additional_data", additionalContext);
            }
            
            // Define o método de controle e o candidato
            experiment.Use(controlFunc);
            experiment.Try(candidateFunc);
        });
    }
    
    /// <summary>
    /// Verifica se o rollout está habilitado
    /// </summary>
    public bool IsRolloutEnabled()
    {
        string enabled = _configProvider.GetConfigValue(ROLLOUT_ENABLED_KEY, "true");
        return bool.TryParse(enabled, out bool result) && result;
    }
    
    /// <summary>
    /// Obtém a porcentagem configurada para o rollout
    /// </summary>
    public int GetRolloutPercentage()
    {
        return _configProvider.GetConfigValueInt(ROLLOUT_PERCENTAGE_KEY, 0);
    }
    
    /// <summary>
    /// Define a porcentagem de tráfego para o rollout
    /// </summary>
    public void SetRolloutPercentage(int percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "O valor deve estar entre 0 e 100");
        }
        
        _configProvider.SetConfigValue(ROLLOUT_PERCENTAGE_KEY, percentage.ToString());
    }
    
    /// <summary>
    /// Habilita ou desabilita o rollout
    /// </summary>
    public void EnableRollout(bool enabled)
    {
        _configProvider.SetConfigValue(ROLLOUT_ENABLED_KEY, enabled.ToString());
    }
    
    /// <summary>
    /// Verifica se os resultados devem ser publicados
    /// </summary>
    private bool ShouldPublishResults()
    {
        string publishResults = _configProvider.GetConfigValue(ROLLOUT_PUBLISH_RESULTS_KEY, "true");
        return bool.TryParse(publishResults, out bool result) && result;
    }
    
    /// <summary>
    /// Decide se o experimento deve ser executado com base na porcentagem configurada
    /// </summary>
    private bool ShouldRunExperiment(int percentage)
    {
        if (percentage <= 0) return false;
        if (percentage >= 100) return true;
        
        return _random.Next(100) < percentage;
    }
}

/// <summary>
/// Publisher que não faz nada com os resultados
/// </summary>
public class NullResultPublisher : IResultPublisher
{
    public Task Publish<T, TClean>(Result<T, TClean> result)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publisher que escreve os resultados no console
/// </summary>
public class ConsoleResultPublisher : IResultPublisher
{
    public Task Publish<T, TClean>(Result<T, TClean> result)
    {
        Console.WriteLine($"Experimento: {result.ExperimentName}");
        Console.WriteLine($"Resultado: {(result.Matched ? "SUCESSO - Valores Correspondentes" : "FALHA - Valores Diferentes")}");
        Console.WriteLine($"Valor de controle: {result.Control.Value}");
        Console.WriteLine($"Duração do controle: {result.Control.Duration.TotalMilliseconds}ms");
        
        foreach (var observation in result.Candidates)
        {
            Console.WriteLine($"Candidato: {observation.Name}");
            Console.WriteLine($"Valor do candidato: {observation.Value}");
            Console.WriteLine($"Duração do candidato: {observation.Duration.TotalMilliseconds}ms");
        }
        
        // Imprime contexto adicional
        foreach (var kvp in result.Contexts)
        {
            Console.WriteLine($"Contexto - {kvp.Key}: {kvp.Value}");
        }
        
        Console.WriteLine("----------------------------------");
        
        return Task.CompletedTask;
    }
} 