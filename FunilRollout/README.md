# Funil de Rollout com GitHub.Scientist

Este projeto implementa um sistema de funil de rollout progressivo para comparar implementações antigas e novas de uma API, utilizando a biblioteca GitHub.Scientist e integração com Redis para configuração dinâmica.

## Características

- Rollout progressivo (1%, 2%, 5%, 10%, etc.)
- Configuração via Redis para alteração em tempo real
- Múltiplos Publishers para resultados de experimentos
- Suporte a métodos síncronos e assíncronos
- API RESTful para configuração e monitoramento
- Validação personalizada de campos e comportamentos

## Configuração

### Requisitos

- .NET 8
- Redis (local ou remoto)

### Configuração do Redis

Edite o arquivo `appsettings.json` para configurar a conexão com o Redis:

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  }
}
```

## Como Usar

### Configuração do Rollout

Use a API para configurar o percentual de tráfego direcionado para a nova implementação:

```http
POST /api/rollout/configure
{
  "enabled": true,
  "percentage": 10
}
```

### Verificar Status

```http
GET /api/rollout/status
```

### Alternando Publishers

```http
POST /api/rollout/publisher
{
  "useCustomPublisher": true
}
```

## Validação de CPF

O projeto inclui um exemplo detalhado de como implementar validação de CPF usando o funil de rollout:

### Endpoints para Teste

```http
GET /api/documentos/validar-cpf?cpf=123.456.789-09
GET /api/documentos/validar-cpf-avancado?cpf=123.456.789-09
GET /api/documentos/validar-formato-cpf?cpf=123.456.789-09
```

### Implementação de Validações Personalizadas

O validador de CPF demonstra três técnicas de validação:

1. **Validação Simples**: Compara se ambas as implementações classificam o CPF como válido/inválido

```csharp
// No ValidadorDocumentos.cs
public bool ValidarCPF(string cpf)
{
    return _funnel.Execute<bool>(
        experimentName: "validacao-cpf",
        controlFunc: () => ServicoDocumentosAntigo.ValidarCPF(cpf),
        candidateFunc: () => ServicoDocumentosNovo.ValidarCPF(cpf),
        additionalContext: new { CPF = AnonymizeCPF(cpf) }
    );
}
```

2. **Validação Avançada**: Compara detalhes específicos do resultado usando um comparador customizado

```csharp
experiment.Compare((controle, candidato) =>
{
    // Comparação principal: ambos devem concordar se o CPF é válido
    if (controle.EhValido != candidato.EhValido)
        return false;

    // Se ambos consideram válido, verificar os dígitos verificadores
    if (controle.EhValido && candidato.EhValido)
    {
        return controle.DigitoVerificador1 == candidato.DigitoVerificador1 &&
               controle.DigitoVerificador2 == candidato.DigitoVerificador2;
    }

    return true;
});
```

3. **Anonimização de Dados Sensíveis**: Remove dados sensíveis antes do armazenamento nos logs

```csharp
private string AnonymizeCPF(string cpf)
{
    if (string.IsNullOrEmpty(cpf))
        return string.Empty;

    // Remove formatação
    string cpfLimpo = cpf.Replace(".", "").Replace("-", "").Trim();
    
    // Mantém apenas os primeiros dígitos e os últimos
    return $"{cpfLimpo.Substring(0, 3)}****{cpfLimpo.Substring(9, 2)}";
}
```

## Validação Personalizada de Dados

Para casos onde você precisa de maior controle sobre como os dados são comparados, fornecemos diferentes abordagens:

### 1. Fornecendo Informações de Contexto Adicionais

```csharp
_funnel.Execute<RespostaDados>(
    experimentName: "validacao-dados-cliente",
    controlFunc: () => ServicoAntigo.ObterDados(id),
    candidateFunc: () => ServicoNovo.ObterDados(id),
    additionalContext: new 
    { 
        CamposImportantes = new[] { "nome", "cpf", "endereco" },
        ValidarTipo = true
    }
);
```

### 2. Usando Comparadores Personalizados

Para comparações mais complexas, você pode definir seu próprio comparador:

```csharp
Scientist.Science<RespostaDados>("validacao-especifica", experiment =>
{
    // Função de comparação personalizada
    experiment.Compare((controle, candidato) => 
    {
        // Apenas campos específicos são importantes
        return controle.Nome == candidato.Nome && 
               controle.CPF == candidato.CPF;
    });
    
    // Limpeza de dados sensíveis para logs
    experiment.Clean(dados => new {
        dados.Id,
        TemCPF = !string.IsNullOrEmpty(dados.CPF)
    });
    
    experiment.Use(() => ServicoAntigo.ObterDados(id));
    experiment.Try(() => ServicoNovo.ObterDados(id));
});
```

### 3. Validando Comportamento com Exceções

É possível também comparar o comportamento quando ocorrem exceções:

```csharp
await _funnel.ExecuteAsync<bool>(
    experimentName: "validacao-excecoes",
    controlFunc: async () => {
        try {
            await ServicoAntigo.ValidarPermissaoAsync(id);
            return true;
        }
        catch (PermissaoException) {
            return false; // Comportamento esperado
        }
    },
    candidateFunc: async () => {
        try {
            await ServicoNovo.ValidarPermissaoAsync(id);
            return true;
        }
        catch (AcessoNegadoException) {
            return false; // Nova exceção, comportamento equivalente
        }
    }
);
```

### Criando um Publisher Personalizado

Você pode criar um publisher personalizado implementando a interface `IResultPublisher`:

```csharp
public class MeuPublisher : IResultPublisher
{
    public Task Publish<T, TClean>(Result<T, TClean> result)
    {
        // Implemente sua lógica aqui
        return Task.CompletedTask;
    }
}
```

## Considerações de Uso

- Use o funil apenas para operações de leitura, não para operações de escrita
- Monitore os resultados para identificar diferenças entre as implementações
- Aumente o percentual de tráfego gradualmente
- Mantenha ambas as implementações em produção até confirmar a estabilidade da nova versão

## Estrutura do Projeto

- **RolloutFunnel.cs**: Implementação principal do funil de rollout
- **RedisConfigProvider.cs**: Gerencia a configuração via Redis
- **CustomResultPublisher.cs**: Exemplo de publisher personalizado
- **RolloutController.cs**: API para controle e demonstração
- **ValidadorRollout.cs**: Demonstração de validações customizadas
- **ValidadorDocumentos.cs**: Exemplo específico de validação de CPF 