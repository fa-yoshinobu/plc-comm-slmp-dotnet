# Mixed Block Write 1406 Notes 2026-05-03

Scope:

- Command: `DeviceWriteBlock` (`0x1406`)
- Affected libraries: Rust and .NET SLMP clients
- Focus: one-request mixed word-block + bit-block write rejection handling

## Findings

- Both Rust and .NET encode one mixed `0x1406` request with:
  - word block count > 0
  - bit block count > 0
  - block specs followed by word payloads, then bit payloads
- For iQ-R style compatibility, both libraries use subcommand `0x0002`.
- When the PLC rejects the one-request mixed shape with known rejection end
  codes (`0xC056`, `0xC05B`, `0xC061`, `0x414A`), both libraries can retry as:
  - word-only `0x1406`
  - bit-only `0x1406`
- Non-live tests now verify this retry path and request split shape.

## Live Observations

- On the current QnUDV target, word-only, bit-only, and mixed `0x1406` block
  writes returned `0xC059`. That points to target command support rather than a
  mixed-only encoding issue.
- On the live iQ-L stress run, a two word-block write candidate returned
  `0xC05B`. That shows some rejections are not limited to mixed word+bit
  payloads; multiple block entries can also hit target-specific constraints.

## Conclusion

No current evidence points to a Rust/.NET payload assembly mismatch. The
remaining behavior is treated as PLC command support or request-shape capability
variation. The implemented fallback is the intended practical behavior for
mixed word+bit writes on targets that accept split block writes.
