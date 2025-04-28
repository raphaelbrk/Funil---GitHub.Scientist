using System;
using System.Threading.Tasks;
using GitHub;
using Microsoft.Extensions.Logging;

namespace FunilRollout.Services;

/// <summary>
/// Classe para validação personalizada de dados entre implementações antiga e nova
/// </summary>
public class ValidadorRollout
{
    private readonly RolloutFunnel _funnel;
    private readonly ILogger<ValidadorRollout> _logger;
    private readonly Random _random = new();
    
    public ValidadorRollout(RolloutFunnel funnel, ILogger<ValidadorRollout> logger)
    {
        _funnel = funnel;
        _logger = logger;
    }
    
    /// <summary>
    /// Executa o funil de rollout com validação personalizada de campos
    /// </summary>
    public RespostaDados ObterDadosComValidacao(int id)
    {
        return _funnel.Execute<RespostaDados>(
            experimentName: "validacao-dados-cliente",
            controlFunc: () => ServicoAntigo.ObterDados(id),
            candidateFunc: () => ServicoNovo.ObterDados(id),
            additionalContext: new 
            { 
                CamposImportantes = new[] { "nome", "cpf", "endereco" },
                ValidarTipo = true,
                IdRequisicao = Guid.NewGuid()
            }
        );
    }
    
    /// <summary>
    /// Implementação personalizada com comparação de campos específicos
    /// </summary>
    public RespostaDados ObterDadosComComparadorPersonalizado(int id)
    {
        var experimento = Scientist.Science<RespostaDados>("validacao-campos-especificos", experiment =>
        {
            // Informações contextuais
            experiment.AddContext("cliente_id", id);
            experiment.AddContext("timestamp", DateTime.UtcNow);
            
            // Função de comparação personalizada que valida apenas os campos importantes
            experiment.Compare((controle, candidato) => 
            {
                // Se algum for nulo, compara normalmente
                if (controle == null || candidato == null)
                    return controle == candidato;
                
                // Valida campos específicos
                return controle.Nome == candidato.Nome && 
                       controle.CPF == candidato.CPF && 
                       controle.ValorTotal == candidato.ValorTotal;
            });
            
            // Limpeza de dados para log (remove informações sensíveis)
            experiment.Clean(dados => new {
                dados.Id,
                dados.Nome,
                TemCPF = !string.IsNullOrEmpty(dados.CPF),
                TemEndereco = !string.IsNullOrEmpty(dados.Endereco),
                DataProcessamento = dados.DataProcessamento
            });
            
            // Define quando executar com base na porcentagem configurada
            experiment.RunIf(() => _funnel.IsRolloutEnabled() && 
                             _random.Next(100) < _funnel.GetRolloutPercentage());
            
            // Métodos para comparação
            experiment.Use(() => ServicoAntigo.ObterDados(id));
            experiment.Try(() => ServicoNovo.ObterDados(id));
        });
        
        return experimento;
    }
    
    /// <summary>
    /// Método para validar comportamento de exceções
    /// </summary>
    public async Task<bool> ValidarComportamentoComExcecoes(int id)
    {
        return await _funnel.ExecuteAsync<bool>(
            experimentName: "validacao-excecoes",
            controlFunc: async () => {
                try {
                    await ServicoAntigo.ValidarPermissaoAsync(id);
                    return true;
                }
                catch (PermissaoException) {
                    // Esperado em alguns casos
                    return false;
                }
            },
            candidateFunc: async () => {
                try {
                    await ServicoNovo.ValidarPermissaoAsync(id);
                    return true;
                }
                catch (AcessoNegadoException) {
                    // Nova exceção, mas comportamento equivalente
                    return false;
                }
            },
            additionalContext: new { 
                TipoValidacao = "Permissão",
                RequerAutenticacao = true
            }
        );
    }
}

/// <summary>
/// Modelo de dados de exemplo para resposta da API
/// </summary>
public class RespostaDados
{
    public int Id { get; set; }
    public string Nome { get; set; }
    public string CPF { get; set; } 
    public string Endereco { get; set; }
    public decimal ValorTotal { get; set; }
    public DateTime DataProcessamento { get; set; }
    
    // Outros campos não críticos para comparação
    public string Observacoes { get; set; }
    public int? CodigoInterno { get; set; }
}

/// <summary>
/// Exceções de exemplo para demonstração
/// </summary>
public class PermissaoException : Exception
{
    public PermissaoException(string message) : base(message) { }
}

public class AcessoNegadoException : Exception
{
    public AcessoNegadoException(string message) : base(message) { }
}

/// <summary>
/// Classes que simulam os serviços antigo e novo
/// </summary>
public static class ServicoAntigo
{
    public static RespostaDados ObterDados(int id)
    {
        // Implementação simulada
        return new RespostaDados
        {
            Id = id,
            Nome = "Cliente Exemplo",
            CPF = "123.456.789-00",
            Endereco = "Rua Exemplo, 123",
            ValorTotal = 1250.50m,
            DataProcessamento = DateTime.Now,
            Observacoes = "Cliente VIP",
            CodigoInterno = 5001
        };
    }
    
    public static Task<bool> ValidarPermissaoAsync(int id)
    {
        if (id <= 0)
            throw new PermissaoException("Usuário sem permissão");
            
        return Task.FromResult(true);
    }
}

public static class ServicoNovo
{
    public static RespostaDados ObterDados(int id)
    {
        // Implementação simulada - nova versão
        return new RespostaDados
        {
            Id = id,
            Nome = "Cliente Exemplo",
            CPF = "123.456.789-00",
            Endereco = "Rua Exemplo, 123",
            ValorTotal = 1250.50m,
            DataProcessamento = DateTime.Now,
            Observacoes = "Cliente VIP - Nova API",
            CodigoInterno = 10001 // Código diferente, mas não relevante para a validação
        };
    }
    
    public static Task<bool> ValidarPermissaoAsync(int id)
    {
        if (id <= 0)
            throw new AcessoNegadoException("Acesso negado ao recurso");
            
        return Task.FromResult(true);
    }
} 