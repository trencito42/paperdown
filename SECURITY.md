# Security Policy

## Supported versions

| Version        | Supported |
| -------------- | --------- |
| 0.1.x beta     | Yes       |

## Reporting a vulnerability

Please report security issues privately via GitHub Security Advisories for the repository, or email the maintainers listed in the README once published.

Do not open public issues for exploitable vulnerabilities.

## Threat model notes

Paperdown treats Markdown as untrusted input. Script tags and iframes are stripped before HTML rendering. WebView2 script execution is disabled for preview/export hosts. Remote HTTP(S) navigation is blocked in the preview control.
