# Compatibility Probe Latest

- Timestamp: 2026-03-19 20:23:02 +09:00
- Host: 192.168.250.101
- Port: 1025
- Write check: enabled

| Target | Transport | Command | Status | Detail |
|---|---|---|---|---|
| SELF | tcp | 0101 read_type_name | OK | model=R120PCPU, model_code=0x4844 |
| SELF | tcp | 0401 read_sm400 | OK | values=[True] |
| SELF | tcp | 0403 random_read | OK | words=[4660], dwords=[305419896] |
| SELF | tcp | 0406 block_read | OK | words=[4096, 4097], bit_words=[1] |
| SELF | tcp | 1402 random_write | OK | completed |
| SELF | tcp | 1406 block_write | OK | completed |
