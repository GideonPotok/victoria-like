## Summary

<!-- What does this change and why? -->

## Tests Run

<!-- Which tests did you run? Paste the command and any relevant output. -->

```
dotnet test server/VictoriaLike.Server.sln
```

## Known Limitations

<!-- Anything incomplete, deferred, or worth flagging for reviewers? -->

## Checklist

- [ ] Gameplay logic stays in `VictoriaLike.Core`; client remains presentation-only
- [ ] Tests added or updated for simulation, command, loader, or invariant changes
- [ ] No local secrets, build output, Unity `Library/`, or scratch files committed
- [ ] Docs updated if the change affects setup, modding, or architecture
