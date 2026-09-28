# Selenium IDE scripts

`iobuild-smoke.side` is a Selenium IDE project (Firefox/Chrome extension) for the
deployed app (`https://iobuild-remix.arroz.dev`). It mirrors the IAM journeys of
the Playwright suite using only the UI.

| Test | Creates data |
|---|---|
| IAM login: wrong credentials give a generic error | no |
| IAM registration: builder form names each failing field | no |
| IAM Builder happy path: register, logout, login | yes: `sel.b.<timestamp>@example.test` |
| IAM Owner happy path: register, logout, login | yes: `sel.o.<timestamp>@example.test` |

The two happy-path tests register a real account in whatever environment they run against.

## Run in the extension

Selenium IDE → *Open an existing project* → select `iobuild-smoke.side` → run the suite.

## Run from the command line

```bash
npm i -g selenium-side-runner        # plus geckodriver on PATH for Firefox
selenium-side-runner -c "browserName=firefox" iobuild-smoke.side
# against the local Docker stack instead of the deployed site
selenium-side-runner -c "browserName=firefox" --base-url http://localhost:8081 iobuild-smoke.side
# only the tests that create no data
selenium-side-runner -c "browserName=firefox" --filter "wrong credentials|names each failing field" iobuild-smoke.side
```
