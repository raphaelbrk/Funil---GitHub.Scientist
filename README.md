# Funil de Rollout com GitHub.Scientist para .NET 8

Este repositório contém uma implementação de funil de rollout progressivo utilizando a biblioteca GitHub.Scientist para comparação de implementações antigas e novas em aplicações .NET.

## Características

- Implementação de funil de rollout progressivo (1%, 2%, 5%, 10%, etc.)
- Integração com Redis para configuração dinâmica
- API para configuração e monitoramento do rollout
- Exemplos de uso com requisições síncronas e assíncronas
- Publishers personalizáveis para análise de resultados

## Visão Geral

O sistema permite uma migração segura e gradual de implementações antigas para novas, permitindo:

1. Configurar a porcentagem de tráfego que será roteado para a nova implementação
2. Comparar resultados entre implementações antigas e novas
3. Monitorar diferenças e desempenho
4. Configurar parâmetros via Redis para ajustes em tempo real

## Estrutura do Projeto

- **/FunilRollout**: Aplicação ASP.NET Core que implementa o funil de rollout
  - **/Services**: Classes principais para implementação do funil
  - **/Controllers**: API para configuração e demonstração

## Começando

```bash
# Clone o repositório
git clone https://github.com/seu-usuario/funil-rollout.git

# Entre no diretório do projeto
cd FunilRollout

# Execute o projeto
dotnet run
```

Consulte o README dentro do diretório `/FunilRollout` para instruções detalhadas sobre como usar a implementação.

## Pré-requisitos

- .NET 8
- Instância Redis (local ou remota)

## Contribuindo

Contribuições são bem-vindas! Sinta-se à vontade para abrir issues ou enviar pull requests.

## Licença

Este projeto está licenciado sob a licença MIT. 