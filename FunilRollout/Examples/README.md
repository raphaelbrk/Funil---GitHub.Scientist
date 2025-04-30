# Advanced Funnel Usage Examples

This directory contains examples demonstrating how to use the `AdvancedFunnel` component for gradual rollout of new features with sophisticated eligibility validation.

## Overview

The Advanced Funnel combines GitHub's Scientist experimentation framework with custom validation logic to allow for:

1. Percentage-based rollout to a subset of users
2. User eligibility based on multiple criteria including:
   - User type (Premium, VIP, etc.)
   - Behavioral data (purchase history, usage patterns)
   - Contextual data (location, device, etc.)
   - CPF validation (using a Redis-stored allowlist)
   - LinkedPerson service validation

## Key Files

- **AdvancedFunnelExample.cs**: Shows how to configure and use the `AdvancedFunnel` in both synchronous and asynchronous methods.
- **ExampleUsage.cs**: Provides a runnable example with dependency injection setup and test cases.

## How to Use

### 1. Setup and Configuration

```csharp
// Register services in DI container
services.AddSingleton<RedisConfigProvider>();
services.AddSingleton<RolloutFunnel>();
services.AddSingleton<AdvancedFunnel>();

// Configure the funnel
var config = new AdvancedConfiguration
{
    ValidationActive = true,
    Percentage = 10, // 10% of users
    MultipleCriteria = true,
    AllowedCriteria = new List<string> { "Premium", "VIP" },
    AllowedCpfList = new List<string> { "12345678900", "98765432100" }
};

advancedFunnel.ConfigureAdvancedValidation(config);
```

### 2. Using in Synchronous Methods

```csharp
public decimal CalculateDiscount(int userId, string cpf, decimal orderValue)
{
    var parameters = new ValidationParameters
    {
        UserId = userId,
        UserType = GetUserType(userId),
        ContextualData = new Dictionary<string, object>
        {
            { "CPF", cpf },
            { "CheckLinkedPerson", true }
        }
    };
    
    return _advancedFunnel.ExecuteWithValidation(
        experimentName: "new_discount_calculator",
        validationParameters: parameters,
        controlFunc: () => OriginalDiscountMethod(orderValue),
        candidateFunc: () => NewDiscountMethod(orderValue)
    );
}
```

### 3. Using in Asynchronous Methods

```csharp
public async Task<List<string>> GetRecommendationsAsync(int userId, string cpf)
{
    var parameters = new ValidationParameters
    {
        UserId = userId,
        UserType = GetUserType(userId),
        ContextualData = new Dictionary<string, object>
        {
            { "CPF", cpf },
            { "CheckLinkedPerson", true }
        }
    };
    
    return await _advancedFunnel.ExecuteWithValidationAsync(
        experimentName: "new_recommendations",
        validationParameters: parameters,
        controlFunc: () => OriginalRecommendationsAsync(userId),
        candidateFunc: () => NewRecommendationsAsync(userId)
    );
}
```

## Implementation Details

### Eligibility Validation

The `AdvancedFunnel` performs multiple validation steps:

1. First checks if the user is within the rollout percentage
2. If enabled, validates functional criteria (user type, CPF in allowed list)
3. If multiple criteria are enabled, also validates behavioral and contextual criteria

### LinkedPerson Integration

The funnel integrates with LinkedPerson service to verify user eligibility. To use this:

1. Include `{ "CheckLinkedPerson", true }` in the `ContextualData` dictionary
2. Also include the user's CPF in the same dictionary with key "CPF"
3. The funnel will verify eligibility through the LinkedPerson service

### Redis Configuration

All configuration is stored in Redis for easy updates without code deployments:

- `rollout:config_active`: Whether validation is active
- `rollout:user_criteria`: List of allowed user types
- `rollout:multiple_criteria`: Whether to check all criteria types
- `rollout:cpf_list`: List of allowed CPFs

## Running the Example

To run the example:

```bash
dotnet run --project FunilRollout/Examples
```

This will execute the `ExampleUsage` class which demonstrates the funnel with various test users. 