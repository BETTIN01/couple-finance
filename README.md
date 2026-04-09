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
- opcional: repositorio publico no GitHub para publicar releases e atualizacoes automaticas
- opcional: bucket publico no Supabase Storage para publicar releases

## Configuracao local

Edite [`CoupleFinance.Desktop/appsettings.json`](./CoupleFinance.Desktop/appsettings.json):

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

O fluxo principal agora passa pelo script [`scripts/Publish-Release.ps1`](./scripts/Publish-Release.ps1). Ele:

- compila a solucao
- executa testes
- publica a versao portatil em `artifacts\portable`
- ajusta o `appsettings.json` da release com `ManifestUrl` remoto
- gera `CoupleFinance-portable.zip`
- gera `CoupleFinance-Setup.exe`
- gera `update-manifest.json`
- opcionalmente envia tudo para `GitHub Releases`
- opcionalmente envia tudo para um bucket publico do Supabase Storage
- falha por padrao quando voce tenta gerar uma release sem canal remoto de atualizacao, para evitar instalar builds que nunca vao atualizar em outra maquina

### Melhor fluxo: GitHub Releases

Esse e o caminho mais simples para fazer futuras atualizacoes chegarem automaticamente em outra maquina sem reenviar pasta manualmente.

Pre-requisitos:

- repositorio publico, por exemplo `BETTIN01/couple-finance`
- token do GitHub com permissao para criar releases e enviar assets
- token salvo em `GITHUB_TOKEN` ou passado por parametro

Exemplo:

```powershell
$env:GITHUB_TOKEN = "seu_token_aqui"

powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -Version 1.1.20 `
  -GitHubRepo "BETTIN01/couple-finance" `
  -ReleaseNotes "Canal remoto de atualizacao via GitHub Releases."
```

Nesse modo, o script:

- cria ou atualiza a release `v1.1.20`
- publica `CoupleFinance-Setup.exe`
- publica `CoupleFinance-portable.zip`
- publica `update-manifest.json`
- prepara o app com `ManifestUrl` estavel em:
  `https://github.com/BETTIN01/couple-finance/releases/latest/download/update-manifest.json`

Observacao importante:

- para auto-update sem login, o repositorio precisa ser publico, ou pelo menos os assets precisam estar publicos
- o manifesto usa a URL estavel `latest/download`, mas os arquivos internos apontam para a tag da versao, evitando baixar um pacote diferente do manifesto validado

Se voce passar `-GitHubRepo` sem token, o script ainda gera tudo com as URLs certas do GitHub, mas o upload dos assets precisa ser feito manualmente na release.

### Publicacao automatica para este repositorio

Como este projeto ja esta em `BETTIN01/couple-finance`, o fluxo mais simples daqui para frente e:

1. configurar um token do GitHub no Windows uma unica vez
2. rodar um script curto de release

Para salvar o token de forma persistente no Windows:

```powershell
setx GITHUB_TOKEN "SEU_TOKEN_AQUI"
```

Depois feche e reabra o terminal/Codex.

Com isso configurado, a publicacao automatica da release fica assim:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-GitHubRelease.ps1 `
  -ReleaseNotes "Resumo curto da versao."
```

Observacoes:

- se `-Version` ficar vazio, o script usa a versao que ja estiver no `.csproj`
- o script publica direto em `BETTIN01/couple-finance`
- ele ja gera manifesto remoto, setup e pacote zip
- ele usa o fluxo novo com `installer` como caminho principal de auto-update

### Release local com URLs ja definidas

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -Version 1.1.20 `
  -PublicBaseUrl "https://seu-dominio.com/couple-finance/stable" `
  -ManifestUrl "https://seu-dominio.com/couple-finance/stable/update-manifest.json" `
  -ReleaseNotes "Setup proprio e canal remoto de atualizacao."
```

Saidas:

- [`artifacts/portable/CoupleFinance.Desktop.exe`](./artifacts/portable/CoupleFinance.Desktop.exe)
- [`artifacts/installer/CoupleFinance-portable.zip`](./artifacts/installer/CoupleFinance-portable.zip)
- [`artifacts/installer/CoupleFinance-Setup.exe`](./artifacts/installer/CoupleFinance-Setup.exe)
- [`artifacts/installer/update-manifest.json`](./artifacts/installer/update-manifest.json)

### Release com upload direto para Supabase Storage

Use um bucket publico ja criado, por exemplo `releases`.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Publish-Release.ps1 `
  -Version 1.1.20 `
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

O setup distribuivel agora e o executavel gerado pelo projeto [`CoupleFinance.Setup`](./CoupleFinance.Setup/CoupleFinance.Setup.csproj).

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
3. Publique as proximas releases no mesmo canal remoto

Depois disso, o app baixa e aplica atualizacoes automaticamente em segundo plano, sem precisar reenviar a pasta inteira para o outro computador.

Se voce quiser gerar um setup apenas para uso local, sem auto-update, acrescente `-AllowOfflineDistribution`. Nesse caso, a tela de configuracoes do app mostra claramente que o canal remoto nao foi configurado.

Exemplo de manifesto em [`deployment/update-manifest.example.json`](./deployment/update-manifest.example.json).

## Supabase

Execute o SQL em [`supabase/schema.sql`](./supabase/schema.sql) para criar as tabelas minimas usadas por autenticacao complementar e sincronizacao.

Se quiser sincronizacao entre duas maquinas em redes diferentes, configure tambem:

- `Supabase:Url`
- `Supabase:AnonKey`

## Estrutura

- [`CoupleFinance.Desktop`](./CoupleFinance.Desktop): UI WPF, tema, navegacao e viewmodels
- [`CoupleFinance.Application`](./CoupleFinance.Application): contratos, DTOs, projecoes, dashboard e insights
- [`CoupleFinance.Domain`](./CoupleFinance.Domain): entidades e enums
- [`CoupleFinance.Infrastructure`](./CoupleFinance.Infrastructure): SQLite, auth, sync e servicos
- [`CoupleFinance.Setup`](./CoupleFinance.Setup): instalador distribuivel em `.exe`
- [`CoupleFinance.Tests`](./CoupleFinance.Tests): testes unitarios

## Observacoes

- O pacote de graficos usado em WPF emite avisos `NU1701` no restore, mas a compilacao e a execucao do app seguem funcionais no ambiente atual.
- O instalador legado do Inno Setup continua no repositorio em [`installer/CoupleFinance.iss`](./installer/CoupleFinance.iss), mas a esteira principal agora usa o setup proprio em `.NET`.
