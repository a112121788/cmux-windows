# ECode 完整重命名计划（破坏性变更）

> 你的选择：完整重命名（项目结构 / 管道 / 二进制 / 数据目录 / MCP 工具 / 文档 / 安装器全部统一为 `ECode`）。此变更会破坏现有 agent 钩子、CI 脚本与已安装的 cmux，因此需要按里程碑拆分 PR。

## 1. 重命名总表

| 类别 | 旧 | 新 |
|---|---|---|
| 仓库目录（git mv） | `cmux-windows/` | `ECode/`（仓库根目录；不自动执行 `mv`，需你确认） |
| 解决方案 | `Cmux.sln` | `ECode.sln` |
| 主程序目录 | `src/Cmux/` | `src/ECode/` |
| 核心库目录 | `src/Cmux.Core/` | `src/ECode.Core/` |
| CLI 目录 | `src/Cmux.Cli/` | `src/ECode.Cli/` |
| 守护进程目录 | `src/Cmux.Daemon/` | `src/ECode.Daemon/` |
| 测试目录 | `tests/Cmux.Tests/` | `tests/ECode.Tests/` |
| 烟雾测试目录 | `tests/Cmux.Smoke/` | `tests/ECode.Smoke/` |
| 主程序产物 | `cmuxw.exe` | `ecode-app.exe` |
| CLI 产物 | `cmux.exe` | `ecode.exe` |
| 守护进程产物 | `cmux-daemon.exe` | `ecode-daemon.exe` |
| 程序集名 | `cmuxw` / `cmux` / `cmux-daemon` | `ecode-app` / `ecode` / `ecode-daemon` |
| 根命名空间 | `Cmux.*` | `ECode.*` |
| `cmux.json` 配置文件名 | `.cmux/cmux.json`、`cmux.json` | `.ecode/ecode.json`、`ecode.json`（保留 `cmux.json` 作为旧别名兼容期） |
| 命名管道（主应用） | `\\.\pipe\cmux`、`\\.\pipe\cmux-{tag}` | `\\.\pipe\ecode`、`\\.\pipe\ecode-{tag}` |
| 命名管道（守护进程） | `\\.\pipe\cmux-daemon` | `\\.\pipe\ecode-daemon` |
| 守护进程互斥体 | `Global\CmuxDaemon` | `Global\ECodeDaemon` |
| 数据目录 | `%LOCALAPPDATA%\cmux\` | `%LOCALAPPDATA%\ecode\`（保留旧目录作为读取兼容期） |
| 安装目录 | `publish/cmux-win-x64/`、`publish/cmux-cli/` | `publish/ecode-win-x64/`、`publish/ecode-cli/` |
| 资源 / 主题键 | `CmuxButton`、`CmuxTextBox` | `ECodeButton`、`ECodeTextBox` |
| MCP / Agent 工具名 | `cmux_status`、`cmux_pane_*`、`cmux_*` | `ecode_status`、`ecode_pane_*`、`ecode_*`（破坏性） |
| `%LOCALAPPDATA%\cmux\daemon-debug.log` 标识 | `[cmux-daemon]` | `[ecode-daemon]` |
| `STATUS.version` 起始版本 | `1.0.6`（程序集版本） | `0.1.0`（roadmap 已规划） |

## 2. 不重命名的项（明确边界）

- **上游引用**：spec/ 中描述 “上游 macOS 原版 `manaflow-ai/cmux`” 时保留 `cmux` 一词；只在描述本仓库时使用 `ECode`。
- **历史 `cmux.json` 字段 / 协议命令**：保留 v1 行为以做兼容垫片（`WORKSPACE.*`、`PANE.*`、`NOTIFY` 等）。
- **旧 `cmux.json` 配置文件路径**：本期仍可读取 `.cmux/cmux.json`，但写入位置改为 `.ecode/ecode.json`；日志会提示迁移。
- **旧 `%LOCALAPPDATA%\cmux\` 数据**：本期 `SessionPersistenceService` 启动时若 `ecode/` 不存在但 `cmux/` 存在，自动把 `session.json`、`snippets.json`、`agent/`、`logs/` 复制到 `ecode/`，并在 `daemon-debug.log` 写一条 `migrated-data` 事件。

## 3. 实施切片（5 个 PR）

### PR1：项目结构与代码命名空间（最大、最底层）

1. 仓库根用 `git mv` 移动目录（你执行 git mv，或者我先建新文件后用脚本 `git mv` 老文件）。
2. `git mv`：`src/Cmux` → `src/ECode`、`src/Cmux.Core` → `src/ECode.Core`、`src/Cmux.Cli` → `src/ECode.Cli`、`src/Cmux.Daemon` → `src/ECode.Daemon`、`tests/Cmux.Tests` → `tests/ECode.Tests`、`tests/Cmux.Smoke` → `tests/ECode.Smoke`、`Cmux.sln` → `ECode.sln`。
3. 改每个 `*.csproj`：
   - `RootNamespace`：C# 默认从目录名推；新目录已是 `ECode.*`，原 `using Cmux.*;` 全部改为 `using ECode.*;`。
   - `AssemblyName`：`cmuxw` → `ecode-app`、`cmux` → `ecode`、`cmux-daemon` → `ecode-daemon`。
   - `ProjectReference` 路径全部更新。
4. 改 `ECode.sln` 的 `Project` 行 + `GlobalSection(ProjectConfigurationPlatforms)` 的 GUID 段全部更新项目路径。
5. 全仓 `namespace Cmux` → `namespace ECode`、所有 `using Cmux.*;` 改为 `using ECode.*;`。
6. 所有 `x:Class="Cmux.Views.*"`、`clr-namespace:Cmux.*` 改为 `ECode.*`。
7. 资源键 `CmuxButton` → `ECodeButton`、`CmuxTextBox` → `ECodeTextBox`；`Style="{StaticResource CmuxButton}"` 同步更新。
8. 修改 `scripts/publish.ps1`：路径 `cmux-$Rid` / `cmux-$Rid-sc` / `cmux-cli` → `ecode-...`；`cmuxw.exe` / `cmux.exe` → `ecode-app.exe` / `ecode.exe`；清理路径 `src/Cmux/obj|bin` → `src/ECode/obj|bin`。
9. 修 `scripts/append-wide-tests.ps1` 里的硬编码 `C:\Users\...\cmux-windows\tests\Cmux.Tests\CoreTests.cs` → `tests/ECode.Tests/CoreTests.cs`（相对路径或脚本所在目录解析）。

### PR2：管道 / 互斥体 / 数据目录

1. `src/ECode.Core/IPC/NamedPipeServer.cs`：`_pipeName = "cmux"` / `"cmux-{tag}"` → `"ecode"` / `"ecode-{tag}"`；日志/注释同步。
2. `src/ECode.Core/IPC/DaemonClient.cs`：`PipeName = "cmux-daemon"` → `"ecode-daemon"`；启动探测中找 `cmux-daemon.exe` 的回退路径全部改为 `ecode-daemon.exe`，`Cmux.Daemon` 目录名改为 `ECode.Daemon`。
3. `src/ECode.Daemon/Program.cs`：`MutexName = "Global\\CmuxDaemon"` → `"Global\\ECodeDaemon"`；所有 `[cmux-daemon]` 日志前缀改为 `[ecode-daemon]`。
4. `src/ECode.Core/Services/{SessionPersistenceService,SnippetService,SecretStoreService,CommandLogService,AgentConversationStoreService}.cs`：`%LOCALAPPDATA%\cmux` → `%LOCALAPPDATA%\ecode`；首启自动迁移旧目录并写 `daemon-debug.log` 记录 `migrated-data`。
5. `src/ECode.Cli/Program.cs`：帮助文本中所有 `cmux` 字面量改为 `ecode`；`"cmux 1.0.6 (Windows)"` → `"ecode 0.1.0 (Windows)"`；`Console.Error` 中 `Could not connect to cmux` 改为 `ecode`；`Usage:` 提示同步。
6. `src/ECode/Views/MainWindow.xaml.cs` 等：窗口标题 `ECode` 保持；about 对话框 / 状态栏文案中 `cmux` → `ecode`；提示词字符串同步（`"running inside cmux"` → `"running inside ecode"`）。

### PR3：MCP / Agent 工具契约（破坏性，需文档配套）

1. `src/ECode/Services/AgentRuntimeService.cs`：所有 `cmux_status`、`cmux_workspace_*`、`cmux_surface_*`、`cmux_pane_*`、`cmux_split_*`、`cmux_notify` 改为 `ecode_*`。
2. `src/ECode.Cli/Program.cs` / 任何 `cmux *` 工具名（如果有）同步。
3. 同步更新 `spec/05-cli-commands.md` 与 `spec/06-roadmap.md` 中的工具清单与 `cmux_status` 例子。
4. `CHANGELOG.md` 顶部新增 “Breaking Changes” 段：列明 MCP 工具重命名、管道重命名、数据目录迁移、配置文件名重命名。

### PR4：文档与模板

1. 根 `README.md` / `README.en.md`：项目名、截图说明、徽章名称、安装命令中的 `cmuxw.exe` / `cmux.exe` 改为 `ecode-app.exe` / `ecode.exe`；`install PATH` 段落中“放入 PATH”的目录名同步。
2. `spec/01-architecture.md` ~ `spec/07-implementation-backlog.md`：所有本仓库自称处由 `cmux-windows` 改为 `ECode`；`STATUS.version` 起始 `0.1.0`；管道/互斥体/数据目录表格同步；`%LOCALAPPDATA%\cmux` → `%LOCALAPPDATA%\ecode`。
3. `.github/ISSUE_TEMPLATE/*.yml`：
   - `bug_report.yml`：版本字段提示从 `1.0.6` 改为 `0.1.0`；日志路径 `%LOCALAPPDATA%/cmux/daemon-debug.log` → `%LOCALAPPDATA%/ecode/daemon-debug.log`。
   - `cmux_json_schema.yml`：标题与提示改为 `ecode.json` / `.ecode/ecode.json`；与 macOS 兼容时单独说明（上游 `cmux.json` 仍可读）。
   - 其他模板中的 `cmux` 字面量同步。
4. `.github/PULL_REQUEST_TEMPLATE.md`、`config.yml`：链接文本更新。
5. `.github/workflows/ci.yml`：发布路径 `publish/app` / `publish/cli` 不动（已是中性名），但 `name: cmux-windows-x64` 改为 `ecode-windows-x64`；CLI artifact name 同步。
6. `CHANGELOG.md`：补 Unreleased 段；将 0.1.0 标记为 “unreleased — 即将发布”。

### PR5：安装器 / 注册表键 / 兼容性开关（远期，M6 之前不强制）

1. 新增 `installer/ecode.iss`（Inno Setup 模板），用 `ECode` 替换产品名、AppId、安装目录 `ECode\`。
2. `scripts/publish.ps1` 增加 `-ProductName ECode` 参数（默认 `ECode`）；同步所有 `out` 路径与 README 中的 install 步骤。
3. 注册表键（计划 M6 hooks setup 时再写）：`HKCU\Software\ECode\...`，迁移期可读 `HKCU\Software\Cmux\...`。

## 4. 验证清单

- `dotnet build ECode.sln -c Debug` 零警告通过（`TreatWarningsAsErrors=true`）。
- `dotnet test tests/ECode.Tests/ECode.Tests.csproj` 全绿。
- `dotnet run --project tests/ECode.Smoke/ECode.Smoke.csproj` 启动 ConPTY 成功，日志 `[ecode-daemon]`。
- `scripts/publish.ps1 -Flavor All` 在 PowerShell 下输出 `publish/ecode-win-x64/ecode-app.exe` / `publish/ecode-cli/ecode.exe`。
- 命名管道：PowerShell `[System.IO.Directory]::GetFiles("\\.\pipe\")` 含 `ecode`、`ecode-daemon`。
- 互斥体：单实例启动后，`Get-Item Global:\ECodeDaemon` 存在。
- 数据目录：首启 `%LOCALAPPDATA%\ecode\` 自动创建；旧 `%LOCALAPPDATA%\cmux\` 仍可读。
- MCP 工具：执行 `ecode_status` 返回 0.1.0；旧 `cmux_status` 在 CLI 中返回 `Unknown command`。
- 文档：每个 spec 章节中的本仓库代称都是 `ECode`；`STATUS.version` 注释为 `0.1.0`。
- `grep -RIn 'cmux' .` 限制为：上游 `manaflow-ai/cmux` 链接、`cmux.json` 旧路径兼容说明、`--compat-cmux` 兼容开关（如有）这三类显式标注的兼容边界。

## 5. 不做的事

- 不自动 `git mv` 仓库根目录到 `ECode/`（你按平台工具决定时机）。
- 不立刻改 `assets/` 里图片标题中含 “cmux” 的水印（如有），会列在 PR4 跟进。
- 不在 PR1-PR3 中触碰 M6 hooks setup 的注册表键（等 M6 落地时合并到一个独立 PR）。
- 不删除旧管道名 `cmux` / `cmux-daemon` / 旧目录 `%LOCALAPPDATA%\cmux\`，本期保持读取兼容；下线开关在 `ECodeSettings` 留位（M1-C 阶段处理）。

## 6. 风险与回滚

- **风险**：根命名空间 `Cmux` → `ECode` 改错会导致 `x:Class` 解析失败、XAML 资源键解析失败、ViewModel DI 失败。
  - 缓解：每改一个项目先 `dotnet build` 验证；保留 PR1 短提交历史便于 `git revert`。
- **风险**：MCP 工具重命名破坏现有 agent 集成。
  - 缓解：在 `ECode.Cli` 中保留 `cmux_status` 等老名字为薄封装，1 个小版本周期后下线；在 CHANGELOG 与 issue 模板明示。
- **风险**：CI 上传 artifact 名称变化影响现有下载链接。
  - 缓解：先在 CHANGELOG 显式标注 “artifact 名称从 cmux-windows-x64 改为 ecode-windows-x64”，保留 1 个版本作为 redirect 链接（若你确认要保留）。
- **回滚**：每个 PR 独立；任一 PR 失败可 `git revert <sha>`；数据目录兼容读取让 `cmux` → `ecode` 数据迁移可暂停。