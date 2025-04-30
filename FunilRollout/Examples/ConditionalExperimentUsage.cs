using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FunilRollout.Services;
using GitHub;

namespace FunilRollout.Examples;

/// <summary>
/// Demonstrates how to set up and use the ConditionalExperiment in a real application
/// </summary>
public class ConditionalExperimentUsage
{
    public static async Task Main(string[] args)
    {
        // Set up dependency injection
        var serviceProvider = ConfigureServices();
        
        // Get the example class from the service provider
        var example = serviceProvider.GetRequiredService<ConditionalExperimentExample>();
        
        Console.WriteLine("===== CONDITIONAL EXPERIMENT EXAMPLES =====\n");
        
        // Example 1: Conditional based on user type
        Console.WriteLine("Example 1: Price calculation based on user type");
        ExamplePriceCalculation(example);
        
        // Example 2: Conditional based on rollout percentage
        Console.WriteLine("\nExample 2: Recommendations with rollout percentage");
        ExampleRecommendations(example);
        
        // Example 3: Asynchronous conditional experiment
        Console.WriteLine("\nExample 3: Asynchronous payment processing");
        await ExampleAsyncPaymentProcessing(example);
        
        Console.WriteLine("\nExamples completed. Press any key to exit.");
        Console.ReadKey();
    }
    
    private static IServiceProvider ConfigureServices()
    {
        // Set up the service collection
        var services = new ServiceCollection();
        
        // Add logging
        services.AddLogging(configure => configure.AddConsole());
        
        // Configure GitHub Scientist result publisher for better console output
        Scientist.ResultPublisher = new ConsoleDetailsPublisher();
        
        // Register the required services
        services.AddSingleton<ConditionalExperiment>();
        services.AddSingleton<ConditionalExperimentExample>();
        
        return services.BuildServiceProvider();
    }
    
    private static void ExamplePriceCalculation(ConditionalExperimentExample example)
    {
        // Test with regular user
        int product1 = 123;
        bool isRegularUser = false;
        decimal regularPrice = example.CalculatePrice(product1, isRegularUser);
        Console.WriteLine($"Regular user price for product {product1}: {regularPrice:C}");
        
        // Test with VIP user
        int product2 = 456;
        bool isVipUser = true;
        decimal vipPrice = example.CalculatePrice(product2, isVipUser);
        Console.WriteLine($"VIP user price for product {product2}: {vipPrice:C}");
    }
    
    private static void ExampleRecommendations(ConditionalExperimentExample example)
    {
        // Test with multiple users to see rollout percentage in action
        TestUserRecommendations(example, 101, "Electronics");
        TestUserRecommendations(example, 202, "Books");
        TestUserRecommendations(example, 303, "Clothing");
        TestUserRecommendations(example, 404, "Home");
        TestUserRecommendations(example, 505, "Sports");
    }
    
    private static void TestUserRecommendations(ConditionalExperimentExample example, int userId, string category)
    {
        var recommendations = example.GetRecommendations(userId, category);
        
        Console.WriteLine($"User {userId} - Category {category}:");
        foreach (var recommendation in recommendations)
        {
            Console.WriteLine($"  - {recommendation}");
        }
        Console.WriteLine();
    }
    
    private static async Task ExampleAsyncPaymentProcessing(ConditionalExperimentExample example)
    {
        // Standard checkout
        int order1 = 12345;
        bool isStandard = false;
        string standardResult = await example.ProcessPaymentAsync(order1, isStandard);
        Console.WriteLine($"Order {order1}: {standardResult}");
        
        // Express checkout
        int order2 = 67890;
        bool isExpress = true;
        string expressResult = await example.ProcessPaymentAsync(order2, isExpress);
        Console.WriteLine($"Order {order2}: {expressResult}");
    }
}

/// <summary>
/// Custom publisher that formats the results nicely for console output
/// </summary>
public class ConsoleDetailsPublisher : IResultPublisher
{
    public Task Publish<T, TClean>(Result<T, TClean> result)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"\n[EXPERIMENT: {result.ExperimentName}]");
        Console.ResetColor();
        
        Console.WriteLine($"Result: {(result.Matched ? "MATCHED" : "MISMATCHED")}");
        
        Console.WriteLine($"Control ({result.Control.Duration.TotalMilliseconds:F1}ms): {result.Control.Value}");
        
        foreach (var observation in result.Candidates)
        {
            Console.WriteLine($"Candidate ({observation.Duration.TotalMilliseconds:F1}ms): {observation.Value}");
        }
        
        Console.WriteLine("Context:");
        foreach (var ctx in result.Contexts)
        {
            Console.WriteLine($"  {ctx.Key}: {ctx.Value}");
        }
        
        Console.WriteLine("----------------------------");
        
        return Task.CompletedTask;
    }
} 