using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitHub;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Linq;

namespace FunilRollout.Services;

/// <summary>
/// Advanced implementation that integrates RolloutFunnel with custom validations
/// within a unified progressive rollout flow
/// </summary>
public class AdvancedFunnel
{
    private readonly RolloutFunnel _rolloutFunnel;
    private readonly RedisConfigProvider _redisConfig;
    private readonly ILogger<AdvancedFunnel> _logger;

    // Redis configuration keys
    private const string ACTIVE_CONFIG_KEY = "rollout:config_active";
    private const string USER_CRITERIA_KEY = "rollout:user_criteria";
    private const string MULTIPLE_CRITERIA_KEY = "rollout:multiple_criteria";
    private const string CPF_LIST_KEY = "rollout:cpf_list";

    public AdvancedFunnel(
        RolloutFunnel rolloutFunnel,
        RedisConfigProvider redisConfig,
        ILogger<AdvancedFunnel> logger)
    {
        _rolloutFunnel = rolloutFunnel;
        _redisConfig = redisConfig;
        _logger = logger;
    }

    /// <summary>
    /// MAIN METHOD: Executes the operation using Scientist.net integrated with custom criteria
    /// </summary>
    /// <typeparam name="T">Return type</typeparam>
    /// <param name="experimentName">Experiment name</param>
    /// <param name="validationParameters">Parameters to validate eligibility</param>
    /// <param name="controlFunc">Old method (control)</param>
    /// <param name="candidateFunc">New method (candidate)</param>
    /// <returns>Result of the control method execution</returns>
    public T ExecuteWithValidation<T>(
        string experimentName,
        ValidationParameters validationParameters,
        Func<T> controlFunc,
        Func<T> candidateFunc)
    {
        // Check if the user is eligible with multiple criteria
        // before passing to RolloutFunnel
        bool userEligible = ValidateCompleteEligibility(validationParameters);
        
        // If not eligible, return the result of the old method without executing the funnel
        if (!userEligible)
        {
            return controlFunc();
        }
        
        // Add detailed context for analysis
        var detailedContext = CreateDetailedContext(validationParameters);
        
        // Use RolloutFunnel with the eligible user for progressive rollout
        return _rolloutFunnel.Execute(
            experimentName: experimentName,
            controlFunc: controlFunc,
            candidateFunc: candidateFunc,
            additionalContext: detailedContext
        );
    }
    
    /// <summary>
    /// Asynchronous version for execution with complete validation
    /// </summary>
    public async Task<T> ExecuteWithValidationAsync<T>(
        string experimentName,
        ValidationParameters validationParameters,
        Func<Task<T>> controlFunc,
        Func<Task<T>> candidateFunc)
    {
        // Check if the user is eligible with multiple criteria
        bool userEligible = ValidateCompleteEligibility(validationParameters);
        
        // If not eligible, return the result of the old method without executing the funnel
        if (!userEligible)
        {
            return await controlFunc();
        }
        
        // Add detailed context for analysis
        var detailedContext = CreateDetailedContext(validationParameters);
        
        // Use RolloutFunnel with the eligible user
        return await _rolloutFunnel.ExecuteAsync(
            experimentName: experimentName,
            controlFunc: controlFunc,
            candidateFunc: candidateFunc,
            additionalContext: detailedContext
        );
    }
    
    /// <summary>
    /// Validates if the user is eligible to participate in the experiment
    /// Combines percentage validation with additional criteria
    /// </summary>
    private bool ValidateCompleteEligibility(ValidationParameters parameters)
    {
        try
        {
            // Check if advanced validation is active
            if (!IsCriteriaValidationActive())
            {
                // If criteria validation is not active, use only percentage validation
                return IsUserEligibleByPercentage(parameters.UserId);
            }
            
            // Check if the user is within the rollout percentage
            if (!IsUserEligibleByPercentage(parameters.UserId))
            {
                return false;
            }
            
            // Check if multiple criteria need to be evaluated
            if (IsMultipleCriteriaEnabled())
            {
                return ValidateFunctionalCriteria(parameters) && 
                       ValidateBehavioralCriteria(parameters) &&
                       ValidateContextualCriteria(parameters);
            }
            else
            {
                // If there are no multiple criteria, validate only basic functional criteria
                return ValidateFunctionalCriteria(parameters);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating eligibility for user {UserId}", parameters.UserId);
            
            // In case of error, fallback to old implementation
            return false;
        }
    }
    
    /// <summary>
    /// Checks if the user is within the configured rollout percentage
    /// </summary>
    private bool IsUserEligibleByPercentage(int userId)
    {
        // First check if rollout is enabled
        if (!_rolloutFunnel.IsRolloutEnabled())
        {
            return false;
        }
        
        // Get current percentage
        int percentage = _rolloutFunnel.GetRolloutPercentage();
        
        // If percentage is 0 or less, no one is eligible
        if (percentage <= 0)
        {
            return false;
        }
        
        // If percentage is 100 or more, everyone is eligible
        if (percentage >= 100)
        {
            return true;
        }
        
        // Create a Random based on the user ID to ensure consistency
        // so the same user always gets the same result
        int seed = userId.GetHashCode();
        Random userRandom = new Random(seed);
        
        // Return true if the generated number is within the percentage
        return userRandom.Next(100) < percentage;
    }
    
    /// <summary>
    /// Validates functional criteria (user type, groups, etc.)
    /// </summary>
    private bool ValidateFunctionalCriteria(ValidationParameters parameters)
    {
        // Get configuration
        string allowedCriteria = _redisConfig.GetConfigValue(USER_CRITERIA_KEY, "");
        
        // If there are no criteria, all are valid
        if (string.IsNullOrEmpty(allowedCriteria))
        {
            return true;
        }
        
        // Split criteria and convert to HashSet for more efficient search
        HashSet<string> criteria = new HashSet<string>(
            allowedCriteria.Split(','), 
            StringComparer.OrdinalIgnoreCase
        );
        
        // Check if the user type is in the list of allowed types
        if (criteria.Count > 0 && !string.IsNullOrEmpty(parameters.UserType))
        {
            if (!criteria.Contains(parameters.UserType))
            {
                return false;
            }
        }
        
        // Check if the CPF is in the allowed list
        if (parameters.ContextualData != null && 
            parameters.ContextualData.ContainsKey("CPF"))
        {
            string cpf = parameters.ContextualData["CPF"]?.ToString();
            if (!string.IsNullOrEmpty(cpf) && !ValidateCpfInList(cpf))
            {
                return false;
            }
        }
        
        // Check with the LinkedPerson service if the user is eligible
        if (parameters.ContextualData != null && 
            parameters.ContextualData.ContainsKey("CheckLinkedPerson") && 
            bool.TryParse(parameters.ContextualData["CheckLinkedPerson"].ToString(), out bool checkLinked) && 
            checkLinked)
        {
            return ValidateLinkedPersonUser(parameters);
        }
        
        return true;
    }
    
    /// <summary>
    /// Checks if the user's CPF is in the allowed list in Redis
    /// </summary>
    private bool ValidateCpfInList(string cpf)
    {
        try
        {
            // Remove non-numeric characters from CPF
            cpf = new string(cpf.Where(char.IsDigit).ToArray());
            
            // Get the list of allowed CPFs from Redis
            string cpfList = _redisConfig.GetConfigValue(CPF_LIST_KEY, "");
            
            if (string.IsNullOrEmpty(cpfList))
            {
                return true; // If there's no list, allow all
            }
            
            // Split the list and check if the CPF is contained
            return cpfList.Split(',').Any(c => c.Trim() == cpf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating CPF {CPF} in the list", cpf);
            return false;
        }
    }
    
    /// <summary>
    /// Checks eligibility using the LinkedPerson service
    /// </summary>
    private bool ValidateLinkedPersonUser(ValidationParameters parameters)
    {
        try
        {
            // According to the requirement, should return true if LinkedPerson service validates the user
            if (parameters.ContextualData == null)
            {
                return false;
            }
            
            // Get data for LinkedPerson service query
            string cpf = parameters.ContextualData.ContainsKey("CPF") ? 
                        parameters.ContextualData["CPF"]?.ToString() : null;
            
            // To avoid invalid queries
            if (string.IsNullOrEmpty(cpf))
            {
                return false;
            }
            
            // Here would call the real LinkedPerson service
            // In this example we simulate validation based on CPF
            // In production, you would implement the real service call
            bool userEligible = SimulateLinkedPersonQuery(cpf, parameters.UserId);
            
            _logger.LogInformation(
                "LinkedPerson query for CPF {CPF} (user {UserId}): Eligible = {Eligible}",
                cpf,
                parameters.UserId,
                userEligible);
                
            return userEligible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying LinkedPerson service for user {UserId}", parameters.UserId);
            return false;
        }
    }
    
    /// <summary>
    /// Simulates the query to the LinkedPerson service
    /// In production, this method would be replaced by the real service call
    /// </summary>
    private bool SimulateLinkedPersonQuery(string cpf, int userId)
    {
        // Basic simulation for testing purposes
        // Returns true for CPFs ending with even digits
        if (!string.IsNullOrEmpty(cpf) && cpf.Length >= 1)
        {
            int lastDigit = int.Parse(cpf.Substring(cpf.Length - 1));
            return lastDigit % 2 == 0;
        }
        
        // Or based on user ID for tests
        return userId % 2 == 0;
    }
    
    /// <summary>
    /// Validates behavioral criteria (purchase history, usage time, etc.)
    /// </summary>
    private bool ValidateBehavioralCriteria(ValidationParameters parameters)
    {
        // Basic implementation - in practice you can add more complex rules
        
        // Example: check if the user has purchase history
        if (parameters.BehavioralData != null && 
            parameters.BehavioralData.ContainsKey("PurchaseHistory"))
        {
            bool hasHistory = bool.TryParse(
                parameters.BehavioralData["PurchaseHistory"].ToString(), 
                out bool result) && result;
                
            if (!hasHistory)
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Validates contextual criteria (location, device, etc.)
    /// </summary>
    private bool ValidateContextualCriteria(ValidationParameters parameters)
    {
        // Basic implementation - in practice you can add more complex rules
        
        // Example: check the user's region
        if (parameters.ContextualData != null && 
            parameters.ContextualData.ContainsKey("Region"))
        {
            string region = parameters.ContextualData["Region"]?.ToString();
            string allowedRegions = _redisConfig.GetConfigValue("rollout:allowed_regions", "");
            
            if (!string.IsNullOrEmpty(allowedRegions) && !string.IsNullOrEmpty(region))
            {
                HashSet<string> regions = new HashSet<string>(
                    allowedRegions.Split(','), 
                    StringComparer.OrdinalIgnoreCase
                );
                
                if (regions.Count > 0 && !regions.Contains(region))
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Creates a detailed context for experiment analysis
    /// </summary>
    private object CreateDetailedContext(ValidationParameters parameters)
    {
        // Maps only relevant properties to the context
        // avoiding adding sensitive information
        return new
        {
            UserId = parameters.UserId,
            UserType = parameters.UserType,
            HasBehavioralData = parameters.BehavioralData?.Count > 0,
            HasContextualData = parameters.ContextualData?.Count > 0,
            ExecutionDate = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Configures validation parameters in Redis
    /// </summary>
    public void ConfigureAdvancedValidation(AdvancedConfiguration config)
    {
        try
        {
            // Activate or deactivate criteria validation
            _redisConfig.SetConfigValue(ACTIVE_CONFIG_KEY, config.ValidationActive.ToString());
            
            // Activate or deactivate multiple criteria
            _redisConfig.SetConfigValue(MULTIPLE_CRITERIA_KEY, config.MultipleCriteria.ToString());
            
            // Configure user criteria
            if (config.AllowedCriteria != null)
            {
                _redisConfig.SetConfigValue(USER_CRITERIA_KEY, 
                    string.Join(",", config.AllowedCriteria));
            }
            
            // Configure the allowed CPF list
            if (config.AllowedCpfList != null)
            {
                _redisConfig.SetConfigValue(CPF_LIST_KEY, 
                    string.Join(",", config.AllowedCpfList));
            }
            
            // Also configure the percentage in RolloutFunnel
            _rolloutFunnel.EnableRollout(config.ValidationActive);
            _rolloutFunnel.SetRolloutPercentage(config.Percentage);
            
            _logger.LogInformation(
                "Advanced configuration updated: Active={Active}, Percentage={Percentage}%, Multiple Criteria={MultCriteria}",
                config.ValidationActive,
                config.Percentage,
                config.MultipleCriteria);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error configuring advanced validation");
            throw;
        }
    }
    
    /// <summary>
    /// Checks if criteria validation is active
    /// </summary>
    private bool IsCriteriaValidationActive()
    {
        string value = _redisConfig.GetConfigValue(ACTIVE_CONFIG_KEY, "false");
        return bool.TryParse(value, out bool result) && result;
    }
    
    /// <summary>
    /// Checks if multiple criteria validation is enabled
    /// </summary>
    private bool IsMultipleCriteriaEnabled()
    {
        string value = _redisConfig.GetConfigValue(MULTIPLE_CRITERIA_KEY, "false");
        return bool.TryParse(value, out bool result) && result;
    }
}

/// <summary>
/// Parameters for eligibility validation
/// </summary>
public class ValidationParameters
{
    public int UserId { get; set; }
    public string UserType { get; set; }
    public Dictionary<string, object> BehavioralData { get; set; }
    public Dictionary<string, object> ContextualData { get; set; }
}

/// <summary>
/// Configuration for advanced validation
/// </summary>
public class AdvancedConfiguration
{
    public bool ValidationActive { get; set; } = true;
    public int Percentage { get; set; } = 0;
    public bool MultipleCriteria { get; set; } = false;
    public List<string> AllowedCriteria { get; set; }
    public List<string> AllowedCpfList { get; set; }
} 