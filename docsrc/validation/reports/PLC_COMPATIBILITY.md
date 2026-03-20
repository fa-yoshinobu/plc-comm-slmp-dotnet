# SLMP PLC Compatibility Database (Generated)

This document is generated from structured compatibility probe results.

- Source probe files: `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_fx5u/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_fx5uc/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_kv_xle02_mc/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_kv7500_mc/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_l16hcpu/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_l26cpu_bt_sn11112/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_q06udvcpu_sn17062/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_q26udehcpu_sn20081/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_qj71e71_100_sn24071/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_r00cpu/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_r08cpu/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_r08pcpu/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_r120pcpu/compatibility_probe_latest.json`, `D:/PLC_COMM_PROJ/plc-comm-slmp-python/internal_docsrc/compatibility_rj71en71/compatibility_probe_latest.json`
- Omit all-PENDING columns: yes

## 1. Protocol Capability Matrix

| PLC | Detected Model | **0101** | **0401** | **0403** | **0801** | **0802** | **0406** | **0619** | **0613** |
|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| R00CPU | R00CPU | YES | YES | YES | YES | YES | YES | YES | YES |
| R08CPU | R08CPU | YES | YES | YES | YES | YES | YES | YES | YES |
| R08PCPU | R08PCPU | YES | YES | YES | YES | YES | YES | YES | YES |
| R120PCPU | R120PCPU | YES | YES | YES | YES | YES | YES | YES | YES |
| RJ71EN71 | R08CPU | YES | YES | YES | YES | YES | YES | YES | YES |
| Q06UDVCPU_SN17062 | unknown_target | NO | PARTIAL | PARTIAL | PARTIAL | PARTIAL | NO | NO | NO |
| Q26UDEHCPU_SN20081 | unknown_target | NO | PARTIAL | PARTIAL | PARTIAL | PARTIAL | NO | NO | NO |
| QJ71E71_100_SN24071 | Q26UDHCPU, Q26UDEHCPU | YES | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | YES | YES |
| L26CPU_BT_SN11112 | unknown_target | NO | PARTIAL | PARTIAL | PARTIAL | PARTIAL | NO | NO | NO |
| L16HCPU | L16HCPU | YES | YES | YES | YES | YES | YES | YES | YES |
| FX5U | FX5U-32MR/DS | PARTIAL | PARTIAL | PARTIAL | NO | NO | PARTIAL | PARTIAL | NO |
| FX5UC | FX5UC-32MT/D | PARTIAL | PARTIAL | PARTIAL | NO | NO | PARTIAL | PARTIAL | NO |
| KV7500_MC | V7500 | YES | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | YES | YES |
| KV_XLE02_MC | V7500 | YES | PARTIAL | PARTIAL | PARTIAL | PARTIAL | PARTIAL | YES | NO |

### Command Legend

| Code | Name |
|:---:|:---|
| **0101** | Read Type Name |
| **0401** | Batch Read |
| **0403** | Random Read |
| **0801** | Monitor Entry |
| **0802** | Monitor Execute |
| **0406** | Block Read |
| **0619** | Self Test |
| **0613** | Buffer Read |

Status legend: `YES` = every executed probe for that command succeeded, `PARTIAL` = mixed outcomes across combinations or subprobes, `NO` = executed but no successful probe, `PENDING` = not executed.

## 2. Probe Coverage Notes

- `R00CPU`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=R00CPU
- `R08CPU`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=R08CPU
- `R08PCPU`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=R08PCPU
- `R120PCPU`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=R120PCPU
- `RJ71EN71`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=R08CPU; note=Ethernet module endpoint; `0101` reflects the attached CPU path rather than the module part number.
- `Q06UDVCPU_SN17062`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=unknown_target; note=No stable type-name identity was returned; the operator label is the only endpoint identifier here.
- `Q26UDEHCPU_SN20081`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=unknown_target; note=No stable type-name identity was returned; the operator label is the only endpoint identifier here.
- `QJ71E71_100_SN24071`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=Q26UDHCPU, Q26UDEHCPU; note=Ethernet module endpoint; `0101` reflects the attached CPU path rather than the module part number.
- `L26CPU_BT_SN11112`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=unknown_target; note=No stable type-name identity was returned; the operator label is the only endpoint identifier here.
- `L16HCPU`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=L16HCPU
- `FX5U`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=FX5U-32MR/DS
- `FX5UC`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=FX5UC-32MT/D
- `KV7500_MC`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=V7500; note=Third-party MC-compatible endpoint; results describe MC compatibility, not Mitsubishi native identity.
- `KV_XLE02_MC`: combos=3e/iqr, 3e/ql, 4e/iqr, 4e/ql, detected_model=V7500; note=Third-party MC-compatible endpoint; results describe MC compatibility, not Mitsubishi native identity.

## 3. Aggregated Non-OK Reasons

| Reason | Count | PLCs | Commands | Combos |
|:---|---:|:---|:---|:---|
| 0xC059 | 77 | Q06UDVCPU_SN17062, Q26UDEHCPU_SN20081, L26CPU_BT_SN11112, FX5U, +3 more | 0101, 0401, 0403, 0801, 0802, 0406, +2 more | 3e/ql, 3e/iqr, 4e/ql, 4e/iqr |
| timed out | 48 | Q06UDVCPU_SN17062, Q26UDEHCPU_SN20081, L26CPU_BT_SN11112 | 0101, 0401, 0403, 0801, 0802, 0406, +2 more | 4e/ql, 4e/iqr |
| connection closed | 24 | FX5U, FX5UC | 0101, 0401, 0403, 0801, 0802, 0406, +2 more | 3e/iqr, 4e/iqr, 4e/ql |
| 0xC061 | 10 | QJ71E71_100_SN24071 | 0401, 0403, 0801, 0802, 0406 | 3e/iqr, 4e/iqr |
| 0xC056 | 4 | KV_XLE02_MC | 0613 | 3e/ql, 3e/iqr, 4e/ql, 4e/iqr |

## 4. Practical Recommended Profiles

- `R00CPU`: prefer 3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `R08CPU`: prefer 3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `R08PCPU`: prefer 3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `R120PCPU`: prefer 3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `RJ71EN71`: prefer 3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `Q06UDVCPU_SN17062`: prefer 3e/ql; limits=0101, 0406, 0619, 0613
- `Q26UDEHCPU_SN20081`: prefer 3e/ql; limits=0101, 0406, 0619, 0613
- `QJ71E71_100_SN24071`: prefer 3e/ql, 4e/ql
- `L26CPU_BT_SN11112`: prefer 3e/ql; limits=0101, 0406, 0619, 0613
- `L16HCPU`: prefer 3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `FX5U`: prefer 3e/ql, 4e/ql; limits=0801, 0802, 0613
- `FX5UC`: prefer 3e/ql; limits=0801, 0802, 0613
- `KV7500_MC`: prefer 3e/ql, 4e/ql
- `KV_XLE02_MC`: prefer 3e/ql, 4e/ql; limits=0613

## 5. Non-Recommended Combinations

- `Q06UDVCPU_SN17062`: avoid 3e/iqr, 4e/ql, 4e/iqr; reasons=0xC059, timed out
- `Q26UDEHCPU_SN20081`: avoid 3e/iqr, 4e/ql, 4e/iqr; reasons=0xC059, timed out
- `QJ71E71_100_SN24071`: avoid 3e/iqr, 4e/iqr; reasons=0xC061
- `L26CPU_BT_SN11112`: avoid 3e/iqr, 4e/ql, 4e/iqr; reasons=0xC059, timed out
- `FX5U`: avoid 3e/iqr, 4e/iqr; reasons=connection closed
- `FX5UC`: avoid 3e/iqr, 4e/ql, 4e/iqr; reasons=0xC059, connection closed
- `KV7500_MC`: avoid 3e/iqr, 4e/iqr; reasons=0xC059
- `KV_XLE02_MC`: avoid 3e/iqr, 4e/iqr; reasons=0xC059, 0xC056

## 6. Product-Family Conclusions

- `iQ-R`: members=R00CPU, R08CPU, R08PCPU, R120PCPU, RJ71EN71; preferred_profiles=3e/ql, 3e/iqr, 4e/ql, 4e/iqr; notes=Ethernet module endpoint; `0101` reflects the attached CPU path rather than the module part number.
- `MELSEC-Q`: members=Q06UDVCPU_SN17062, Q26UDEHCPU_SN20081, QJ71E71_100_SN24071; preferred_profiles=3e/ql, 4e/ql; common_non_ok_reasons=0xC059, timed out, 0xC061; notes=No stable type-name identity was returned; the operator label is the only endpoint identifier here., Ethernet module endpoint; `0101` reflects the attached CPU path rather than the module part number.
- `MELSEC-L`: members=L26CPU_BT_SN11112; preferred_profiles=3e/ql; common_non_ok_reasons=0xC059, timed out; notes=No stable type-name identity was returned; the operator label is the only endpoint identifier here.
- `iQ-L`: members=L16HCPU; preferred_profiles=3e/ql, 3e/iqr, 4e/ql, 4e/iqr
- `iQ-F`: members=FX5U, FX5UC; preferred_profiles=3e/ql, 4e/ql; common_non_ok_reasons=connection closed, 0xC059
- `Third-Party MC-Compatible`: members=KV7500_MC, KV_XLE02_MC; preferred_profiles=3e/ql, 4e/ql; common_non_ok_reasons=0xC059, 0xC056; notes=Third-party MC-compatible endpoint; results describe MC compatibility, not Mitsubishi native identity.

## 7. Detailed PARTIAL or NO Breakdown


### Q06UDVCPU_SN17062

- `0101 Read Type Name`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0801 Monitor Entry`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0802 Monitor Execute`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0406 Block Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0619 Self Test`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0613 Buffer Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)

### Q26UDEHCPU_SN20081

- `0101 Read Type Name`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0801 Monitor Entry`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0802 Monitor Execute`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0406 Block Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0619 Self Test`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0613 Buffer Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)

### QJ71E71_100_SN24071

- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC061); 4e/ql=OK; 4e/iqr=NG (0xC061)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC061); 4e/ql=OK; 4e/iqr=NG (0xC061)
- `0801 Monitor Entry`: 3e/ql=OK; 3e/iqr=NG (0xC061); 4e/ql=OK; 4e/iqr=NG (0xC061)
- `0802 Monitor Execute`: 3e/ql=OK; 3e/iqr=NG (0xC061); 4e/ql=OK; 4e/iqr=NG (0xC061)
- `0406 Block Read`: 3e/ql=OK; 3e/iqr=NG (0xC061); 4e/ql=OK; 4e/iqr=NG (0xC061)

### L26CPU_BT_SN11112

- `0101 Read Type Name`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0801 Monitor Entry`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0802 Monitor Execute`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0406 Block Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0619 Self Test`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)
- `0613 Buffer Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (timed out); 4e/iqr=NG (timed out)

### FX5U

- `0101 Read Type Name`: 3e/ql=OK; 3e/iqr=NG (connection closed); 4e/ql=OK; 4e/iqr=NG (connection closed)
- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (connection closed); 4e/ql=OK; 4e/iqr=NG (connection closed)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (connection closed); 4e/ql=OK; 4e/iqr=NG (connection closed)
- `0801 Monitor Entry`: 3e/ql=NG (0xC059); 3e/iqr=NG (connection closed); 4e/ql=NG (0xC059); 4e/iqr=NG (connection closed)
- `0802 Monitor Execute`: 3e/ql=NG (0xC059); 3e/iqr=NG (connection closed); 4e/ql=NG (0xC059); 4e/iqr=NG (connection closed)
- `0406 Block Read`: 3e/ql=OK; 3e/iqr=NG (connection closed); 4e/ql=OK; 4e/iqr=NG (connection closed)
- `0619 Self Test`: 3e/ql=OK; 3e/iqr=NG (connection closed); 4e/ql=OK; 4e/iqr=NG (connection closed)
- `0613 Buffer Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (connection closed); 4e/ql=NG (0xC059); 4e/iqr=NG (connection closed)

### FX5UC

- `0101 Read Type Name`: 3e/ql=OK; 3e/iqr=OK; 4e/ql=NG (connection closed); 4e/iqr=OK
- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (connection closed); 4e/iqr=NG (0xC059)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (connection closed); 4e/iqr=NG (0xC059)
- `0801 Monitor Entry`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (connection closed); 4e/iqr=NG (0xC059)
- `0802 Monitor Execute`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (connection closed); 4e/iqr=NG (0xC059)
- `0406 Block Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=NG (connection closed); 4e/iqr=NG (0xC059)
- `0619 Self Test`: 3e/ql=OK; 3e/iqr=OK; 4e/ql=NG (connection closed); 4e/iqr=OK
- `0613 Buffer Read`: 3e/ql=NG (0xC059); 3e/iqr=NG (0xC059); 4e/ql=NG (connection closed); 4e/iqr=NG (0xC059)

### KV7500_MC

- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0801 Monitor Entry`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0802 Monitor Execute`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0406 Block Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)

### KV_XLE02_MC

- `0401 Batch Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0403 Random Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0801 Monitor Entry`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0802 Monitor Execute`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0406 Block Read`: 3e/ql=OK; 3e/iqr=NG (0xC059); 4e/ql=OK; 4e/iqr=NG (0xC059)
- `0613 Buffer Read`: 3e/ql=NG (0xC056); 3e/iqr=NG (0xC056); 4e/ql=NG (0xC056); 4e/iqr=NG (0xC056)

