using System;
using System.Threading.Tasks;
using FunilRollout.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace FunilRollout.Controllers;

/// <summary>
/// Controlador para demonstrar o uso do ValidadorRollout
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ValidacaoController : ControllerBase
{
    private readonly ValidadorRollout _validador;
    private readonly ILogger<ValidacaoController> _logger;

    public ValidacaoController(ValidadorRollout validador, ILogger<ValidacaoController> logger)
    {
        _validador = validador;
        _logger = logger;
    }

    /// <summary>
    /// Endpoint que demonstra a validação básica de dados
    /// </summary>
    [HttpGet("dados/{id:int}")]
    public IActionResult ObterDados(int id)
    {
        try
        {
            var resultado = _validador.ObterDadosComValidacao(id);
            return Ok(new { 
                Dados = resultado,
                Mensagem = "Dados obtidos com sucesso"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dados para ID {Id}", id);
            return StatusCode(500, new { Mensagem = "Erro ao processar a requisição" });
        }
    }

    /// <summary>
    /// Endpoint que demonstra o uso do comparador personalizado
    /// </summary>
    [HttpGet("campos-personalizados/{id:int}")]
    public IActionResult ObterDadosComComparadorPersonalizado(int id)
    {
        try
        {
            var resultado = _validador.ObterDadosComComparadorPersonalizado(id);
            return Ok(new { 
                Dados = resultado,
                Mensagem = "Dados comparados com validação personalizada"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao obter dados com comparador personalizado para ID {Id}", id);
            return StatusCode(500, new { Mensagem = "Erro ao processar a requisição" });
        }
    }

    /// <summary>
    /// Endpoint que demonstra a validação de comportamento de exceções
    /// </summary>
    [HttpGet("permissao/{id:int}")]
    public async Task<IActionResult> ValidarPermissao(int id)
    {
        try
        {
            var resultado = await _validador.ValidarComportamentoComExcecoes(id);
            return Ok(new { 
                TemPermissao = resultado,
                Mensagem = resultado 
                    ? "Usuário tem permissão" 
                    : "Usuário não tem permissão"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar permissão para ID {Id}", id);
            return StatusCode(500, new { Mensagem = "Erro ao processar a requisição" });
        }
    }
} 