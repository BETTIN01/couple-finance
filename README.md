# Couple Finance

Aplicativo desktop para Windows, em `C# + WPF + MVVM`, focado em controle financeiro pessoal compartilhado para casal, com `SQLite` local, sincronizacao opcional via `Supabase` e funcionamento offline-first.

## O que ja esta incluido

- autenticacao local com criacao de household e convite por codigo
- persistencia local em `SQLite`
- sincronizacao remota basica via `Supabase` quando configurado
- dashboard com cards, graficos e resumos do casal
- contas bancarias, lancamentos, transferencias e categorias
- cartoes, compras parceladas, faturas e pagamento
- metas, projecoes, carteira manual de investimentos e insights por regras
- atualizacao automatica por manifesto remoto
- setup proprio em `.exe`, sem depender do Inno Setup para a distribuicao principal

## Pre-requisitos

- Windows 10/11 x64
- .NET 8 SDK
- opcional: projeto Supabase para sincronizacao entre duas maquinas
- opcional: bucket publico no Supabase Storage para publicar releases

## Configuracao local

Edite [appsettings.json](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Desktop/appsettings.json):

```json
{
  "Updates": {
    "Enabled": false,
    "CheckOnStartup": true,
    "AutoInstallOnStartup": true,
    "PeriodicCheckIntervalMinutes": 45,
    "ManifestUrl": "",
    "StartupDelaySeconds": 3,
    "DownloadFolderName": "Updates"
  },
  "SyncAutomation": {
    "Enabled": true,
    "SyncOnStartup": true,
    "SyncAfterLocalChanges": true,
    "RefreshAfterAutomaticSync": true,
    "IntervalSeconds": 15
  },
  "Supabase": {
    "Url": "https://SEU-PROJETO.supabase.co",
    "AnonKey": "SUA_CHAVE_ANON"
  }
}
```

Se os campos do Supabase ficarem vazios, o app roda apenas em modo local com `SQLite`.

## Build de desenvolvimento

```powershell
dotnet restore .\CoupleFinance.sln
dotnet build .\CoupleFinance.sln
dotnet test .\CoupleFinance.Tests\CoupleFinance.Tests.csproj
dotnet run --project .\CoupleFinance.Desktop\CoupleFinance.Desktop.csproj
```

## Publicacao e distribuicao

O fluxo principal agora passa pelo script [Publish-Release.ps1](/C:/Users/rober/Desktop/App%20financeiro/scripts/Publish-Release.ps1). Ele:

- compila a solucao
- executa testes
- publica a versao portatil em `artifacts\portable`
- ajusta o `appsettings.json` da release com `ManifestUrl` remoto
- gera `CoupleFinance-portable.zip`
- gera `CoupleFinance-Setup.exe`
- gera `update-manifest.json`
- opcionalmente envia tudo para um bucket publico do Supabase Storage
- falha por padrao quando voce tenta gerar uma release sem canal remoto de atualizacao, para evitar instalar builds que nunca vao atualizar em outra maquina

### Release local com URLs ja definidas

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -Version 1.1.15 `
  -PublicBaseUrl "https://seu-dominio.com/couple-finance/stable" `
  -ManifestUrl "https://seu-dominio.com/couple-finance/stable/update-manifest.json" `
  -ReleaseNotes "Setup proprio e canal remoto de atualizacao."
```

Saidas:

- [CoupleFinance.Desktop.exe](/C:/Users/rober/Desktop/App%20financeiro/artifacts/portable/CoupleFinance.Desktop.exe)
- [CoupleFinance-portable.zip](/C:/Users/rober/Desktop/App%20financeiro/artifacts/installer/CoupleFinance-portable.zip)
- [CoupleFinance-Setup.exe](/C:/Users/rober/Desktop/App%20financeiro/artifacts/installer/CoupleFinance-Setup.exe)
- [update-manifest.json](/C:/Users/rober/Desktop/App%20financeiro/artifacts/installer/update-manifest.json)

### Release com upload direto para Supabase Storage

Use um bucket publico ja criado, por exemplo `releases`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -Version 1.1.15 `
  -ReleaseNotes "Setup proprio e canal remoto de atualizacao." `
  -SupabaseUrl "https://SEU-PROJETO.supabase.co" `
  -SupabaseAnonKey "SUA_CHAVE_ANON" `
  -StorageProjectUrl "https://SEU-PROJETO.supabase.co" `
  -StorageApiKey "SUA_CHAVE_SECRET_OU_SERVICE_ROLE" `
  -StorageBucket "releases" `
  -StoragePrefix "couple-finance/stable"
```

Nesse modo, o script publica:

- `couple-finance/stable/packages/<versao>/CoupleFinance-Setup.exe`
- `couple-finance/stable/packages/<versao>/CoupleFinance-portable.zip`
- `couple-finance/stable/update-manifest.json`

E o app ja sai com `ManifestUrl` apontando para o manifesto publico, o que permite atualizacao automatica em outro computador sem reenviar a pasta manualmente.

## Instalador

O setup distribuivel agora e o executavel gerado pelo projeto [CoupleFinance.Setup](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Setup/CoupleFinance.Setup.csproj).

Ele instala o app em:

`%LocalAppData%\Programs\Couple Finance`

Comportamentos principais:

- cria atalho no menu Iniciar
- repara atalho da area de trabalho quando necessario
- aceita modo silencioso para o auto-update
- usa o pacote portatil embutido na release

Argumentos suportados:

- `/VERYSILENT`
- `/SUPPRESSMSGBOXES`
- `/SP-`
- `/DIR=C:\Caminho\Desejado`
- `/NOLAUNCH`

Esses parametros mantem compatibilidade com o fluxo silencioso ja usado pelo atualizador do app.

## Atualizacao automatica

O app checa novas versoes no startup e depois continua verificando em ciclos automaticos.

1. Gere e publique uma release com manifesto remoto acessivel por HTTPS
2. Instale essa release na maquina principal e na outra maquina
3. Mantenha o `update-manifest.json` atualizado ao publicar novas versoes

Depois disso, o app baixa e aplica atualizacoes automaticamente em segundo plano, sem precisar reenviar a pasta inteira para o outro computador.

Se voce quiser gerar um setup apenas para uso local, sem auto-update, acrescente `-AllowOfflineDistribution`. Nesse caso, a tela de configuracoes do app mostra claramente que o canal remoto nao foi configurado.

Exemplo de manifesto em [update-manifest.example.json](/C:/Users/rober/Desktop/App%20financeiro/deployment/update-manifest.example.json).

## Supabase

Execute o SQL em [schema.sql](/C:/Users/rober/Desktop/App%20financeiro/supabase/schema.sql) para criar as tabelas minimas usadas por autenticacao complementar e sincronizacao.

Se quiser sincronizacao entre duas maquinas em redes diferentes, configure tambem:

- `Supabase:Url`
- `Supabase:AnonKey`

## Estrutura

- [CoupleFinance.Desktop](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Desktop): UI WPF, tema, navegacao e viewmodels
- [CoupleFinance.Application](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Application): contratos, DTOs, projecoes, dashboard e insights
- [CoupleFinance.Domain](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Domain): entidades e enums
- [CoupleFinance.Infrastructure](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Infrastructure): SQLite, auth, sync e servicos
- [CoupleFinance.Setup](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Setup): instalador distribuivel em `.exe`
- [CoupleFinance.Tests](/C:/Users/rober/Desktop/App%20financeiro/CoupleFinance.Tests): testes unitarios

## Observacoes

- O pacote de graficos usado em WPF emite avisos `NU1701` no restore, mas a compilacao e a execucao do app seguem funcionais no ambiente atual.
- O instalador legado do Inno Setup continua no repositorio em [CoupleFinance.iss](/C:/Users/rober/Desktop/App%20financeiro/installer/CoupleFinance.iss), mas a esteira principal agora usa o setup proprio em `.NET`.
