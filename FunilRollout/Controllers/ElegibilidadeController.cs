using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FunilRollout.Services;

namespace FunilRollout.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ElegibilidadeController : ControllerBase
{
    private readonly ValidadorElegibilidadeFunil _validador;
    private readonly ILogger<ElegibilidadeController> _logger;

    public ElegibilidadeController(
        ValidadorElegibilidadeFunil validador,
        ILogger<ElegibilidadeController> logger)
    {
        _validador = validador;
        _logger = logger;
    }

    /// <summary>
    /// Verifica se um usuário é elegível para participar do rollout
    /// </summary>
    [HttpPost("validar")]
    public IActionResult ValidarElegibilidade([FromBody] RequisicaoElegibilidade requisicao)
    {
        try
        {
            var resultado = _validador.ValidarElegibilidade(
                requisicao.UsuarioId,
                requisicao.Perfil
            );

            return Ok(new
            {
                Elegivel = resultado.EhElegivel,
                Motivo = resultado.Motivo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar elegibilidade do usuário {UsuarioId}", requisicao.UsuarioId);
            return StatusCode(500, new { Mensagem = "Erro ao processar a validação de elegibilidade" });
        }
    }

    /// <summary>
    /// Verifica a elegibilidade usando o funil do Scientist para comparar a implementação nova e antiga
    /// </summary>
    [HttpPost("validar-com-rollout")]
    public IActionResult ValidarElegibilidadeComRollout([FromBody] RequisicaoElegibilidade requisicao)
    {
        try
        {
            var resultado = _validador.ValidarElegibilidadeComRollout(
                requisicao.UsuarioId,
                requisicao.Perfil
            );

            return Ok(new
            {
                Elegivel = resultado.EhElegivel,
                Motivo = resultado.Motivo
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar elegibilidade com rollout para o usuário {UsuarioId}", requisicao.UsuarioId);
            return StatusCode(500, new { Mensagem = "Erro ao processar a validação de elegibilidade" });
        }
    }

    /// <summary>
    /// Configura as regras de elegibilidade para o funil
    /// </summary>
    [HttpPost("configurar")]
    public IActionResult ConfigurarElegibilidade([FromBody] ConfiguracaoElegibilidade config)
    {
        try
        {
            _validador.ConfigurarElegibilidade(config);

            return Ok(new
            {
                Mensagem = "Configuração de elegibilidade atualizada com sucesso",
                ValidarPerfil = config.ValidarPerfil,
                GruposPermitidos = config.GruposPermitidos,
                TiposUsuario = config.TiposUsuarioPermitidos,
                Regioes = config.RegioesPermitidas
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao configurar regras de elegibilidade");
            return StatusCode(500, new { Mensagem = "Erro ao processar a configuração de elegibilidade" });
        }
    }
    
    /// <summary>
    /// Teste da validação de elegibilidade com diferentes tipos de usuários
    /// </summary>
    [HttpGet("exemplo")]
    public IActionResult ExemploElegibilidade()
    {
        try
        {
            var resultados = new List<object>();
            
            // Teste com usuário Premium
            var perfilPremium = new PerfilUsuario
            {
                Id = 1,
                TipoUsuario = "Premium",
                Grupos = new List<string> { "Beta Testers" },
                Regiao = "Sudeste"
            };
            var resultadoPremium = _validador.ValidarElegibilidade(1, perfilPremium);
            resultados.Add(new
            {
                Perfil = "Premium/Beta Tester",
                Elegivel = resultadoPremium.EhElegivel,
                Motivo = resultadoPremium.Motivo
            });
            
            // Teste com usuário Básico
            var perfilBasico = new PerfilUsuario
            {
                Id = 2,
                TipoUsuario = "Básico",
                Grupos = new List<string> { "Comum" },
                Regiao = "Sul"
            };
            var resultadoBasico = _validador.ValidarElegibilidade(2, perfilBasico);
            resultados.Add(new
            {
                Perfil = "Básico/Comum",
                Elegivel = resultadoBasico.EhElegivel,
                Motivo = resultadoBasico.Motivo
            });
            
            // Teste com usuário Enterprise
            var perfilEnterprise = new PerfilUsuario
            {
                Id = 3,
                TipoUsuario = "Enterprise",
                Grupos = new List<string> { "Parceiros", "Beta Testers" },
                Regiao = "Nordeste"
            };
            var resultadoEnterprise = _validador.ValidarElegibilidade(3, perfilEnterprise);
            resultados.Add(new
            {
                Perfil = "Enterprise/Parceiros",
                Elegivel = resultadoEnterprise.EhElegivel,
                Motivo = resultadoEnterprise.Motivo
            });
            
            return Ok(new { Resultados = resultados });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar exemplo de elegibilidade");
            return StatusCode(500, new { Mensagem = "Erro ao processar o exemplo" });
        }
    }
}

/// <summary>
/// Modelo para requisição de validação de elegibilidade
/// </summary>
public class RequisicaoElegibilidade
{
    public int UsuarioId { get; set; }
    public PerfilUsuario Perfil { get; set; }
} 