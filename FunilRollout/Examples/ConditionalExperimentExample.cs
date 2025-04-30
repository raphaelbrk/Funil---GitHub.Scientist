using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FunilRollout.Services;
using Microsoft.Extensions.Logging;

namespace FunilRollout.Examples;

/// <summary>
/// Example showing how to use the ConditionalExperiment class
/// </summary>
public class ConditionalExperimentExample
{
    private readonly ConditionalExperiment _conditionalExperiment;
    private readonly ILogger<ConditionalExperimentExample> _logger;

    public ConditionalExperimentExample(
        ConditionalExperiment conditionalExperiment,
        ILogger<ConditionalExperimentExample> logger)
    {
        _conditionalExperiment = conditionalExperiment;
        _logger = logger;
    }

    /// <summary>
    /// Example of a basic conditional experiment where the implementation
    /// depends on a specific condition
    /// </summary>
    public decimal CalculatePrice(int productId, bool isVipUser)
    {
        try
        {
            _logger.LogInformation("Calculating price for product {ProductId}, VIP user: {IsVip}", productId, isVipUser);
            
            // Execute the experiment with condition determining which implementation is used as control
            return _conditionalExperiment.ExecuteConditionalExperiment(
                experimentName: "price_calculation",
                trueImplementation: () => CalculateVipPrice(productId),
                falseImplementation: () => CalculateRegularPrice(productId),
                condition: isVipUser,
                experimentType: "A",
                additionalContext: new { ProductId = productId }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating price for product {ProductId}", productId);
            return CalculateRegularPrice(productId);
        }
    }
    
    /// <summary>
    /// Example of experiment where the implementation depends on a rollout percentage
    /// </summary>
    public List<string> GetRecommendations(int userId, string category)
    {
        try
        {
            _logger.LogInformation("Getting recommendations for user {UserId} in category {Category}", userId, category);
            
            // Execute the experiment with rollout percentage (50%)
            return _conditionalExperiment.ExecuteRolloutExperiment(
                experimentName: "recommendations",
                newImplementation: () => GetPersonalizedRecommendations(userId, category),
                oldImplementation: () => GetStandardRecommendations(category),
                rolloutPercentage: 50,
                userId: userId,
                experimentType: "B",
                additionalContext: new { Category = category }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recommendations for user {UserId}", userId);
            return GetStandardRecommendations(category);
        }
    }
    
    /// <summary>
    /// Example of asynchronous experiment with condition
    /// </summary>
    public async Task<string> ProcessPaymentAsync(int orderId, bool isExpressCheckout)
    {
        try
        {
            _logger.LogInformation("Processing payment for order {OrderId}, Express: {IsExpress}", orderId, isExpressCheckout);
            
            // Execute the async experiment with condition
            return await _conditionalExperiment.ExecuteConditionalExperimentAsync(
                experimentName: "payment_processing",
                trueImplementation: () => ProcessExpressPaymentAsync(orderId),
                falseImplementation: () => ProcessStandardPaymentAsync(orderId),
                condition: isExpressCheckout,
                experimentType: "A"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment for order {OrderId}", orderId);
            return await ProcessStandardPaymentAsync(orderId);
        }
    }
    
    #region Private implementation methods
    
    private decimal CalculateVipPrice(int productId)
    {
        // Simulate VIP pricing with 15% discount
        decimal basePrice = GetBasePrice(productId);
        return basePrice * 0.85m;
    }
    
    private decimal CalculateRegularPrice(int productId)
    {
        // Simulate regular pricing with 5% discount
        decimal basePrice = GetBasePrice(productId);
        return basePrice * 0.95m;
    }
    
    private decimal GetBasePrice(int productId)
    {
        // Simulate fetching a product price
        return 100m + (productId % 10) * 25m;
    }
    
    private List<string> GetPersonalizedRecommendations(int userId, string category)
    {
        // Simulate personalized recommendations
        return new List<string>
        {
            $"Personalized {category} item 1 for user {userId}",
            $"Personalized {category} item 2 for user {userId}",
            $"Special offer for user {userId}"
        };
    }
    
    private List<string> GetStandardRecommendations(string category)
    {
        // Simulate standard recommendations
        return new List<string>
        {
            $"Popular {category} item 1",
            $"Popular {category} item 2",
            $"Best-selling {category} item"
        };
    }
    
    private async Task<string> ProcessExpressPaymentAsync(int orderId)
    {
        // Simulate express payment processing
        await Task.Delay(50); // Faster processing
        return $"Express payment for order {orderId} processed successfully";
    }
    
    private async Task<string> ProcessStandardPaymentAsync(int orderId)
    {
        // Simulate standard payment processing
        await Task.Delay(200); // Slower processing
        return $"Standard payment for order {orderId} processed successfully";
    }
    
    #endregion
} 