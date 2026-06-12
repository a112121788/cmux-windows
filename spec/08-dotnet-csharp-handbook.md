# .NET 与 C# 学习手册（ECode 版）

> 目标读者：只有 Ruby / Python 经验的工程师，第一次进入 ECode（`.NET 10` + `WPF` + `ConPTY` + `Named Pipe` + `xUnit`）代码库。
> 范围：把 C# / .NET 里和 Ruby、Python 差别最大、并且会真实出现在本项目里的概念串成一份“速通”手册。不做穷举式语法教学，重点是建立**和本仓库对齐的心智模型**。
> 配套阅读：[`01-architecture.md`](01-architecture.md) · [`02-modules.md`](02-modules.md) · [`03-data-and-ipc.md`](03-data-and-ipc.md) · [`04-build-deploy.md`](04-build-deploy.md) · [`05-cli-commands.md`](05-cli-commands.md) · [`06-roadmap.md`](06-roadmap.md) · [`07-implementation-backlog.md`](07-implementation-backlog.md)。

---

## 0. 速查：和 Ruby / Python 的最关键差异

| 维度 | Ruby / Python | C# / .NET |
|---|---|---|
| 执行模型 | 解释执行 + 字节码 | 编译到 **IL**（中间语言），运行时由 **CLR JIT**（或 AOT）编译成机器码 |
| 类型系统 | 动态、鸭子类型 | 静态、显式声明（但 `dynamic` 可选） |
| 可空性 | `nil` / `None` 默认合法 | `Nullable=enable` 下，`string?` 表示可空，`string` 表示**保证非空**（编译器静态分析） |
| 并发模型 | 线程 + GIL（Python）/ GVL（Ruby MRI）；IO 用 `asyncio` / `EventMachine` | 多线程 + `async/await` 协程（基于 `Task`），**不**用 GIL |
| 包管理 | `Gemfile` + `bundle` / `pyproject.toml` + `uv`/`pip` | `csproj` + `NuGet`（`.NET CLI` 用 `dotnet add package`） |
| 项目结构 | `lib/ spec/ test/` | `.sln` + 一组 `.csproj`（类库 / `Exe` / `WinExe`） |
| 入口 | `Gemfile` / `__main__` | 每个 `Exe` / `WinExe` 项目有 `Main` 方法；WPF 由 `App.xaml` 启动 |
| 跨平台 | 默认 | .NET 是跨平台的，**但本仓库只面向 Windows**（`net10.0-windows*`） |
| 生态 | Rails、Django、Gem、PyPI | ASP.NET Core、MAUI、WPF、WinUI、Entity Framework |

**一句话先记住**：C# 是“**强类型 + 编译期静态分析 + 显式资源管理**”的语言，`.NET` 既是运行时也是类库体系，写 ECode 时你大部分时间都在和**类型、生命周期、async、interop** 打交道。

---

## 1. 工具链与项目结构

### 1.1 安装与版本

- 安装 [.NET 10 SDK](https://dotnet.microsoft.com/download)（含 Desktop Runtime）。
- 仓库根的 `global.json` 把 SDK 钉到 `10.0.301`，并允许 `rollForward=latestFeature`，所以本地要装 **10.0.x** 系列，不要用 9.x 或预览版：

  ```json
  {
    "sdk": {
      "version": "10.0.301",
      "rollForward": "latestFeature"
    }
  }
  ```

- 自检：

  ```powershell
  dotnet --version           # 应输出 10.0.x
  dotnet --list-sdks         # 列出本机所有 SDK
  dotnet workload list       # WPF 需要 desktop runtime（默认包含）
  ```

### 1.2 解决方案与项目

`.sln` 文件是“IDE 友好的项目集合”，一个仓库通常只有一个。`.csproj` 是单个项目的 MSBuild 定义（类似 `Gemfile` + Rakefile 的合体）：

```text
ECode.sln
├── src/
│   ├── ECode/                # WinExe: ecode-app.exe（WPF 主程序）
│   ├── ECode.Cli/            # Exe:   ecode.exe
│   ├── ECode.Core/           # Library: 跨进程复用
│   └── ECode.Daemon/         # WinExe: ecode-daemon.exe
└── tests/
    ├── ECode.Tests/          # xUnit
    └── ECode.Smoke/          # ConPTY 集成烟雾测试
```

常见 `csproj` 关键字段：

| 字段 | 含义 | 典型值（本项目） |
|---|---|---|
| `TargetFramework` | 目标 TFM | `net10.0-windows`（库、CLI、Daemon），`net10.0-windows10.0.17763.0`（WPF） |
| `OutputType` | 产物类型 | `Library` / `Exe`（控制台） / `WinExe`（无控制台窗口） |
| `AssemblyName` | 程序集名 | `ecode-app` / `ecode` / `ECode.Core` |
| `RootNamespace` | 默认命名空间 | `ECode.Core` |
| `Nullable` | 启用可空引用类型 | `enable` |
| `LangVersion` | C# 语言版本 | `14`（由 `Directory.Build.props` 统一） |
| `ImplicitUsings` | 启用隐式 `using` | `enable` |
| `TreatWarningsAsErrors` | 警告即错误 | `true`（**全仓库强制**） |
| `AllowUnsafeBlocks` | 允许 `unsafe` | 仅 `ECode.Core` 为 `true`（ConPty Interop 用） |

> 类比：`csproj` ≈ `Gemfile` + `package.gemspec`；`.sln` ≈ Gemfile + 工作区元数据；`obj/`、`bin/` ≈ `.bundle/` 缓存。

### 1.3 全局编译开关

`Directory.Build.props` 会**自动被所有子项目继承**，相当于一个“仓库级 Rakefile / `pyproject.toml [tool.ruff]`”：

```xml
<Project>
  <PropertyGroup>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <WarningLevel>7</WarningLevel>
  </PropertyGroup>
</Project>

```

这意味着：

- 你写的代码必须 **0 warning 0 error** 才能 `dotnet build` 通过（`WarningLevel=7` 比默认 `4` 严格，会开启 style/quality 警告）。
- `Nullable=enable` 是“语言级别的类型安全网”，不是 `.nullable.rb` 那种运行期补丁——编译器**静态地**追踪 `string?` 的可空性。
- `ImplicitUsings=enable` 自动导入 `System`、`System.Collections.Generic`、`System.IO`、`System.Linq` 等，省去每个文件顶部的 `using`。

### 1.4 常用 `dotnet` 命令

| 场景 | 命令 | 对应 Ruby/Python |
|---|---|---|
| 还原包 | `dotnet restore` | `bundle install` / `uv sync` |
| 编译 | `dotnet build ECode.sln -c Debug` | 编译 gem 扩展 / `python -m build` |
| 跑 WPF | `dotnet run --project src/ECode/ECode.csproj` | `bundle exec ruby bin/ecode` |
| 单元测试 | `dotnet test tests/ECode.Tests/ECode.Tests.csproj` | `bundle exec rspec` / `pytest` |
| 加包 | `dotnet add src/ECode.Core package System.IO.Pipelines` | `bundle add` / `uv add` |
| 加项目引用 | `dotnet add src/ECode reference src/ECode.Core` | — |
| 格式化 | `dotnet format` | `rubocop -a` / `ruff format` |
| 发布 | `dotnet publish ... -r win-x64 --self-contained true` | `gem build` + `gem push` |

> WPF 项目第一次构建很慢（生成 `*.g.cs` 等临时文件）。`scripts/publish.ps1` 会在开始前清理 `obj/bin` 避免 WPF 临时 csproj 残留——本地反复 `clean` 时也可以参考这个动作。

---

## 2. 语言核心（和 Ruby / Python 差别最大的部分）

### 2.1 静态类型 + `Nullable`

```csharp
// 文件：src/ECode.Core/Models/Workspace.cs（示意）
namespace ECode.Core.Models;

public sealed class Workspace
{
    public string Id { get; }
    public string Name { get; set; }               // 非空 string
    public string? Description { get; set; }       // 可空 string?
    public List<Surface> Surfaces { get; } = new();

    public Workspace(string id, string name)
    {
        Id   = id ?? throw new ArgumentNullException(nameof(id));
        Name = name;                              // 编译器知道这里非空
    }
}
```

对应 Ruby：

```ruby
class Workspace
  attr_reader :id
  attr_accessor :name, :description
  def initialize(id, name)
    @id = id
    @name = name
  end
end
```

关键点：

1. **可空性是类型的一部分**。`string?` ≠ `string`，编译器在 `Nullable=enable` 下会拒绝 `Workspace` 把 `Description` 当非空用。
2. **所有引用类型默认是“可能为 null”**，要靠 `?` 标记成可空，或靠 `[NotNullWhen(true)]` / `throw` 等告诉编译器“到这步一定非空”。
3. **类比**：`string?` ≈ TypeScript 的 `string | undefined`；`string` ≈ `string`（开了 `strictNullChecks`）。

### 2.2 值类型 vs 引用类型

| 类别 | 行为 | 例子 |
|---|---|---|
| **引用类型**（`class`、`record`、`interface`、`delegate`、`string`、`array`） | 堆上分配，变量持有的是引用；赋值复制引用 | `Workspace`、`List<T>`、`string` |
| **值类型**（`struct`、`record struct`、`enum`） | 通常栈上分配，赋值复制整个值；不能为 `null`（除非 `Nullable<T>` / `T?`） | `int`、`Guid`、`DateTime`、`(int, int)` 元组 |

本仓库典型场景：

- `ConPtyInterop.COORD` / `STARTUPINFO` / `SECURITY_ATTRIBUTES` 都是 `struct`，因为它们是给 P/Invoke 用的**连续内存布局**，必须按值传。
- `Workspace` / `Surface` / `SplitNode` 是 `class`，因为需要多态、引用相等、可空。

```csharp
// 引用类型
var a = new Workspace("w1", "main");
var b = a;            // b 和 a 指向同一个对象
b.Name = "renamed";
// a.Name == "renamed"  ← true

// 值类型
var p1 = (X: 1, Y: 2);
var p2 = p1;          // 复制整个 tuple
p2.X = 99;
// p1.X == 1          ← 不受影响
```

### 2.3 `record` 和 `record class` / `record struct`

C# 9+ 起 `record` 是“按值相等 + 不可变”语义的语法糖，非常适合 DTO / 不可变模型：

```csharp
public record struct CellCoord(int Row, int Col);                 // 值类型
public sealed record PaneSnapshot(string PaneId, string Title, int ExitCode);
```

特点：

- 自动生成 `Equals` / `GetHashCode`（按成员比较），避免 Ruby 那种“`==` 比较引用”的陷阱。
- `with` 表达式可以复制并改字段：

  ```csharp
  var updated = snapshot with { Title = "build" };
  ```

- 本项目 DTO（如 IPC 消息）几乎都可以用 `record`。

### 2.4 模式匹配与 `switch` 表达式

C# 的 `is`、`switch` 表达式比 `case/when` 更结构化，**优先用**：

```csharp
public static string Describe(ExitReason reason) => reason switch
{
    ExitReason.Normal        => "ok",
    ExitReason.Signal        => "signaled",
    ExitReason.Crashed       => "crashed",
    ExitReason.TimedOut      => "timed out",
    _                        => "unknown",
};

if (msg is DaemonResponse.Ok ok)
{
    Use(ok.Payload);
}
else if (msg is DaemonResponse.Error err)
{
    Log(err.Code, err.Message);
}
```

对应 Ruby：

```ruby
case reason
when :normal then 'ok'
when :signal then 'signaled'
else 'unknown'
end
```

### 2.5 `string` 是不可变 + 驻留

和 Python `str` 类似，但更激进：字符串字面量会**驻留**（intern），`==` 走引用相等；想按值比较就用 `string.Equals(a, b, StringComparison.Ordinal)`。

```csharp
"file".ToUpperInvariant() == "FILE"   // true（按值）
```

**坑**：不要在循环里 `s += x`（会反复分配），用 `StringBuilder` 或 `string.Create`。

### 2.6 集合：数组 / `List<T>` / `Span<T>`

| 类型 | 用途 |
|---|---|
| `T[]` | 定长、性能最好；interop（`MarshalAs`）必用 |
| `List<T>` | 动态数组，类似 `Array` / `list` |
| `Dictionary<K,V>` | 哈希表，类似 `Hash` / `dict` |
| `Span<T>` / `ReadOnlySpan<T>` | 栈上/连续内存切片，**零分配**；P/Invoke、VT 解析、缓冲区切分都靠它 |
| `Memory<T>` / `ReadOnlyMemory<T>` | 可跨 `await` 的 `Span`（`Span` 不能跨 `await`） |
| `ImmutableArray<T>` / `ImmutableList<T>` | 不可变集合（函数式风格） |

```csharp
ReadOnlySpan<byte> vt = stackalloc byte[256];
int n = ReadVtChunk(vt);
// 不用分配，零 GC 压力
```

### 2.7 异常处理

和 Python 一样是 `try/catch/finally`，但**类型精确**：

```csharp
try
{
    var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut);
    pipe.Connect(300);
}
catch (TimeoutException)
{
    // 守护进程没起
}
catch (IOException io) when (io.Message.Contains("pipe", StringComparison.OrdinalIgnoreCase))
{
    // 管道错误，且消息含 "pipe"
}
finally
{
    _pipe?.Dispose();
}
```

注意：

- C# 没有 `rescue` / `ensure` 的“关键词糖”，但 `try/catch/finally` 行为一致。
- **不要**用异常做正常控制流（和 Python 一样，但 .NET 里异常构造昂贵）。
- `[Obsolete]` 标记弃用；编译器在 `WarningsAsErrors` 下会拒编。

---

## 3. 资源、内存、生命周期

### 3.1 `IDisposable` 和 `using`

凡是包装**非托管资源**（文件、Socket、SafeHandle、Native 内存）的类都实现 `IDisposable`，**用 `using` 自动释放**：

```csharp
using var pipe = new NamedPipeClientStream(".", "ecode-daemon", PipeDirection.InOut);
using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
pipe.Connect(300);
// reader/pipe 在 scope 结束时自动 Dispose
```

C# 8+ 的 `using var` 会在当前作用域结束时释放，比 `using (var x = ...) { }` 简洁。

本项目 `DaemonClient` 是经典实现：

```csharp
public sealed class DaemonClient : IDisposable
{
    private volatile bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _listenCts?.Cancel(); } catch { /* ignore */ }
        _reader?.Dispose();
        _pipe?.Dispose();
    }
}
```

### 3.2 GC 与 `GC.Collect`

- .NET 是**分代追踪式 GC**，分 Gen0/Gen1/Gen2 + LOH（大对象堆）。
- 一般**不要**手动 `GC.Collect()`。要做性能工作用 `BenchmarkDotNet` / `dotnet-counters` / `dotnet-trace`。
- 长生命周期对象 + 短生命周期对象混用容易触发 Gen2 收集——这也是本项目用 `Channel<T>` / `ArrayPool<byte>` 的原因。

### 3.3 `Span<T>` / `ArrayPool<T>`：零分配缓冲区

终端引擎要处理大量 VT 字节，本项目用 `ArrayPool<byte>` + `Span<byte>` 避免每帧分配：

```csharp
var pool = ArrayPool<byte>.Shared;
byte[] buf = pool.Rent(4096);
try
{
    int n = await _pipe.ReadAsync(buf.AsMemory(0, 4096), ct);
    ProcessVt(buf.AsSpan(0, n));
}
finally
{
    pool.Return(buf);
}
```

`AsMemory(0, n)` 得到 `ReadOnlyMemory<byte>`，可以跨 `await`；`AsSpan(0, n)` 是栈切片，不跨 `await`。

---

## 4. 异步与并发

### 4.1 `Task` 和 `async/await`

C# 的 `async/await` 编译成**状态机**，不是“协程库”——它跑在 `TaskScheduler` 上（默认线程池）。和 Python `asyncio` 的差异：

| 维度 | `asyncio` | `Task` / `async` |
|---|---|---|
| 调度 | 单线程事件循环 | 默认线程池，可多线程 |
| 阻塞 | 不能调阻塞调用 | 可以并发跑阻塞调用（吃线程） |
| 取消 | `task.cancel()` | `CancellationToken`（贯穿全链路） |
| 背压 | `Queue` / `Semaphore` | `Channel<T>` / `SemaphoreSlim` |
| 并行 | `gather` / `TaskGroup` | `Task.WhenAll` / `Parallel.ForEachAsync` |

```csharp
public async Task<string> ReadFrameAsync(NamedPipeClientStream pipe, CancellationToken ct)
{
    var buf = new byte[4096];
    int n = await pipe.ReadAsync(buf.AsMemory(0, buf.Length), ct);
    return Encoding.UTF8.GetString(buf, 0, n);
}
```

关键约束：

1. **`async void` 只允许用于事件处理器**（`button.Click += async (...) => { }`）。其他地方永远用 `async Task` / `async Task<T>`，否则异常会直接拖垮进程。
2. **不要 `.Result` / `.Wait()`** —— 会死锁 UI 线程，且把异步转成同步。本项目 WPF 是 STA 线程，死锁比 winform 更隐蔽。
3. **`ConfigureAwait(false)`**：库代码（`ECode.Core`）默认加，避免无谓回到调用方同步上下文；WPF UI 代码不写，等回 UI 线程。
4. **取消**：所有 IO 方法都有 `CancellationToken` 重载——用起来。

### 4.2 `Channel<T>` 做生产者/消费者

`DaemonClient` 的“监听循环 → 派发事件”非常适合 `Channel<T>`：

```csharp
private readonly Channel<DaemonEvent> _events =
    Channel.CreateUnbounded<DaemonEvent>(new() { SingleReader = true });

private async Task PumpAsync(CancellationToken ct)
{
    await foreach (var ev in _events.Reader.ReadAllAsync(ct))
    {
        Dispatch(ev);
    }
}
```

类比：Ruby 的 `Queue` + `SizedQueue`；Python 的 `asyncio.Queue`。

### 4.3 锁与同步

- `lock (obj) { }` —— 等价于 `Monitor.Enter/Exit`，**不能锁 `string` / `ValueType` / `null`**。
- `SemaphoreSlim` —— 跨 `await` 的信号量（`Semaphore` 不能跨 await）。
- `Interlocked` —— 原子 `Increment` / `Exchange` / `CompareExchange`。
- `volatile` —— 保证字段读写不被 JIT 优化掉（`DaemonClient._connected` 就是 `volatile`）。

> 避免：本项目代码里看到 `Thread.Sleep` / `.Result` / `.Wait()` / `new Thread(...)` 就要警惕。

---

## 5. LINQ：把 `Enumerable` 升级成语言级

C# 的 LINQ 像 Ruby `Enumerable` + Python `itertools`，但**默认延迟执行**：

```csharp
var dirty = messages
    .Where(m => m.Type == MessageType.Output)
    .Select(m => m.Payload)
    .TakeLast(64)
    .ToList();                              // 真正求值
```

陷阱：

- `Where` / `Select` / `OrderBy` 是**延迟**的，多个枚举会跑多遍；用 `.ToList()` 物化。
- 副作用（`ForEach` 里写日志）会让延迟求值变得不可预测——本项目在热路径上避免 LINQ 副作用。

| LINQ | Ruby | Python |
|---|---|---|
| `.Select` | `.map` | `map(...)` |
| `.Where` | `.select` | `filter(...)` |
| `.OrderBy` | `.sort_by` | `sorted(..., key=)` |
| `.Any` / `.All` | `.any?` / `.all?` | `any(...)` / `all(...)` |
| `.FirstOrDefault` | `.first` (抛 nil) | `next(iter, None)` |
| `.ToDictionary(k => ...)` | `.to_h { ... }` | `{k: v for ...}` |
| `.GroupBy` | `.group_by` | `itertools.groupby` |
| `.Aggregate` | `.reduce` / `.inject` | `functools.reduce` |

---

## 6. WPF / MVVM 基础（本项目 UI 层）

### 6.1 WPF 是什么

- **WPF**（Windows Presentation Foundation）是 Windows 原生 UI 框架：XAML 写布局，C# 写行为，数据绑定 + 路由事件 + 控件模板。
- 启动对象在 `App.xaml`：

  ```xml
  <Application x:Class="ECode.App"
               xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
               StartupUri="Views/MainWindow.xaml" />
  ```

- 项目输出类型必须是 `WinExe`（不要控制台窗口）。

### 6.2 MVVM：`Model` / `View` / `ViewModel`

| 角色 | 作用 | 本项目 |
|---|---|---|
| **Model** | 业务数据 / 状态 | `ECode.Core/Models/*`（`Workspace`、`Surface`、`SplitNode`） |
| **View** | XAML，零业务逻辑 | `ECode/Views/MainWindow.xaml`、`ECode/Controls/TerminalControl.xaml` |
| **ViewModel** | 持有状态、暴露 `ICommand`、实现 `INotifyPropertyChanged` | `ECode/ViewModels/MainViewModel.cs` 等 |

### 6.3 `CommunityToolkit.Mvvm` 的源生成器

手写 `INotifyPropertyChanged` 啰嗦，本项目用 [CommunityToolkit.Mvvm 8.3.2](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm)：

```csharp
public sealed partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusText = "ready";

    [RelayCommand(CanExecute = nameof(CanQuit))]
    private void Quit() { Application.Current.Shutdown(); }

    private bool CanQuit() => !IsShuttingDown;
}

// XAML
// <Button Content="Quit" Command="{Binding QuitCommand}" />
```

要点：

- `[ObservableProperty]` 在 `_statusText` 字段上生成 `public string StatusText` 和 `OnStatusTextChanged` 钩子。
- `[RelayCommand]` 生成 `QuitCommand`（`IAsyncRelayCommand` 如果返回 `Task`）。
- **不要**在构造函数里调用 `StatusText = ...` 之外的初始化逻辑——用 `partial void OnStatusTextChanged(string value)`。

### 6.4 绑定（Binding）

```xml
<TextBlock Text="{Binding StatusText, FallbackValue=loading}" />
<Button  Command="{Binding QuitCommand}" />
```

绑定路径是**反射 + 表达式树**，调试时 `Output` 窗口会有 binding error。常见错：

- `DataContext` 没设（VM 没注入）。
- 绑定是 `OneWay` 但属性是 `{ get; }`，要 `TwoWay`。
- 集合改动没通知 → 用 `ObservableCollection<T>` 或 `BindingList<T>`，别用 `List<T>`。

### 6.5 线程亲和性

WPF 控件**只能从创建它们的线程（UI 线程 / Dispatcher）访问**。后台线程要更新 UI：

```csharp
Application.Current.Dispatcher.Invoke(() => StatusText = "ready");
// 或
await Application.Current.Dispatcher.InvokeAsync(() => StatusText = "ready");
```

类比：Python `tkinter` 的 `after` / `mainloop`；Ruby `Tk` 的 `tk_bind`。

---

## 7. 互操作：`unsafe` / P/Invoke（ConPTY 关键路径）

本项目 `ECode.Core/Terminal/ConPtyInterop.cs` 大量使用 P/Invoke 调 Win32（`kernel32!CreatePseudoConsole` 等）。要点：

### 7.1 `partial` + `[LibraryImport]`（源生成 P/Invoke）

```csharp
internal static partial class ConPtyInterop
{
    [LibraryImport("kernel32.dll", EntryPoint = "CreatePseudoConsole", SetLastError = true)]
    internal static partial int CreatePseudoConsole(
        COORD size,
        IntPtr hInput,
        IntPtr hOutput,
        uint dwFlags,
        out IntPtr phPC);
}
```

- `partial` + `[LibraryImport]` 由源生成器产出 marshaling 代码，比老式 `[DllImport]` **快、零分配、AOT 友好**。
- `SetLastError = true` 后用 `Marshal.GetLastWin32Error()` 取错误码。

### 7.2 `StructLayout` 与 marshaling

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct COORD
{
    public short X;
    public short Y;
}
```

- `LayoutKind.Sequential` —— 按字段顺序连续内存，匹配 Win32 头文件。
- `LayoutKind.Explicit` + `FieldOffset` —— 联合体（union），本项目未用。
- 字符串字段加 `CharSet = CharSet.Unicode` + `[MarshalAs(UnmanagedType.LPWStr)]`。

### 7.3 `SafeHandle`

不要用裸 `IntPtr` 包句柄；用 `SafeFileHandle` / 自定义 `SafeHandle` 派生类，让 GC + finalizer 自动释放。

### 7.4 `unsafe` 和 `fixed`

- `ECode.Core.csproj` 显式开 `AllowUnsafeBlocks=true`，因为有些 Interop 需要指针/内存钉住。
- `fixed (byte* p = &bytes[0]) { ... }` —— 在 scope 内阻止 GC 移动数组；用完立即释放。
- `Span<T>` 已经 99% 替代裸 `unsafe`，优先用 `Span`。

### 7.5 错误检查

Win32 错误码在 P/Invoke 边界要 `Marshal.ThrowExceptionForHR` / `Marshal.GetLastWin32Error` + `throw new Win32Exception(...)`，**不要**默默吞。

---

## 8. IPC：命名管道（JSON over line）

`ECode.Core/IPC/NamedPipeServer.cs` 与 `DaemonClient.cs` 实现 `\\.\pipe\ecode` / `\\.\pipe\ecode-daemon` 上的 JSON 协议。要点（详见 [`03-data-and-ipc.md`](03-data-and-ipc.md)）：

- **服务端**：`NamedPipeServerStream` + `StreamReader` / `StreamWriter` + UTF-8。
- **客户端**：`NamedPipeClientStream.Connect(timeoutMs)`，超时抛 `TimeoutException`。
- **多实例**：`CreateInstance` 配合 `MaxNumberOfServerInstances`，可同时服务多个客户端（CLI、Agent、另一个 WPF）。
- **半双工 vs 双工**：默认半双工；本项目用 `PipeOptions.Asynchronous` 启用重叠 IO，实现读监听和写请求并发。
- **协议形状**：一行一帧 JSON（`\n` 分隔），用 `System.Text.Json`（`SourceGeneration` 在热路径上避免反射）。

```csharp
var pipe = new NamedPipeServerStream(
    "ecode",
    PipeDirection.InOut,
    NamedPipeServerStream.MaxAllowedServerInstances,
    PipeTransmissionMode.Byte,
    PipeOptions.Asynchronous);

while (true)
{
    await pipe.WaitForConnectionAsync(ct);
    _ = HandleClientAsync(pipe, ct);   // 关键：不等当前 client，直接等下一个
    pipe = CreateNewInstance();       // 准备新实例
}
```

> 调试技巧：用 `\\.\pipe\ecode-daemon` 路径在 PowerShell 里 `Get-ChildItem` 可以列出当前打开的管道。

---

## 9. 持久化：JSON 文件 + 原子写

本项目偏好**纯 JSON 文件 + 原子 rename** 而不是数据库，配置 / 缓冲区快照 / 会话状态都走它：

```csharp
var json = JsonSerializer.Serialize(state, JsonOpts);
var tmp  = Path.Combine(dir, $"{name}.json.tmp");
File.WriteAllText(tmp, json, new UTF8Encoding(false));
File.Move(tmp, finalPath, overwrite: true);   // 原子替换
```

要点：

- **先写 `.tmp` 再 `File.Move(..., overwrite: true)`** 是 Windows 上接近原子的写法（`File.Move` 走 `MoveFileEx`）。
- 不要把整文件读进内存反序列化大状态；用 `Utf8JsonReader` 流式读。
- 加密：API Key 走 `ProtectedData.Protect(...)`（DPAPI，当前用户作用域），不要自己写加密。

---

## 10. 测试：`xUnit` + `FluentAssertions`

`ECode.Tests` 用 xUnit 2.9.3 + FluentAssertions 7.2.0。类比 RSpec / pytest：

```csharp
public class DaemonClientTests
{
    [Fact]
    public async Task TryConnect_returns_false_when_no_daemon()
    {
        var client = new DaemonClient();
        var ok = await Task.Run(() => client.TryConnect(timeoutMs: 50));
        ok.Should().BeFalse();
    }

    [Theory]
    [InlineData("",        "missing id")]
    [InlineData("abc def", "id contains space")]
    public void Validate_id_rejects_bad_input(string id, string why)
    {
        Action act = () => Workspace.ValidateId(id);
        act.Should().Throw<ArgumentException>()
           .WithMessage($"*{why}*");
    }
}
```

| xUnit | RSpec | pytest |
|---|---|---|
| `[Fact]` | `it { ... }` | 一个 `def test_...` |
| `[Theory] + [InlineData]` | 共享 `describe` + `let` | `@pytest.mark.parametrize` |
| `IClassFixture<T>` | `let(:t) { create(...) }` | `conftest.py` fixture |
| `Assert.Equal` / `FluentAssertions` | `expect(x).to eq` | `assert x == ...` |

`ECode.Smoke` 是 `Exe`，跑 ConPTY 真集成——产物是日志 + 退出码，不归 xUnit 接管。

---

## 11. 调试与诊断

| 工具 | 用途 |
|---|---|
| Visual Studio / Rider / VSCode (C# Dev Kit) | 调试器、调用栈、即时窗口 |
| `dotnet format` | 一键 format（项目用 `dotnet format` + stylecop 如有） |
| `dotnet test --logger "console;verbosity=detailed"` | 看每个测试的 stdout |
| `dotnet-trace collect -- <app>` | 采样追踪（替代 perfetto） |
| `dotnet-counters monitor -- <app>` | 实时看 GC、线程池、异常计数 |
| `dotnet-dump collect --pid <pid>` | 抓内存转储；用 `dotnet-dump analyze` 看对象 |
| PerfView / `dotnet-trace` | 火焰图（CPU、GC、IO、锁） |
| `ILSpy` / `ILDasm` | 反编译，验证源生成器没出意外 |
| `Get-ChildItem \\.\pipe\` | 列命名管道，调试 IPC |
| `DebugView` (Sysinternals) | 看 `OutputDebugString`，捕获 P/Invoke 错误 |
| `%LOCALAPPDATA%/ecode/daemon-debug.log` | 守护进程日志 |
| WPF Snoop | 看可视化树、绑定值 |

常用快捷（VS / Rider）：

- `F5` 调试运行 · `Ctrl+F5` 不调试 · `F9` 断点 · `F10` 单步 · `F11` 步入 · `Shift+F11` 步出 · `Ctrl+K,Ctrl+C` 注释。
- 附加到运行中进程：`dotnet attach <pid>` 或 IDE 的“Attach to Process”。

---

## 12. 从 Ruby / Python 视角的高频踩坑清单

> 每条都来自真实项目经验，按踩坑严重度排序。

1. **`==` vs `Equals`**。`==` 对引用类型默认是**引用相等**；字符串、`record` 等重载过，按值比较；自定义 struct/class 想按值比较必须重写 `Equals` + `GetHashCode`，否则放进 `HashSet` 会出问题。
2. **`string` 不可变**。`s.Replace(...)` 不会改原字符串，要接收返回值。
3. **`async void`**。除了事件处理器，**永远用** `async Task`。`async void` 抛异常会拖垮进程。
4. **`.Result` / `.Wait()`**。在 WPF / winform / ASP.NET（同步上下文存在时）会**死锁**。用 `await`。
5. **LINQ 延迟求值**。在热路径 / 多次枚举场景要 `.ToList()` / `.ToArray()` 物化。
6. **异常吞掉**。空 `catch { }` 是大忌——至少 `LogError(ex)`。`WarningsAsErrors` 也会提醒 `CA1031`。
7. **裸 `new Thread(...)`**。优先 `Task.Run(...)`；线程是稀缺资源。
8. **装箱**。`List<object>` + `int` 会反复装箱；用 `List<int>` / `T` 泛型。
9. **资源释放**。任何持有 `IDisposable` 的字段都该在 `Dispose` 里释放 + `GC.SuppressFinalize(this)`（或派生自 `SafeHandle`）。
10. **字符串比较**。用 `StringComparison.Ordinal` / `OrdinalIgnoreCase`，不要默认（默认是当前文化，会让 `"i".ToUpper() == "I"` 在土耳其失败）。
11. **结构体不能默认**。`struct` 不能有**显式无参构造函数**直到 C# 10；现在可写但行为有坑——`record struct` 更可预测。
12. **DateTime vs DateTimeOffset**。跨时区一律用 `DateTimeOffset`；`DateTime.Kind=Unspecified` 是地雷。
13. **浮点**。`double` / `float` 不要比较相等；用 `Math.Abs(a-b) < 1e-9`。
14. **`public` 字段**。不要——用属性（`{ get; private set; }`）。
15. **Mutation 跨线程**。共享可变状态要锁；优先用不可变类型 / `Channel<T>`。
16. **P/Invoke 字符串**。默认是 `LPWStr`（UTF-16） + 由调用方负责释放；本项目走 `[MarshalAs(UnmanagedType.LPWStr)]` + 自己 `Marshal.FreeHGlobal`。
17. **Windows-only**。本仓库只面向 Windows；任何 `Path.Combine` / `Directory.GetCurrentDirectory` / 换行符假设都要意识到。
18. **NuGet 缓存与 lockfile**。本仓库没有 `packages.lock.json`（故意的，便于 SDK 升级）；CI 上加 `dotnet restore --locked-mode` 时会失败——不要乱开。

---

## 13. 推荐的进一步阅读（按优先级）

> 这些是 2026-06 当前可访问的官方/权威资料，能补齐本手册没展开的细节。

1. [.NET 10 官方文档](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-10) — 跟当前 SDK 版本同步。
2. [C# 14 新特性](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-14) — `field` 关键字、扩展成员、null 条件赋值等。
3. [WPF 文档（archived）](https://learn.microsoft.com/dotnet/desktop/wpf/) — 配合仓库现有 XAML 看。
4. [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm) — 本项目 VM 的源生成器用法。
5. [ConPTY 背景](https://learn.microsoft.com/windows/console/creating-a-pseudoconsole-session) — Win32 伪终端 API 行为。
6. [.NET 异步编程模式（TAP）](https://learn.microsoft.com/dotnet/standard/asynchronous-programming-patterns/task-based-asynchronous-pattern-tap) — `async/await` / `CancellationToken` 规范。
7. [P/Invoke + 源生成器](https://learn.microsoft.com/dotnet/standard/native-interop/pinvoke-source-generation) — `[LibraryImport]` 写法。
8. [xUnit 文档](https://xunit.net/docs/getting-started/v2/whats-new) — 写法与 fixture 机制。
9. [C# 编码约定（Microsoft）](https://learn.microsoft.com/dotnet/csharp/fundamentals/coding-style/coding-conventions) — 与本仓库 `dotnet format` 风格保持一致。
10. *C# 12 in a Nutshell* / *CLR via C#* — 想理解底层（GC、JIT、内存模型）时看。

---

## 14. 项目速查：本仓库“能跑 / 能改 / 能测”的最小路径

> 把上面所有概念串成一次“打开仓库 → 改一行 → 跑测试”的实战流程。

1. **克隆并自检**：

   ```powershell
   dotnet --version                                    # 确认 10.0.x
   dotnet build ECode.sln -c Debug                    # 期望：0 error 0 warning
   ```

2. **跑现有测试**：

   ```powershell
   dotnet test tests/ECode.Tests/ECode.Tests.csproj
   dotnet run --project tests/ECode.Smoke/ECode.Smoke.csproj
   ```

3. **写一个最小改动**：在 `src/ECode.Core/Models/` 加一个 `record struct Cell(int Row, int Col)`，在 `ECode.Tests/Models/` 加一个 `[Fact]` 验证相等性，`dotnet test` 通过。
4. **起 WPF**：

   ```powershell
   dotnet run --project src/ECode/ECode.csproj -c Debug
   ```

   触发命令面板（`Ctrl+Shift+P`），调出 `Quit` 命令——确认 MVVM 链路通。
5. **接守护进程**：

   ```powershell
   dotnet run --project src/ECode.Daemon/ECode.Daemon.csproj
   # 另开窗口
   .\src\ECode\bin\Debug\net10.0-windows10.0.17763.0\ecode-app.exe
   ```

6. **CLI ping 守护**：

   ```powershell
   dotnet run --project src/ECode.Cli/ECode.Cli.csproj -- ping
   ```

7. **做一次完整发布**（macOS / Linux 上跑不了，需要 Windows 或 `pwsh` + Windows agent）：

   ```powershell
   pwsh ./scripts/publish.ps1 -Flavor SelfContained -Rid win-x64
   ```

8. **回滚验证**：`git restore --staged .` + `git checkout -- .` 撤销未暂存改动，重复 `dotnet build` 确认回到绿。

---

## 15. 与本仓库其他 spec 的对应关系

| 本手册章节 | 关联 spec |
|---|---|
| 工具链与项目结构 | [`04-build-deploy.md`](04-build-deploy.md)（构建/发布脚本、`publish.ps1`） |
| 进程模型、WPF UI | [`01-architecture.md`](01-architecture.md)（分层视图、进程角色、启动序列） |
| 模块职责、关键类 | [`02-modules.md`](02-modules.md) |
| IPC 协议形状、错误码 | [`03-data-and-ipc.md`](03-data-and-ipc.md) |
| CLI 命令解析 | [`05-cli-commands.md`](05-cli-commands.md) |
| 路线图、依赖 | [`06-roadmap.md`](06-roadmap.md) |
| 可执行 backlog、Issue/PR 模板 | [`07-implementation-backlog.md`](07-implementation-backlog.md) |

> 维护规则与本目录其他 spec 一致：手改 C# / XAML / csproj / 协议时同步更新本手册；新增 P/Invoke 时补“7. 互操作”一节。
