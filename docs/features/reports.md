# Relatórios de produtividade (Reports)

Gera um relatório de produtividade (PDF ou CSV) agregando `AppFocusEvent`
de um período — por organização inteira, por equipe, ou por um único usuário.
Backend-only: hoje não existe uma tela de dashboard chamando esses endpoints
(o `TimeZoon-Front`, de onde este código foi portado, tem uma tela de
relatório própria, mais antiga e desconectada, que busca dados direto via
`getAppFocusEventsByUser` e gera o PDF no navegador com `html2canvas`/`jsPDF`
— não usa `/api/reports/*`). Este módulo entrega a API pronta para um
consumidor futuro.

Arquivos: `Controllers/ReportsController.cs`, `Services/Reports/
ReportDataService.cs`, `ReportPdfService.cs`, `ReportCsvService.cs`,
`DTOs/Reports/*`, `html/Reports/ProductivityReport.html`.

## Endpoints

Base: `api/reports`, `[Authorize]` + `[HasPermission(Permissions.
ReportsExport)]` em ambas as rotas (os dois permission constants já existiam
em `RolePermissionMatrix`, sem nenhum endpoint usando-os até este módulo).

| Método | Rota | Uso |
|---|---|---|
| `POST` | `/pdf` | Gera o relatório em PDF (`ReportFilterDTO` no corpo) |
| `POST` | `/csv` | Gera o relatório em CSV (mesmo filtro) |

`ReportFilterDTO`: `TeamId?`, `UserId?` (mutuamente combináveis — um `UserId`
dentro de um `TeamId` restringe a um membro específico da equipe, e é
validado que o usuário realmente pertence à equipe informada), `StartDate`/
`EndDate` (`DateTime`, só a parte de data é usada), `StartTime`/`EndTime`
(`TimeSpan`, recorte de horário dentro de cada dia do período).

## Como os dados são montados (`ReportDataService`)

1. Resolve a organização via `User.GetOrganizationId()` (a mesma extension
   usada em todo o resto do backend — nada de parsing de claim manual).
2. Converte `StartDate+StartTime`/`EndDate+EndTime` de horário **local** para
   UTC (`DateTimeKind.Local` → `ToUniversalTime()`), já que o filtro é
   pensado em termos do fuso do usuário, mas `AppFocusEvent.StartTime` é
   armazenado em UTC.
3. Resolve o "assunto" do relatório (`SubjectType`/`SubjectName`): time todo
   (`TeamId`), um usuário (`UserId`), ou a organização inteira (nenhum dos
   dois) — e a partir disso a lista de `targetUserIds` a consultar.
4. Para cada `userId`, chama `IAppFocusService.GetHistoryAsync` e agrega tudo
   numa lista só, depois filtra por `OrganizationId` e pelo intervalo UTC
   calculado.
5. Soma segundos por `ActivityCategory` (Produtivo/Neutro/Improdutivo — não
   existe categoria de ociosidade ainda, `IdleSeconds` é sempre `0`), monta o
   top-5 de aplicações por tempo total, e uma quebra diária de contagem de
   atividades/tempo total (usada pelo gráfico de barras do PDF).
6. `GeneratedAt` usa o fuso horário do Brasil, resolvido de forma
   cross-platform: `"E. South America Standard Time"` no Windows,
   `"America/Sao_Paulo"` em qualquer outro SO (`OperatingSystem.IsWindows()`)
   — hardcoded só o ID do Windows quebraria em produção num container Linux.

## Geração de PDF (`ReportPdfService`)

Sem motor de template — faz substituição literal de tokens `{{TOKEN}}` num
HTML estático (`html/Reports/ProductivityReport.html`), incluindo gráficos
inteiramente em CSS (donut de produtividade via `conic-gradient`, gráfico de
barras diário via `<div>`s com `height` percentual — nenhuma lib de gráfico,
nenhuma imagem gerada), depois renderiza com **Chromium headless via
PuppeteerSharp** (`page.PdfDataAsync`, A4 paisagem, fundo impresso).

- **`CHROMIUM_EXECUTABLE_PATH`** é checado primeiro; só se não estiver
  configurado é que cai no `BrowserFetcher` (baixa um Chromium próprio em
  runtime — lento e não confiável em produção). O `Dockerfile` já instala
  `chromium` via `apt-get` e define essa env var apontando para
  `/usr/bin/chromium`, então em produção o download nunca deveria acontecer.
- Args de launch incluem `--disable-dev-shm-usage --disable-gpu --no-zygote`
  — necessários porque o `/dev/shm` de containers tipo Cloud Run é pequeno
  demais para o Chromium por padrão.
- Cada etapa (localizar template, montar HTML, localizar Chromium, abrir
  browser, criar página, carregar HTML, gerar PDF) é rastreada numa variável
  `step`; qualquer exceção é re-lançada como `"ERRO PDF | Etapa: {step} | ..."`
  — a etapa também volta no corpo do erro 500 do controller
  (`{ Step, Exception, Message, InnerException, StackTrace }`), porque sem
  isso um erro de PDF em produção (biblioteca do sistema faltando, template
  não publicado, etc.) é indistinguível de qualquer outro 500.

## Geração de CSV (`ReportCsvService`)

UTF-8 com BOM (para acentos abrirem corretos no Excel), `;` como delimitador,
uma linha por `ReportActivityDTO` (Aplicação/URL/Categoria/Início/Fim/Duração
em segundos). Campos com `;`, `"` ou quebra de linha são colocados entre
aspas com `"` duplicado escapando aspas internas — escaping CSV padrão.

## Nome do arquivo

`Relatorio_Produtividade_{Assunto}_{dd-MM-yyyy}_a_{dd-MM-yyyy}.{pdf|csv}`,
com o nome do assunto normalizado (`NormalizationForm.FormD` remove
acentos, tudo que não é letra/dígito vira `_`) para não quebrar como nome de
arquivo em nenhum SO/navegador.

## Pré-requisitos de infraestrutura

- **`WiseMonitor.Api.csproj`**: `PackageReference PuppeteerSharp 25.4.0` +
  `<None Update="html\Reports\ProductivityReport.html">` com
  `CopyToOutputDirectory`/`CopyToPublishDirectory` — sem o segundo, o template
  não vai para o container publicado (só funciona em `dotnet run` local, onde
  os caminhos são relativos ao código-fonte).
- **`Dockerfile`**: instala `chromium` + suas dependências de sistema
  (`libnss3`, `libgtk-3-0`, `libasound2`, etc.) via `apt-get` — a imagem base
  `aspnet:9.0` não vem com nada disso, e sem essas libs o Chromium falha ao
  iniciar com erros como `libglib-2.0.so.0: cannot open shared object file`.
