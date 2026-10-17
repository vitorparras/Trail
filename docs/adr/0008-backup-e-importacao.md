# ADR 0008 — Backup por arquivo e importação da planilha

- **Status:** Aceita
- **Data:** 2026-10-16
- **Origem:** requisito do autor. O formato do arquivo é proposta ([2.5](../02-dominio-e-regras.md#25-backup-e-restauração))

## Contexto

Os dados precisam poder ser salvos, restaurados e levados de uma instância para outra.
Os alunos que vão adotar o sistema já têm a planilha preenchida.

## Decisão

1. **Backup próprio da aplicação:** um arquivo `.zip` versionado, independente do banco,
   que o usuário exporta e restaura pela interface.
2. **Importação da planilha `.xlsx`** no modelo atual, com prévia e relatório de avisos.
3. **Backup operacional do banco inteiro,** feito pelo servidor (`VACUUM INTO` ou
   `pg_dump`), separado dos dois anteriores.

## Consequências

- ✅ Os dados são portáteis entre instâncias e entre SQLite e PostgreSQL.
- ✅ Os alunos que já têm a planilha adotam o sistema sem redigitar nada.
- ❌ O formato de backup precisa ser versionado, e versões novas devem ler os formatos
  antigos.
- ❌ O importador precisa tolerar planilhas alteradas pelos alunos.

## Alternativas consideradas

- **Só o backup do banco** (copiar o arquivo ou `pg_dump`): não é portátil entre bancos e
  exige acesso ao servidor.
- **Importar CSV em vez de `.xlsx`:** obrigaria cada aluno a converter as abas à mão.
