# MaaFramework 控制器方案参考

本文档整理自 [MaaEnd](https://github.com/MaaEnd/MaaEnd) 项目的 Win32 控制器配置，供本项目的截图与输入方案选型参考。

---

## Win32 控制器预设（MaaEnd）

MaaEnd 在 `assets/interface.json` 中定义了三种 Win32 控制器预设，每种打包了**截图方式 + 鼠标输入 + 键盘输入**的组合：

| 预设 | 截图方式 | 鼠标 | 键盘 | 适用场景 |
|------|----------|------|------|----------|
| **Win32-Window** | `Background` | `SendMessageWithCursorPos` | `PostMessage` | 后台窗口（默认推荐） |
| **Win32-Window-Background** | `PrintWindow` | `SendMessageWithWindowPos` | `PostMessage` | 后台窗口（PrintWindow 兼容模式） |
| **Win32-Front** | `ScreenDC` | `Seize` | `Seize` | 前台独占模式 |

> `Background` 在 MaaFramework 中对应底层 DXGI/GDI 等后台截图实现；`Seize` 表示直接接管前台输入。

---

## 截图方式对照

| 名称 | 底层实现 | 特点 | 常见问题 |
|------|----------|------|----------|
| `Background` | DXGI/GDI 后台捕获 | 不遮挡窗口、性能较好 | 部分游戏/硬件加速窗口可能黑屏 |
| `PrintWindow` | Windows `PrintWindow` API | 兼容性好、可截后台 | 硬件加速窗口可能黑屏或空白 |
| `ScreenDC` | 屏幕 DC (`BitBlt`) | 最稳定、能截所有内容 | 必须窗口在前台，会截到遮挡物 |

### 选型建议

1. **首选 `Background`** — 后台截图，不打断用户操作，MaaEnd 默认推荐。
2. **`Background` 黑屏时切 `PrintWindow`** — PrintWindow 的兼容性兜底，但同样对硬件加速窗口可能失效。
3. **最后 fallback 到 `ScreenDC`** — 截全屏再裁剪，最稳但要求目标窗口在前台且不能被遮挡。

---

## 鼠标/键盘输入方式对照

| 名称 | 说明 | 是否后台 |
|------|------|----------|
| `SendMessageWithCursorPos` | 发送消息 + 使用屏幕坐标 | 是 |
| `SendMessageWithWindowPos` | 发送消息 + 使用窗口内相对坐标 | 是 |
| `PostMessage` | PostMessage 发送输入 | 是 |
| `Seize` | 直接抢占前台输入（Seize） | 否（需要前台） |

> 后台模式下鼠标和键盘通常搭配 `SendMessage` / `PostMessage`；前台独占模式才用 `Seize`。

---

## 辅助检测机制（MaaEnd）

MaaEnd 在任务执行前增加了两项前置检测，可借鉴：

### 1. HDR 检测 (`hdrcheck`)

- **目的**：检测显示器是否开启 HDR。
- **原因**：HDR 开启后截图的色域/亮度信息与普通 SDR 不同，会导致图像识别（模板匹配/OCR）准确率下降。
- **行为**：首次任务开始时检测，若 HDR 开启则弹警告，避免反复打扰。

### 2. 覆盖层软件检测 (`processcheck`)

- **目的**：检测常见覆盖层/注入类软件。
- **黑名单**：`RTSSHooksLoader64.exe`、`RTSSHooksLoader.exe`、`GamePP.exe` 等。
- **原因**：这些软件的覆盖层会改变屏幕内容，或注入 DXGI 导致截图异常。

---

## 本项目现状

| 能力 | 状态 | 说明 |
|------|------|------|
| 截图方式选择 | ✅ 已实现 | 设置页提供 `DXGI_DesktopDup_Window` / `DXGI_DesktopDup` / `GDI` / `PrintWindow` / `ScreenDC` 五种方式 |
| 鼠标/键盘方式选择 | ❌ 未实现 | 目前未暴露鼠标/键盘输入方式配置 |
| 控制器预设打包 | ❌ 未实现 | 截图/鼠标/键盘是独立配置，未打包成场景预设 |
| HDR 检测 | ❌ 未实现 | — |
| 覆盖层软件检测 | ❌ 未实现 | — |

---

## 后续可优化项

1. **控制器预设打包**  
   将截图方式 + 鼠标方式 + 键盘方式打包成"后台模式 / 前台模式 / 兼容模式"三个预设，降低用户选择成本。

2. **鼠标/键盘方式联动**  
   选择 Win32 截图方式时，自动推荐匹配的鼠标/键盘输入方式（如后台截图配 `SendMessage`，前台截图配 `Seize`）。

3. **HDR 检测**  
   连接 Win32 控制器前检测 HDR 状态，若开启则弹警告提示用户关闭 HDR 或降低识别期望。

4. **覆盖层软件检测**  
   启动前扫描常见覆盖层进程（RTSS、GamePP、MSI Afterburner 等），提示用户关闭以避免截图异常。
