# API 参考手册

本文档详细说明所有 MCP Tools 的输入参数、返回值及使用示例。

MaaBoss 启动后，MCP Server 在 `http://localhost:5000` 提供 SSE 接入。

---

## 连接与启动

### `launch_app`

启动或连接到 Boss 直聘招聘端客户端。所有其他操作的前置条件。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `platform` | string | 是 | - | `win32` 或 `adb` |
| `adb_address` | string/null | 否 | null | ADB 设备地址，adb 模式下必填 |
| `wait_ready` | boolean | 否 | true | 是否等待应用完全加载 |

**返回值：**

```json
{
  "success": true,
  "controller_type": "win32",
  "resolution": "1920x1080",
  "message": "连接成功"
}
```

**错误码：**

| 错误码 | 说明 |
|--------|------|
| `LAUNCH_FAILED` | 连接失败，检查客户端或设备 |

---

## 候选人浏览

### `browse_candidates`

浏览候选人（牛人）列表，支持多维度筛选。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `keyword` | string/null | 否 | null | 技能/岗位关键词 |
| `experience` | string/null | 否 | null | 经验要求 |
| `education` | string/null | 否 | null | 学历要求 |
| `salary_expectation` | string/null | 否 | null | 期望薪资范围 |
| `max_pages` | integer | 否 | 3 | 最大浏览页数 |
| `list_type` | string | 否 | "recommend" | `recommend`/`new`/`nearby`/`active` |

**返回值：**

```json
{
  "success": true,
  "candidates": [
    {
      "name": "张三",
      "age": 28,
      "gender": "男",
      "experience": "5年",
      "education": "本科",
      "current_company": "某某互联网公司",
      "current_position": "Python 后端工程师",
      "salary_expectation": "25k-40k",
      "location": "北京",
      "skills": ["Python", "Go", "Kubernetes", "Redis"],
      "is_new": true,
      "active_status": "刚刚活跃"
    }
  ],
  "total_found": 2,
  "keyword": "Python",
  "list_type": "recommend",
  "current_page": 1,
  "has_more": true
}
```

**错误码：**

| 错误码 | 说明 |
|--------|------|
| `NOT_CONNECTED` | 控制器未连接 |
| `SCREEN_NOT_FOUND` | 未能识别候选人列表界面 |

---

### `swipe_candidates`

在候选人列表中滑动浏览。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `direction` | string | 否 | "down" | `up` / `down` / `left` / `right` |
| `count` | integer | 否 | 5 | 滑动次数 |
| `interval_ms` | integer | 否 | 1500 | 滑动间隔(ms) |

---

### `view_candidate_detail`

查看候选人详细简历。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `candidate_name` | string/null | 否 | null | 候选人姓名 |
| `extract_info` | boolean | 否 | true | 是否提取结构化信息 |

**返回值：**

```json
{
  "success": true,
  "name": "张三",
  "age": 28,
  "gender": "男",
  "location": "北京·海淀区",
  "work_experience": [
    {
      "company": "某某互联网公司",
      "position": "Python 后端工程师",
      "duration": "2021.06 - 至今",
      "description": "负责微服务架构设计"
    }
  ],
  "skills": ["Python", "Go", "Kubernetes", "Redis", "MySQL", "gRPC"],
  "job_status": "在职-看机会",
  "salary_expectation": "25k-40k"
}
```

---

## 沟通管理

### `greet_candidate`

向候选人发起沟通（打招呼/约聊）。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `candidate_name` | string/null | 否 | null | 目标候选人姓名 |
| `greeting_msg` | string/null | 否 | null | 自定义招呼语 |
| `confirm` | boolean | 否 | true | 是否自动确认弹窗 |

**返回值：**

```json
{
  "success": true,
  "candidate_name": "张三",
  "greeted_at": "2026-05-25T11:21:47",
  "greeting_sent": true,
  "today_greeted": 5,
  "daily_limit": 100
}
```

**错误码：**

| 错误码 | 说明 |
|--------|------|
| `GREET_LIMIT` | 今日沟通次数已达上限 |
| `ELEMENT_NOT_FOUND` | 未找到目标候选人或沟通按钮 |

---

### `batch_greet`

批量向当前列表中的候选人发起沟通。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `max_count` | integer | 否 | 10 | 最大沟通数量 |
| `filter_new_only` | boolean | 否 | true | 仅向新牛人打招呼 |

---

### `get_unread_messages`

获取未读消息列表（求职者的回复）。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `filter_type` | string | 否 | "all" | `all` / `replied` / `interview` / `system` |

**返回值：**

```json
{
  "success": true,
  "messages": [
    {
      "contact_name": "张三",
      "candidate_info": "Python 后端 · 5年 · 本科",
      "message_preview": "您好，我对贵公司的岗位很感兴趣...",
      "unread_count": 2,
      "time": "11:05",
      "type": "replied"
    }
  ],
  "total_unread": 3,
  "filter_type": "all"
}
```

---

### `send_message`

向指定求职者发送回复消息。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `contact_name` | string | 是 | - | 求职者姓名 |
| `message` | string | 是 | - | 消息内容 |
| `wait_reply` | boolean | 否 | false | 是否等待回复 |
| `timeout_sec` | integer | 否 | 30 | 等待超时(秒) |

---

### `mark_all_read`

标记所有消息为已读。

---

## 职位管理

### `check_job_posts`

查看已发布职位的状态和曝光数据。

**返回值：**

```json
{
  "success": true,
  "jobs": [
    {
      "title": "高级 Python 工程师",
      "status": "招聘中",
      "location": "北京",
      "salary": "25k-45k·14薪",
      "exposure_today": 128,
      "applications_today": 5,
      "unread_chats": 3,
      "publish_date": "2026-05-20"
    }
  ],
  "total_jobs": 2,
  "total_exposure_today": 213,
  "total_applications_today": 7,
  "total_unread_chats": 4
}
```

---

### `browse_applications`

浏览求职者对职位的投递记录。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `job_title` | string/null | 否 | null | 职位名称 |
| `max_count` | integer | 否 | 20 | 最大查看数量 |
| `filter_unread` | boolean | 否 | true | 仅看未处理投递 |

---

## 系统工具

### `screenshot`

截取当前屏幕。

**输入参数：**

| 字段 | 类型 | 必填 | 默认值 | 说明 |
|------|------|------|--------|------|
| `save_path` | string/null | 否 | null | 保存路径 |

---

### `reload_resources`

热重载 Pipeline 配置和模板资源。

---

## 通用错误码

| 错误码 | 说明 | 建议 |
|--------|------|------|
| `NOT_CONNECTED` | 控制器未连接 | 先调用 `launch_app` |
| `SCREEN_NOT_FOUND` | 未识别到目标界面 | 检查客户端是否在前台 |
| `TIMEOUT` | 操作超时 | 增加 timeout 或检查网络 |
| `ELEMENT_NOT_FOUND` | 元素未找到 | UI 可能已更新，需更新模板图 |
| `EXECUTION_ERROR` | 执行异常 | 查看日志定位问题 |
| `TOOL_NOT_FOUND` | 未知工具 | 检查工具名拼写 |
| `GREET_LIMIT` | 今日沟通次数已达上限 | 明日再试或升级账号 |
