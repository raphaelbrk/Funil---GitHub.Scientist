using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FunilRollout.Services;
using Microsoft.AspNetCore.Mvc;

namespace FunilRollout.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RolloutController : ControllerBase
{
    private readonly RolloutFunnel _rolloutFunnel;
    private readonly CustomResultPublisher _customPublisher;
    
    public RolloutController(RolloutFunnel rolloutFunnel, CustomResultPublisher customPublisher)
    {
        _rolloutFunnel = rolloutFunnel;
        _customPublisher = customPublisher;
    }
    
    /// <summary>
    /// Endpoint para configurar a porcentagem de rollout
    /// </summary>
    [HttpPost("configure")]
    public IActionResult ConfigureRollout([FromBody] RolloutConfig config)
    {
        try
        {
            _rolloutFunnel.EnableRollout(config.Enabled);
            _rolloutFunnel.SetRolloutPercentage(config.Percentage);
            
            return Ok(new { 
                Message = "Configuração de rollout atualizada com sucesso",
                Enabled = config.Enabled,
                Percentage = config.Percentage
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }
    
    /// <summary>
    /// Endpoint para obter a configuração atual do rollout
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetRolloutStatus()
    {
        bool enabled = _rolloutFunnel.IsRolloutEnabled();
        int percentage = _rolloutFunnel.GetRolloutPercentage();
        
        return Ok(new {
            Enabled = enabled,
            Percentage = percentage
        });
    }
    
    /// <summary>
    /// Endpoint para alternar entre publishers de resultados
    /// </summary>
    [HttpPost("publisher")]
    public IActionResult SetPublisher([FromBody] PublisherConfig config)
    {
        if (config.UseCustomPublisher)
        {
            _rolloutFunnel.SetPublisher(_customPublisher);
        }
        else
        {
            // Volta para o publisher padrão (console)
            _rolloutFunnel.SetPublisher(new ConsoleResultPublisher());
        }
        
        return Ok(new { 
            Message = $"Publisher alterado para: {(config.UseCustomPublisher ? "Custom" : "Console")}" 
        });
    }
    
    /// <summary>
    /// Exemplo de endpoint que usa o funil para comparar duas implementações
    /// </summary>
    [HttpGet("example")]
    public IActionResult GetExample([FromQuery] int id = 1)
    {
        var result = _rolloutFunnel.Execute<string>(
            experimentName: "get-example-data",
            controlFunc: () => LegacyGetData(id),
            candidateFunc: () => NewGetData(id),
            additionalContext: new { RequestId = Guid.NewGuid(), UserId = id }
        );
        
        return Ok(new { Data = result });
    }
    
    /// <summary>
    /// Exemplo de endpoint assíncrono que usa o funil para comparar duas implementações
    /// </summary>
    [HttpGet("example-async")]
    public async Task<IActionResult> GetExampleAsync([FromQuery] int id = 1)
    {
        var result = await _rolloutFunnel.ExecuteAsync<List<string>>(
            experimentName: "get-example-data-async",
            controlFunc: () => LegacyGetDataAsync(id),
            candidateFunc: () => NewGetDataAsync(id),
            additionalContext: new { RequestId = Guid.NewGuid(), UserId = id }
        );
        
        return Ok(new { Data = result });
    }
    
    #region Métodos de exemplo
    
    // Método simulado da implementação antiga
    private string LegacyGetData(int id)
    {
        // Simula processamento
        System.Threading.Thread.Sleep(50);
        return $"Dados do usuário {id} (implementação antiga)";
    }
    
    // Método simulado da nova implementação
    private string NewGetData(int id)
    {
        // Simula processamento
        System.Threading.Thread.Sleep(30);
        return $"Dados do usuário {id} (implementação nova)";
    }
    
    // Método assíncrono simulado da implementação antiga
    private async Task<List<string>> LegacyGetDataAsync(int id)
    {
        await Task.Delay(100);
        return new List<string> { 
            $"Item 1 para usuário {id} (implementação antiga)",
            $"Item 2 para usuário {id} (implementação antiga)" 
        };
    }
    
    // Método assíncrono simulado da nova implementação
    private async Task<List<string>> NewGetDataAsync(int id)
    {
        await Task.Delay(70);
        return new List<string> { 
            $"Item 1 para usuário {id} (implementação nova)",
            $"Item 2 para usuário {id} (implementação nova)"
        };
    }
    
    #endregion
}

public class RolloutConfig
{
    public bool Enabled { get; set; } = true;
    public int Percentage { get; set; } = 0;
}

public class PublisherConfig
{
    public bool UseCustomPublisher { get; set; } = true;
} 