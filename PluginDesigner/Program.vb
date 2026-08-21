Imports System
Imports System.Windows.Forms
Imports System.Linq

Namespace PluginLayoutDesigner

    ''' <summary>入口：同时打开两个布局设计窗体（F5 预览），用于查看真实像素坐标。</summary>
    Friend Module Program

        <STAThread>
        Public Sub Main(args As String())
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            If args IsNot Nothing AndAlso args.Any(Function(a) String.Equals(a, "--studio", StringComparison.OrdinalIgnoreCase)) Then
                Application.Run(New JsonLayoutStudioForm())
                Return
            End If
            Dim layout = New PluginLayoutForm()
            layout.Show()
            Dim preview = New PreviewLayoutForm()
            preview.Show()
            Application.Run()
        End Sub

    End Module

End Namespace
