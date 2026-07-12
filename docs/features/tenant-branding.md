# White-Label / Branding do Tenant

Permite que cada organização personalize a aparência do dashboard (logo,
nome de exibição, cores, fonte) — os mesmos campos de branding vivem na
entidade `Organization`, mas este é o único controller dedicado a
gerenciá-los.

## 1. Por que é separado de `RegisterOrganizationController`

Ambos leem/escrevem a mesma tabela (`Organizations`), mas
[`register-organization.md`](register-organization.md) cobre dados legais/
cadastrais (nome, razão social, CNPJ) sem checagem de role, enquanto este
controller cobre identidade visual e **exige `TenantAdmin`** para qualquer
escrita — a separação existe porque são preocupações diferentes (dados
fiscais vs. personalização de UI), não uma duplicação acidental.

## 2. Endpoints

- `GET /` (`[Authorize]`, qualquer role): retorna o branding atual da
  organização do token. `DisplayName` cai de volta para `Organization.Name`
  se `BrandingDisplayName` estiver vazio — todo tenant tem *algum* nome de
  exibição mesmo sem branding customizado.
- `PUT /` (`TenantAdmin` apenas — checado manualmente lendo o claim de role,
  não via `[HasPermission]`): multipart/form-data. Aceita um `logoFile`
  (upload real, salvo em `wwwroot/uploads/logos/{guid}.{ext}`, disco local do
  servidor) **ou** um `LogoUrl` direto no DTO (link externo) — se ambos
  vierem, o arquivo enviado tem prioridade. Campos de cor/fonte são
  atualizados só se não-nulos no DTO (patch parcial).
- `DELETE /` (`TenantAdmin` apenas): zera todos os campos de branding
  (`null`), restaurando o visual padrão — não deleta o arquivo de logo do
  disco, só a referência no banco (arquivo órfão fica em
  `wwwroot/uploads/logos/`).

A checagem de role em `Update`/`Reset` é feita lendo o claim diretamente
(`ClaimTypes.Role` ou a URI antiga do WS-Federation como fallback) em vez de
usar o sistema de permissões (`[HasPermission]`) usado no resto da API — é a
única checagem de autorização do sistema feita "à mão" dessa forma.

## 3. Campos de branding (na entidade `Organization`)

`BrandingLogoUrl`, `BrandingDisplayName`, `BrandingPrimaryColor`,
`BrandingSecondaryColor`, `BrandingAccentColor`, `BrandingFontFamily` — todos
opcionais (`null` = usa o padrão do produto).

## 4. Referência de endpoints

Base: `api/tenant/branding`. Todos exigem JWT.

| Método | Rota | Autorização extra | Uso |
|---|---|---|---|
| `GET` | `/` | — | Branding atual (com fallback pro nome da organização) |
| `PUT` | `/` | `TenantAdmin` | Atualiza logo/cores/fonte/nome de exibição |
| `DELETE` | `/` | `TenantAdmin` | Restaura o padrão |

## 5. Limitação conhecida

Upload de logo vai para disco local (`wwwroot/uploads/logos/`), não para
object storage — mesmo padrão usado em `UserController.UpdateMyAvatar`. Em
um ambiente com múltiplas instâncias/containers efêmeros (ex.: Cloud Run sem
disco persistente compartilhado), isso é uma limitação real: o arquivo pode
não sobreviver a um redeploy ou não estar visível a partir de outra
instância.

## 6. Relação com outros módulos

- Compartilha a entidade `Organization` com [`register-organization.md`](register-organization.md).
- É consumido pelo frontend/dashboard para pintar a UI com a identidade da
  organização logada (fora do escopo deste repositório).
