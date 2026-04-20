# Day 64 POP Inspection API/Admin Visibility

Day 64 deliverable: authored province POP groups are visible through province inspection APIs and the admin dashboard.

## Implemented

Province detail responses now include `pop_groups` with the persisted POP data loaded from the database.

The public province detail API exposes POP inspection data at:

```text
GET /api/world/provinces/{provinceId}
```

The admin province inspector exposes the same inspection data at:

```text
GET /api/admin/provinces/{provinceId}
```

Each POP group includes:

- id
- size
- population share
- POP type
- strata
- culture
- religion
- literacy
- militancy
- consciousness
- cash
- life/everyday/luxury needs fulfillment
- employed and unemployed counts

## Admin Dashboard

The admin province inspector now renders a POP table for the selected province.

The table shows:

- POP type and strata
- size and population share
- culture and religion
- literacy
- militancy/consciousness
- employed/unemployed counts
- life/everyday/luxury needs fulfillment

## Result

Day 64 is complete when:

- province detail API returns POP groups
- admin province inspector API returns POP groups
- admin dashboard shows a readable POP table for the selected province
- tests verify POP inspection mapping preserves demographic, employment, and needs fields
- server build passes
- core tests pass
