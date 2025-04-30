using System;
using System.Threading.Tasks;
using GitHub;
using StackExchange.Redis;

namespace FunilRollout.Services;

/// <summary>
/// Implementation of rollout funnel for comparison between old and new API
/// </summary>
public class RolloutFunnel
{
    private readonly RedisConfigProvider _configProvider;
    private readonly Random _random = new();
    
    // Redis configuration keys
    private const string ROLLOUT_ENABLED_KEY = "rollout:enabled";
    private const string ROLLOUT_PERCENTAGE_KEY = "rollout:percentage";
    private const string ROLLOUT_PUBLISH_RESULTS_KEY = "rollout:publish_results";
    
    public RolloutFunnel(RedisConfigProvider configProvider)
    {
        _configProvider = configProvider;
        
        // Configure the default publisher
        Scientist.ResultPublisher = new ConsoleResultPublisher();
    }
    
    /// <summary>
    /// Sets a custom publisher for experiment results
    /// </summary>
    /// <param name="publisher">Publisher to be used</param>
    public void SetPublisher(IResultPublisher publisher)
    {
        Scientist.ResultPublisher = new FireAndForgetResultPublisher(publisher);
    }
    
    /// <summary>
    /// Executes an experiment comparing the old and new methods,
    /// respecting the rollout percentage configuration
    /// </summary>
    /// <typeparam name="T">Return type of the methods</typeparam>
    /// <param name="experimentName">Experiment name</param>
    /// <param name="controlFunc">Control method (old implementation)</param>
    /// <param name="candidateFunc">Candidate method (new implementation)</param>
    /// <param name="additionalContext">Additional context for logging</param>
    /// <returns>Result of the control method</returns>
    public T Execute<T>(
        string experimentName,
        Func<T> controlFunc, 
        Func<T> candidateFunc,
        object? additionalContext = null)
    {
        bool isEnabled = IsRolloutEnabled();
        int percentage = GetRolloutPercentage();
        bool shouldPublish = ShouldPublishResults();
        
        // If rollout is disabled or percentage check fails, just execute control function directly
        if (!isEnabled || !ShouldRunExperiment(percentage))
        {
            return controlFunc();
        }
        
        // Otherwise, run the experiment
        return Scientist.Science<T>(experimentName, experiment =>
        {
            // Configure the publisher
            if (!shouldPublish)
            {
                Scientist.ResultPublisher = new NullResultPublisher();
            }
            
            // Add context
            experiment.AddContext("rollout_percentage", percentage);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            if (additionalContext != null)
            {
                experiment.AddContext("additional_data", additionalContext);
            }
            
            // Define the control method and the candidate
            experiment.Use(controlFunc);
            experiment.Try(candidateFunc);
        });
    }
    
    /// <summary>
    /// Asynchronous version for experiment execution
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
        
        // If rollout is disabled or percentage check fails, just execute control function directly
        if (!isEnabled || !ShouldRunExperiment(percentage))
        {
            return await controlFunc();
        }
        
        // Otherwise, run the experiment
        return await Scientist.ScienceAsync<T>(experimentName, experiment =>
        {
            // Configure the publisher
            if (!shouldPublish)
            {
                Scientist.ResultPublisher = new NullResultPublisher();
            }
            
            // Add context
            experiment.AddContext("rollout_percentage", percentage);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            if (additionalContext != null)
            {
                experiment.AddContext("additional_data", additionalContext);
            }
            
            // Define the control method and the candidate
            experiment.Use(controlFunc);
            experiment.Try(candidateFunc);
        });
    }
    
    /// <summary>
    /// Checks if rollout is enabled
    /// </summary>
    public bool IsRolloutEnabled()
    {
        string enabled = _configProvider.GetConfigValue(ROLLOUT_ENABLED_KEY, "true");
        return bool.TryParse(enabled, out bool result) && result;
    }
    
    /// <summary>
    /// Gets the configured percentage for the rollout
    /// </summary>
    public int GetRolloutPercentage()
    {
        return _configProvider.GetConfigValueInt(ROLLOUT_PERCENTAGE_KEY, 0);
    }
    
    /// <summary>
    /// Sets the traffic percentage for the rollout
    /// </summary>
    public void SetRolloutPercentage(int percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Value must be between 0 and 100");
        }
        
        _configProvider.SetConfigValue(ROLLOUT_PERCENTAGE_KEY, percentage.ToString());
    }
    
    /// <summary>
    /// Enables or disables the rollout
    /// </summary>
    public void EnableRollout(bool enabled)
    {
        _configProvider.SetConfigValue(ROLLOUT_ENABLED_KEY, enabled.ToString());
    }
    
    /// <summary>
    /// Checks if results should be published
    /// </summary>
    private bool ShouldPublishResults()
    {
        string publishResults = _configProvider.GetConfigValue(ROLLOUT_PUBLISH_RESULTS_KEY, "true");
        return bool.TryParse(publishResults, out bool result) && result;
    }
    
    /// <summary>
    /// Decides if the experiment should be executed based on the configured percentage
    /// </summary>
    private bool ShouldRunExperiment(int percentage)
    {
        if (percentage <= 0) return false;
        if (percentage >= 100) return true;
        
        return _random.Next(100) < percentage;
    }
}

/// <summary>
/// Publisher that does nothing with the results
/// </summary>
public class NullResultPublisher : IResultPublisher
{
    public Task Publish<T, TClean>(Result<T, TClean> result)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// Publisher that writes the results to the console
/// </summary>
public class ConsoleResultPublisher : IResultPublisher
{
    public Task Publish<T, TClean>(Result<T, TClean> result)
    {
        Console.WriteLine($"Experiment: {result.ExperimentName}");
        Console.WriteLine($"Result: {(result.Matched ? "SUCCESS - Matching Values" : "FAILURE - Different Values")}");
        Console.WriteLine($"Control value: {result.Control.Value}");
        Console.WriteLine($"Control duration: {result.Control.Duration.TotalMilliseconds}ms");
        
        foreach (var observation in result.Candidates)
        {
            Console.WriteLine($"Candidate: {observation.Name}");
            Console.WriteLine($"Candidate value: {observation.Value}");
            Console.WriteLine($"Candidate duration: {observation.Duration.TotalMilliseconds}ms");
        }
        
        // Print additional context
        foreach (var kvp in result.Contexts)
        {
            Console.WriteLine($"Context - {kvp.Key}: {kvp.Value}");
        }
        
        Console.WriteLine("----------------------------------");
        
        return Task.CompletedTask;
    }
} 