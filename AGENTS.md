# Agent 开发指南

本文档面向基于 MaaBoss 的 AI Agent 开发者，说明如何集成和调用招聘端能力。

---

## 快速集成

### MCP SSE 方式（推荐）

MaaBoss 启动后，Kestrel 在 `http://localhost:5000` 监听 MCP 请求。将以下配置加入你的 MCP Client：

```json
{
  "mcpServers": {
    "boss-agent": {
      "url": "http://localhost:5000/sse"
    }
  }
}
```

---

## 工具清单（Tools）

### 连接与启动

#### `launch_app`
启动或连接到 Boss 直聘招聘端客户端。

```json
{
  "platform": "win32",
  "adb_address": null,
  "wait_ready": true
}
```

**返回**：`{"success": true, "controller_type": "win32", "resolution": "1920x1080"}`

---

### 候选人浏览

#### `browse_candidates`
浏览候选人（牛人）列表，支持按关键词、经验、学历、期望薪资等筛选。返回候选人列表供 Agent 决策。

```json
{
  "keyword": "Python",
  "experience": "3-5年",
  "education": "本科",
  "salary_expectation": "20k-30k",
  "max_pages": 3,
  "list_type": "recommend"
}
```

**返回**：候选人列表数组，包含 `name`, `age`, `experience`, `education`, `current_company`, `skills`, `is_new` 等字段。

#### `swipe_candidates`
滑动浏览候选人列表（推荐牛人/新牛人/附近牛人/活跃牛人）。

```json
{
  "direction": "down",
  "count": 5,
  "interval_ms": 1500
}
```

#### `view_candidate_detail`
查看指定候选人的详细简历。

```json
{
  "candidate_name": "张三",
  "extract_info": true
}
```

**返回**：简历详情，含 `name`, `work_experience`, `projects`, `education`, `skills`, `self_evaluation`, `job_status`。

---

### 沟通管理

#### `greet_candidate`
向候选人发起沟通（打招呼/约聊）。支持自定义招呼语。

```json
{
  "candidate_name": "张三",
  "greeting_msg": null,
  "confirm": true
}
```

**返回**：`{"success": true, "candidate_name": "...", "greeted_at": "2026-05-25T11:21:47"}`

#### `batch_greet`
批量向当前列表候选人发起沟通。

```json
{
  "max_count": 10,
  "filter_new_only": true
}
```

#### `get_unread_messages`
获取未读消息列表（求职者的回复）。

```json
{
  "filter_type": "all"
}
```

**返回**：消息数组，含 `contact_name`, `message_preview`, `unread_count`, `time`。

#### `send_message`
向指定求职者（候选人）发送回复消息。

```json
{
  "contact_name": "张三",
  "message": "您好，您的简历很符合我们的要求...",
  "wait_reply": false,
  "timeout_sec": 30
}
```

#### `mark_all_read`
一键标记所有消息为已读。

---

### 职位管理

#### `check_job_posts`
查看当前已发布职位的状态和曝光数据。

**返回**：
```json
{
  "jobs": [
    {
      "title": "高级 Python 工程师",
      "status": "招聘中",
      "exposure_today": 128,
      "applications_today": 5,
      "unread_chats": 3
    }
  ]
}
```

#### `browse_applications`
浏览求职者对指定职位的投递记录。

```json
{
  "job_title": "高级 Python 工程师",
  "max_count": 20,
  "filter_unread": true
}
```

---

## 使用模式建议

### 模式 A：主动找人（日常拓源）

```
1. launch_app → 确保招聘端已启动
2. browse_candidates(keyword=目标技能, list_type="recommend", max_pages=5) → 收集候选人
3. 分析返回的候选人列表，决策沟通目标
4. view_candidate_detail(candidate_name=选定候选人) → 查看详细简历
5. greet_candidate(candidate_name=选定候选人, greeting_msg=定制话术) → 发起沟通
6. get_unread_messages → 查看求职者回复
7. send_message → 跟进沟通/安排面试
```

### 模式 B：被动处理（消息维护）

```
1. launch_app → 启动招聘端
2. get_unread_messages(filter_type="replied") → 优先处理求职者回复
3. send_message → 回复感兴趣候选人
4. check_job_posts → 查看职位曝光和投递情况
5. browse_applications → 处理新投递简历
```

### 模式 C：批量拓源

```
1. launch_app
2. swipe_candidates(direction="down", count=30) → 刷新增量候选人
3. batch_greet(max_count=20, filter_new_only=true) → 批量打招呼
4. get_unread_messages → 批量回复
```

---

## 错误处理

所有工具返回均包含 `success` 字段。当 `success: false` 时，会附带：

```json
{
  "success": false,
  "error_code": "SCREEN_NOT_FOUND",
  "error_message": "未能识别到目标界面元素，可能客户端未在前台",
  "suggestion": "请确保 Boss 直聘招聘端客户端已启动并处于前台"
}
```

常见错误码：

| 错误码 | 含义 | 建议 |
|--------|------|------|
| `SCREEN_NOT_FOUND` | 未识别到目标界面 | 检查客户端是否在前台 |
| `TIMEOUT` | 操作超时 | 增加 timeout 参数或检查网络 |
| `ELEMENT_NOT_FOUND` | 元素未找到 | UI 可能已更新，需更新模板图 |
| `NOT_CONNECTED` | 控制器未连接 | 先调用 launch_app |
| `GREET_LIMIT` | 当日沟通次数已达上限 | 提示用户明日再试或升级账号 |

---

## 注意事项

1. **频率控制**：Boss 直聘有反自动化机制，建议设置合理间隔，避免操作过于频繁
2. **弹窗处理**：操作过程中可能出现验证码、更新提示等弹窗，当前版本需人工介入
3. **多分辨率**：模板图基于特定分辨率截取，不同分辨率需单独适配
4. **版本兼容**：Boss 直聘客户端更新可能导致 UI 变化，需同步更新模板图和 Pipeline
5. **账号等级**：招聘端沟通次数受账号等级限制，批量操作前请确认剩余额度
