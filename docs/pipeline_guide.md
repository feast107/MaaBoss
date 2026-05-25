# Pipeline 开发指南

本文档说明如何为 MaaBoss 编写和调试 Pipeline JSON。

## 1. Pipeline 概述

Pipeline 是 MaaFramework 的核心概念，用于定义自动化任务的执行流程。每个 Pipeline 是一个 JSON 文件，其中包含多个**节点 (Node)**，节点之间通过 `next` 字段连接形成有向图。

## 2. 节点基本结构

```json
{
    "NodeName": {
        "recognition": "TemplateMatch",
        "template": "relative/path/to/image.png",
        "roi": [100, 200, 300, 400],
        "action": "Click",
        "target": [150, 250, 350, 450],
        "pre_delay": 200,
        "post_delay": 500,
        "timeout": 20000,
        "rate_limit": 1000,
        "next": ["NextNode1", "NextNode2"],
        "on_error": ["ErrorHandlerNode"]
    }
}
```

### 2.1 核心字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `recognition` | string | 识别算法：`TemplateMatch`, `OCR`, `ColorMatch`, `DirectHit` |
| `template` | string | 模板图片路径（相对于 `resources/images/`） |
| `roi` | [x, y, w, h] | 感兴趣区域，限制识别范围以提高性能 |
| `action` | string | 动作类型：`Click`, `Swipe`, `Input`, `Key`, `DoNothing`, `StartApp`, `StopApp` |
| `target` | [x, y, w, h] 或 [x, y] | 动作目标坐标/区域 |
| `next` | string / [string] | 命中后执行的下一个节点 |
| `on_error` | string / [string] | 超时/失败后执行的节点 |
| `timeout` | int | 超时时间(ms)，默认 20000 |
| `rate_limit` | int | 识别速率限制(ms)，默认 1000 |
| `pre_delay` | int | 动作前等待(ms) |
| `post_delay` | int | 动作后等待(ms) |

## 3. 识别算法

### 3.1 TemplateMatch（模板匹配）

最常用算法，通过 OpenCV 模板匹配在屏幕中寻找目标图片。

```json
{
    "FindLoginButton": {
        "recognition": "TemplateMatch",
        "template": "startup/btn_login.png",
        "threshold": 0.8,
        "roi": [800, 600, 400, 200],
        "action": "Click",
        "next": ["WaitHomePage"]
    }
}
```

| 额外参数 | 说明 |
|----------|------|
| `threshold` | 匹配阈值 (0-1)，默认 0.7 |
| `method` | 匹配方法：`Ccoeff`, `CcoeffNormed`, `SqDiff`, `SqDiffNormed` |

### 3.2 OCR（文字识别）

识别屏幕上的文字内容，支持预期文字匹配。

```json
{
    "CheckJobTitle": {
        "recognition": "OCR",
        "expected": ["Python", "后端", "工程师"],
        "roi": [100, 200, 500, 100],
        "replace": [["Pyth0n", "Python"]],
        "action": "DoNothing",
        "next": ["ClickJobCard"]
    }
}
```

| 额外参数 | 说明 |
|----------|------|
| `expected` | 预期文字列表，命中任一即成功 |
| `replace` | 文字替换规则，用于纠正 OCR 误识别 |

### 3.3 ColorMatch（颜色匹配）

通过颜色特征定位元素。

```json
{
    "FindRedDot": {
        "recognition": "ColorMatch",
        "lower": [0, 100, 100],
        "upper": [10, 255, 255],
        "count": 50,
        "action": "Click",
        "next": []
    }
}
```

### 3.4 DirectHit（直接命中）

不做识别，直接执行动作。用于已知坐标的固定操作。

```json
{
    "SwipeDown": {
        "recognition": "DirectHit",
        "action": "Swipe",
        "swipe": [960, 800, 960, 300],
        "next": []
    }
}
```

## 4. 动作类型

### 4.1 Click（点击）

```json
{
    "action": "Click",
    "target": [100, 200]
}
```

- 若 `target` 未指定，使用识别结果的命中区域中心
- 若 `target` 为 [x, y, w, h]，点击区域中心

### 4.2 Swipe（滑动）

```json
{
    "action": "Swipe",
    "swipe": [x1, y1, x2, y2],
    "duration": 500
}
```

### 4.3 Input（输入）

```json
{
    "action": "Input",
    "input": "要输入的文本"
}
```

### 4.4 Key（按键）

```json
{
    "action": "Key",
    "key": 4
}
```

- Android KeyCode: `4` = Back, `3` = Home, etc.

## 5. 流程控制

### 5.1 分支与循环

`next` 列表中的节点会按顺序尝试识别，第一个命中的被执行：

```json
{
    "MainScreen": {
        "next": ["HasNewMessage", "NoNewMessage"]
    },
    "HasNewMessage": {
        "recognition": "TemplateMatch",
        "template": "chat/red_dot.png",
        "action": "Click",
        "next": ["ProcessMessage"]
    },
    "NoNewMessage": {
        "recognition": "DirectHit",
        "action": "DoNothing",
        "next": ["DoOtherThing"]
    }
}
```

### 5.2 异常处理

```json
{
    "SomeAction": {
        "recognition": "TemplateMatch",
        "template": "xxx.png",
        "timeout": 10000,
        "next": ["SuccessNode"],
        "on_error": ["HandleError", "FallbackNode"]
    }
}
```

### 5.3 子任务（SubTask）

```json
{
    "DoComplexThing": {
        "action": "SubTask",
        "subtask": "OtherPipeline.json",
        "next": []
    }
}
```

## 6. 参数注入

Pipeline 支持从外部注入变量，使用 `{{variable_name}}` 语法：

```json
{
    "TypeKeyword": {
        "action": "Input",
        "input": "{{keyword}}"
    }
}
```

在 C# 中调用时传入：

```csharp
await controller.RunPipelineAsync(
    "BrowseCandidates",
    new { keyword = "Python 后端" }
);
```

## 7. 调试技巧

### 7.1 使用 Avalonia 调试面板

MaaBoss 内置 Debug 视图，提供：
- **截图按钮**：一键截取当前屏幕
- **Pipeline 执行**：手动输入 Pipeline 名称并运行
- **坐标操作**：直接输入坐标进行点击/滑动
- **实时日志**：查看每步识别和动作的执行结果

### 7.2 使用 MaaDebugger

社区工具 [MaaDebugger](https://github.com/MaaXYZ/MaaDebugger) 提供可视化 Pipeline 调试界面。

### 7.3 常见调试步骤

1. **截图采样**：在 Avalonia Debug 面板点击"截图"按钮保存截图
2. **裁剪模板**：使用画图工具裁剪目标元素，保存为 PNG 到 `resources/images/`
3. **调整 ROI**：缩小 `roi` 范围以提高识别速度和准确率
4. **调整阈值**：`threshold` 从 0.7 开始，根据实际效果微调
5. **多分辨率**：不同分辨率需单独截取模板，或使用相对坐标

## 8. 最佳实践

1. **模板图片**：使用 PNG 格式，背景透明（可选），尺寸尽量小（< 200x200）
2. **ROI 设置**：尽量精确设置 `roi`，减少搜索范围
3. **命名规范**：节点名使用 PascalCase，如 `ClickLoginButton`
4. **默认参数**：在 `default_pipeline.json` 中设置全局默认值
5. **错误处理**：每个关键节点都配置 `on_error`
6. **版本管理**：模板图与客户端版本绑定，UI 更新后需同步更新
