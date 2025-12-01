# AWS DDNS API Only

> [!WARNING]
> **AI-authored:** This change was autonomously planned and implemented by an AI software factory from a human-authored specification, with possible subsequent human review or modification.

> [!WARNING]
> This experiment is effectively abandoned. The generated material is retained primarily as a research artifact.

Tests a dynamic address registry with DNS removed: a C# Lambda behind an API Gateway REST API keeps one DynamoDB item per stable identity, where `POST /identity/{identity}` records an address and `GET /identity/{identity}` returns it with `last_reported` and `last_changed` timestamps. A console client reports its address on a fixed interval.

```sh
make test
make deploy
make e2e
make destroy
```

## Notes

- It's a thing. More an output of the factory than anything.
