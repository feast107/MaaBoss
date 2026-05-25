# MaaBoss

基于 [MaaFramework](https://github.com/MaaXYZ/MaaFramework) 的 **Boss 直聘招聘端**视觉自动化辅助工具。

**主程序采用 Avalonia Desktop + 内嵌 Kestrel**，单进程同时提供：
- 🖥️ **可视化操作面板**（Avalonia 跨平台桌面 UI）
- 🌐 **MCP Server**（Kestrel HTTP SSE，供 Agent 调用）
- 🎯 **直接控制 Boss 直聘**（通过 MaaFramework C# Binding）

> **⚠️ 免责声明**：本项目仅供学习研究及合法自动化测试用途。请遵守 Boss 直聘用户协议及相关法律法规。

---

## ✨ 功能特性

- 🔍 **候选人智能浏览**：自动浏览推荐牛人、新牛人，按条件筛选
- 👤 **简历快速查看**：识别候选人卡片，点击查看详细简历
- 💬 **沟通高效处理**：自动识别新消息、批量回复求职者、快捷打招呼
- 📋 **职位管理辅助**：查看职位状态、浏览投递记录
- 🖥️ **可视化调试面板**：实时截图、坐标点击、Pipeline 执行、日志查看
- 🤖 **Agent 原生支持**：基于 MCP 协议，任何兼容 Agent 均可直接调用
- 🌍 **跨平台**：Windows / Linux / macOS（Avalonia + .NET 10）

---

## 🏗️ 技术架构

```
┌─────────────────────────────────────────────────────────────┐
│                    Single Process (.NET 10)                  │
│                                                              │
│  ┌─────────────────────┐    ┌───────────────────────────┐  │
│  │  Avalonia Desktop   │    │      Kestrel (HTTP)       │  │
│  │  ┌───────────────┐  │    │  ┌─────────────────────┐  │  │
│  │  │  Dashboard    │  │    │  │  /sse (MCP SSE)     │  │  │
│  │  │  Candidate    │  │    │  │  /message (MCP RPC) │  │  │
│  │  │  Chat         │  │    │  │  /health            │  │  │
│  │  │  Debug Panel  │  │    │  └─────────────────────┘  │  │
│  │  └───────────────┘  │    └───────────────────────────┘  │
│  └──────────┬──────────┘                                      │
│             │                                                │
│  ┌──────────▼──────────┐                                     │
│  │   ControllerService │                                     │
│  │   TaskService       │                                     │
│  └──────────┬──────────┘                                     │
│             │                                                │
│  ┌──────────▼──────────┐                                     │
│  │  IMaaController     │  ← Mock / MaaFramework.Binding.CSharp│
│  └──────────┬──────────┘                                     │
│             │ Win32 / ADB                                    │
│  ┌──────────▼──────────┐                                     │
│  │  Boss Zhipin Client │                                     │
│  └─────────────────────┘                                     │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 环境要求

- **.NET SDK 10.0** 或更高版本
- **Boss 直聘招聘端** PC 客户端（Win32）或安卓模拟器（ADB）

---

## 🚀 快速开始

### 编译并运行

```bash
cd MaaBoss.Desktop
dotnet run
```

程序启动后：
- **Avalonia UI** 桌面窗口打开
- **Kestrel** 在 `http://localhost:5000` 监听 MCP 请求

### Agent 接入（MCP SSE 模式）

在 MCP Client 配置中添加：

```json
{
  "mcpServers": {
    "boss-agent": {
      "url": "http://localhost:5000/sse"
    }
  }
}
```

### 可用 MCP Tools

| Tool | 功能 |
|------|------|
| `launch_app` | 连接客户端（Win32 / ADB） |
| `browse_candidates` | 浏览候选人列表 |
| `swipe_candidates` | 滑动浏览 |
| `view_candidate_detail` | 查看候选人简历 |
| `greet_candidate` | 发起沟通/打招呼 |
| `batch_greet` | 批量打招呼 |
| `get_unread_messages` | 获取未读消息 |
| `send_message` | 回复求职者 |
| `mark_all_read` | 标记已读 |
| `check_job_posts` | 查看职位状态 |
| `browse_applications` | 浏览投递记录 |
| `screenshot` | 截图 |
| `reload_resources` | 热重载资源 |

---

## 📁 项目结构

```
MaaBoss/
├── README.md
├── AGENTS.md
├── docs/                          # 详细文档
│   ├── architecture.md
│   ├── pipeline_guide.md
│   └── api_reference.md
│
├── MaaBoss.Desktop/               # Avalonia 主程序 (C#)
│   ├── MaaBoss.Desktop.csproj
│   ├── Program.cs                 # 启动 Avalonia + Kestrel
│   ├── App.axaml / App.axaml.cs
│   │
│   ├── Infrastructure/
│   │   ├── Mcp/
│   │   │   └── McpServerSetup.cs    # MCP SSE /message 端点
│   │   └── Maa/
│   │       ├── IMaaController.cs    # MaaFramework 接口
│   │       └── MaaControllerMock.cs # Mock 实现（可替换）
│   │
│   ├── Models/
│   │   ├── Candidate.cs
│   │   ├── JobPost.cs
│   │   └── ChatMessage.cs
│   │
│   ├── Services/
│   │   ├── ControllerService.cs     # 控制器管理
│   │   ├── TaskService.cs           # 业务任务执行
│   │   └── LogService.cs            # 日志服务
│   │
│   ├── ViewModels/
│   │   ├── MainWindowViewModel.cs
│   │   ├── DashboardViewModel.cs
│   │   ├── CandidateBrowserViewModel.cs
│   │   ├── ChatViewModel.cs
│   │   ├── DebugViewModel.cs
│   │   └── SettingsViewModel.cs
│   │
│   └── Views/
│       ├── MainWindow.axaml
│       ├── DashboardView.axaml
│       ├── CandidateBrowserView.axaml
│       ├── ChatView.axaml
│       ├── DebugView.axaml
│       └── SettingsView.axaml
│
├── assets/pipeline/               # Pipeline JSON 配置
│   ├── default_pipeline.json
│   ├── startup.json
│   ├── browse_candidates.json
│   ├── candidate_detail.json
│   ├── greet_candidate.json
│   ├── chat.json
│   └── job_management.json
│
└── resources/images/              # 模板截图
    ├── startup/
    ├── candidates/
    ├── chat/
    └── jobs/
```

---

## 🔌 接入真实 MaaFramework

当前 `MaaControllerMock` 为 Mock 实现，用于框架验证和 UI 调试。

接入真实 MaaFramework C# Binding：

1. 从 [MaaFramework Release](https://github.com/MaaXYZ/MaaFramework/releases) 下载 C# Binding
2. 将 DLL 添加为项目引用
3. 修改 `ControllerService.cs`：

```csharp
// 替换这一行
_controller = new MaaControllerMock();

// 为
_controller = new MaaControllerReal(); // 你的真实实现
```

---

## 🎨 调试面板说明

**Debug 视图**提供以下功能：
- **实时截图**：截取当前屏幕并在面板中显示
- **坐标点击**：输入 X/Y 坐标，模拟鼠标点击
- **滑动操作**：输入起点/终点，模拟手指/鼠标滑动
- **Pipeline 执行**：手动输入 Pipeline 名称并运行
- **运行日志**：实时查看所有操作日志

---

## 📄 许可证

[MIT License](LICENSE)

---

## 🙏 致谢

- [MaaFramework](https://github.com/MaaXYZ/MaaFramework) — 图像识别自动化框架
- [MFAAvalonia](https://github.com/MaaXYZ/MFAAvalonia) — 参考 UI 架构设计
- [ModelContextProtocol](https://github.com/modelcontextprotocol/csharp-sdk) — MCP .NET SDK
