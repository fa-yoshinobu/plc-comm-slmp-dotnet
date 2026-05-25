# Release Process

This repository publishes `PlcComm.Slmp`. NuGet publishing is intentionally
manual, but GitHub Releases must stay synchronized with the manually published
NuGet version.

## Required Artifacts

Every GitHub Release must include these three assets:

- `PlcComm.Slmp.<version>.nupkg`
- `PlcComm.Slmp.<version>.snupkg`
- `PlcComm.Slmp.<version>.dll.zip`

The DLL zip should be built from the same commit as the release tag. Do not
upload assets built from `main` when the release points at an older tag.

## Version And Tag Rules

- Use `vX.Y.Z` for new release tags.
- Keep `<Version>` in `src\PlcComm.Slmp\PlcComm.Slmp.csproj` equal to the
  NuGet package version.
- If older non-`v` tags exist, do not reuse the mixed style for new releases.
- GitHub Release, NuGet package, and attached asset filenames must all refer to
  the same version.

## Checklist

1. Update the package version in `src\PlcComm.Slmp\PlcComm.Slmp.csproj`.
2. Update `CHANGELOG.md`.
3. Run the release checks:

   ```powershell
   .\release_check.bat
   ```

4. Create and push the `vX.Y.Z` tag:

   ```powershell
   git tag vX.Y.Z
   git push origin vX.Y.Z
   ```

5. Build release artifacts from the tag:

   ```powershell
   git switch --detach vX.Y.Z
   dotnet restore PlcComm.Slmp.sln
   dotnet build PlcComm.Slmp.sln -c Release --no-restore
   dotnet test PlcComm.Slmp.sln -c Release --no-build
   dotnet pack src\PlcComm.Slmp\PlcComm.Slmp.csproj -c Release --no-build --output out
   Compress-Archive -Path src\PlcComm.Slmp\bin\Release\net9.0\* -DestinationPath out\PlcComm.Slmp.X.Y.Z.dll.zip -Force
   git switch main
   ```

6. Publish the NuGet package manually.
7. Create or update the GitHub Release for `vX.Y.Z`, then upload the `.nupkg`,
   `.snupkg`, and DLL zip assets.
8. Mark the GitHub Release as Latest when it is the newest stable release.

## Release Notes

Each GitHub Release should include:

- `Added APIs`: list new public API names and the version they first appear in.
- `Compatibility`: target framework, behavior changes, and migration notes.
- `Assets`: list the attached `.nupkg`, `.snupkg`, and DLL zip filenames.

Use `No new public API additions are called out for this release.` when there
are no public API additions.

## Verification

After publishing, verify the release:

```powershell
gh release view vX.Y.Z --repo fa-yoshinobu/plc-comm-slmp-dotnet --json tagName,name,assets,url
gh release list --repo fa-yoshinobu/plc-comm-slmp-dotnet --limit 3
```

Confirm that:

- the latest GitHub Release tag is `vX.Y.Z`;
- exactly three release assets are present;
- NuGet shows the same `PlcComm.Slmp` version;
- release notes mention added APIs and compatibility.
