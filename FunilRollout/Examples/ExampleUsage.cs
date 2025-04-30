using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FunilRollout.Services;

namespace FunilRollout.Examples;

/// <summary>
/// Demonstrates how to set up and use the AdvancedFunnel in a real application
/// </summary>
public class ExampleUsage
{
    public static void Main(string[] args)
    {
        // Set up dependency injection
        var serviceProvider = ConfigureServices();
        
        // Get the example class from the service provider
        var example = serviceProvider.GetRequiredService<AdvancedFunnelExample>();
        
        // Configure the experiment
        example.ConfigureExperiment();
        
        // Test with different users to see how the funnel works
        TestSyncMethod(example);
        TestAsyncMethod(example).Wait();
        
        Console.WriteLine("Example execution completed.");
    }
    
    private static IServiceProvider ConfigureServices()
    {
        // Set up the service collection
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(configure => configure.AddConsole());
        
        // Register the required services
        services.AddSingleton<RedisConfigProvider>(); // You'd need to implement this
        services.AddSingleton<RolloutFunnel>();
        services.AddSingleton<AdvancedFunnel>();
        services.AddSingleton<AdvancedFunnelExample>();
        
        return services.BuildServiceProvider();
    }
    
    private static void TestSyncMethod(AdvancedFunnelExample example)
    {
        Console.WriteLine("Testing synchronous method:");
        
        // Test with different user profiles
        TestUserDiscount(example, 123, "12345678900", 100m); // Regular user, CPF in allowed list
        TestUserDiscount(example, 300, "11122233344", 200m); // Premium user (id divisible by 3), CPF in allowed list
        TestUserDiscount(example, 305, "99999999999", 150m); // VIP user (id divisible by 5), CPF not in list
    }
    
    private static void TestUserDiscount(AdvancedFunnelExample example, int userId, string cpf, decimal orderValue)
    {
        try
        {
            decimal discount = example.CalculateOrderDiscount(userId, cpf, orderValue);
            Console.WriteLine($"User {userId} with CPF {cpf} gets discount: {discount:C} on order value {orderValue:C}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing user {userId}: {ex.Message}");
        }
    }
    
    private static async System.Threading.Tasks.Task TestAsyncMethod(AdvancedFunnelExample example)
    {
        Console.WriteLine("\nTesting asynchronous method:");
        
        // Test with different user profiles
        await TestUserRecommendations(example, 123, "12345678900"); // Regular user, CPF in allowed list
        await TestUserRecommendations(example, 300, "11122233344"); // Premium user, CPF in allowed list
        await TestUserRecommendations(example, 305, "99999999999"); // VIP user, CPF not in list
    }
    
    private static async System.Threading.Tasks.Task TestUserRecommendations(AdvancedFunnelExample example, int userId, string cpf)
    {
        try
        {
            var recommendations = await example.GetRecommendedProductsAsync(userId, cpf);
            
            Console.WriteLine($"User {userId} with CPF {cpf} gets recommendations:");
            foreach (var product in recommendations)
            {
                Console.WriteLine($"  - {product}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error processing user {userId}: {ex.Message}");
        }
    }
} 