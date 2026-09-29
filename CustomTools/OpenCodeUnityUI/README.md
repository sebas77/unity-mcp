# OpenCode Unity uGUI tools

Editor-only custom tools for inspecting, validating, temporarily previewing, and capturing Unity uGUI through MCP for Unity. The tools are project-scoped C# extensions discovered through `[McpForUnityTool]`; they do not add runtime/player code or modify the MCP server.

## Requirements

- MCP for Unity with project custom-tool discovery.
- Unity uGUI (`UnityEngine.UI`) and TextMesh Pro (`Unity.TextMeshPro`).
- An open Game view for `ugui_capture_matrix`.

## Deploy

From the `unity-mcp-fork` repository:

```powershell
pwsh -File scripts/deploy-opencode-unity-ui.ps1 -ProjectPath C:\path\to\UnityProject -Clean
```

For a project with the writable local MCP package at `Packages/unity-mcp`, deploy beside that package's source instead:

```powershell
pwsh -File scripts/deploy-opencode-unity-ui.ps1 -ProjectPath C:\path\to\UnityProject -Destination Package -Clean
```

Add `-IncludeTests` for a test deployment. Use `-Remove` to remove the selected destination. Do not deploy both destinations simultaneously because duplicate tool names would be registered. The script owns only `Assets/Editor/OpenCodeUnityUI` or `Packages/unity-mcp/CustomTools/OpenCodeUnityUI`; it never edits `Packages/manifest.json`, package locks, scenes, prefabs, or project settings.

After Unity compiles the deployment, reconnect the MCP client so it refreshes dynamic tools. Confirm all four tools in `mcpforunity://custom-tools`:

- `ugui_inspect`
- `ugui_validate`
- `ugui_preview_layout`
- `ugui_capture_matrix`

The tools use group `ui`; activate that group if the client hides non-core tools.

## Workflow

1. Inspect exact hierarchy paths, component values, and screen bounds.
2. Validate the baseline.
3. Enter Play Mode and apply one declarative preview transaction.
4. Capture the requested resolution matrix and validate again.
5. Restore the transaction. Preview transactions also restore on Play Mode exit, assembly reload, and Editor quit.
6. Implement approved persistent changes through the project's normal source/scene/prefab workflow.

`ugui_preview_layout` deliberately has no persistent apply action. It supports existing components only and validates every operation before the first mutation.

## Selectors

Selectors accept:

- a GameObject instance ID;
- a full path such as `SceneName:/Canvas/Panel/Button`;
- a scene-relative path such as `Canvas/Panel/Button`;
- an exact object name only when unambiguous.

Ambiguous names return every candidate path instead of selecting one.

## Tool examples

```json
{
  "tool_name": "ugui_inspect",
  "parameters": {
    "root": "Canvas",
    "targets": ["Canvas/MainPanel"],
    "max_depth": 4,
    "include_text": false
  }
}
```

```json
{
  "tool_name": "ugui_validate",
  "parameters": {
    "root": "Canvas",
    "checks": ["off_screen", "parent_clipping", "zero_size", "text_overflow", "button_readiness", "event_system_count"]
  }
}
```

```json
{
  "tool_name": "ugui_preview_layout",
  "parameters": {
    "action": "apply",
    "transaction_id": "menu-preview",
    "operations": [
      {
        "target": "Canvas/Card",
        "operation": "reparent",
        "parent": "Canvas/CardRow",
        "sibling_index": 0
      },
      {
        "target": "Canvas/Card",
        "operation": "set_properties",
        "component": "LayoutElement",
        "properties": { "preferred_width": 300, "flexible_width": 1 }
      }
    ]
  }
}
```

Restore with `{"action":"restore","transaction_id":"menu-preview"}` or use `restore_all`.

```json
{
  "tool_name": "ugui_capture_matrix",
  "parameters": {
    "action": "start",
    "resolutions": [
      { "width": 1920, "height": 1080, "label": "16x9" },
      { "width": 2560, "height": 1080, "label": "ultrawide" },
      { "width": 3440, "height": 1440, "label": "ultrawide-large" }
    ],
    "file_prefix": "menu-preview",
    "frames_to_wait": 2
  }
}
```

Capture is a polled job with a 120-second deadline. It captures the final frame through `ScreenCapture.CaptureScreenshotAsTexture`, including Screen Space Overlay UI, and restores selected Game-view size, target display, and its temporary custom sizes.

## OpenCode skill

`SKILL.md` is the canonical skill source. Copy it to `.opencode/skills/unity-ugui-tools/SKILL.md` or register this directory in `skills.paths`. Restart OpenCode after installation because skill/config files are loaded only at startup.

## Tests

Deploy with `-IncludeTests`, allow Unity to compile, then run assembly `OpenCode.UnityUI.Editor.Tests` in EditMode. Exact Overlay capture and Play-mode transaction behavior are then verified through the live MCP workflow because EditMode test-runner Play transitions do not preserve the interactive Game view reliably.
