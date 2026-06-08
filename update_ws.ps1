$leftFile = "d:\工作项目\睿格晟\AGV\AGV_WPF\agv_system\AgvDispatcher.Modules.DataQueryModule\Views\DataQueryLeftPanelView.xaml"
$wsFile = "d:\工作项目\睿格晟\AGV\AGV_WPF\agv_system\AgvDispatcher.Modules.DataQueryModule\Views\DataQueryWorkspaceView.xaml"

$leftContent = Get-Content $leftFile -Raw -Encoding UTF8
$wsContent = Get-Content $wsFile -Raw -Encoding UTF8

$resourcesMatch = [regex]::Match($leftContent, '(?s)<UserControl.Resources>(.*?)</UserControl.Resources>')
$leftResources = $resourcesMatch.Groups[1].Value

$borderMatch = [regex]::Match($leftContent, '(?s)<Border BorderBrush="#1F3A60" BorderThickness="1" CornerRadius="4">.*?</Border>')
$leftBorder = $borderMatch.Value

$leftBorder = $leftBorder -replace '<RadioButton Content="运行数据" IsChecked="True"/>', '<RadioButton x:Name="RbDQRun" Content="运行数据" IsChecked="True"/>'
$leftBorder = $leftBorder -replace '<RadioButton Content="任务数据"/>', '<RadioButton x:Name="RbDQTask" Content="任务数据"/>'
$leftBorder = $leftBorder -replace '<RadioButton Content="充电数据"/>', '<RadioButton x:Name="RbDQCharge" Content="充电数据"/>'
$leftBorder = $leftBorder -replace '<RadioButton Content="告警数据"/>', '<RadioButton x:Name="RbDQAlarm" Content="告警数据"/>'
$leftBorder = $leftBorder -replace '<RadioButton Content="交互数据"/>', '<RadioButton x:Name="RbDQInteraction" Content="交互数据"/>'
$leftBorder = $leftBorder -replace '<RadioButton Content="能耗数据"/>', '<RadioButton x:Name="RbDQEnergy" Content="能耗数据"/>'
$leftBorder = $leftBorder -replace '<RadioButton Content="设备日志"/>', '<RadioButton x:Name="RbDQLog" Content="设备日志"/>'

$centerPanel = @"
            <Grid>
                <local:DataQueryCenterPanelView>
                    <local:DataQueryCenterPanelView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQRun}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryCenterPanelView.Style>
                </local:DataQueryCenterPanelView>
                <local:DataQueryTaskDataView>
                    <local:DataQueryTaskDataView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQTask}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryTaskDataView.Style>
                </local:DataQueryTaskDataView>
                <local:DataQueryChargeDataView>
                    <local:DataQueryChargeDataView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQCharge}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryChargeDataView.Style>
                </local:DataQueryChargeDataView>
                <local:DataQueryAlarmDataView>
                    <local:DataQueryAlarmDataView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQAlarm}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryAlarmDataView.Style>
                </local:DataQueryAlarmDataView>
                <local:DataQueryInteractionDataView>
                    <local:DataQueryInteractionDataView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQInteraction}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryInteractionDataView.Style>
                </local:DataQueryInteractionDataView>
                <local:DataQueryEnergyDataView>
                    <local:DataQueryEnergyDataView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQEnergy}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryEnergyDataView.Style>
                </local:DataQueryEnergyDataView>
                <local:DataQueryDeviceLogView>
                    <local:DataQueryDeviceLogView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbDQLog}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:DataQueryDeviceLogView.Style>
                </local:DataQueryDeviceLogView>
            </Grid>
"@

$wsContent = $wsContent -replace '(?s)<Grid Background="#0A111E">', "<UserControl.Resources>`n$leftResources`n</UserControl.Resources>`n<Grid Background=`"#0A111E`">"
$wsContent = $wsContent -replace '(?s)<local:DataQueryLeftPanelView />', $leftBorder
$wsContent = $wsContent -replace '(?s)<local:DataQueryCenterPanelView />', $centerPanel

Set-Content $wsFile -Value $wsContent -Encoding UTF8


# TaskConfig
$tcLeftFile = "d:\工作项目\睿格晟\AGV\AGV_WPF\agv_system\AgvDispatcher.Modules.TaskConfigModule\Views\TaskConfigLeftPanelView.xaml"
$tcWsFile = "d:\工作项目\睿格晟\AGV\AGV_WPF\agv_system\AgvDispatcher.Modules.TaskConfigModule\Views\TaskConfigWorkspaceView.xaml"

$tcLeftContent = Get-Content $tcLeftFile -Raw -Encoding UTF8
$tcWsContent = Get-Content $tcWsFile -Raw -Encoding UTF8

$tcResourcesMatch = [regex]::Match($tcLeftContent, '(?s)<UserControl.Resources>(.*?)</UserControl.Resources>')
$tcResources = $tcResourcesMatch.Groups[1].Value
$tcBorderMatch = [regex]::Match($tcLeftContent, '(?s)<Border BorderBrush="#1F3A60" BorderThickness="1" CornerRadius="4">.*?</Border>')
$tcBorder = $tcBorderMatch.Value

$tcBorder = $tcBorder -replace '<RadioButton Content="任务模板" IsChecked="True"/>', '<RadioButton x:Name="RbTCTemplate" Content="任务模板" IsChecked="True"/>'
$tcBorder = $tcBorder -replace '<RadioButton Content="任务流程"/>', '<RadioButton x:Name="RbTCWorkflow" Content="任务流程"/>'
$tcBorder = $tcBorder -replace '<RadioButton Content="任务优先级"/>', '<RadioButton x:Name="RbTCPriority" Content="任务优先级"/>'
$tcBorder = $tcBorder -replace '<RadioButton Content="任务参数"/>', '<RadioButton x:Name="RbTCParameter" Content="任务参数"/>'
$tcBorder = $tcBorder -replace '<RadioButton Content="定时任务"/>', '<RadioButton x:Name="RbTCSchedule" Content="定时任务"/>'
$tcBorder = $tcBorder -replace '<RadioButton Content="触发规则"/>', '<RadioButton x:Name="RbTCRule" Content="触发规则"/>'
$tcBorder = $tcBorder -replace '<RadioButton Content="任务策略"/>', '<RadioButton x:Name="RbTCStrategy" Content="任务策略"/>'

$tcCenterPanel = @"
            <Grid>
                <local:TaskConfigTopPanelView>
                    <local:TaskConfigTopPanelView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCTemplate}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigTopPanelView.Style>
                </local:TaskConfigTopPanelView>
                <local:TaskConfigWorkflowView>
                    <local:TaskConfigWorkflowView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCWorkflow}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigWorkflowView.Style>
                </local:TaskConfigWorkflowView>
                <local:TaskConfigPriorityView>
                    <local:TaskConfigPriorityView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCPriority}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigPriorityView.Style>
                </local:TaskConfigPriorityView>
                <local:TaskConfigParameterView>
                    <local:TaskConfigParameterView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCParameter}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigParameterView.Style>
                </local:TaskConfigParameterView>
                <local:TaskConfigScheduleView>
                    <local:TaskConfigScheduleView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCSchedule}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigScheduleView.Style>
                </local:TaskConfigScheduleView>
                <local:TaskConfigRuleView>
                    <local:TaskConfigRuleView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCRule}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigRuleView.Style>
                </local:TaskConfigRuleView>
                <local:TaskConfigStrategyView>
                    <local:TaskConfigStrategyView.Style>
                        <Style TargetType="UserControl"><Setter Property="Visibility" Value="Collapsed"/><Style.Triggers><DataTrigger Binding="{Binding IsChecked, ElementName=RbTCStrategy}" Value="True"><Setter Property="Visibility" Value="Visible"/></DataTrigger></Style.Triggers></Style>
                    </local:TaskConfigStrategyView.Style>
                </local:TaskConfigStrategyView>
            </Grid>
"@

$tcWsContent = $tcWsContent -replace '(?s)<Grid Background="#0A111E">', "<UserControl.Resources>`n$tcResources`n</UserControl.Resources>`n<Grid Background=`"#0A111E`">"
$tcWsContent = $tcWsContent -replace '(?s)<local:TaskConfigLeftPanelView />', $tcBorder
$tcWsContent = $tcWsContent -replace '(?s)<local:TaskConfigTopPanelView />', $tcCenterPanel

Set-Content $tcWsFile -Value $tcWsContent -Encoding UTF8

Write-Host "Done"
