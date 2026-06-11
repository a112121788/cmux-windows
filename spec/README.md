# ECode 设计文档（`spec/`）

本目录收录 ECode 项目的工程内部设计文档。它不同于面向最终用户的 `README.md` / `docs/`，目标是：

- 让新贡献者在 30 分钟内理解项目结构、关键模块、数据契约与发布流程。
- 为规划、能力对齐与技术决策提供单一可信来源。
- 让每次架构 / 协议 / 数据模型变更先在此处收敛，再落到代码。

> 范围：本仓库仅做 **Windows 原生版**（WPF + ConPTY + WebView2 + Named Pipe + .NET 10）。本文不规划 macOS / Linux 端实现。
>
> 品牌：项目代号 **ECode**（旧称 `cmux-windows`，参见 `CHANGELOG.md` 的 “Breaking changes” 段）。`cmux` 一词在 spec/ 中仅作为上游 macOS 原版（`manaflow-ai/cmux`）的引用、协议 v1 命令名（`WORKSPACE.* / PANE.*`）与配置文件兼容名（`cmux.json`）出现。

---

## 阅读顺序

1. [`01-architecture.md`](01-architecture.md) — 整体架构、技术栈、进程模型、数据流、关键设计决策、持久化、安全、部署。
2. [`02-modules.md`](02-modules.md) — 按模块 / 类梳理职责、关键方法、协作链路。
3. [`03-data-and-ipc.md`](03-data-and-ipc.md) — 数据模型、命名管道协议、JSON 形状、错误码。
4. [`04-build-deploy.md`](04-build-deploy.md) — 解决方案布局、构建脚本、发布形态、运行依赖、故障排查。
5. [`05-cli-commands.md`](05-cli-commands.md) — `ecode.exe` CLI 与 IPC 命令参考。
6. [`06-roadmap.md`](06-roadmap.md) — 详细开发规划（M0-M7）、依赖关系、风险与指标。
7. [`07-implementation-backlog.md`](07-implementation-backlog.md) — 可执行的 GitHub Issue / PR backlog（按里程碑和依赖排序）。
8. [`08-dotnet-csharp-handbook.md`](08-dotnet-csharp-handbook.md) — 给只有 Ruby / Python 经验的贡献者准备的 .NET 10 / C# 速通手册，把本仓库实际用到的语言特性、工具链与运行时概念串成单一入口。

---

## 文档维护规则

- 任何架构 / 协议 / 数据模型 / 命名管道 / CLI 命令的变更，必须同步更新本目录下的对应文档，并在 PR 描述里写明“spec 改动点”。
- 任何 roadmap 范围变更（新增 / 删除 / 改优先级）必须同时更新 `06-roadmap.md` 与 `07-implementation-backlog.md`。
- 当文档与源码出现冲突时：以源码为准，并在文档里加 `// TODO(spec): align with src/...` 注明。修复冲突需在同一个 PR 内完成。
- 每两周一个迭代结束后，刷新 `06-roadmap.md` 的勾选状态、风险登记、成功指标与 `07-implementation-backlog.md` 的进度。
- `08-dotnet-csharp-handbook.md` 跟随代码演进：当新增 C# 14+ 特性、第三方包、`unsafe`/P/Invoke 调用、`CommunityToolkit.Mvvm` 用法或 xUnit 写法时，相应小节要保持与 `src/` 一致。

---

## 与其他目录的关系

| 目录 | 角色 | 受众 |
|---|---|---|
| `spec/` | 工程内部设计 | 贡献者 / 维护者 |
| `docs/`（规划中，详见 M7） | 用户与运维文档 | 最终用户 / 集成方 |
| `README.md` / `README.en.md` | 项目入口、特性与使用概览 | 所有人 |
| `src/` | 实际实现 | 贡献者 |
| `tests/` | 单元 / 烟雾测试 | 贡献者 |
| `scripts/` | 构建与发布脚本 | 维护者 / 发布者 |

---

## 当前状态

| 文档 | 状态 | 最近一次大改 |
|---|---|---|
| `01-architecture.md` | 与源码一致（待补 v2 协议影响） | 当前 PR |
| `02-modules.md` | 与源码一致 | 当前 PR |
| `03-data-and-ipc.md` | 与源码一致（待补 v2 协议章节） | 当前 PR |
| `04-build-deploy.md` | 与源码一致 | 当前 PR |
| `05-cli-commands.md` | 与源码一致（CLI 顶层命令范围已修正） | 当前 PR |
| `06-roadmap.md` | 详细规划完成 | 当前 PR |
| `07-implementation-backlog.md` | 初始版本（与 M0-M7 对应） | 当前 PR |
| `08-dotnet-csharp-handbook.md` | 初始版本（针对 Ruby/Python 背景贡献者） | 当前 PR |

> 验证方法：在每个 PR 合并前，至少跑一次 `wc -l spec/*.md` 与 `git diff --stat spec/` 复核改动量。
