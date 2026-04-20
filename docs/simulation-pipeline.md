# Simulation Pipeline

Each weekly tick runs in a fixed order:

1. advance date
2. process construction
3. assign employment
4. produce provincial/RGO goods
5. produce factory goods
6. produce artisan goods
7. distribute national stockpile reserves
8. clear/update market prices
9. fulfill pop needs
10. run monthly POP updates when due
11. update treasury
12. emit summaries and log lines

Rule: no presentation code inside the pipeline.
