using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitHub;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Linq;

namespace FunilRollout.Services;

/// <summary>
/// Implementação avançada que integra o RolloutFunnel com validações personalizadas
/// dentro de um fluxo unificado de rollout progressivo
/// </summary>
public class FunilAvancado
{
    private readonly RolloutFunnel _rolloutFunnel;
    private readonly RedisConfigProvider _redisConfig;
    private readonly ILogger<FunilAvancado> _logger;

    // Chaves de configuração no Redis
    private const string CHAVE_CONFIG_ATIVA = "rollout:config_ativa";
    private const string CHAVE_CRITERIOS_USUARIOS = "rollout:criterios_usuarios";
    private const string CHAVE_MULTIPLOS_CRITERIOS = "rollout:multiplos_criterios";
    private const string CHAVE_LISTA_CPFS = "rollout:cpf_list";

    public FunilAvancado(
        RolloutFunnel rolloutFunnel,
        RedisConfigProvider redisConfig,
        ILogger<FunilAvancado> logger)
    {
        _rolloutFunnel = rolloutFunnel;
        _redisConfig = redisConfig;
        _logger = logger;
    }

    /// <summary>
    /// MÉTODO PRINCIPAL: Executa a operação usando Scientist.net integrado com critérios personalizados
    /// </summary>
    /// <typeparam name="T">Tipo de retorno</typeparam>
    /// <param name="experimentName">Nome do experimento</param>
    /// <param name="parametrosValidacao">Parâmetros para validar elegibilidade</param>
    /// <param name="controlFunc">Método antigo (controle)</param>
    /// <param name="candidatoFunc">Método novo (candidato)</param>
    /// <returns>Resultado da execução do método de controle</returns>
    public T ExecutarComValidacao<T>(
        string experimentName,
        ParametrosValidacao parametrosValidacao,
        Func<T> controlFunc,
        Func<T> candidatoFunc)
    {
        // Verifica se o usuário é elegível com os múltiplos critérios
        // antes mesmo de passar para o RolloutFunnel
        bool usuarioElegivel = ValidarElegibilidadeCompleta(parametrosValidacao);
        
        // Se não for elegível, já retorna o resultado do método antigo sem executar o funil
        if (!usuarioElegivel)
        {
            return controlFunc();
        }
        
        // Adiciona contexto detalhado para análise
        var contextoDetalhado = CriarContextoDetalhado(parametrosValidacao);
        
        // Usa o RolloutFunnel com o usuário elegível para fazer o rollout progressivo
        return _rolloutFunnel.Execute(
            experimentName: experimentName,
            controlFunc: controlFunc,
            candidateFunc: candidatoFunc,
            additionalContext: contextoDetalhado
        );
    }
    
    /// <summary>
    /// Versão assíncrona para execução com validação completa
    /// </summary>
    public async Task<T> ExecutarComValidacaoAsync<T>(
        string experimentName,
        ParametrosValidacao parametrosValidacao,
        Func<Task<T>> controlFunc,
        Func<Task<T>> candidatoFunc)
    {
        // Verifica se o usuário é elegível com os múltiplos critérios
        bool usuarioElegivel = ValidarElegibilidadeCompleta(parametrosValidacao);
        
        // Se não for elegível, já retorna o resultado do método antigo sem executar o funil
        if (!usuarioElegivel)
        {
            return await controlFunc();
        }
        
        // Adiciona contexto detalhado para análise
        var contextoDetalhado = CriarContextoDetalhado(parametrosValidacao);
        
        // Usa o RolloutFunnel com o usuário elegível
        return await _rolloutFunnel.ExecuteAsync(
            experimentName: experimentName,
            controlFunc: controlFunc,
            candidateFunc: candidatoFunc,
            additionalContext: contextoDetalhado
        );
    }
    
    /// <summary>
    /// Valida se o usuário é elegível para participar do experimento
    /// Combina validação por porcentagem com critérios adicionais
    /// </summary>
    private bool ValidarElegibilidadeCompleta(ParametrosValidacao parametros)
    {
        try
        {
            // Verifica se a validação avançada está ativa
            if (!IsValidacaoCriteriosAtiva())
            {
                // Se a validação por critérios não está ativa, usa apenas a validação por porcentagem
                return IsUsuarioElegivelPorPorcentagem(parametros.UsuarioId);
            }
            
            // Verifica se o usuário está dentro da porcentagem de rollout
            if (!IsUsuarioElegivelPorPorcentagem(parametros.UsuarioId))
            {
                return false;
            }
            
            // Verifica se precisa avaliar múltiplos critérios
            if (IsMultiplosCriteriosHabilitados())
            {
                return ValidarCriteriosFuncionais(parametros) && 
                       ValidarCriteriosComportamentais(parametros) &&
                       ValidarCriteriosContextuais(parametros);
            }
            else
            {
                // Se não há múltiplos critérios, valida apenas critérios funcionais básicos
                return ValidarCriteriosFuncionais(parametros);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar elegibilidade para o usuário {UsuarioId}", parametros.UsuarioId);
            
            // Em caso de erro, faz o fallback para implementação antiga
            return false;
        }
    }
    
    /// <summary>
    /// Verifica se o usuário está dentro da porcentagem de rollout configurada
    /// </summary>
    private bool IsUsuarioElegivelPorPorcentagem(int usuarioId)
    {
        // Primeiro verifica se o rollout está habilitado
        if (!_rolloutFunnel.IsRolloutEnabled())
        {
            return false;
        }
        
        // Obtém a porcentagem atual
        int percentual = _rolloutFunnel.GetRolloutPercentage();
        
        // Se o percentual for 0 ou menor, ninguém é elegível
        if (percentual <= 0)
        {
            return false;
        }
        
        // Se o percentual for 100 ou maior, todos são elegíveis
        if (percentual >= 100)
        {
            return true;
        }
        
        // Cria um Random baseado no ID do usuário para garantir consistência
        // de forma que o mesmo usuário sempre tenha o mesmo resultado
        int semente = usuarioId.GetHashCode();
        Random randomUsuario = new Random(semente);
        
        // Retorna true se o número gerado estiver dentro da porcentagem
        return randomUsuario.Next(100) < percentual;
    }
    
    /// <summary>
    /// Valida critérios funcionais (tipo de usuário, grupos, etc.)
    /// </summary>
    private bool ValidarCriteriosFuncionais(ParametrosValidacao parametros)
    {
        // Obtém configuração
        string criteriosPermitidos = _redisConfig.GetConfigValue(CHAVE_CRITERIOS_USUARIOS, "");
        
        // Se não há critérios, todos são válidos
        if (string.IsNullOrEmpty(criteriosPermitidos))
        {
            return true;
        }
        
        // Divide critérios e converte para HashSet para busca mais eficiente
        HashSet<string> criterios = new HashSet<string>(
            criteriosPermitidos.Split(','), 
            StringComparer.OrdinalIgnoreCase
        );
        
        // Verifica se o tipo de usuário está na lista de permitidos
        if (criterios.Count > 0 && !string.IsNullOrEmpty(parametros.TipoUsuario))
        {
            if (!criterios.Contains(parametros.TipoUsuario))
            {
                return false;
            }
        }
        
        // Check if the CPF is in the allowed list
        if (parametros.DadosContextuais != null && 
            parametros.DadosContextuais.ContainsKey("CPF"))
        {
            string cpf = parametros.DadosContextuais["CPF"]?.ToString();
            if (!string.IsNullOrEmpty(cpf) && !ValidateCpfInList(cpf))
            {
                return false;
            }
        }
        
        // Check with the LinkedPerson service if the user is eligible
        if (parametros.DadosContextuais != null && 
            parametros.DadosContextuais.ContainsKey("CheckLinkedPerson") && 
            bool.TryParse(parametros.DadosContextuais["CheckLinkedPerson"].ToString(), out bool checkLinked) && 
            checkLinked)
        {
            return ValidateLinkedPersonUser(parametros);
        }
        
        return true;
    }
    
    /// <summary>
    /// Checks if the user's CPF is in the allowed list in Redis
    /// </summary>
    private bool ValidateCpfInList(string cpf)
    {
        try
        {
            // Remove non-numeric characters from CPF
            cpf = new string(cpf.Where(char.IsDigit).ToArray());
            
            // Get the list of allowed CPFs from Redis
            string cpfList = _redisConfig.GetConfigValue(CHAVE_LISTA_CPFS, "");
            
            if (string.IsNullOrEmpty(cpfList))
            {
                return true; // If there's no list, allow all
            }
            
            // Split the list and check if the CPF is contained
            return cpfList.Split(',').Any(c => c.Trim() == cpf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating CPF {CPF} in the list", cpf);
            return false;
        }
    }
    
    /// <summary>
    /// Checks eligibility using the LinkedPerson service
    /// </summary>
    private bool ValidateLinkedPersonUser(ParametrosValidacao parametros)
    {
        try
        {
            // According to the requirement, should return true if LinkedPerson service validates the user
            if (parametros.DadosContextuais == null)
            {
                return false;
            }
            
            // Get data for LinkedPerson service query
            string cpf = parametros.DadosContextuais.ContainsKey("CPF") ? 
                        parametros.DadosContextuais["CPF"]?.ToString() : null;
            
            // To avoid invalid queries
            if (string.IsNullOrEmpty(cpf))
            {
                return false;
            }
            
            // Here would call the real LinkedPerson service
            // In this example we simulate validation based on CPF
            // In production, you would implement the real service call
            bool userEligible = SimulateLinkedPersonQuery(cpf, parametros.UsuarioId);
            
            _logger.LogInformation(
                "LinkedPerson query for CPF {CPF} (user {UserId}): Eligible = {Eligible}",
                cpf,
                parametros.UsuarioId,
                userEligible);
                
            return userEligible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying LinkedPerson service for user {UserId}", parametros.UsuarioId);
            return false;
        }
    }
    
    /// <summary>
    /// Simulates the query to the LinkedPerson service
    /// In production, this method would be replaced by the real service call
    /// </summary>
    private bool SimulateLinkedPersonQuery(string cpf, int userId)
    {
        // Basic simulation for testing purposes
        // Returns true for CPFs ending with even digits
        if (!string.IsNullOrEmpty(cpf) && cpf.Length >= 1)
        {
            int lastDigit = int.Parse(cpf.Substring(cpf.Length - 1));
            return lastDigit % 2 == 0;
        }
        
        // Or based on user ID for tests
        return userId % 2 == 0;
    }
    
    /// <summary>
    /// Valida critérios comportamentais (histórico de compras, tempo de uso, etc.)
    /// </summary>
    private bool ValidarCriteriosComportamentais(ParametrosValidacao parametros)
    {
        // Implementação básica - na prática você pode adicionar regras mais complexas
        
        // Exemplo: verificar se o usuário tem histórico de compras
        if (parametros.DadosComportamentais != null && 
            parametros.DadosComportamentais.ContainsKey("HistoricoCompras"))
        {
            bool temHistorico = bool.TryParse(
                parametros.DadosComportamentais["HistoricoCompras"].ToString(), 
                out bool resultado) && resultado;
                
            if (!temHistorico)
            {
                return false;
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Valida critérios contextuais (localização, dispositivo, etc.)
    /// </summary>
    private bool ValidarCriteriosContextuais(ParametrosValidacao parametros)
    {
        // Implementação básica - na prática você pode adicionar regras mais complexas
        
        // Exemplo: verificar a região do usuário
        if (parametros.DadosContextuais != null && 
            parametros.DadosContextuais.ContainsKey("Regiao"))
        {
            string regiao = parametros.DadosContextuais["Regiao"]?.ToString();
            string regioesPermitidas = _redisConfig.GetConfigValue("rollout:regioes_permitidas", "");
            
            if (!string.IsNullOrEmpty(regioesPermitidas) && !string.IsNullOrEmpty(regiao))
            {
                HashSet<string> regioes = new HashSet<string>(
                    regioesPermitidas.Split(','), 
                    StringComparer.OrdinalIgnoreCase
                );
                
                if (regioes.Count > 0 && !regioes.Contains(regiao))
                {
                    return false;
                }
            }
        }
        
        return true;
    }
    
    /// <summary>
    /// Cria um contexto detalhado para análise do experimento
    /// </summary>
    private object CriarContextoDetalhado(ParametrosValidacao parametros)
    {
        // Mapeia apenas propriedades relevantes para o contexto
        // evitando adicionar informações sensíveis
        return new
        {
            UsuarioId = parametros.UsuarioId,
            TipoUsuario = parametros.TipoUsuario,
            TemDadosComportamentais = parametros.DadosComportamentais?.Count > 0,
            TemDadosContextuais = parametros.DadosContextuais?.Count > 0,
            DataExecucao = DateTime.UtcNow
        };
    }
    
    /// <summary>
    /// Configura os parâmetros de validação no Redis
    /// </summary>
    public void ConfigurarValidacaoAvancada(ConfiguracaoAvancada config)
    {
        try
        {
            // Ativa ou desativa a validação por critérios
            _redisConfig.SetConfigValue(CHAVE_CONFIG_ATIVA, config.ValidacaoAtiva.ToString());
            
            // Ativa ou desativa múltiplos critérios
            _redisConfig.SetConfigValue(CHAVE_MULTIPLOS_CRITERIOS, config.MultiplosCriterios.ToString());
            
            // Configura critérios dos usuários
            if (config.CriteriosPermitidos != null)
            {
                _redisConfig.SetConfigValue(CHAVE_CRITERIOS_USUARIOS, 
                    string.Join(",", config.CriteriosPermitidos));
            }
            
            // Configure the allowed CPF list
            if (config.AllowedCpfList != null)
            {
                _redisConfig.SetConfigValue(CHAVE_LISTA_CPFS, 
                    string.Join(",", config.AllowedCpfList));
            }
            
            // Configura também a porcentagem no RolloutFunnel
            _rolloutFunnel.EnableRollout(config.ValidacaoAtiva);
            _rolloutFunnel.SetRolloutPercentage(config.Porcentagem);
            
            _logger.LogInformation(
                "Configuração avançada atualizada: Ativa={Ativa}, Porcentagem={Porcentagem}%, Múltiplos Critérios={MultCriterios}",
                config.ValidacaoAtiva,
                config.Porcentagem,
                config.MultiplosCriterios);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao configurar validação avançada");
            throw;
        }
    }
    
    /// <summary>
    /// Verifica se a validação de critérios está ativa
    /// </summary>
    private bool IsValidacaoCriteriosAtiva()
    {
        string valor = _redisConfig.GetConfigValue(CHAVE_CONFIG_ATIVA, "false");
        return bool.TryParse(valor, out bool resultado) && resultado;
    }
    
    /// <summary>
    /// Verifica se a validação de múltiplos critérios está habilitada
    /// </summary>
    private bool IsMultiplosCriteriosHabilitados()
    {
        string valor = _redisConfig.GetConfigValue(CHAVE_MULTIPLOS_CRITERIOS, "false");
        return bool.TryParse(valor, out bool resultado) && resultado;
    }
}

/// <summary>
/// Parâmetros para validação de elegibilidade
/// </summary>
public class ParametrosValidacao
{
    public int UsuarioId { get; set; }
    public string TipoUsuario { get; set; }
    public Dictionary<string, object> DadosComportamentais { get; set; }
    public Dictionary<string, object> DadosContextuais { get; set; }
}

/// <summary>
/// Configuração para a validação avançada
/// </summary>
public class ConfiguracaoAvancada
{
    public bool ValidacaoAtiva { get; set; } = true;
    public int Porcentagem { get; set; } = 0;
    public bool MultiplosCriterios { get; set; } = false;
    public List<string> CriteriosPermitidos { get; set; }
    public List<string> AllowedCpfList { get; set; }
} 