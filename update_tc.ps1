$dir = "d:\工作项目\睿格晟\AGV\AGV_WPF\agv_system\AgvDispatcher.Modules.TaskConfigModule\Views"

function Update-View {
    param($file, $columns)
    $path = Join-Path $dir $file
    $text = [IO.File]::ReadAllText($path)
    $text = $text -replace 'ItemsSource="\{Binding TemplateList\}"', 'ItemsSource="{Binding DataList}"'
    $text = [Regex]::Replace($text, '(?s)<DataGrid\.Columns>.*?</DataGrid\.Columns>', $columns)
    [IO.File]::WriteAllText($path, $text, [System.Text.Encoding]::UTF8)
}

$wf = @"
<DataGrid.Columns>
    <DataGridTextColumn Header="流程名称" Binding="{Binding WorkflowName}" Width="1.5*"/>
    <DataGridTextColumn Header="触发条件" Binding="{Binding TriggerCondition}" Width="*"/>
    <DataGridTextColumn Header="节点数量" Binding="{Binding NodeCount}" Width="80"/>
    <DataGridTextColumn Header="创建人" Binding="{Binding Creator}" Width="*"/>
    <DataGridTextColumn Header="是否启用" Binding="{Binding IsEnabled}" Width="80"/>
</DataGrid.Columns>
"@
Update-View "TaskConfigWorkflowView.xaml" $wf

$pr = @"
<DataGrid.Columns>
    <DataGridTextColumn Header="任务类型" Binding="{Binding TaskType}" Width="*"/>
    <DataGridTextColumn Header="基础优先级" Binding="{Binding BasePriority}" Width="100"/>
    <DataGridTextColumn Header="动态规则" Binding="{Binding DynamicRule}" Width="1.5*"/>
    <DataGridTextColumn Header="抢占策略" Binding="{Binding AllowPreempt}" Width="*"/>
</DataGrid.Columns>
"@
Update-View "TaskConfigPriorityView.xaml" $pr

$pa = @"
<DataGrid.Columns>
    <DataGridTextColumn Header="参数键名" Binding="{Binding ParamKey}" Width="1.2*"/>
    <DataGridTextColumn Header="参数名称" Binding="{Binding ParamName}" Width="1.2*"/>
    <DataGridTextColumn Header="参数值" Binding="{Binding ParamValue}" Width="*"/>
    <DataGridTextColumn Header="数据类型" Binding="{Binding DataType}" Width="100"/>
    <DataGridTextColumn Header="描述说明" Binding="{Binding Description}" Width="2*"/>
</DataGrid.Columns>
"@
Update-View "TaskConfigParameterView.xaml" $pa

$sc = @"
<DataGrid.Columns>
    <DataGridTextColumn Header="计划名称" Binding="{Binding ScheduleName}" Width="1.5*"/>
    <DataGridTextColumn Header="目标模板" Binding="{Binding TargetTemplate}" Width="1.5*"/>
    <DataGridTextColumn Header="Cron表达式" Binding="{Binding CronExpr}" Width="*"/>
    <DataGridTextColumn Header="下次执行时间" Binding="{Binding NextRunTime}" Width="1.5*"/>
    <DataGridTextColumn Header="状态" Binding="{Binding IsEnabled}" Width="80"/>
</DataGrid.Columns>
"@
Update-View "TaskConfigScheduleView.xaml" $sc

$ru = @"
<DataGrid.Columns>
    <DataGridTextColumn Header="规则名称" Binding="{Binding RuleName}" Width="*"/>
    <DataGridTextColumn Header="事件源" Binding="{Binding EventSource}" Width="*"/>
    <DataGridTextColumn Header="触发条件" Binding="{Binding Condition}" Width="1.5*"/>
    <DataGridTextColumn Header="执行动作" Binding="{Binding Action}" Width="1.5*"/>
    <DataGridTextColumn Header="是否启用" Binding="{Binding IsEnabled}" Width="80"/>
</DataGrid.Columns>
"@
Update-View "TaskConfigRuleView.xaml" $ru

$st = @"
<DataGrid.Columns>
    <DataGridTextColumn Header="策略名称" Binding="{Binding StrategyName}" Width="1.2*"/>
    <DataGridTextColumn Header="策略类型" Binding="{Binding StrategyType}" Width="100"/>
    <DataGridTextColumn Header="作用分组" Binding="{Binding TargetGroup}" Width="100"/>
    <DataGridTextColumn Header="策略说明" Binding="{Binding Description}" Width="2*"/>
    <DataGridTextColumn Header="是否启用" Binding="{Binding IsEnabled}" Width="80"/>
</DataGrid.Columns>
"@
Update-View "TaskConfigStrategyView.xaml" $st
