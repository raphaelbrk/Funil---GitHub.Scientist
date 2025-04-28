using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FunilRollout.Services;

namespace FunilRollout.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentosController : ControllerBase
{
    private readonly ValidadorDocumentos _validador;
    private readonly ILogger<DocumentosController> _logger;
    
    public DocumentosController(ValidadorDocumentos validador, ILogger<DocumentosController> logger)
    {
        _validador = validador;
        _logger = logger;
    }
    
    /// <summary>
    /// Validação simples de CPF
    /// </summary>
    [HttpGet("validar-cpf")]
    public IActionResult ValidarCPF([FromQuery] string cpf)
    {
        try
        {
            if (string.IsNullOrEmpty(cpf))
            {
                return BadRequest(new { Mensagem = "CPF não informado" });
            }
            
            bool resultado = _validador.ValidarCPF(cpf);
            
            return Ok(new {
                CPF = cpf,
                Valido = resultado,
                Mensagem = resultado ? "CPF válido" : "CPF inválido"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar CPF: {CPF}", cpf);
            return StatusCode(500, new { Mensagem = "Erro ao processar a validação" });
        }
    }
    
    /// <summary>
    /// Validação avançada de CPF
    /// </summary>
    [HttpGet("validar-cpf-avancado")]
    public IActionResult ValidarCPFAvancado([FromQuery] string cpf)
    {
        try
        {
            if (string.IsNullOrEmpty(cpf))
            {
                return BadRequest(new { Mensagem = "CPF não informado" });
            }
            
            var resultado = _validador.ValidarCPFAvancado(cpf);
            
            return Ok(new {
                CPF = cpf,
                Valido = resultado.EhValido,
                Motivo = resultado.MotivoInvalidez,
                TemDigitosVerificadores = resultado.DigitoVerificador1.HasValue && resultado.DigitoVerificador2.HasValue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar CPF avançado: {CPF}", cpf);
            return StatusCode(500, new { Mensagem = "Erro ao processar a validação" });
        }
    }
    
    /// <summary>
    /// Validação do formato do CPF
    /// </summary>
    [HttpGet("validar-formato-cpf")]
    public IActionResult ValidarFormatoCPF([FromQuery] string cpf)
    {
        try
        {
            if (string.IsNullOrEmpty(cpf))
            {
                return BadRequest(new { Mensagem = "CPF não informado" });
            }
            
            bool resultado = _validador.ValidarFormatoCPF(cpf);
            
            return Ok(new {
                CPF = cpf,
                FormatoValido = resultado,
                Mensagem = resultado ? "Formato de CPF válido (xxx.xxx.xxx-xx)" : "Formato de CPF inválido"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar formato do CPF: {CPF}", cpf);
            return StatusCode(500, new { Mensagem = "Erro ao processar a validação" });
        }
    }
} 