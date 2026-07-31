# Specification Pattern with EF Core

A runnable companion project for the article **Specification Pattern in .NET:
How to Tame Complex EF Core Queries**.

The sample runs against **SQLite**, not the EF Core InMemory provider. That is
deliberate: InMemory accepts expressions no relational provider can translate,
so it hides exactly the bugs a query-focused sample should expose. Every SQL
statement printed by this project is real SQL that went to a real database.

The sample demonstrates:

- a minimal `ISpecification<T>` contract with criteria, includes, ordering,
  paging, tracking and split-query behavior
- a `SpecificationEvaluator` that turns the description into `IQueryable<T>`
- a thin repository with separate `ListAsync` and `CountAsync` paths
- `OrdersForReviewSpecification` built on a reusable `OrderCriteria` expression
- a projection query that answers a question without a third `Include`
- integration tests that assert on the generated SQL

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download/dotnet/10.0)

No database server is needed: SQLite runs in memory inside the process.

## Run

```bash
dotnet run --project src/SpecificationExample
```

The program prints the SQL the specification produces, the matching page of
orders, and the outstanding balance computed through a projection.

## Test

```bash
dotnet test SpecificationExample.slnx
```

Seven tests cover the two levels the article describes:

| Test | What it proves |
|---|---|
| `Criteria_rejects_order_from_another_tenant` | business rule, compiled predicate only |
| `Paging_arguments_are_validated` | `Page`/`PageSize` guards |
| `Query_returns_only_orders_of_the_requested_tenant` | tenant isolation against SQLite |
| `Unused_filters_do_not_reach_the_generated_sql` | EF Core prunes `parameter == null` branches |
| `Populated_filters_are_translated_to_sql` | every filter translates and matches |
| `Count_ignores_paging` | `criteriaOnly` path of the evaluator |
| `Orders_are_sorted_by_creation_date_descending` | ordering survives the evaluator |

## Notes on the model

`Order.CreatedAt` is a `DateTime` (UTC) rather than a `DateTimeOffset`. The
SQLite provider throws on `DateTimeOffset` in an `ORDER BY` clause:

```text
System.NotSupportedException: SQLite does not support expressions of type
'DateTimeOffset' in ORDER BY clauses.
```

If your production database is SQL Server or PostgreSQL, `DateTimeOffset`
orders fine there - but then the integration tests have to run against that
engine, for example through Testcontainers, instead of SQLite.

`SQLitePCLRaw.bundle_e_sqlite3` is pinned in `Directory.Packages.props` above
the version EF Core 10.0.10 resolves transitively, because 2.1.11 carries
[GHSA-2m69-gcr7-jv3q](https://github.com/advisories/GHSA-2m69-gcr7-jv3q).

## Project layout

```text
src/SpecificationExample/
  Domain/           Order aggregate and supporting entities
  Data/             DbContext and the specification-aware repository
  Specifications/   Contract, base class, evaluator, order specification
  SeedData.cs       Deterministic demo data
  Program.cs        Console entry point
tests/
  SpecificationExample.Tests/
```

The sample starts from the refactored state described in the article's reuse
section: the criteria live in `OrderCriteria.ForReview`, and the specification
composes them with includes, ordering and paging.

## License

[MIT](LICENSE)
