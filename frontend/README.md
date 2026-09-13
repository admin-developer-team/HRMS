# Frontend

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 22.0.7.

## Local company workspaces

Start the API from the repository root in one terminal (PostgreSQL and your local environment settings must be available):

```powershell
dotnet run --project backend/src/Hrms.Api/Hrms.Api.csproj --launch-profile http
```

Start Angular in another terminal:

```powershell
cd frontend
npm install
npm start
```

The development server binds to `127.0.0.1:4200` so company `.localhost` names work on Windows. Restart an already-running `ng serve` after changing `angular.json`. No DNS record or hosts-file edit is needed for `.localhost` in current browsers. Open `http://localhost:4200/` for the **platform** account, and `http://<company-slug>.localhost:4200/` for a company account. For example, the `ssym` tenant uses `http://ssym.localhost:4200/login` and its calendar is at `http://ssym.localhost:4200/calendar`. Sign in again on the company URL: browser storage is separate for each hostname, and an old company session on plain `localhost` will be rejected by tenant isolation.

The Angular proxy forwards `/api` and `/hubs` to `http://localhost:5207` while preserving the company hostname. Verify the selected workspace before logging in:

```powershell
Invoke-RestMethod http://ssym.localhost:4200/api/v1/auth/workspace
```

That should return `slug: ssym`. `http://localhost:4200/api/v1/auth/workspace` should return `slug: platform`. For API-only testing, open `http://ssym.localhost:5207/swagger` and authorize with a token obtained by logging in on that same company host. A token from another company or the platform host must return 403. The application reloads automatically when you edit source files.

If the company URL still displays **HRMS Platform**, an older dev server is likely still occupying port 4200. Stop that terminal and restart `npm run frontend`. If you cannot stop it, run `npm --prefix frontend start -- --port 4201` from the repository root and open `http://ssym.localhost:4201/login`. Check `/api/v1/auth/workspace` on the same port before signing in; it must return the company slug.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
