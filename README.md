# Time Reporting
A web application for recording hours worked.

## Deploying

The whole solution deploys in one call, from the repository root:

```cmd
dotnet msbuild deploy.proj
```

That builds the UI with Vite, copies it to the site root, then publishes the API into the
`api` folder beneath it. Both destinations live in `Directory.Build.props`:

| Property       | Default                                    | What lands there       |
| -------------- | ------------------------------------------ | ---------------------- |
| `SiteRoot`     | `\\DARMIK\Web\gmgdesk.com\timereporting` | the base of both       |
| `UiDeployDir`  | `$(SiteRoot)`                              | the built React app    |
| `ApiDeployDir` | `$(SiteRoot)\api`                          | the published .NET API |

`ApiDeployDir` is the physical path of the `/api` sub-application in IIS, which is why it
sits inside the UI's folder rather than beside it.

Override any of them for a different target:

```cmd
dotnet msbuild deploy.proj -p:SiteRoot=\\DARMIK\Web\staging\timereporting
```

The deploy is additive — it overwrites what it copies and leaves everything else alone.
Nothing mirrors or purges, so the `api` folder survives a UI deploy and neither half can
delete the other. Stale files are never removed; clear the folder by hand for a clean slate.

Each half can still be deployed on its own: `npm run deploy` in `timereporting-ui`, or
right-click the WebApi project in Visual Studio and Publish with the `FolderProfile`
profile. All three routes read the same destinations, so they cannot drift apart.
