using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using FunilRollout.Services;
using System.Threading.Tasks;

namespace FunilRollout.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FunilAvancadoController : ControllerBase
{
    private readonly FunilAvancado _funilAvancado;
    private readonly ILogger<FunilAvancadoController> _logger;

    public FunilAvancadoController(
        FunilAvancado funilAvancado,
        ILogger<FunilAvancadoController> logger)
    {
        _funilAvancado = funilAvancado;
        _logger = logger;
    }

    /// <summary>
    /// Executa uma operação com rollout completo unificado
    /// </summary>
    [HttpPost("executar")]
    public IActionResult ExecutarComRollout([FromBody] RequisicaoFunilAvancado requisicao)
    {
        try
        {
            // Monta os parâmetros de validação
            var parametros = new ParametrosValidacao
            {
                UsuarioId = requisicao.UsuarioId,
                TipoUsuario = requisicao.TipoUsuario,
                DadosComportamentais = requisicao.DadosComportamentais,
                DadosContextuais = requisicao.DadosContextuais
            };

            // Executa com o funil unificado que integra porcentagem e critérios
            var resultado = _funilAvancado.ExecutarComValidacao<RespostaExecucao>(
                experimentName: "funil-unificado-" + requisicao.OperacaoId,
                parametrosValidacao: parametros,
                controlFunc: () => ExecutarImplementacaoAntiga(requisicao),
                candidatoFunc: () => ExecutarImplementacaoNova(requisicao)
            );

            return Ok(new
            {
                Sucesso = true,
                Dados = resultado,
                OperacaoId = requisicao.OperacaoId,
                Mensagem = "Operação executada com funil unificado"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar operação com funil unificado para o usuário {UsuarioId}", requisicao.UsuarioId);
            return StatusCode(500, new { Mensagem = "Erro ao executar operação com funil unificado" });
        }
    }

    /// <summary>
    /// Executa uma operação assíncrona com rollout completo unificado
    /// </summary>
    [HttpPost("executar-async")]
    public async Task<IActionResult> ExecutarComRolloutAsync([FromBody] RequisicaoFunilAvancado requisicao)
    {
        try
        {
            // Monta os parâmetros de validação
            var parametros = new ParametrosValidacao
            {
                UsuarioId = requisicao.UsuarioId,
                TipoUsuario = requisicao.TipoUsuario,
                DadosComportamentais = requisicao.DadosComportamentais,
                DadosContextuais = requisicao.DadosContextuais
            };

            // Executa com o funil unificado que integra porcentagem e critérios
            var resultado = await _funilAvancado.ExecutarComValidacaoAsync<RespostaExecucao>(
                experimentName: "funil-unificado-async-" + requisicao.OperacaoId,
                parametrosValidacao: parametros,
                controlFunc: () => ExecutarImplementacaoAntigaAsync(requisicao),
                candidatoFunc: () => ExecutarImplementacaoNovaAsync(requisicao)
            );

            return Ok(new
            {
                Sucesso = true,
                Dados = resultado,
                OperacaoId = requisicao.OperacaoId,
                Mensagem = "Operação assíncrona executada com funil unificado"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar operação assíncrona com funil unificado para o usuário {UsuarioId}", requisicao.UsuarioId);
            return StatusCode(500, new { Mensagem = "Erro ao executar operação assíncrona com funil unificado" });
        }
    }

    /// <summary>
    /// Configura as regras de elegibilidade para o funil avançado unificado
    /// </summary>
    [HttpPost("configurar")]
    public IActionResult ConfigurarFunilAvancado([FromBody] ConfiguracaoAvancada config)
    {
        try
        {
            _funilAvancado.ConfigurarValidacaoAvancada(config);

            return Ok(new
            {
                Mensagem = "Configuração do funil avançado atualizada com sucesso",
                ConfiguracaoAtiva = config.ValidacaoAtiva,
                Porcentagem = config.Porcentagem,
                MultiplosCriterios = config.MultiplosCriterios,
                CriteriosDefinidos = config.CriteriosPermitidos?.Count > 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao configurar regras do funil avançado");
            return StatusCode(500, new { Mensagem = "Erro ao processar a configuração do funil avançado" });
        }
    }
    
    /// <summary>
    /// Teste do funil avançado com diferentes tipos de usuários e contextos
    /// </summary>
    [HttpGet("exemplo")]
    public IActionResult ExemploFunilAvancado()
    {
        try
        {
            var resultados = new List<object>();
            
            // Exemplo com usuário Premium
            var requisicaoPremium = new RequisicaoFunilAvancado
            {
                UsuarioId = 1,
                OperacaoId = "op-premium",
                TipoUsuario = "Premium",
                DadosContextuais = new Dictionary<string, object>
                {
                    { "Regiao", "Sudeste" }
                },
                DadosComportamentais = new Dictionary<string, object>
                {
                    { "HistoricoCompras", true }
                }
            };
            
            var resultadoPremium = _funilAvancado.ExecutarComValidacao<RespostaExecucao>(
                "exemplo-premium",
                new ParametrosValidacao
                {
                    UsuarioId = requisicaoPremium.UsuarioId,
                    TipoUsuario = requisicaoPremium.TipoUsuario,
                    DadosComportamentais = requisicaoPremium.DadosComportamentais,
                    DadosContextuais = requisicaoPremium.DadosContextuais
                },
                () => ExecutarImplementacaoAntiga(requisicaoPremium),
                () => ExecutarImplementacaoNova(requisicaoPremium)
            );
            
            resultados.Add(new
            {
                Perfil = "Premium/Sudeste/Com Histórico",
                Resultado = resultadoPremium
            });
            
            // Exemplo com usuário Básico
            var requisicaoBasico = new RequisicaoFunilAvancado
            {
                UsuarioId = 2,
                OperacaoId = "op-basico",
                TipoUsuario = "Básico",
                DadosContextuais = new Dictionary<string, object>
                {
                    { "Regiao", "Sul" }
                },
                DadosComportamentais = new Dictionary<string, object>
                {
                    { "HistoricoCompras", false }
                }
            };
            
            var resultadoBasico = _funilAvancado.ExecutarComValidacao<RespostaExecucao>(
                "exemplo-basico",
                new ParametrosValidacao
                {
                    UsuarioId = requisicaoBasico.UsuarioId,
                    TipoUsuario = requisicaoBasico.TipoUsuario,
                    DadosComportamentais = requisicaoBasico.DadosComportamentais,
                    DadosContextuais = requisicaoBasico.DadosContextuais
                },
                () => ExecutarImplementacaoAntiga(requisicaoBasico),
                () => ExecutarImplementacaoNova(requisicaoBasico)
            );
            
            resultados.Add(new
            {
                Perfil = "Básico/Sul/Sem Histórico",
                Resultado = resultadoBasico
            });
            
            return Ok(new { Resultados = resultados });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao executar exemplo de funil avançado");
            return StatusCode(500, new { Mensagem = "Erro ao processar o exemplo" });
        }
    }
    
    // Métodos privados para simulação das implementações antigas e novas
    
    private RespostaExecucao ExecutarImplementacaoAntiga(RequisicaoFunilAvancado requisicao)
    {
        // Simulação da implementação antiga - na prática chamaria outro serviço
        return new RespostaExecucao
        {
            Resultado = $"Resultado OLD para {requisicao.TipoUsuario}",
            DataProcessamento = DateTime.UtcNow,
            Versao = "1.0",
            DetalhesExtras = new Dictionary<string, object>
            {
                { "Origem", "Implementação Original" },
                { "TipoOperacao", "Simulação" }
            }
        };
    }
    
    private RespostaExecucao ExecutarImplementacaoNova(RequisicaoFunilAvancado requisicao)
    {
        // Simulação da implementação nova - na prática implementaria a nova lógica
        return new RespostaExecucao
        {
            Resultado = $"Resultado NEW para {requisicao.TipoUsuario} - com melhorias",
            DataProcessamento = DateTime.UtcNow,
            Versao = "2.0",
            DetalhesExtras = new Dictionary<string, object>
            {
                { "Origem", "Nova Implementação" },
                { "TipoOperacao", "Implementação Otimizada" },
                { "ContextoRegional", requisicao.DadosContextuais?.ContainsKey("Regiao") == true ? 
                    requisicao.DadosContextuais["Regiao"] : "N/A" }
            }
        };
    }
    
    private async Task<RespostaExecucao> ExecutarImplementacaoAntigaAsync(RequisicaoFunilAvancado requisicao)
    {
        // Simulação da implementação antiga assíncrona
        await Task.Delay(50); // Simula algum processamento
        return ExecutarImplementacaoAntiga(requisicao);
    }
    
    private async Task<RespostaExecucao> ExecutarImplementacaoNovaAsync(RequisicaoFunilAvancado requisicao)
    {
        // Simulação da implementação nova assíncrona
        await Task.Delay(30); // Simula algum processamento (mais rápido)
        return ExecutarImplementacaoNova(requisicao);
    }
}

/// <summary>
/// Modelo para requisição do funil avançado
/// </summary>
public class RequisicaoFunilAvancado
{
    public int UsuarioId { get; set; }
    public string OperacaoId { get; set; }
    public string TipoUsuario { get; set; }
    public Dictionary<string, object> DadosComportamentais { get; set; }
    public Dictionary<string, object> DadosContextuais { get; set; }
}

/// <summary>
/// Modelo para resposta da execução
/// </summary>
public class RespostaExecucao
{
    public string Resultado { get; set; }
    public DateTime DataProcessamento { get; set; }
    public string Versao { get; set; }
    public Dictionary<string, object> DetalhesExtras { get; set; }
} 