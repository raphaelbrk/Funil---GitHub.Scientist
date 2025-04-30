using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FunilRollout.Services;
using Microsoft.Extensions.Logging;

namespace FunilRollout.Examples;

/// <summary>
/// Practical example of how to use AdvancedFunnel to run experiments with custom validations
/// </summary>
public class AdvancedFunnelExample
{
    private readonly AdvancedFunnel _advancedFunnel;
    private readonly ILogger<AdvancedFunnelExample> _logger;

    public AdvancedFunnelExample(
        AdvancedFunnel advancedFunnel,
        ILogger<AdvancedFunnelExample> logger)
    {
        _advancedFunnel = advancedFunnel;
        _logger = logger;
    }

    /// <summary>
    /// Configures the advanced funnel for a specific experiment
    /// </summary>
    public void ConfigureExperiment()
    {
        // Example configuration for the experiment
        var config = new AdvancedConfiguration
        {
            // Activate criteria validation
            ValidationActive = true,
            
            // Set rollout percentage (10% of users)
            Percentage = 10,
            
            // Enable multiple criteria validation
            MultipleCriteria = true,
            
            // List of allowed user types
            AllowedCriteria = new List<string> { "Premium", "VIP", "Beta" },
            
            // List of CPFs allowed for specific tests
            AllowedCpfList = new List<string> { 
                "12345678900", 
                "98765432100", 
                "11122233344" 
            }
        };
        
        // Apply the configuration
        _advancedFunnel.ConfigureAdvancedValidation(config);
        
        _logger.LogInformation("Experiment successfully configured");
    }
    
    /// <summary>
    /// Example of using the advanced funnel in a synchronous method
    /// </summary>
    public decimal CalculateOrderDiscount(int userId, string cpf, decimal orderValue)
    {
        try
        {
            // Prepare validation parameters
            var parameters = new ValidationParameters
            {
                UserId = userId,
                UserType = GetUserType(userId),
                ContextualData = new Dictionary<string, object>
                {
                    { "CPF", cpf },
                    { "OrderValue", orderValue },
                    { "Region", "Southeast" },
                    { "CheckLinkedPerson", true } // Request verification in the LinkedPerson service
                },
                BehavioralData = new Dictionary<string, object>
                {
                    { "PurchaseHistory", HasPurchaseHistory(userId) },
                    { "DaysSinceRegistration", GetDaysSinceRegistration(userId) }
                }
            };
            
            // Experiment name
            string experimentName = "new_discount_algorithm";
            
            // Execute the experiment with validation
            return _advancedFunnel.ExecuteWithValidation(
                experimentName: experimentName,
                validationParameters: parameters,
                controlFunc: () => CalculateDiscountOriginal(orderValue),
                candidateFunc: () => CalculateDiscountNew(orderValue, userId)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating discount for user {UserId}", userId);
            
            // In case of error, return the original calculation
            return CalculateDiscountOriginal(orderValue);
        }
    }
    
    /// <summary>
    /// Example of using the advanced funnel in an asynchronous method
    /// </summary>
    public async Task<List<string>> GetRecommendedProductsAsync(int userId, string cpf)
    {
        try
        {
            // Prepare validation parameters
            var parameters = new ValidationParameters
            {
                UserId = userId,
                UserType = GetUserType(userId),
                ContextualData = new Dictionary<string, object>
                {
                    { "CPF", cpf },
                    { "Platform", "Mobile" },
                    { "CheckLinkedPerson", true }
                }
            };
            
            // Experiment name
            string experimentName = "new_recommendation_algorithm";
            
            // Execute the asynchronous experiment with validation
            return await _advancedFunnel.ExecuteWithValidationAsync(
                experimentName: experimentName,
                validationParameters: parameters,
                controlFunc: () => GetRecommendationsOriginalAsync(userId),
                candidateFunc: () => GetRecommendationsNewAsync(userId)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for user {UserId}", userId);
            
            // In case of error, return the original algorithm
            return await GetRecommendationsOriginalAsync(userId);
        }
    }
    
    #region Helper methods - would simulate calls to real services
    
    private string GetUserType(int userId)
    {
        // Simulation - in practice would query a database or service
        if (userId % 3 == 0) return "Premium";
        if (userId % 5 == 0) return "VIP";
        if (userId % 7 == 0) return "Beta";
        return "Regular";
    }
    
    private bool HasPurchaseHistory(int userId)
    {
        // Simulation - checks if the user has purchase history
        return userId % 2 == 0;
    }
    
    private int GetDaysSinceRegistration(int userId)
    {
        // Simulation - calculates days since registration
        return userId % 100 + 10; 
    }
    
    private decimal CalculateDiscountOriginal(decimal orderValue)
    {
        // Original discount calculation algorithm
        decimal baseDiscount = orderValue * 0.05m;
        return Math.Min(baseDiscount, 50); // Maximum discount of 50
    }
    
    private decimal CalculateDiscountNew(decimal orderValue, int userId)
    {
        // New discount calculation algorithm (candidate version)
        decimal baseDiscount = orderValue * 0.07m;
        
        // Bonus for longtime customers
        int days = GetDaysSinceRegistration(userId);
        decimal loyaltyBonus = days > 30 ? 10 : 0;
        
        return Math.Min(baseDiscount + loyaltyBonus, 70); // Maximum discount of 70
    }
    
    private async Task<List<string>> GetRecommendationsOriginalAsync(int userId)
    {
        // Simulation - original recommendation algorithm
        await Task.Delay(50); // Simulates asynchronous processing
        
        return new List<string> 
        { 
            "Product A", 
            "Product B", 
            "Product C" 
        };
    }
    
    private async Task<List<string>> GetRecommendationsNewAsync(int userId)
    {
        // Simulation - new recommendation algorithm
        await Task.Delay(30); // Simulates faster asynchronous processing
        
        // Personalizes based on ID
        string personalized = $"Special for user {userId}";
        
        return new List<string> 
        { 
            personalized,
            "Product X", 
            "Product Y", 
            "Product Z" 
        };
    }
    
    #endregion
} 