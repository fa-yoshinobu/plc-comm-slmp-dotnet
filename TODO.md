# TODO

Current active TODOs only.

## Current Status

### SLMP-DOTNET-TODO-1: Remove WriteDWordsBlockAsync after its compatibility release

Status: `approved`. Both overloads are obsolete direct delegates during the current compatibility release and must be removed in the immediately following release.

- [ ] Remove the `SlmpDeviceAddress` and string overloads of `WriteDWordsBlockAsync` from the public API.
- [ ] Remove their compatibility tests and public API entries while retaining both `WriteDWordsSingleRequestAsync` overloads unchanged.
- [ ] Update the API reference, migration note, and changelog for the removal.
