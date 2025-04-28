using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GitHub;
using Microsoft.Extensions.Logging;

namespace FunilRollout.Services;

/// <summary>
/// Validador especializado para documentos como CPF
/// </summary>
public class ValidadorDocumentos
{
    private readonly RolloutFunnel _funnel;
    private readonly ILogger<ValidadorDocumentos> _logger;

    public ValidadorDocumentos(RolloutFunnel funnel, ILogger<ValidadorDocumentos> logger)
    {
        _funnel = funnel;
        _logger = logger;
    }

    /// <summary>
    /// Compara a validação de CPF entre implementações antiga e nova
    /// </summary>
    public bool ValidarCPF(string cpf)
    {
        return _funnel.Execute<bool>(
            experimentName: "validacao-cpf",
            controlFunc: () => ServicoDocumentosAntigo.ValidarCPF(cpf),
            candidateFunc: () => ServicoDocumentosNovo.ValidarCPF(cpf),
            additionalContext: new
            {
                CPF = AnonymizeCPF(cpf),  // Anonimiza o CPF para logs
                DataValidacao = DateTime.UtcNow
            }
        );
    }

    /// <summary>
    /// Validação avançada de CPF com comparação personalizada
    /// </summary>
    public DadosValidacaoCPF ValidarCPFAvancado(string cpf)
    {
        var experimento = Scientist.Science<DadosValidacaoCPF>("validacao-cpf-avancada", experiment =>
        {
            // Adiciona contexto para análise
            experiment.AddContext("cpf_anonimizado", AnonymizeCPF(cpf));
            experiment.AddContext("cpf_formato", Regex.IsMatch(cpf, @"^\d{3}\.\d{3}\.\d{3}-\d{2}$") ? "formatado" : "sem formato");

            // Define comparação personalizada
            experiment.Compare((controle, candidato) =>
            {
                // Se ambos forem nulos ou válidos/inválidos da mesma forma
                if (controle == null || candidato == null)
                    return controle == candidato;

                // Comparação principal: ambos devem concordar se o CPF é válido
                if (controle.EhValido != candidato.EhValido)
                    return false;

                // Se ambos consideram inválido, não importa o motivo
                if (!controle.EhValido && !candidato.EhValido)
                    return true;

                // Verificações adicionais só se ambos considerarem válido
                if (controle.EhValido && candidato.EhValido)
                {
                    // Verificar se os dígitos verificadores são calculados da mesma forma
                    return controle.DigitoVerificador1 == candidato.DigitoVerificador1 &&
                           controle.DigitoVerificador2 == candidato.DigitoVerificador2;
                }

                return true;
            });

            // Limpeza para logs (remove dados sensíveis)
            experiment.Clean(dados => new
            {
                dados.EhValido,
                dados.MotivoInvalidez,
                TemDigitos = dados.DigitoVerificador1 != null && dados.DigitoVerificador2 != null
            });

            // Executa as implementações
            experiment.Use(() => ServicoDocumentosAntigo.ValidarCPFAvancado(cpf));
            experiment.Try(() => ServicoDocumentosNovo.ValidarCPFAvancado(cpf));
        });

        return experimento;
    }

    /// <summary>
    /// Validação do formato do CPF
    /// </summary>
    public bool ValidarFormatoCPF(string cpf)
    {
        return _funnel.Execute<bool>(
            experimentName: "validacao-formato-cpf",
            controlFunc: () => ServicoDocumentosAntigo.ValidarFormatoCPF(cpf),
            candidateFunc: () => ServicoDocumentosNovo.ValidarFormatoCPF(cpf),
            additionalContext: new
            {
                CPFMascarado = AnonymizeCPF(cpf),
                TemFormato = cpf.Contains(".") || cpf.Contains("-")
            }
        );
    }

    /// <summary>
    /// Anonimiza o CPF para logs
    /// </summary>
    private string AnonymizeCPF(string cpf)
    {
        if (string.IsNullOrEmpty(cpf))
            return string.Empty;

        // Remove formatação
        string cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
        
        if (cpfLimpo.Length < 11)
            return "CPF_INVALIDO";

        // Mantém apenas os 3 primeiros e 2 últimos dígitos
        return $"{cpfLimpo.Substring(0, 3)}****{cpfLimpo.Substring(9, 2)}";
    }
}

/// <summary>
/// Modelo de dados para resultados de validação de CPF
/// </summary>
public class DadosValidacaoCPF
{
    public bool EhValido { get; set; }
    public string MotivoInvalidez { get; set; }
    public int? DigitoVerificador1 { get; set; }
    public int? DigitoVerificador2 { get; set; }
}

/// <summary>
/// Simulação do serviço antigo de validação de documentos
/// </summary>
public static class ServicoDocumentosAntigo
{
    public static bool ValidarCPF(string cpf)
    {
        // Implementação antiga simplificada de validação de CPF
        if (string.IsNullOrEmpty(cpf))
            return false;

        string cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
        
        if (cpfLimpo.Length != 11)
            return false;

        // Verifica CPFs conhecidamente inválidos
        if (cpfLimpo == "00000000000" || cpfLimpo == "11111111111")
            return false;

        // Lógica simplificada para exemplo
        return true;
    }
    
    public static DadosValidacaoCPF ValidarCPFAvancado(string cpf)
    {
        if (string.IsNullOrEmpty(cpf))
        {
            return new DadosValidacaoCPF 
            { 
                EhValido = false, 
                MotivoInvalidez = "CPF em branco" 
            };
        }

        string cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
        
        if (cpfLimpo.Length != 11)
        {
            return new DadosValidacaoCPF 
            { 
                EhValido = false, 
                MotivoInvalidez = "CPF deve ter 11 dígitos" 
            };
        }

        // Verifica CPFs conhecidamente inválidos (todos os dígitos iguais)
        if (cpfLimpo == "00000000000" || cpfLimpo == "11111111111")
        {
            return new DadosValidacaoCPF 
            { 
                EhValido = false, 
                MotivoInvalidez = "CPF inválido - dígitos repetidos" 
            };
        }

        // Simulação de cálculo de dígitos verificadores (simplificado)
        int digito1 = 1; // Valor simulado
        int digito2 = 2; // Valor simulado

        return new DadosValidacaoCPF
        {
            EhValido = true,
            DigitoVerificador1 = digito1,
            DigitoVerificador2 = digito2
        };
    }
    
    public static bool ValidarFormatoCPF(string cpf)
    {
        if (string.IsNullOrEmpty(cpf))
            return false;
            
        // Verifica se o formato é 123.456.789-01
        return Regex.IsMatch(cpf, @"^\d{3}\.\d{3}\.\d{3}-\d{2}$");
    }
}

/// <summary>
/// Simulação do serviço novo de validação de documentos
/// </summary>
public static class ServicoDocumentosNovo
{
    public static bool ValidarCPF(string cpf)
    {
        // Nova implementação com pequenas diferenças
        if (string.IsNullOrWhiteSpace(cpf))
            return false;

        string cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
        
        if (cpfLimpo.Length != 11)
            return false;

        // Verifica mais CPFs inválidos
        if (cpfLimpo == "00000000000" || cpfLimpo == "11111111111" || 
            cpfLimpo == "22222222222" || cpfLimpo == "33333333333")
            return false;

        // Implementação mais robusta (simulada)
        return true;
    }
    
    public static DadosValidacaoCPF ValidarCPFAvancado(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
        {
            return new DadosValidacaoCPF 
            { 
                EhValido = false, 
                MotivoInvalidez = "CPF não informado" // Mensagem diferente
            };
        }

        string cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
        
        if (cpfLimpo.Length != 11)
        {
            return new DadosValidacaoCPF 
            { 
                EhValido = false, 
                MotivoInvalidez = "CPF deve conter 11 dígitos" // Texto ligeiramente diferente
            };
        }

        // Verifica CPFs conhecidamente inválidos (mais verificações)
        if (cpfLimpo == "00000000000" || cpfLimpo == "11111111111" ||
            cpfLimpo == "22222222222" || cpfLimpo == "33333333333")
        {
            return new DadosValidacaoCPF 
            { 
                EhValido = false, 
                MotivoInvalidez = "CPF com dígitos repetidos não é válido" // Mensagem mais detalhada
            };
        }

        // Cálculo dos mesmos dígitos verificadores da implementação antiga
        int digito1 = 1;
        int digito2 = 2;

        return new DadosValidacaoCPF
        {
            EhValido = true,
            DigitoVerificador1 = digito1,
            DigitoVerificador2 = digito2
        };
    }
    
    public static bool ValidarFormatoCPF(string cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf))
            return false;
            
        // Nova implementação pode usar abordagem diferente
        // Mas mantém a compatibilidade de resultado
        return Regex.IsMatch(cpf, @"^\d{3}\.\d{3}\.\d{3}-\d{2}$");
    }
} 