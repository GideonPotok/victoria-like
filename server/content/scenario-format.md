# Scenario Data Format

JSON-based scenario definition for initializing the world.

## Structure

```json
{
  "scenario": {
    "name": "string",
    "description": "string",
    "startDate": "YYYY-MM-DD",
    "countries": [
      {
        "id": "uuid or auto",
        "name": "string",
        "tag": "3-letter code",
        "taxRate": 0-100
      }
    ],
    "markets": [
      {
        "id": "uuid or auto",
        "name": "string",
        "goods": {
          "grain": 1.0,
          "iron": 2.5
        }
      }
    ],
    "players": [
      {
        "id": "uuid or auto",
        "username": "string",
        "controls": "country-tag"
      }
    ],
    "provinces": [
      {
        "id": "uuid or auto",
        "name": "string",
        "owner": "country-tag",
        "market": "market-name",
        "population": 5000
      }
    ]
  }
}
```

## Fields

### Scenario
- `name` - Scenario title
- `description` - One-line description
- `startDate` - World date on load (format: YYYY-MM-DD)
- `countries` - List of country definitions
- `markets` - List of market definitions
- `provinces` - List of province definitions
- `players` - Optional actor-to-country control mapping used for command authorization

### Country
- `id` - Optional GUID; auto-generated if omitted
- `name` - Full name (e.g., "England")
- `tag` - 3-letter abbreviation (e.g., "ENG")
- `taxRate` - Initial tax rate (0-100)

### Market
- `id` - Optional GUID; auto-generated if omitted
- `name` - Market identifier
- `goods` - Dictionary mapping good names to starting prices

### Province
- `id` - Optional GUID; auto-generated if omitted
- `name` - Province name
- `owner` - Country tag (must match a defined country)
- `market` - Market name (must match a defined market)
- `population` - Initial population (minimum 100)

### Player
- `id` - Optional GUID; auto-generated if omitted
- `username` - Human-readable label for the actor
- `controls` - Country tag this actor controls

## Validation Rules

- Country tags must be unique, 3 letters
- Country names must be non-empty
- Province owners must reference existing countries
- Provinces must reference existing markets
- Player `controls` values must reference existing countries
- Only one player may control a given country
- Population must be >= 100
- Tax rates must be 0-100
- Scenario must have at least 1 country and 1 province

## Example

See `scenarios/tiny-2country.json` for a minimal valid scenario.
