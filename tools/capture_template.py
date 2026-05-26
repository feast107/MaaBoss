#!/usr/bin/env python3
"""
MaaBoss 模板自动截取工具

用法:
    1. 先运行一次测试，让测试生成截图（即使失败也会保存截图）
    2. 运行: python tools/capture_template.py
    3. 脚本会自动从最新截图中找到左侧边栏的"消息"按钮并更新模板

原理:
    MaaFramework 内部会把截图缩放到 720p 再做 TemplateMatch。
    所以模板必须从 MaaFramework 生成的截图文件中裁剪，
    而不是直接用 QQ 截图 / Windows 截图从原始窗口截取。
"""

import cv2
import numpy as np
import os
import glob
import shutil

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SCREENSHOT_DIR = os.path.join(BASE_DIR, "src", "MaaBoss.Core.Tests", "bin", "Release", "net10.0", "test_screenshots")
TEMPLATE_PATH = os.path.join(BASE_DIR, "src", "MaaBoss.Core", "assets", "image", "消息.png")


def find_latest_screenshot():
    """找到最新的 before_click 截图"""
    pattern = os.path.join(SCREENSHOT_DIR, "before_click_*.png")
    files = glob.glob(pattern)
    if not files:
        print(f"错误: 找不到截图文件，请确认目录存在: {SCREENSHOT_DIR}")
        print("提示: 先运行一次测试，测试会生成截图文件")
        return None
    return max(files, key=os.path.getmtime)


def find_message_button(img):
    """
    在截图中定位左侧边栏的"消息"按钮区域。
    策略: 左侧边栏是深色区域，在 720p 截图中宽度约 80~100px。
    我们在左侧区域搜索具有特定颜色特征的区域。
    """
    h, w = img.shape[:2]
    # 只搜索左侧边栏区域 (x: 0~120)
    sidebar = img[:, :120]

    # 转换为 HSV，更容易分离深色背景
    hsv = cv2.cvtColor(sidebar, cv2.COLOR_BGR2HSV)

    # 深色背景 mask (左侧边栏背景是深青色/深蓝绿色)
    # H: 80~100 (青绿色), S: 较高, V: 较低
    lower = np.array([70, 40, 20])
    upper = np.array([110, 255, 80])
    mask = cv2.inRange(hsv, lower, upper)

    # 找轮廓
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    candidates = []
    for cnt in contours:
        x, y, cw, ch = cv2.boundingRect(cnt)
        # 过滤大小: 消息按钮大约 90x45 ~ 110x60
        if 80 < cw < 120 and 35 < ch < 70 and y > 80:  # 在边栏中下部
            candidates.append((x, y, cw, ch, cv2.contourArea(cnt)))

    if not candidates:
        print("警告: 自动检测失败，使用固定 ROI 裁剪")
        # 固定 ROI: 从左侧边栏约 y=120~180 区域裁剪
        return (0, 120, 100, 60)

    # 选择面积最大的候选
    candidates.sort(key=lambda c: c[4], reverse=True)
    best = candidates[0]
    return (best[0], best[1], best[2], best[3])


def main():
    screenshot = find_latest_screenshot()
    if not screenshot:
        return 1

    print(f"使用截图: {screenshot}")
    img = cv2.imread(screenshot)
    if img is None:
        print(f"错误: 无法读取截图: {screenshot}")
        return 1

    print(f"截图尺寸: {img.shape[1]}x{img.shape[0]}")

    x, y, w, h = find_message_button(img)
    print(f"检测到'消息'按钮区域: ({x}, {y}, {w}, {h})")

    # 稍微扩大一点边距，确保包含完整按钮
    margin = 2
    x1 = max(0, x - margin)
    y1 = max(0, y - margin)
    x2 = min(img.shape[1], x + w + margin)
    y2 = min(img.shape[0], y + h + margin)

    crop = img[y1:y2, x1:x2]
    print(f"裁剪尺寸: {crop.shape[1]}x{crop.shape[0]}")

    # 验证匹配分数
    result = cv2.matchTemplate(img, crop, cv2.TM_CCOEFF_NORMED)
    _, max_val, _, max_loc = cv2.minMaxLoc(result)
    print(f"自匹配分数: {max_val:.4f} (应接近 1.0)")

    # 保存模板
    os.makedirs(os.path.dirname(TEMPLATE_PATH), exist_ok=True)
    cv2.imencode('.png', crop)[1].tofile(TEMPLATE_PATH)
    print(f"模板已保存: {TEMPLATE_PATH}")

    # 同时复制到测试输出目录（如果存在）
    test_out = os.path.join(BASE_DIR, "src", "MaaBoss.Core.Tests", "bin", "Release", "net10.0", "assets", "image", "消息.png")
    if os.path.exists(os.path.dirname(test_out)):
        shutil.copy(TEMPLATE_PATH, test_out)
        print(f"已同步到测试输出目录")

    return 0


if __name__ == "__main__":
    exit(main())
