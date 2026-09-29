---
name: unity-ugui-tools
description: Use when inspecting, validating, temporarily previewing, or capturing Unity uGUI layouts through MCP tools named ugui_inspect, ugui_validate, ugui_preview_layout, or ugui_capture_matrix.
---

# Unity uGUI MCP workflow

Use these tools for existing Canvas/uGUI interfaces. Do not use them for UI Toolkit or IMGUI.

## Preconditions

1. Read `mcpforunity://editor/state` and wait until `data.advice.ready_for_tools` is true.
2. Read `mcpforunity://custom-tools` and confirm the four `ugui_*` tools are present.
3. Activate the MCP `ui` tool group when it is hidden.
4. Use exact hierarchy paths or instance IDs. If a name is ambiguous, inspect the returned candidates; never choose one by guess.

## Required sequence

1. Call `ugui_inspect` before proposing layout changes. Capture current hierarchy, relevant components, bounds, and the target resolution.
2. Call `ugui_validate` to establish the baseline findings.
3. For visual experiments, enter Play Mode and call `ugui_preview_layout` with one declarative transaction. Do not use arbitrary C# execution when the supported operations can express the preview.
4. Call `ugui_capture_matrix` for every requested resolution. Wait for the bounded polled job to complete.
5. Re-run `ugui_validate` and compare finding codes and paths against the baseline.
6. Call `ugui_preview_layout` action `restore` and verify the baseline hierarchy/values/findings return.
7. Apply persistent source, scene, or prefab changes only after the user approves the inspected preview. Use the project's documented persistent-edit workflow, not this preview tool.

## Constraints

- Never invent object names, fallback targets, layout rules, text, links, icons, resolutions, or visual behavior.
- Never treat `button_no_persistent_listener` as proof that a button is inert; runtime listeners are not observable by that check.
- `sibling_overlap` is opt-in because overlapping graphics can be intentional.
- Preview supports existing components only. Do not create components or silently substitute an unsupported property.
- Keep the transaction ID until restore succeeds. If a tool call fails, query `status`; use `restore_all` before leaving the task.
- Capture requires Play Mode and an open Game view. Treat any exact-dimension mismatch or restoration failure as a failed verification.
- Keep generated captures outside source control and delete them after review unless the user explicitly requests retention.

## Minimum completion evidence

- inspected object paths and raw bounds;
- baseline and preview validation summaries;
- capture paths with exact pixel dimensions;
- successful transaction restoration;
- zero Unity compilation errors/warnings caused by the tools;
- no unintended scene/prefab/project dirtiness.
