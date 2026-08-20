Imports System
Imports System.Windows.Forms

Namespace videoenhancer

    ''' <summary>
    ''' FFmpegFreeUI (3fui) 插件入口。宿主约定：
    ''' 程序集名 + ".Entry" 的类，注入 SetHost_* 回调后调用静态 Entry() 方法。
    ''' </summary>
    Public Class Entry

        Private Shared _addCustomWinformPanel As Action(Of String, Control)
        Private Shared _subscribeQueueEvents As Action(Of String, Object)

        Public Shared Sub SetHost_AddCustomWinformPanel(callback As Action(Of String, Control))
            _addCustomWinformPanel = callback
        End Sub

        Public Shared Sub SetHost_AddCustomWpfPanel(callback As Action(Of String, System.Windows.UIElement))
            ' 本插件只使用 WinForms 面板，忽略 WPF 注入
        End Sub

        Public Shared Sub SetHost_AddMissionToQueueWithArgs(callback As Action(Of String, String, String, String))
            QueueHook.HostAddMissionToQueueWithArgs = callback
        End Sub

        Public Shared Sub SetHost_AddMissionToQueueWith3fuiFile(callback As Action(Of String, String, String, String))
            ' 本插件使用命令行任务，不使用预设文件任务
        End Sub

        Public Shared Sub SetHost_MediaStreamVisualSelector(callback As Action(Of String, Object, Object, Object, String, String, String, String))
            ' 未使用
        End Sub

        Public Shared Sub SetHost_SubscribeQueueEvents(callback As Action(Of String, Object))
            _subscribeQueueEvents = callback
        End Sub

        ''' <summary>宿主在注入回调后调用此方法完成初始化。</summary>
        Public Shared Sub Entry()
            Try
                Dim config = PluginConfig.Load()
                BackendProgress.Attach(_subscribeQueueEvents)
                Dim panel As New PluginPanel(config)
                If _addCustomWinformPanel IsNot Nothing Then
                    _addCustomWinformPanel("视频超分", panel)
                End If
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine("[videoenhancer] 初始化失败: " & ex.ToString())
            End Try
        End Sub

    End Class

End Namespace
