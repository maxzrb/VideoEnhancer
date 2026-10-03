Imports System
Imports System.Diagnostics

Namespace videoenhancer
    Public Partial Class PluginPanel
        Private _hostParameterRefreshPending As Boolean
        Private _hostParameterRefreshQueued As Boolean

        Private Sub OnConfigurationSaved(sender As Object, e As EventArgs)
            QueueHostParameterRefresh()
        End Sub

        ''' <summary>合并同一轮操作的通知，在配置、钩子和替代程序路径均更新后刷新宿主。</summary>
        Private Sub QueueHostParameterRefresh()
            If IsDisposed OrElse Disposing Then Return
            _hostParameterRefreshPending = True
            If Not IsHandleCreated OrElse _hostParameterRefreshQueued Then Return
            _hostParameterRefreshQueued = True
            Try
                BeginInvoke(New Action(AddressOf FlushHostParameterRefresh))
            Catch ex As InvalidOperationException
                _hostParameterRefreshQueued = False
            End Try
        End Sub

        Private Sub FlushHostParameterRefresh()
            _hostParameterRefreshQueued = False
            If IsDisposed OrElse Disposing OrElse Not _hostParameterRefreshPending Then Return
            _hostParameterRefreshPending = False
            Try
                Dim panel = TryCast(HostAccess.GetDefaultInstance("Form_v6_参数面板"), System.Windows.Forms.Control)
                If panel Is Nothing OrElse panel.IsDisposed Then Return
                ' 宿主的“请求刷新参数状态”可能只在切页时生效，这里调用实际生成总览和命令模板的入口。
                HostPresetAccess.刷新参数总览(panel)
            Catch ex As Exception
                Trace.WriteLine("[VideoEnhancer] 参数面板刷新失败：" & ex.ToString())
            End Try
        End Sub

        Protected Overrides Sub OnHandleCreated(e As EventArgs)
            MyBase.OnHandleCreated(e)
            If _hostParameterRefreshPending Then QueueHostParameterRefresh()
        End Sub

        Protected Overrides Sub OnHandleDestroyed(e As EventArgs)
            ' 句柄重建会丢弃尚未执行的回调，保留待刷新状态供新句柄重发。
            _hostParameterRefreshQueued = False
            MyBase.OnHandleDestroyed(e)
        End Sub
    End Class
End Namespace
