# OnTopReplica 项目文档

## 项目概述

**OnTopReplica** 是一个 Windows 桌面应用程序，可以创建任意窗口的实时"克隆"副本，并使其保持在屏幕最前端显示。该应用程序利用 Windows DWM (Desktop Window Manager) 缩略图 API 来实现窗口的实时镜像功能。

### 核心功能

**创建窗口的实时克隆副本并保持在最顶层显示**

这让你可以在工作时始终看到另一个窗口的内容，非常适合监控后台进程、边工作边看视频、处理复杂的多窗口游戏或工具等场景。

### 功能详解

| 功能 | 说明 |
|------|------|
| 🪟 **窗口克隆** | 选择任意窗口，创建实时更新的克隆副本，始终显示在其他窗口之上 |
| 📐 **区域选择** | 只克隆窗口的特定区域（如视频播放区域），支持相对坐标 |
| 💾 **区域存储** | 保存常用区域配置，下次快速选用 |
| 📏 **自动调整大小** | 支持原始大小、1/2 尺寸、1/4 尺寸和全屏模式 |
| 📍 **位置锁定** | 锁定克隆窗口到屏幕四角任意位置 |
| 🔍 **透明度调节** | 自定义克隆窗口的透明度，实现半透明效果 |
| 🖱️ **点击转发** | 点击克隆窗口时，操作会传递到原窗口，实现交互 |
| 👆 **点击穿透** | 克隆窗口变成纯覆盖层，鼠标点击穿透到下方窗口 |
| 🔄 **分组切换** | 在多个窗口间自动切换显示 |

### 典型使用场景

| 场景 | 描述 |
|------|------|
| 📺 **边工作边看视频** | 克隆视频播放器窗口，放在屏幕角落，边工作边观看 |
| 🎮 **游戏监控** | 克隆游戏中的小地图、状态栏或聊天窗口 |
| 📊 **数据监控** | 监控后台运行的程序输出、日志或仪表盘 |
| 💬 **即时通讯** | 保持聊天窗口始终可见，同时进行其他工作 |
| 🖥️ **多任务处理** | 在复杂的多窗口环境中保持关键信息可见 |

---

## 核心功能实现详解

### 1. 窗口克隆 (DWM Thumbnail)

**代码位置**：[src/OnTopReplica/ThumbnailPanel.cs](src/OnTopReplica/ThumbnailPanel.cs)

**实现原理**：
利用 Windows DWM (Desktop Window Manager) 的 Thumbnail API 创建窗口的实时镜像。

```csharp
// ThumbnailPanel.cs - 核心方法
public void SetThumbnailHandle(WindowHandle handle, ThumbnailRegion region) {
    // 关闭现有缩略图
    if (_thumbnail != null && !_thumbnail.IsInvalid) {
        _thumbnail.Close();
    }
    
    // 注册新的 DWM 缩略图关系
    _thumbnail = DwmManager.Register(owner, handle.Handle);
    
    // 设置区域并刷新
    _currentRegion = region;
    UpdateThubmnail();
}
```

**关键 API**：
- `DwmManager.Register()` - 注册缩略图关系
- `_thumbnail.Update()` - 更新缩略图的目标区域、源区域和透明度

---

### 2. 复制到剪贴板

**代码位置**：[src/OnTopReplica/MainForm_Features.cs](src/OnTopReplica/MainForm_Features.cs)

**实现原理**：
使用 Windows `PrintWindow` API 捕获源窗口的截图，如果设置了区域则裁剪到选定区域，然后复制到系统剪贴板。

**快捷键**：`Ctrl+C`

```csharp
// MainForm_Features.cs - 复制到剪贴板
public void CopyThumbnailToClipboard() {
    // 获取源窗口的截图
    var bitmap = CaptureWindow(CurrentThumbnailWindowHandle.Handle);
    
    // 如果设置了区域，则裁剪到选定区域
    if (_thumbnailPanel.SelectedRegion != null && _thumbnailPanel.ConstrainToRegion) {
        var regionRect = region.ComputeRegionRectangle(sourceSize);
        var croppedBitmap = bitmap.Clone(regionRect, bitmap.PixelFormat);
        bitmap = croppedBitmap;
    }
    
    Clipboard.SetImage(bitmap);  // 复制到剪贴板
}

// MainForm.cs - 快捷键处理
protected override void OnKeyUp(KeyEventArgs e) {
    //CTRL+C - Copy to clipboard
    if (e.Modifiers == Keys.Control && e.KeyCode == Keys.C) {
        e.Handled = true;
        CopyThumbnailToClipboard();
    }
}
```

**使用方式**：
- 右键菜单 → 高级 → 复制到剪贴板
- 快捷键 `Ctrl+C`

---

### 3. 区域选择

**代码位置**：
- [src/OnTopReplica/ThumbnailPanel.cs](src/OnTopReplica/ThumbnailPanel.cs) - 区域绘制
- [src/OnTopReplica/ThumbnailRegion.cs](src/OnTopReplica/ThumbnailRegion.cs) - 区域数据模型

**实现原理**：
用户通过鼠标拖拽在克隆窗口上绘制矩形区域，系统将客户端坐标转换为源窗口坐标。

```csharp
// ThumbnailPanel.cs - 区域绘制
protected void RaiseRegionDrawn(Point start, Point end) {
    // 计算边界并裁剪
    int left = Math.Max(0, Math.Min(start.X, end.X));
    int right = Math.Min(_thumbnailSize.Width, Math.Max(start.X, end.X));
    
    // 转换为缩略图坐标
    var startPoint = ClientToThumbnail(new Point(left, top));
    var endPoint = ClientToThumbnail(new Point(right, bottom));
    
    OnRegionDrawn(new Rectangle(startPoint.X, startPoint.Y, ...));
}
```

**区域模式**：
- **绝对模式**：固定像素坐标
- **相对模式**：相对于窗口边框的 Padding 值（适应窗口大小变化）

---

### 3. 点击转发

**代码位置**：
- [src/OnTopReplica/MainForm_Features.cs](src/OnTopReplica/MainForm_Features.cs) - 功能开关
- [src/OnTopReplica/ThumbnailPanel.cs](src/OnTopReplica/ThumbnailPanel.cs) - 点击检测

**实现原理**：
捕获克隆窗口上的点击事件，计算对应源窗口的坐标，然后向源窗口发送模拟输入消息。

```csharp
// MainForm_Features.cs - 启用点击转发
public bool ClickForwardingEnabled {
    get { return _thumbnailPanel.ReportThumbnailClicks; }
    set { _thumbnailPanel.ReportThumbnailClicks = value; }
}

// ThumbnailPanel.cs - 点击事件处理
protected override void OnMouseClick(MouseEventArgs e) {
    if (ReportThumbnailClicks) {
        // 转换坐标并触发事件
        OnCloneClick(ClientToThumbnail(e.Location), e.Button, false);
    }
}

// 坐标转换：客户端 → 源窗口
protected Point ClientToThumbnail(Point position) {
    position.X -= _padWidth;  // 补偿内边距
    position.Y -= _padHeight;
    
    // 计算比例位置
    PointF proportionalPosition = new PointF(
        (float)position.X / _thumbnailSize.Width,
        (float)position.Y / _thumbnailSize.Height
    );
    
    // 返回源窗口中的实际像素位置
    return new Point(
        (int)(proportionalPosition.X * source.Width) + offset.X,
        (int)(proportionalPosition.Y * source.Height) + offset.Y
    );
}
```

---

### 4. 点击穿透

**代码位置**：[src/OnTopReplica/MainForm_Features.cs](src/OnTopReplica/MainForm_Features.cs)

**实现原理**：
通过设置窗体的 `TransparencyKey` 属性，使特定颜色区域对鼠标事件透明。

```csharp
// MainForm_Features.cs
public bool ClickThroughEnabled {
    get { return _clickThrough; }
    set {
        // 设置透明键颜色为黑色，使窗口内容区域可穿透
        TransparencyKey = (value) ? Color.Black : DefaultNonClickTransparencyKey;
        
        if (value) {
            // 重新强制置顶
            TopMost = false;
            this.Activate();
            TopMost = true;
        }
        _clickThrough = value;
    }
}
```

**附加功能**：当鼠标悬停在全透明的点击穿透窗口上时，窗口会临时变为半透明，方便用户识别。

---

### 5. 位置锁定

**代码位置**：
- [src/OnTopReplica/MainForm_Features.cs](src/OnTopReplica/MainForm_Features.cs) - 锁定逻辑
- [src/OnTopReplica/ScreenPosition.cs](src/OnTopReplica/ScreenPosition.cs) - 位置计算

**实现原理**：
定义屏幕位置枚举（左上、右上、左下、右下、居中），计算目标位置并移动窗体。

```csharp
// ScreenPosition.cs - 位置枚举
enum ScreenPosition {
    TopLeft, TopRight, BottomLeft, BottomRight, Center
}

// 计算屏幕位置坐标
public static Point ResolveScreenPosition(this Screen screen, ScreenPosition position) {
    var rect = screen.WorkingArea;
    switch (position) {
        case ScreenPosition.TopLeft:
            return new Point(rect.X, rect.Y);
        case ScreenPosition.BottomRight:
            return new Point(rect.X + rect.Width, rect.Y + rect.Height);
        // ...
    }
}

// MainForm_Features.cs - 应用位置锁定
public ScreenPosition? PositionLock {
    set {
        if (value != null)
            this.SetScreenPosition(value.Value);
        _positionLock = value;
    }
}
```

---

### 6. 全屏模式

**代码位置**：[src/OnTopReplica/FullscreenFormManager.cs](src/OnTopReplica/FullscreenFormManager.cs)

**实现原理**：
保存当前窗体状态，然后移除边框并调整窗体大小以填充屏幕。

```csharp
// FullscreenFormManager.cs
public void SwitchFullscreen(FullscreenMode mode) {
    // 保存当前状态
    _preFullscreenLocation = _mainForm.Location;
    _preFullscreenSize = _mainForm.ClientSize;
    _preFullscreenBorderStyle = _mainForm.FormBorderStyle;
    
    // 切换到全屏
    _mainForm.FormBorderStyle = FormBorderStyle.None;
    MoveToFullscreenMode(mode);
}

private void MoveToFullscreenMode(FullscreenMode mode) {
    var currentScreen = Screen.FromControl(_mainForm);
    switch (mode) {
        case FullscreenMode.Standard:      // 工作区（不含任务栏）
            size = currentScreen.WorkingArea.Size;
            break;
        case FullscreenMode.Fullscreen:    // 整个屏幕
            size = currentScreen.Bounds.Size;
            break;
        case FullscreenMode.AllScreens:    // 所有显示器
            size = SystemInformation.VirtualScreen.Size;
            break;
    }
    _mainForm.Size = size;
}
```

---

### 7. 透明度调节

**代码位置**：[src/OnTopReplica/MainForm_MenuEvents.cs](src/OnTopReplica/MainForm_MenuEvents.cs)

**实现原理**：
通过 Windows Forms 的 `Form.Opacity` 属性设置窗体透明度（0.0 - 1.0）。

```csharp
// 设置透明度（示例）
this.Opacity = 0.75;  // 75% 不透明度
```

---

### 8. 分组切换

**代码位置**：
- [src/OnTopReplica/SidePanels/GroupSwitchPanel.cs](src/OnTopReplica/SidePanels/GroupSwitchPanel.cs) - UI 面板
- [src/OnTopReplica/MessagePumpProcessors/GroupSwitchManager.cs](src/OnTopReplica/MessagePumpProcessors/) - 切换逻辑

**实现原理**：
用户选择多个窗口组成一个组，当用户激活组内任一窗口时，自动切换显示该窗口的克隆。

```csharp
// GroupSwitchPanel.cs - 启用分组切换
public override void OnClosing(MainForm form) {
    if (_enableOnClose && listWindows.SelectedItems.Count > 0) {
        List<WindowHandle> ret = new List<WindowHandle>();
        foreach (ListViewItem i in listWindows.SelectedItems) {
            ret.Add((WindowHandle)i.Tag);
        }
        form.SetThumbnailGroup(ret);  // 设置窗口组
    }
}
```

---

### 系统要求

- Microsoft Windows Vista 或更高版本（使用原生 DWM 缩略图）
- Microsoft .NET Framework 4.7
- 桌面组合（Windows Aero）已启用

---

## 项目结构

```
OnTopReplica/
├── Docs/                          # 文档目录
├── Graphics/                      # 图形资源
│   ├── Raster/                   # 位图资源
│   └── Vector/                   # 矢量图资源
├── Installer/                     # 安装程序脚本
│   ├── script.nsi                # NSIS 安装脚本
│   ├── DotNet.nsh                # .NET 检测脚本
│   └── PostInstaller/            # 安装后处理程序
├── OriginalAssets/               # 原始设计资源
├── src/                          # 源代码目录
│   ├── OnTopReplica.sln          # 解决方案文件
│   ├── OnTopReplica/             # 主项目
│   ├── Installer/                # 安装程序项目
│   └── packages/                 # NuGet 包
├── CODE_OF_CONDUCT.md            # 行为准则
├── LICENSE                       # 许可证 (MS-RL)
└── README.md                     # 项目说明
```

---

## 核心模块详解

### 1. 入口点与应用程序初始化

#### `Program.cs`
应用程序的入口点，负责：
- 设置应用程序路径 (`AppPaths.SetupPaths()`)
- 初始化平台支持检查
- 处理命令行参数
- 加载语言设置
- 创建并运行主窗体
- 持久化用户设置

```csharp
// 核心启动流程
AppPaths.SetupPaths();           // 设置路径
Platform = PlatformSupport.Create(); // 初始化平台支持
Platform.CheckCompatibility();   // 兼容性检查
Application.Run(_mainForm);      // 进入主循环
```

### 2. 主窗体架构

主窗体采用**分部类 (Partial Class)** 设计模式，将功能分散到多个文件中：

| 文件 | 职责 |
|------|------|
| `MainForm.cs` | 核心逻辑、事件处理、初始化 |
| `MainForm.Designer.cs` | WinForms 设计器生成的 UI 代码 |
| `MainForm_Features.cs` | 功能实现（点击转发、点击穿透、透明度等） |
| `MainForm_Gui.cs` | GUI 相关方法 |
| `MainForm_MenuEvents.cs` | 菜单事件处理 |
| `MainForm_ChildForms.cs` | 子窗体管理 |

#### `MainForm.cs`
继承自 `AspectRatioForm`，主要组件：
- `ThumbnailPanel _thumbnailPanel` - 显示缩略图的面板
- `MessagePumpManager _msgPumpManager` - 消息泵管理
- `WindowListMenuManager _windowListManager` - 窗口列表管理
- `FullscreenFormManager FullscreenManager` - 全屏管理

### 3. 缩略图系统

#### `ThumbnailPanel.cs`
核心缩略图显示控件，功能包括：
- 使用 DWM Thumbnail API 显示窗口缩略图
- 支持区域选择和约束
- 鼠标区域绘制模式
- 点击事件报告

#### `ThumbnailRegion.cs`
管理缩略图区域的类，支持：
- 相对坐标计算
- 区域边界定义
- 区域存储和恢复

#### `StoredRegion.cs` / `StoredRegionArray.cs`
用于保存和管理用户定义的区域：
- 区域持久化
- 区域比较和排序

### 4. 窗口管理

#### `WindowHandle.cs`
窗口句柄封装类：
- 保存窗口句柄 (HWND)
- 获取窗口标题
- 加载窗口图标
- 获取窗口类名

#### `WindowListMenuManager.cs`
管理窗口选择菜单：
- 使用 `TaskWindowSeeker` 扫描可用窗口
- 填充右键菜单
- 处理窗口选择事件

### 5. 平台兼容层

#### `PlatformSupport.cs`
抽象基类，根据操作系统版本创建具体实现：

```csharp
// 支持的平台
├── WindowsVista.cs     // Windows Vista
├── WindowsSeven.cs     // Windows 7
├── WindowsEight.cs     // Windows 8/8.1/10/11
├── WindowsXp.cs        // Windows XP (不完全支持)
└── Other.cs            // 其他平台
```

功能：
- 兼容性检查
- 窗体初始化
- 窗体隐藏/恢复
- 任务栏交互

### 6. 原生 API 封装

`Native/` 目录包含 Windows API 封装：

| 文件 | 功能 |
|------|------|
| `WindowMethods.cs` | 窗口操作方法 |
| `MessagingMethods.cs` | Windows 消息处理 |
| `HotKeyMethods.cs` | 热键注册 |
| `InputMethods.cs` | 输入模拟 |
| `HookMethods.cs` | 钩子相关 |
| `WindowManagerMethods.cs` | 窗口管理器方法 |
| `WM.cs` | Windows 消息常量 |

### 7. 侧边面板系统

`SidePanels/` 目录包含可滑出的侧边面板：

| 面板 | 功能 |
|------|------|
| `AboutPanel.cs` | 关于信息 |
| `OptionsPanel.cs` | 选项设置 |
| `RegionPanel.cs` | 区域编辑 |
| `GroupSwitchPanel.cs` | 分组切换 |

### 8. 命令行选项

`StartupOptions/` 目录处理命令行参数：
- `Factory.cs` - 解析命令行参数
- `Options.cs` - 选项数据类
- 各种值转换器 (`SizeConverter.cs`, `RectangleConverter.cs` 等)

### 9. 消息泵处理

#### `MessagePumpManager.cs` / `MessagePumpProcessors/`
处理 Windows 消息循环：
- 热键处理
- 系统消息拦截
- 自定义消息处理

### 10. 全屏管理

#### `FullscreenFormManager.cs`
管理全屏模式：
- 保存/恢复窗体状态
- 多显示器支持
- 全屏模式切换

---

## 多语言支持

项目支持多种语言，资源文件位于项目根目录：

| 语言 | 文件 |
|------|------|
| 英语 (默认) | `Strings.resx` |
| 简体中文 | `Strings.zh-Hans.resx` |
| 繁体中文 | `Strings.zh-TW.resx` |
| 德语 | `Strings.de.resx` |
| 日语 | `Strings.ja-JP.resx` |
| 俄语 | `Strings.ru.resx` |
| 更多... | `Strings.*.resx` |

---

## 关键技术点

### DWM Thumbnail API
应用程序的核心功能依赖于 Windows Desktop Window Manager 的缩略图 API：
- `DwmRegisterThumbnail` - 注册缩略图关系
- `DwmUpdateThumbnailProperties` - 更新缩略图属性
- `DwmUnregisterThumbnail` - 取消注册缩略图

通过 `WindowsFormsAero` 库封装调用。

### AspectRatioForm
自定义窗体基类，支持：
- 保持宽高比调整大小
- 玻璃效果边距
- 自定义边框样式

### Click Forwarding
点击转发功能通过以下步骤实现：
1. 捕获克隆窗口上的点击位置
2. 计算对应原窗口的坐标
3. 发送模拟输入到原窗口

### Click-Through
点击穿透通过设置 `TransparencyKey` 实现：
- 将背景色设为透明键颜色
- Windows 自动将该颜色区域的点击传递到下层窗口

---

## 构建说明

### 前提条件
- Visual Studio 2017 或更高版本
- .NET Framework 4.7 SDK

### 构建步骤
1. 打开 `src/OnTopReplica.sln`
2. 选择 `Debug` 或 `Release` 配置
3. 构建解决方案 (Ctrl+Shift+B)

### 输出
- 可执行文件：`src/OnTopReplica/bin/{Configuration}/OnTopReplica.exe`

---

## 依赖项

| 依赖 | 用途 |
|------|------|
| `WindowsFormsAero` | DWM API 封装、Aero 主题控件 |
| `NDesk.Options` | 命令行参数解析 |
| `System.Windows.Forms` | Windows Forms UI 框架 |
| `System.Drawing` | 图形绘制 |

---

## 许可证

本项目采用 **MS-RL (Microsoft Reciprocal License)** 许可证。

---

## 贡献指南

1. Fork 本仓库
2. 创建功能分支
3. 提交更改
4. 发起 Pull Request

欢迎提交 Issue 和反馈！

---

## 路线图

- [x] 更新到最新 WindowsFormsAero 版本
- [x] 迁移到 .NET 4.7
- [ ] 改进/添加高 DPI 支持
- [ ] "存储场景"功能
- [ ] 迁移到 Windows Store (Centennial)

---

*文档最后更新：2026年1月22日*
