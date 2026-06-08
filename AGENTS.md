# AGV 调度系统项目说明

## 项目概览

这是一个基于 WPF 和 Prism 的 AGV 智能调度系统界面项目，主要代码组织在 `AgvDispatcher.*` 系列目录中。

当前目录同时包含一个根级 WPF 占位项目 `agv_system.csproj`，但真正的主程序是 `AgvDispatcher.Shell/AgvDispatcher.Shell.csproj`。解决方案文件是 `agv_system.slnx`。

## 技术栈

- 运行时：`.NET 8`，WPF 项目使用 `net8.0-windows`。
- UI 框架：WPF。
- 模块框架：Prism 9，主要使用 `Prism.DryIoc`、`Prism.Wpf`、`Prism.Core`。
- UI 资源：Shell 引入了 HandyControl 暗色主题。
- 数据状态：多数 ViewModel 继承 `Prism.Mvvm.BindableBase`，列表数据多用 `ObservableCollection<T>`。

## 主要目录

- `AgvDispatcher.Shell/`：主程序壳，负责窗口、侧边栏导航、header、Prism 模块加载。
- `AgvDispatcher.Core/`：共享常量、事件和枚举。
- `AgvDispatcher.Modules.*Module/`：各业务模块，每个模块独立项目。
- `UI_pic/`：主界面图标和 logo 图片资源。
- `FixViewsApp/`：一个辅助控制台工具，用正则批量调整部分 XAML 布局。
- `bin/`、`obj/`、`.vs/`：生成或 IDE 目录，阅读和修改时通常跳过。

## Shell 入口

`AgvDispatcher.Shell/App.xaml.cs` 继承 `PrismApplication`。

- `CreateShell()` 解析并创建 `MainWindow`。
- `CreateModuleCatalog()` 使用 `DirectoryModuleCatalog`，从 Shell 输出目录下的 `Modules` 文件夹动态加载模块。
- `OnInitialized()` 默认导航到 `MonitorLayoutView`，并发布当前页面标题事件。

`AgvDispatcher.Shell/MainWindow.xaml` 是主窗口布局：

- 左侧为可折叠侧边栏，宽度由 `MainWindowViewModel.SidebarWidth` 控制。
- 右侧上方 60px 是 `HeaderRegion`。
- 右侧下方是 `MainWorkspaceRegion`，用于加载各主功能页面。

## Prism 区域与事件

区域常量在 `AgvDispatcher.Core/Constants/RegionNames.cs`：

- `HeaderRegion`
- `MainWorkspaceRegion`
- `LeftPanelRegion`
- `RightPanelRegion`
- `MapRegion`
- `RobotListRegion`

页面标题事件在 `AgvDispatcher.Core/Events/NavigationTitleEvent.cs`，类型是 `PubSubEvent<string>`。

Shell 的 `MainWindowViewModel` 负责：

- `ToggleSidebarCommand`：切换侧边栏展开/收起。
- `NavigateCommand`：调用 `IRegionManager.RequestNavigate()` 导航到模块页面。
- 导航后通过 `NavigationTitleEvent` 发布 header 标题。
- 构造时把 `HeaderView` 注册到 `HeaderRegion`。

## 导航名称

侧边栏使用的主要导航名如下：

- `MonitorLayoutView`：运行监控。
- `TaskWorkspaceView`：任务管理。
- `ChargeWorkspaceView`：充电管理。
- `SignalWorkspaceView`：信号交互。
- `DataQueryWorkspaceView`：数据查询。
- `TaskConfigWorkspaceView`：任务配置。
- `SystemSettingView`：系统设置；当前未看到对应模块注册，点此项可能无法导航。

## 模块功能

- `AgvDispatcher.Modules.MonitorWorkspaceModule`：注册 `MonitorLayoutView`，该页面通过 Prism 子区域组合左侧统计、地图、AGV 列表和右侧统计。
- `AgvDispatcher.Modules.DashboardModule`：向 `LeftPanelRegion` 和 `RightPanelRegion` 注册仪表盘左右面板。
- `AgvDispatcher.Modules.MapModule`：向 `MapRegion` 注册地图视图，包含 `Images/main_bg.png` 资源。
- `AgvDispatcher.Modules.MonitoringModule`：向 `RobotListRegion` 注册 AGV 列表。
- `AgvDispatcher.Modules.TaskModule`：注册 `TaskWorkspaceView`，页面包含任务列表、左侧统计、详情和分析视图。
- `AgvDispatcher.Modules.ChargeModule`：注册 `ChargeWorkspaceView`，包含充电概览、地图、列表和右侧策略/告警视图，包含 `Images/Charge_bg.png` 资源。
- `AgvDispatcher.Modules.SignalModule`：注册 `SignalWorkspaceView`，包含信号地图、信号列表、交互日志和右侧统计视图。
- `AgvDispatcher.Modules.DataQueryModule`：注册 `DataQueryWorkspaceView`，内部通过 RadioButton 切换运行、任务、充电、告警、交互、能耗和设备日志数据页。
- `AgvDispatcher.Modules.TaskConfigModule`：注册 `TaskConfigWorkspaceView`，内部切换任务模板、流程、优先级、参数、定时任务、触发规则和任务策略。
- `AgvDispatcher.Modules.TemplateModule`：模板模块，目前基本为空，没有实际区域注册。

## 数据与模型

项目当前大量使用模拟数据，主要散落在各模块 ViewModel 中：

- 运行监控：`RobotModel`、`RobotListViewModel`。
- 任务管理：`TaskModel`、`TaskMainPanelViewModel`。
- 充电管理：`ChargeStationModel`、`ChargeListViewModel`。
- 信号交互：`SignalListModel`、`SignalLogModel` 及对应 ViewModel。
- 数据查询：`DataQueryModel` 和 `DataQueryMockViewModels.cs` 中的多类查询模型。
- 任务配置：`TaskTemplateModel`、`TaskStepModel` 和 `TaskConfigMockViewModels.cs` 中的配置模拟模型。

共享枚举在 `AgvDispatcher.Core/Enums/`：

- `RobotState`：`Idle`、`Running`、`Fault`、`Offline`。
- `TaskState`：`Pending`、`Running`、`Completed`、`Failed`、`Cancelled`。
- `SignalState`：`Normal`、`Active`、`Abnormal`、`Offline`。

## 资源与界面风格

- 整体是深色调工业监控风格，常用背景色包括 `#050B14`、`#0A111E`、`#112233`、`#1A2B3C`，强调色常用 `#00BFFF`。
- `AgvDispatcher.Shell/Views/HeaderView.xaml` 使用三列布局：左侧 logo，中间系统标题和当前页标题，右侧当前时间。
- Shell 项目已把 `../UI_pic/logo.png` 作为 WPF `Resource` 链接到 `UI_pic/logo.png`，header 使用 `Source="/UI_pic/logo.png"`。
- `UI_pic/` 中还有运行监控、任务管理、信号交互、数据查询、任务配置等图标图片。

## 构建与验证

推荐优先构建 Shell 项目：

```powershell
dotnet build AgvDispatcher.Shell\AgvDispatcher.Shell.csproj
```

如果只是验证 XAML 或小改动，并且依赖已经还原过，可以用：

```powershell
dotnet build AgvDispatcher.Shell\AgvDispatcher.Shell.csproj --no-restore /m:1 /p:RunPostBuildEvent=Never
```

注意事项：

- 各模块 `.csproj` 都有 PostBuild `xcopy`，会把模块输出复制到 `AgvDispatcher.Shell/bin/<Configuration>/<TargetFramework>/Modules/`。
- 如果 Shell 程序正在运行，构建可能因为输出 DLL 或 EXE 被占用而失败，需要先关闭正在运行的 `AgvDispatcher.Shell.exe`。
- 并行构建时多个模块同时复制到同一 `Modules` 目录，可能出现 `xcopy` 共享违规；可用 `/m:1` 串行构建降低概率。
- 之前验证过 `dotnet build AgvDispatcher.Shell\AgvDispatcher.Shell.csproj --no-restore /m:1 /p:RunPostBuildEvent=Never` 可以通过，但会有现有 CA1416 平台兼容性警告。

## 开发约定

- 新增主导航页面时，通常需要：
  1. 在对应模块 `RegisterTypes()` 中 `RegisterForNavigation<目标View>()`。
  2. 在 `AgvDispatcher.Shell/MainWindow.xaml` 增加侧边栏按钮。
  3. 在 `MainWindowViewModel.NavigateCommand` 的 `switch` 中补充页面标题。
- 运行监控主页面采用 Prism 子区域组合；若要替换左侧、地图、列表或右侧面板，优先改对应模块的 `RegisterViewWithRegion()`。
- 数据目前多为前端模拟数据，接入真实接口时优先替换 ViewModel 的数据来源，避免大范围改 XAML。
- XAML 里有不少中文注释和文案在终端中可能显示为乱码；编辑时注意文件编码，避免盲目批量替换中文字符串。
- 根目录的 `update_ws.ps1`、`update_tc.ps1`、`update_tc2.ps1` 和 `FixViewsApp` 都是批量修改 XAML 的辅助工具，使用前应先确认目标文件和正则不会误伤。
- 当前目录未检测到可用的 Git 仓库；需要版本管理操作时先确认仓库根目录。

## 不建议修改的内容

- 不要手工改 `bin/`、`obj/`、`.vs/` 中的生成文件。
- 不要把模块 DLL 直接提交为源码逻辑的一部分；模块加载依赖构建后输出。
- 不要在没有确认编码的情况下批量重写所有 XAML 文件。
