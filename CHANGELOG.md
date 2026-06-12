# Changelog

ECode 的用户可读变更记录。维护规则参见 `spec/06-roadmap.md` §3.2 与 `spec/07-implementation-backlog.md`。

格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)；roadmap 规划版本线从 `0.1.0` 起步（详见 `spec/06-roadmap.md` §3.1）。

> 下一节“未发布”条目将在每次 PR 合并后由维护者补全；建议结合 `git-cliff` 或 GitHub 自动 release-drafter 生成 release notes（见 backlog `M6-B-06`）。

## [Unreleased]

### Breaking changes

- 项目品牌与代码标识统一为 **ECode**（旧称 `cmux-windows`），覆盖以下外部契约：
  - 主程序：`cmuxw.exe` → `ecode-app.exe`
  - CLI：`cmux.exe` → `ecode.exe`
  - 守护进程：`cmux-daemon.exe` → `ecode-daemon.exe`
  - 解决方案：`Cmux.sln` → `ECode.sln`
  - C# 根命名空间：`Cmux.*` → `ECode.*`；类名 `CmuxSettings` → `ECodeSettings`
  - MCP / Agent 工具名：`cmux_status` / `cmux_pane_*` / `cmux_workspace_*` / `cmux_surface_*` / `cmux_split_*` / `cmux_notify` / `cmux_scaffold_agents_files` → `ecode_*`
- 命名管道与互斥体：
  - `\\.\pipe\cmux`、`\\.\pipe\cmux-{tag}` → `\\.\pipe\ecode`、`\\.\pipe\ecode-{tag}`
  - `\\.\pipe\cmux-daemon` → `\\.\pipe\ecode-daemon`
  - `Global\CmuxDaemon` → `Global\ECodeDaemon`
- 数据目录与配置文件：
  - `%LOCALAPPDATA%\cmux\` / `%LOCALAPPDATA%\ecode\` → `%USERPROFILE%\.ecode\`
  - `.cmux/cmux.json` / `%USERPROFILE%\.config\cmux\cmux.json` → `.ecode/ecode.json` / `%USERPROFILE%\.config\ecode\ecode.json`
  - CI artifact 名称：`cmux-windows-x64` → `ecode-windows-x64`、`cmux-cli-windows-x64` → `ecode-cli-windows-x64`
- 守护进程日志前缀：`[cmux-daemon]` → `[ecode-daemon]`。
- 主程序集版本起点：`1.0.6` → `0.1.0`（按 roadmap 重启版本线）。

### Compatibility

- 本期保留对旧接口 / 旧配置的 **读取兼容**：
  - 命名管道客户端若旧 `cmux` / `cmux-daemon` 管道存在，仍可正常通信。
  - 命令面板解析仍支持 `.cmux/cmux.json`（`M1-C` 阶段可关闭兼容；详见 spec/06-roadmap.md §6.3）。
- 运行时数据目录不做旧路径兼容：新版只读写 `%USERPROFILE%\.ecode\`，不会自动读取或迁移 `%LOCALAPPDATA%\ecode\` / `%LOCALAPPDATA%\cmux\`。
- 旧 `cmux_*` 工具名在 CLI 顶层命令里保留为薄封装 1 个小版本周期，之后下线。

### Migration

1. 升级到 `0.1.0` 之前，请停止旧版 `cmuxw.exe` / `ecode-app.exe`。
2. 卸载旧安装器；删除 `C:\Program Files\ECode` 旧安装目录（若已存在）。
3. 安装新版 `0.1.x`（`ecode-setup.exe` 或 `ecode-cli`）后，运行时数据只写入 `%USERPROFILE%\.ecode\`；旧 `%LOCALAPPDATA%\ecode\` / `%LOCALAPPDATA%\cmux\` 不再自动读取或迁移。
4. 自动化脚本中的 `cmux status` 等命令在 CLI 顶层继续可运行，但建议改写为 `ecode ...`。
5. Agent 集成方需更新 MCP 工具调用名为 `ecode_*`。

### Known issues

- 资产（`assets/screenshots/*.jpg`）的 alt 文本可能仍含“cmux”水印（计划在 `M7-A` 文档站同步刷新）。
- 旧注册表键（`HKCU\Software\Cmux\...`，计划 M6 hooks setup 时落地）将在 M6 阶段统一。

---

<!-- 之后逐版本补全（roadmap 版本线）：0.1.x、0.2.x、0.3.x、0.4.x、0.5.x、0.6.x、0.7.x、1.0.0 -->
