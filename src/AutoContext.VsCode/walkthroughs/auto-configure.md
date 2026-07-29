## Auto Configure

AutoContext looks at your workspace and turns on the guidelines and checks that match it. This happens on its own — there is no setup step — and it keeps up as your project changes.

### What it looks for

- **The files in your project** — project files, source files, and markers like `Dockerfile` or a Git repository.
- **What your project depends on** — packages and libraries referenced by your project files and `package.json`.

From that it works out which technologies you actually use, and enables the matching guidelines and checks. Anything general — like code review or testing guidance — is always on.

### Seeing the result

The Instructions and Tools panels show an enabled/total count in their headers.

### Run it yourself

It runs automatically, but you can re-run it any time:

[Auto Configure](command:autocontext.auto-configure)

Fine-tune the result afterwards from the sidebar panels.
