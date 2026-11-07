# Shapes URP repair — 2026-11-07

- Original seven files: `before-20261107-142509/` (six code/config files plus environment documentation).
- `editor-verification.txt`: in-editor verification summary; not Play mode or VR acceptance.
- `editor-pool-samples.csv`: sampled pool occupancy and queued command counts.
- `ShapesUrpRepairProbe.cs`: archived temporary editor observer, removed from Assets after sampling.
- Renderer Features, compatibility settings and guards have been applied without upgrading Shapes or building a player.
- Unity automatically serialized `m_FallOffLookup` while importing the global URP settings.
