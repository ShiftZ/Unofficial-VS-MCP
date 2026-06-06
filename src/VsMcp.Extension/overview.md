# VS MCP Server

**Model Context Protocol (MCP) server for Visual Studio** — Enables AI agents like Claude Code to control Visual Studio features including debugging, building, editing, and UI automation.

> This is a **beta release**. Feedback and bug reports are welcome on [GitHub Issues](https://github.com/dhq-boiler/Unoffcial-VS-MCP/issues).

## Features

VS MCP Server exposes tools across the following categories:

### General
- Execute VS commands, get IDE status, and view available tool help

### Solution & Project
- Open/close solutions, list projects, get project details

### Build
- Build solution/project, clean, rebuild, and retrieve build errors

### Editor
- Open/close/read/write/edit files, get active document info, search in files

### Debugger
- Start/stop/restart debugging, attach to processes, step over/into/out, get call stack, locals, threads, evaluate expressions

### Breakpoints
- Set/remove/list breakpoints, conditional breakpoints, hit count breakpoints, function breakpoints

### Output & Diagnostics
- Read/write output panes, get error list items, view XAML binding errors

### UI Automation
- Capture window/region screenshots, inspect UI element trees, find/click/invoke UI elements

### Advanced Debug
- Watch variables, thread management (switch/freeze/thaw), process management, immediate window execution, module listing, CPU register inspection, exception settings, memory reading, parallel stacks/watch/tasks

## Requirements

- **Visual Studio 2022** (17.0 or later) — Community, Professional, or Enterprise edition
- **Windows** (amd64)
- **.NET Framework 4.8**
- **.NET 8.0 Runtime** (for the StdioProxy component)

## Installation

1. Install the extension from Visual Studio Marketplace or download the `.vsix` file
2. Restart Visual Studio
3. The MCP server starts automatically when Visual Studio launches

### Connecting with Claude Code

Add the following to your Claude Code MCP configuration:

```json
{
  "mcpServers": {
    "vs-mcp": {
      "command": "%LOCALAPPDATA%\\VsMcp\\bin\\VsMcp.StdioProxy.exe"
    }
  }
}
```

## How It Works

The extension runs an HTTP-based MCP server inside Visual Studio. A lightweight StdioProxy bridges stdio-based MCP clients (like Claude Code) to the HTTP server, enabling seamless communication.

When Visual Studio is not running, the StdioProxy provides offline responses for basic protocol operations and returns cached tool definitions.

## Source Code

[GitHub Repository](https://github.com/dhq-boiler/Unoffcial-VS-MCP)

## License

[MIT License](https://github.com/dhq-boiler/Unoffcial-VS-MCP/blob/main/LICENSE.txt)
