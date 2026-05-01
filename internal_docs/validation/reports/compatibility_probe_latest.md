# Compatibility Probe Latest

- Timestamp: 2026-05-01 10:21:38 +09:00
- Host: 192.168.250.100
- Port: 1025
- Write check: disabled

| Target | Transport | Command | Status | Detail |
|---|---|---|---|---|
| SELF | tcp | 0101 read_type_name | OK | model=L16HCPU, model_code=0x48C2 |
| SELF | tcp | 0401 read_sm400 | OK | values=[True] |
| SELF | tcp | 0403 random_read | OK | words=[0], dwords=[0] |
| SELF | tcp | 0406 block_read | OK | words=[0, 0], bit_words=[0] |
