# Funil de Rollout com GitHub.Scientist

Este projeto implementa um sistema de funil de rollout progressivo para comparar implementações antigas e novas de uma API, utilizando a biblioteca GitHub.Scientist e integração com Redis para configuração dinâmica.

## Características

- Rollout progressivo (1%, 2%, 5%, 10%, etc.)
- Configuração via Redis para alteração em tempo real
- Múltiplos Publishers para resultados de experimentos
- Suporte a métodos síncronos e assíncronos
- API RESTful para configuração e monitoramento

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

### Implementação do Funil em seu Código

Após configurar o serviço via injeção de dependência, você pode implementar o funil da seguinte forma:

```csharp
public class MeuServico
{
    private readonly RolloutFunnel _funnel;
    
    public MeuServico(RolloutFunnel funnel)
    {
        _funnel = funnel;
    }
    
    public DadosUsuario ObterDadosUsuario(int id)
    {
        return _funnel.Execute<DadosUsuario>(
            experimentName: "obter-dados-usuario",
            controlFunc: () => MetodoAntigo(id),
            candidateFunc: () => MetodoNovo(id),
            additionalContext: new { UserId = id }
        );
    }
    
    // Versão assíncrona
    public async Task<DadosUsuario> ObterDadosUsuarioAsync(int id)
    {
        return await _funnel.ExecuteAsync<DadosUsuario>(
            experimentName: "obter-dados-usuario-async",
            controlFunc: () => MetodoAntigoAsync(id),
            candidateFunc: () => MetodoNovoAsync(id),
            additionalContext: new { UserId = id }
        );
    }
    
    // Métodos de implementação...
}
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