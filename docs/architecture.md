# 架构设计文档

## 1. 设计目标

构建一个基于计算机视觉的 **Boss 直聘招聘端**自动化辅助工具，所有执行能力通过 **MCP (Model Context Protocol)** 暴露，供 AI Agent 调用。

主程序采用 **Avalonia Desktop + 内嵌 Kestrel** 的单进程架构，同时提供可视化操作面板和 MCP Server。

## 2. 核心架构

### 2.1 分层模型

```
┌──────────────────────────────────────────────┐
│  Layer 4: Agent Interface (MCP Protocol)     │
│  • HTTP SSE Endpoint (/sse)                  │
│  • JSON-RPC Message Endpoint (/message)      │
│  • Tool Registry / Schema Validation          │
│  • Result Serialization (JSON)                │
└──────────────────────┬─────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────┐
│  Layer 3: Task Orchestrator (C#)               │
│  • StartupTask, BrowseCandidatesTask           │
│  • CandidateDetailTask, GreetCandidateTask     │
│  • ChatTask, JobManagementTask                 │
│  • BrowseApplicationsTask, ...                 │
│  • Business Logic & State Management           │
└──────────────────────┬─────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────┐
│  Layer 2: Pipeline Engine (MaaFramework)       │
│  • JSON Pipeline Definition                    │
│  • Recognition (TemplateMatch / OCR / ...)     │
│  • Action (Click / Swipe / Input / Key)        │
│  • Node Graph & Flow Control                   │
└──────────────────────┬─────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────┐
│  Layer 1: Controller Abstraction               │
│  • Win32 Controller (SendMessage / BitBlt)     │
│  • ADB Controller (shell input / screencap)    │
│  • Screenshot / Click / Swipe / InputText      │
└──────────────────────┬─────────────────────────┘
                       │
┌──────────────────────▼─────────────────────────┐
│  Layer 0: Target Application                   │
│  • Boss 直聘招聘端 PC 客户端 (Win32)            │
│  • Boss 直聘招聘端 Android APP (Emulator)      │
└────────────────────────────────────────────────┘
```

### 2.2 模块职责

| 模块 | 职责 | 关键文件 |
|------|------|----------|
| `McpServerSetup.cs` | MCP Server 实现，Tool 注册与分发 | `MapMcpSseEndpoints` |
| `ControllerService.cs` | MaaFramework 封装，设备连接与原子操作 | `IMaaController` |
| `TaskService.cs` | 招聘端业务任务实现 | `*Task` 方法 |
| `assets/pipeline/` | Pipeline JSON 配置 | `*.json` |
| `resources/images/` | UI 模板截图 | `*.png` |

### 2.3 单进程设计

MaaBoss 采用单进程架构：

- **Avalonia UI 线程**：运行桌面界面，与用户交互
- **Kestrel HTTP 线程**：运行 MCP Server，与 Agent 交互
- **业务逻辑**：共享同一套 ControllerService 和 TaskService

```
┌─────────────────────────────────────┐
│           Process (.NET 10)          │
│                                      │
│  ┌─────────────┐  ┌──────────────┐  │
│  │ Avalonia UI │  │ Kestrel HTTP │  │
│  │  (Main)     │  │  (Background)│  │
│  └──────┬──────┘  └──────┬───────┘  │
│         │                │           │
│         └───────┬────────┘           │
│                 ▼                    │
│      ┌────────────────────┐          │
│      │ ControllerService  │          │
│      │ TaskService        │          │
│      └─────────┬──────────┘          │
│                ▼                     │
│      ┌────────────────────┐          │
│      │ IMaaController     │          │
│      └─────────┬──────────┘          │
│                ▼                     │
│      ┌────────────────────┐          │
│      │ Boss Zhipin Client │          │
│      └────────────────────┘          │
└─────────────────────────────────────┘
```

## 3. 数据流

### 3.1 Agent → Tool 调用流

```
Agent (Natural Language)
        │
        ▼
┌───────────────┐
│  MCP Client   │  ──(HTTP SSE)──►  Kestrel (/sse)
└───────────────┘                      │
                                       ▼
                              ┌─────────────────┐
                              │  McpServerSetup │
                              │  (Tool Router)  │
                              └────────┬────────┘
                                       │
              ┌────────────┬───────────┼───────────┬────────────────┐
              ▼            ▼           ▼           ▼                ▼
        BrowseCandidates  GreetCandidate  ChatTask    JobManagement  Screenshot
              │            │           │           │                │
              ▼            ▼           ▼           ▼                ▼
        TaskService   TaskService  TaskService  TaskService   ControllerService
              │            │           │           │                │
              └────────────┴───────────┴───────────┴────────────────┘
                                       │
                                       ▼
                              ┌─────────────────┐
                              │ IMaaController  │
                              │ (MaaFramework)  │
                              └─────────────────┘
```

### 3.2 视觉识别流程

```
Screenshot
    │
    ▼
ROI Crop (可选)
    │
    ▼
┌─────────────────┬─────────────────┬─────────────────┐
│ TemplateMatch   │      OCR        │   ColorMatch    │
│ (特征匹配)       │   (文字识别)     │   (颜色匹配)     │
└────────┬────────┴────────┬────────┴────────┬────────┘
         │                 │                 │
         ▼                 ▼                 ▼
    Match Score       Text Results      Color Diff
         │                 │                 │
         └─────────────────┴─────────────────┘
                           │
                    Threshold Check
                           │
              ┌────────────┴────────────┐
              ▼                         ▼
           Hit (命中)                Miss (未命中)
              │                         │
              ▼                         ▼
        Execute Action            on_error / next
```

## 4. 状态管理

### 4.1 控制器状态

- `DISCONNECTED` → `CONNECTING` → `CONNECTED` → `DISCONNECTED`
- 连接断开后支持自动恢复（Win32 recover_mode）

### 4.2 任务状态

TaskService 维护：
- 今日沟通计数 (`_todayGreeted`)
- 当前分辨率 (`_controller.Resolution`)

### 4.3 Agent 会话状态

MCP Server 为无状态设计，所有上下文由 Agent 维护。

## 5. 扩展设计

### 5.1 添加新 Tool

1. 在 `TaskService.cs` 添加业务方法
2. 在 `McpServerSetup.cs` 的 `GetToolSchemas()` 添加 Tool Schema
3. 在 `HandleToolCallAsync()` 中增加分支处理
4. 在 `assets/pipeline/` 新建 Pipeline JSON
5. 在 `resources/images/` 添加模板截图
6. 更新 `AGENTS.md` 和 `docs/api_reference.md`

### 5.2 支持新平台

MaaFramework 已内置 Win32 / ADB 支持。若需扩展：
1. 在 `IMaaController` 接口中增加新方法
2. 在 `MaaControllerMock` 和真实实现中实现

### 5.3 自定义识别算法

1. 继承 MaaFramework 的 `CustomRecognition` 接口
2. 在真实 Controller 实现中注册自定义识别器
3. Pipeline JSON 中 `recognition` 字段引用自定义类型

## 6. 容错设计

| 场景 | 策略 |
|------|------|
| 元素识别失败 | 重试 3 次，每次间隔 `rate_limit`，最终走 `on_error` |
| 连接断开 | Win32 自动恢复，ADB 重新连接 |
| 沟通上限 | 返回 `GREET_LIMIT` 错误，提示用户 |
| UI 版本不兼容 | 返回 `ELEMENT_NOT_FOUND`，需更新模板图 |
| 弹窗干扰 | Pipeline 中插入 `Interrupt` 节点处理常见弹窗 |
| 超时 | 可配置 `timeout`，超时后返回错误 |

## 7. 安全与合规

1. **频率控制**：默认 `rate_limit=1000ms`，避免操作过于频繁
2. **用户确认**：敏感操作（批量打招呼）建议由 Agent 显式确认
3. **日志脱敏**：日志中不记录候选人联系方式、聊天记录等敏感信息
4. **免责声明**：项目仅用于学习研究和合法自动化测试
