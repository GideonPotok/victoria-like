# Phase 1 Demo Script

This is the short launch-video path for `v0.1-prealpha`.

## Goal

Show that the project has real simulation bones: POPs, markets, prices, taxes, and inspection working through a server-authoritative loop.

## Flow

1. Start local services:

```bash
make up
```

2. Start the Albion server scenario:

```bash
make run-albion
```

3. Confirm server health:

```bash
curl http://localhost:5001/health
```

4. Open `client-unity/v2` in Unity 2023 LTS and enter Play Mode.

   Whenever this Unity path is used for a public demo or documentation, also
   show the equivalent curl-based play path, using the Codex sessions in
   `playwithcurl.zip` as the reference.

5. Show the country/province inspection view:

- Albion treasury and tax rate.
- Northshire and Ironvale.
- Farmers, laborers, and artisans.
- Life/everyday/luxury needs where visible.
- Market prices and local production.

6. Let time run and show values changing.

7. Change an exposed peaceful economy control if available in the current UI.

8. End on the docs and roadmap:

- `docs/current_status.md`
- `docs/roadmap.md`
- `docs/for-victoria-2-fans.md`

## Do Not Claim

- Finished war or diplomacy.
- Complete multiplayer.
- Historical accuracy.
- Balanced economy.
- Production-quality UI.
