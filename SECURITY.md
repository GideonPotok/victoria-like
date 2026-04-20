# Security Policy

## Reporting a Vulnerability

Please do not open a public GitHub issue for security vulnerabilities.

Email **g.potok4@gmail.com** with:
- A description of the vulnerability and its potential impact
- Steps to reproduce or a proof of concept

You can expect an acknowledgment within 3 days and a status update within 14 days.

## Scope

This project is a local development simulation server. The primary attack surface is the REST API and WebSocket hub running on localhost. There are no multi-tenant production deployments.

## Out of Scope

- The hardcoded `victoria_dev_password` in `appsettings.json` and `docker-compose.yml` is an intentional local-only Docker dev default. Do not report it.
- Vulnerabilities in third-party dependencies should be reported upstream.
