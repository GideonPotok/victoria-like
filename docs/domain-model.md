# Domain Model

Minimal entities and value objects for the Victoria-Like simulation.

## Value Objects (Strongly-Typed IDs)

All identifiers are explicit, not implicit. This prevents accidentally mixing up different ID types.

- `CountryId` - Unique country identifier
- `ProvinceId` - Unique province identifier
- `MarketId` - Unique market identifier
- `ActorId` - Unique player/actor identifier
- `CommandId` - Unique command identifier

All implement `IEquatable<T>` and are backed by GUID for global uniqueness.

Usage:
```csharp
var countryId = CountryId.New();
var country = new Country(countryId, "England", "ENG", taxRate: 15);
```

## Entities

### Country
- `Id` - CountryId
- `Name` - Display name
- `Tag` - Short code ("ENG", "FRA", etc.)
- `TaxRate` - 0-100, percentage

### Province
- `Id` - ProvinceId
- `Name` - Display name
- `OwnerId` - Which country owns it
- `MarketId` - Which market serves this province
- `Population` - Number of pops (minimum 100)

### Market
- `Id` - MarketId
- `Name` - Display name
- `GoodPrices` - Dictionary of good_name → price (decimal)

### PlayerAccount
- `Id` - ActorId
- `Username` - Player name
- `ControlledCountry` - Which CountryId this player controls
- `CreatedAt` - Account creation timestamp

### CommandEnvelope
- `Id` - CommandId (auto-generated)
- `ActorId` - Who issued the command
- `CommandType` - String type (e.g., "ChangeTaxRate")
- `Payload` - Dictionary of command parameters
- `IssuedAt` - When command was created

## Design Principles

1. **Thin Models** - Entities are data containers, not logic holders
2. **No Implicit State** - IDs are explicit, not auto-indexes
3. **Type Safety** - Strongly-typed IDs catch mixing errors at compile time
4. **Immutable Where Possible** - Value objects are readonly structs
5. **No Business Logic Here** - Rules are enforced in handlers/services

## Future Expansion (Not Q1)

Do not add:
- Pop dynamics (Q2)
- Good production/consumption (Q2)
- Budget systems (Q3)
- Diplomacy/relations (Q3+)
- Military units (Q3+)

This is the skeleton. Flesh it out after the backbone is solid.
