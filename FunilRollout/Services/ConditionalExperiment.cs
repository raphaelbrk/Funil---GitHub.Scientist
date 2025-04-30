using System;
using System.Threading.Tasks;
using GitHub;

namespace FunilRollout.Services;

/// <summary>
/// Implements conditional experiments using GitHub Scientist
/// </summary>
public class ConditionalExperiment
{
    private readonly Random _random = new();
    
    /// <summary>
    /// Executes an experiment where the control and candidate are determined by a condition
    /// </summary>
    /// <typeparam name="T">Return type of the methods</typeparam>
    /// <param name="experimentName">Base name for the experiment</param>
    /// <param name="trueImplementation">Implementation to use when condition is true</param>
    /// <param name="falseImplementation">Implementation to use when condition is false</param>
    /// <param name="condition">Condition that determines which implementation is control vs candidate</param>
    /// <param name="experimentType">Identifier for which experiment type to run (A or B)</param>
    /// <param name="additionalContext">Additional context for result analysis</param>
    /// <returns>Result of the control method (which depends on the condition)</returns>
    public T ExecuteConditionalExperiment<T>(
        string experimentName,
        Func<T> trueImplementation,
        Func<T> falseImplementation,
        bool condition,
        string experimentType = "A",
        object? additionalContext = null)
    {
        // Determine which experiment to use
        string fullExperimentName = $"{experimentName}_{experimentType}";
        
        return Scientist.Science<T>(fullExperimentName, experiment =>
        {
            // Add common configuration and context
            experiment.AddContext("condition_value", condition);
            experiment.AddContext("experiment_type", experimentType);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            if (additionalContext != null)
            {
                experiment.AddContext("additional_data", additionalContext);
            }
            
            // In any experiment type, use the value matching the condition as control
            // and the opposite value as candidate
            if (condition)
            {
                experiment.Use(trueImplementation);   // Condition = true, use true implementation
                experiment.Try(falseImplementation);  // And test the false implementation
            }
            else
            {
                experiment.Use(falseImplementation);  // Condition = false, use false implementation
                experiment.Try(trueImplementation);   // And test the true implementation
            }
        });
    }
    
    /// <summary>
    /// Asynchronous version of the conditional experiment
    /// </summary>
    public async Task<T> ExecuteConditionalExperimentAsync<T>(
        string experimentName,
        Func<Task<T>> trueImplementation,
        Func<Task<T>> falseImplementation,
        bool condition,
        string experimentType = "A",
        object? additionalContext = null)
    {
        // Determine which experiment to use
        string fullExperimentName = $"{experimentName}_{experimentType}";
        
        return await Scientist.ScienceAsync<T>(fullExperimentName, experiment =>
        {
            // Add common configuration and context
            experiment.AddContext("condition_value", condition);
            experiment.AddContext("experiment_type", experimentType);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            if (additionalContext != null)
            {
                experiment.AddContext("additional_data", additionalContext);
            }
            
            // In any experiment type, use the value matching the condition as control
            // and the opposite value as candidate
            if (condition)
            {
                experiment.Use(trueImplementation);   // Condition = true, use true implementation
                experiment.Try(falseImplementation);  // And test the false implementation
            }
            else
            {
                experiment.Use(falseImplementation);  // Condition = false, use false implementation
                experiment.Try(trueImplementation);   // And test the true implementation
            }
        });
    }
    
    /// <summary>
    /// Executes a rollout experiment where the condition is based on a percentage
    /// </summary>
    public T ExecuteRolloutExperiment<T>(
        string experimentName,
        Func<T> newImplementation,
        Func<T> oldImplementation,
        int rolloutPercentage,
        int userId,
        string experimentType = "A",
        object? additionalContext = null)
    {
        // Create a hash based on the user ID to ensure consistent behavior
        int seed = userId.GetHashCode();
        Random userRandom = new Random(seed);
        
        // Check if the user is within the rollout percentage
        bool isInRolloutGroup = userRandom.Next(100) < rolloutPercentage;
        
        // Add rollout information to context
        var contextWithRollout = new 
        {
            RolloutPercentage = rolloutPercentage,
            UserId = userId,
            IsInRolloutGroup = isInRolloutGroup,
            AdditionalData = additionalContext
        };
        
        return ExecuteConditionalExperiment(
            experimentName,
            trueImplementation: newImplementation,
            falseImplementation: oldImplementation,
            condition: isInRolloutGroup,
            experimentType: experimentType,
            additionalContext: contextWithRollout
        );
    }
    
    /// <summary>
    /// Asynchronous version of the rollout experiment
    /// </summary>
    public Task<T> ExecuteRolloutExperimentAsync<T>(
        string experimentName,
        Func<Task<T>> newImplementation,
        Func<Task<T>> oldImplementation,
        int rolloutPercentage,
        int userId,
        string experimentType = "A",
        object? additionalContext = null)
    {
        // Create a hash based on the user ID to ensure consistent behavior
        int seed = userId.GetHashCode();
        Random userRandom = new Random(seed);
        
        // Check if the user is within the rollout percentage
        bool isInRolloutGroup = userRandom.Next(100) < rolloutPercentage;
        
        // Add rollout information to context
        var contextWithRollout = new 
        {
            RolloutPercentage = rolloutPercentage,
            UserId = userId,
            IsInRolloutGroup = isInRolloutGroup,
            AdditionalData = additionalContext
        };
        
        return ExecuteConditionalExperimentAsync(
            experimentName,
            trueImplementation: newImplementation,
            falseImplementation: oldImplementation,
            condition: isInRolloutGroup,
            experimentType: experimentType,
            additionalContext: contextWithRollout
        );
    }
} 