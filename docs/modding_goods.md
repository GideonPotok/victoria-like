# Modding Goods

Goods live in `server/content/goods.json`.

Each good has:

- `id`: stable lowercase identifier used by scenarios and simulation code.
- `displayName`: player-facing name.
- `basePrice`: starting/reference price.
- `category`: broad grouping such as `food`, `raw`, `consumer`, `industrial`, `manufactured`, or `luxury`.

Example:

```json
{ "id": "grain", "displayName": "Grain", "basePrice": 1.0, "category": "food" }
```

## Adding a Good

1. Add an entry to `server/content/goods.json`.
2. Use a stable lowercase `id` with underscores if needed.
3. Add the good to scenario market starting prices if the scenario should trade it.
4. Add it to POP needs, production outputs, or factory inputs only where the current loader supports those fields.
5. Run tests:

```bash
dotnet test server/VictoriaLike.Server.sln
```

## Current Goods

The current set includes food, raw materials, consumer goods, industrial goods, and luxuries:

```text
grain, fish, iron, coal, timber, cotton, fabric, clothes, furniture, liquor,
tools, luxury_clothes, luxury_furniture, steel, cement
```

## Guidelines

- Prefer goods that create interesting production chains or POP pressure.
- Avoid adding many flavor-only goods before the market and UI explain them clearly.
- Keep prices plausible relative to nearby goods, but do not worry about perfect balance yet.
