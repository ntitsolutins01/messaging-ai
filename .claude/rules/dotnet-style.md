# Regra: Estilo .NET

- .NET 8, C# com `nullable` habilitado e `TreatWarningsAsErrors` em `Directory.Build.props`.
- Código assíncrono de ponta a ponta; sempre propagar `CancellationToken`.
- Injeção de dependência por construtor. Sem service locator.
- Registros e eventos como `record` imutável.
- Nomes: `IFooService`, `FooHandler`, `FooConsumer`, `FooAdapter`. Arquivos têm um tipo público.
- Erros de negócio esperados retornam resultado (`Result<T>`); exceções ficam para o inesperado.
- EF Core: migrations versionadas em `Infrastructure/Persistence/Migrations`. Nunca editar migration já aplicada.
- Formatação por `dotnet format`; o hook de pós-edição verifica.
