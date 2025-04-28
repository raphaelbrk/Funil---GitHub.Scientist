using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GitHub;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace FunilRollout.Services;

/// <summary>
/// Serviço responsável por determinar elegibilidade para o funil de rollout
/// com validações adicionais além da porcentagem
/// </summary>
public class ValidadorElegibilidadeFunil
{
    private readonly RolloutFunnel _rolloutFunnel;
    private readonly RedisConfigProvider _redisConfig;
    private readonly ILogger<ValidadorElegibilidadeFunil> _logger;
    private readonly Random _random = new();

    // Redis keys para configurações adicionais
    private const string CHAVE_GRUPOS_PERMITIDOS = "rollout:grupos_permitidos";
    private const string CHAVE_TIPOS_USUARIO_PERMITIDOS = "rollout:tipos_usuario";
    private const string CHAVE_FLAG_VALIDAR_PERFIL = "rollout:validar_perfil";
    private const string CHAVE_REGIOES_PERMITIDAS = "rollout:regioes";

    public ValidadorElegibilidadeFunil(
        RolloutFunnel rolloutFunnel,
        RedisConfigProvider redisConfig,
        ILogger<ValidadorElegibilidadeFunil> logger)
    {
        _rolloutFunnel = rolloutFunnel;
        _redisConfig = redisConfig;
        _logger = logger;
    }

    /// <summary>
    /// Determina se o usuário está elegível para participar do experimento
    /// baseado em critérios adicionais além da porcentagem
    /// </summary>
    /// <param name="usuarioId">ID do usuário</param>
    /// <param name="perfilUsuario">Perfil do usuário com informações adicionais</param>
    /// <returns>Resultado da validação de elegibilidade</returns>
    public ResultadoElegibilidade ValidarElegibilidade(int usuarioId, PerfilUsuario perfilUsuario)
    {
        // Primeiro verifica se o rollout está habilitado
        if (!_rolloutFunnel.IsRolloutEnabled())
        {
            return new ResultadoElegibilidade
            {
                EhElegivel = false,
                Motivo = "Rollout desabilitado"
            };
        }

        // Obtém a porcentagem configurada do rollout
        int percentualRollout = _rolloutFunnel.GetRolloutPercentage();
        
        // Verifica se precisa validar o perfil
        bool validarPerfil = DeveValidarPerfil();
        
        try
        {
            // Define a semente para randomização consistente por usuário
            int semente = usuarioId.GetHashCode();
            Random randomUsuario = new Random(semente);
            
            // Verifica o percentual baseado no ID do usuário (consistente)
            bool elegibilidadePercentual = randomUsuario.Next(100) < percentualRollout;
            
            // Se não precisa validar perfil e atende o percentual, está elegível
            if (!validarPerfil && elegibilidadePercentual)
            {
                return new ResultadoElegibilidade
                {
                    EhElegivel = true,
                    Motivo = "Elegível por porcentagem, sem validação de perfil"
                };
            }
            
            // Se não atende o percentual, já retorna não elegível
            if (!elegibilidadePercentual)
            {
                return new ResultadoElegibilidade
                {
                    EhElegivel = false,
                    Motivo = "Não elegível pelo percentual configurado"
                };
            }
            
            // Se chegou aqui, precisa validar perfil e já atende o percentual
            return ValidarPerfilUsuario(perfilUsuario);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar elegibilidade para o usuário {UsuarioId}", usuarioId);
            
            // Em caso de erro, não inclui no experimento
            return new ResultadoElegibilidade
            {
                EhElegivel = false,
                Motivo = "Erro durante validação: " + ex.Message
            };
        }
    }
    
    /// <summary>
    /// Valida a elegibilidade com a nova implementação e a antiga
    /// usando o funil do Scientist
    /// </summary>
    public ResultadoElegibilidade ValidarElegibilidadeComRollout(int usuarioId, PerfilUsuario perfilUsuario)
    {
        return _rolloutFunnel.Execute<ResultadoElegibilidade>(
            experimentName: "validacao-elegibilidade",
            controlFunc: () => ServicoAntigo.ValidarElegibilidade(usuarioId, perfilUsuario),
            candidateFunc: () => ValidarElegibilidade(usuarioId, perfilUsuario),
            additionalContext: new
            {
                UsuarioId = usuarioId,
                TipoUsuario = perfilUsuario.TipoUsuario,
                TemGrupos = perfilUsuario.Grupos?.Count > 0,
                Região = perfilUsuario.Regiao
            }
        );
    }

    /// <summary>
    /// Verifica se deve validar o perfil baseado na configuração
    /// </summary>
    private bool DeveValidarPerfil()
    {
        string valor = _redisConfig.GetConfigValue(CHAVE_FLAG_VALIDAR_PERFIL, "false");
        return bool.TryParse(valor, out bool resultado) && resultado;
    }
    
    /// <summary>
    /// Obtém a lista de grupos permitidos do Redis
    /// </summary>
    private HashSet<string> ObterGruposPermitidos()
    {
        string grupos = _redisConfig.GetConfigValue(CHAVE_GRUPOS_PERMITIDOS, "");
        if (string.IsNullOrWhiteSpace(grupos))
            return new HashSet<string>();
            
        return new HashSet<string>(grupos.Split(','), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Obtém a lista de tipos de usuário permitidos do Redis
    /// </summary>
    private HashSet<string> ObterTiposUsuarioPermitidos()
    {
        string tipos = _redisConfig.GetConfigValue(CHAVE_TIPOS_USUARIO_PERMITIDOS, "");
        if (string.IsNullOrWhiteSpace(tipos))
            return new HashSet<string>();
            
        return new HashSet<string>(tipos.Split(','), StringComparer.OrdinalIgnoreCase);
    }
    
    /// <summary>
    /// Obtém a lista de regiões permitidas do Redis
    /// </summary>
    private HashSet<string> ObterRegioesPermitidas()
    {
        string regioes = _redisConfig.GetConfigValue(CHAVE_REGIOES_PERMITIDAS, "");
        if (string.IsNullOrWhiteSpace(regioes))
            return new HashSet<string>();
            
        return new HashSet<string>(regioes.Split(','), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Valida o perfil do usuário com base nos critérios configurados
    /// </summary>
    private ResultadoElegibilidade ValidarPerfilUsuario(PerfilUsuario perfilUsuario)
    {
        // Obtém as configurações
        HashSet<string> gruposPermitidos = ObterGruposPermitidos();
        HashSet<string> tiposPermitidos = ObterTiposUsuarioPermitidos();
        HashSet<string> regioesPermitidas = ObterRegioesPermitidas();

        // Validação do tipo de usuário
        if (tiposPermitidos.Count > 0 && !tiposPermitidos.Contains(perfilUsuario.TipoUsuario))
        {
            return new ResultadoElegibilidade
            {
                EhElegivel = false,
                Motivo = $"Tipo de usuário não permitido: {perfilUsuario.TipoUsuario}"
            };
        }

        // Validação dos grupos do usuário
        if (gruposPermitidos.Count > 0)
        {
            bool pertenceGrupoPermitido = false;
            
            if (perfilUsuario.Grupos != null)
            {
                foreach (string grupo in perfilUsuario.Grupos)
                {
                    if (gruposPermitidos.Contains(grupo))
                    {
                        pertenceGrupoPermitido = true;
                        break;
                    }
                }
            }
            
            if (!pertenceGrupoPermitido)
            {
                return new ResultadoElegibilidade
                {
                    EhElegivel = false,
                    Motivo = "Usuário não pertence a nenhum grupo permitido"
                };
            }
        }

        // Validação da região
        if (regioesPermitidas.Count > 0 && !regioesPermitidas.Contains(perfilUsuario.Regiao))
        {
            return new ResultadoElegibilidade
            {
                EhElegivel = false,
                Motivo = $"Região não permitida: {perfilUsuario.Regiao}"
            };
        }

        // Se passou por todas as validações, está elegível
        return new ResultadoElegibilidade
        {
            EhElegivel = true,
            Motivo = "Usuário elegível - atende todos os critérios"
        };
    }
    
    /// <summary>
    /// Configura as regras de elegibilidade no Redis
    /// </summary>
    public void ConfigurarElegibilidade(ConfiguracaoElegibilidade config)
    {
        // Configura se deve validar o perfil
        _redisConfig.SetConfigValue(CHAVE_FLAG_VALIDAR_PERFIL, config.ValidarPerfil.ToString());
        
        // Configura grupos permitidos
        if (config.GruposPermitidos != null)
        {
            _redisConfig.SetConfigValue(CHAVE_GRUPOS_PERMITIDOS, string.Join(",", config.GruposPermitidos));
        }
        
        // Configura tipos de usuário permitidos
        if (config.TiposUsuarioPermitidos != null)
        {
            _redisConfig.SetConfigValue(CHAVE_TIPOS_USUARIO_PERMITIDOS, string.Join(",", config.TiposUsuarioPermitidos));
        }
        
        // Configura regiões permitidas
        if (config.RegioesPermitidas != null)
        {
            _redisConfig.SetConfigValue(CHAVE_REGIOES_PERMITIDAS, string.Join(",", config.RegioesPermitidas));
        }
    }
}

/// <summary>
/// Resultado da validação de elegibilidade
/// </summary>
public class ResultadoElegibilidade
{
    public bool EhElegivel { get; set; }
    public string Motivo { get; set; }
}

/// <summary>
/// Modelo de perfil do usuário com informações adicionais
/// </summary>
public class PerfilUsuario
{
    public int Id { get; set; }
    public string TipoUsuario { get; set; } // Ex: "Premium", "Básico", "Enterprise"
    public List<string> Grupos { get; set; } // Ex: "Beta Testers", "Parceiros", etc.
    public string Regiao { get; set; } // Ex: "Sul", "Nordeste", "Sudeste"
}

/// <summary>
/// Modelo para configuração de elegibilidade
/// </summary>
public class ConfiguracaoElegibilidade
{
    public bool ValidarPerfil { get; set; }
    public List<string> GruposPermitidos { get; set; }
    public List<string> TiposUsuarioPermitidos { get; set; }
    public List<string> RegioesPermitidas { get; set; }
}

/// <summary>
/// Simulação de serviço antigo para validação
/// </summary>
public static class ServicoAntigo
{
    public static ResultadoElegibilidade ValidarElegibilidade(int usuarioId, PerfilUsuario perfilUsuario)
    {
        // Implementação simplificada para demonstração
        // Na vida real, a implementação antiga poderia ter lógica diferente
        
        // Verificação básica de tipo de usuário
        if (perfilUsuario.TipoUsuario != "Premium" && perfilUsuario.TipoUsuario != "Enterprise")
        {
            return new ResultadoElegibilidade
            {
                EhElegivel = false,
                Motivo = "Implementação antiga: Apenas usuários Premium e Enterprise são permitidos"
            };
        }

        return new ResultadoElegibilidade
        {
            EhElegivel = true,
            Motivo = "Implementação antiga: Usuário elegível"
        };
    }
} 